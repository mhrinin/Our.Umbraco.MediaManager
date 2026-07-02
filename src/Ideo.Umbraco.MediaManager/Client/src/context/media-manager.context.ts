import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { MediaManagerRepository } from "../services/media-manager.repository.js";
import type { MediaManagerTab, ScanResult, ScanType } from "../types.d.js";

export type ScanState = "idle" | "scanning" | "done" | "failed";

export interface ScanSlice {
  state: ScanState;
  processed: number;
  result?: ScanResult;
  selected: string[];
}

export type Slices = Record<ScanType, ScanSlice>;

const CLEANUP_SCAN_TYPES: ScanType[] = ["UnusedMedia", "OrphanedFiles", "BrokenMedia", "Duplicates"];

const emptySlice = (): ScanSlice => ({ state: "idle", processed: 0, selected: [] });

const POLL_INTERVAL_MS = 1000;
// Job state is in-memory server-side; this many consecutive missing statuses means the job was
// lost (app restart) and polling must stop instead of retrying forever.
const MAX_MISSING_STATUS = 5;
const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export class MediaManagerContext extends UmbControllerBase {
  #repository = new MediaManagerRepository(this);
  #notification?: UmbNotificationContext;
  #aborters = new Map<ScanType, AbortController>();

  #slices = new UmbObjectState<Slices>({
    UnusedMedia: emptySlice(),
    OrphanedFiles: emptySlice(),
    BrokenMedia: emptySlice(),
    Duplicates: emptySlice(),
    StorageReport: emptySlice(),
  });
  #activeTab = new UmbObjectState<MediaManagerTab>("UnusedMedia");

  readonly slices = this.#slices.asObservable();
  readonly activeTab = this.#activeTab.asObservable();
  readonly isScanning = this.#slices.asObservablePart((slices) =>
    Object.values(slices).some((slice) => slice.state === "scanning"),
  );

  constructor(host: UmbControllerHost) {
    super(host, "Ideo.Umbraco.MediaManager.Context");
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notification = context;
    });
  }

  override destroy(): void {
    for (const aborter of this.#aborters.values()) {
      aborter.abort();
    }
    this.#aborters.clear();
    super.destroy();
  }

  getSlices(): Slices {
    return this.#slices.getValue();
  }

  setActiveTab(tab: MediaManagerTab): void {
    this.#activeTab.setValue(tab);
    // The storage report is loaded lazily, the first time its tab is opened.
    if (tab === "StorageReport" && this.#slices.getValue().StorageReport.state === "idle") {
      this.scan("StorageReport");
    }
  }

  setSelection(type: ScanType, selected: string[]): void {
    this.#patch(type, { selected });
  }

  async scanAll(): Promise<void> {
    const types = [...CLEANUP_SCAN_TYPES];
    // Refresh the storage report too once it has been loaded, so it never shows stale totals.
    if (this.#slices.getValue().StorageReport.state !== "idle") {
      types.push("StorageReport");
    }
    await Promise.all(types.map((type) => this.scan(type)));
  }

  async scan(type: ScanType): Promise<void> {
    if (this.#slices.getValue()[type].state === "scanning") {
      return;
    }

    this.#aborters.get(type)?.abort();
    const aborter = new AbortController();
    this.#aborters.set(type, aborter);
    const signal = aborter.signal;

    this.#patch(type, { state: "scanning", processed: 0, result: undefined, selected: [] });

    try {
      const jobId = await this.#repository.startScan(type, signal);
      let missingStatus = 0;

      while (!signal.aborted) {
        await sleep(POLL_INTERVAL_MS);
        if (signal.aborted) {
          return;
        }

        const status = await this.#repository.getStatus(jobId, signal);
        if (signal.aborted) {
          return;
        }

        if (!status) {
          if (++missingStatus >= MAX_MISSING_STATUS) {
            this.#fail(type, "The scan is no longer available on the server. Please rescan.");
            return;
          }
          continue;
        }

        missingStatus = 0;
        this.#patch(type, { processed: status.processed });

        if (status.state === "Completed") {
          const result = (await this.#repository.getResult(jobId, signal)) ?? undefined;
          if (signal.aborted) {
            return;
          }
          if (!result) {
            this.#fail(type, "The scan result could not be retrieved. Please rescan.");
            return;
          }
          this.#patch(type, { state: "done", result });
          return;
        }

        if (status.state === "Failed" || status.state === "Cancelled") {
          this.#fail(type, `Scan ${status.state.toLowerCase()}${status.error ? `: ${status.error}` : ""}.`);
          return;
        }
      }
    } catch (error) {
      if (!signal.aborted) {
        this.#fail(type, "The scan could not be started.");
        console.error(error);
      }
    }
  }

  async deleteSelected(type: ScanType): Promise<void> {
    const slice = this.#slices.getValue()[type];
    const ids = slice.selected;
    if (ids.length === 0) {
      return;
    }

    try {
      // Orphaned files are physical files, validated server-side against the scan result
      // identified by jobId; every other scan targets media nodes.
      const result =
        type === "OrphanedFiles"
          ? await this.#repository.deleteFiles(slice.result?.jobId ?? "", ids, false)
          : await this.#repository.deleteMedia(ids, false);

      const affected = result?.affected ?? 0;
      const errors = result?.errors ?? [];
      this.#notification?.peek(errors.length ? "warning" : "positive", {
        data: {
          message: `${affected} item(s) processed${errors.length ? `, ${errors.length} error(s)` : ""}.`,
        },
      });

      // A deleted item can appear in more than one scan (e.g. unused AND duplicate), so refresh
      // every scan to keep all tabs and stat cards consistent.
      await this.scanAll();
    } catch (error) {
      this.#notification?.peek("danger", { data: { message: "Delete failed." } });
      console.error(error);
    }
  }

  #fail(type: ScanType, message: string): void {
    this.#patch(type, { state: "failed", result: undefined });
    this.#notification?.peek("danger", { data: { message } });
  }

  #patch(type: ScanType, patch: Partial<ScanSlice>): void {
    const current = this.#slices.getValue();
    this.#slices.setValue({ ...current, [type]: { ...current[type], ...patch } });
  }
}
