import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { UmbNumberState, UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { MediaManagerRepository } from "../services/media-manager.repository.js";
import type { MediaManagerTab, ScanItem, ScanResultSummary, ScanType } from "../types.d.js";

export type ScanState = "idle" | "scanning" | "done" | "failed";

export interface ScanSlice {
  state: ScanState;
  processed: number;
  result?: ScanResultSummary;
  /** 1-based page currently loaded into pageItems. */
  page: number;
  pageItems: ScanItem[];
  selected: string[];
  /** "Select all N" across pages: the server resolves the targets from the scan result. */
  allSelected: boolean;
}

export type Slices = Record<ScanType, ScanSlice>;

export const PAGE_SIZE = 50;

const CLEANUP_SCAN_TYPES: ScanType[] = ["UnusedMedia", "OrphanedFiles", "BrokenMedia", "Duplicates"];

const emptySlice = (): ScanSlice => ({
  state: "idle",
  processed: 0,
  page: 1,
  pageItems: [],
  selected: [],
  allSelected: false,
});

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
    Export: emptySlice(),
  });
  #activeTab = new UmbObjectState<MediaManagerTab>("UnusedMedia");
  // Computed server-side across scans (an item can be unused AND a duplicate; counted once).
  #reclaimableBytes = new UmbNumberState(0);

  readonly slices = this.#slices.asObservable();
  readonly activeTab = this.#activeTab.asObservable();
  readonly reclaimableBytes = this.#reclaimableBytes.asObservable();
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
    this.#patch(type, { selected, allSelected: false });
  }

  selectAll(type: ScanType): void {
    this.#patch(type, { selected: [], allSelected: true });
  }

  async loadPage(type: ScanType, page: number): Promise<void> {
    const slice = this.#slices.getValue()[type];
    const jobId = slice.result?.jobId;
    if (!jobId || slice.state !== "done") {
      return;
    }

    const signal = this.#aborters.get(type)?.signal;
    const items = await this.#repository.getResultItems(jobId, (page - 1) * PAGE_SIZE, PAGE_SIZE, signal);
    if (signal?.aborted) {
      return;
    }
    if (!items) {
      this.#fail(type, "The scan result is no longer available. Please rescan.");
      return;
    }

    this.#patch(type, { page, pageItems: items.items });
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

    this.#patch(type, {
      state: "scanning",
      processed: 0,
      result: undefined,
      page: 1,
      pageItems: [],
      selected: [],
      allSelected: false,
    });

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
          await this.#completeScan(type, jobId, signal);
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
    const { selected, allSelected } = slice;
    if (!allSelected && selected.length === 0) {
      return;
    }

    const jobId = slice.result?.jobId;
    if (!jobId) {
      return;
    }

    try {
      // Every id is validated server-side against the scan result identified by jobId; with
      // allSelected the server resolves the full target list from that same result.
      const result = allSelected
        ? await this.#repository.deleteAll(jobId, false)
        : await this.#repository.deleteItems(jobId, selected, false);

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

  async #completeScan(type: ScanType, jobId: string, signal: AbortSignal): Promise<void> {
    const result = (await this.#repository.getResult(jobId, signal)) ?? undefined;
    if (signal.aborted) {
      return;
    }
    if (!result) {
      this.#fail(type, "The scan result could not be retrieved. Please rescan.");
      return;
    }

    const isCleanup = CLEANUP_SCAN_TYPES.includes(type);
    let pageItems: ScanItem[] = [];
    if (isCleanup) {
      const page = await this.#repository.getResultItems(jobId, 0, PAGE_SIZE, signal);
      if (signal.aborted) {
        return;
      }
      pageItems = page?.items ?? [];
    }

    this.#patch(type, { state: "done", result, page: 1, pageItems });

    if (isCleanup) {
      void this.#refreshReclaimable(signal);
    }
  }

  async #refreshReclaimable(signal: AbortSignal): Promise<void> {
    const bytes = await this.#repository.getReclaimableBytes(signal);
    if (bytes !== null && !signal.aborted) {
      this.#reclaimableBytes.setValue(bytes);
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
