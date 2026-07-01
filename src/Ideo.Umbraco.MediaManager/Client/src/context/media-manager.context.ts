import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { MediaManagerRepository } from "../services/media-manager.repository.js";
import type { ScanResult, ScanType } from "../types.d.js";

export type ScanState = "idle" | "scanning" | "done" | "failed";

export interface ScanSlice {
  state: ScanState;
  processed: number;
  result?: ScanResult;
  selected: string[];
}

export type Slices = Record<ScanType, ScanSlice>;

const emptySlice = (): ScanSlice => ({ state: "idle", processed: 0, selected: [] });

const POLL_INTERVAL_MS = 1000;
const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export class MediaManagerContext extends UmbControllerBase {
  #repository = new MediaManagerRepository(this);
  #notification?: UmbNotificationContext;

  #slices = new UmbObjectState<Slices>({
    OrphanedMedia: emptySlice(),
    OrphanedFiles: emptySlice(),
  });
  #activeTab = new UmbObjectState<ScanType>("OrphanedMedia");

  readonly slices = this.#slices.asObservable();
  readonly activeTab = this.#activeTab.asObservable();

  constructor(host: UmbControllerHost) {
    super(host, "Ideo.Umbraco.MediaManager.Context");
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notification = context;
    });
  }

  getSlices(): Slices {
    return this.#slices.getValue();
  }

  setActiveTab(type: ScanType): void {
    this.#activeTab.setValue(type);
  }

  setSelection(type: ScanType, selected: string[]): void {
    this.#patch(type, { selected });
  }

  async scanAll(): Promise<void> {
    await Promise.all([this.scan("OrphanedMedia"), this.scan("OrphanedFiles")]);
  }

  async scan(type: ScanType): Promise<void> {
    this.#patch(type, { state: "scanning", processed: 0, result: undefined, selected: [] });

    try {
      const jobId = await this.#repository.startScan(type);

      while (true) {
        await sleep(POLL_INTERVAL_MS);
        const status = await this.#repository.getStatus(jobId);
        if (!status) {
          continue;
        }
        this.#patch(type, { processed: status.processed });

        if (status.state === "Completed") {
          const result = (await this.#repository.getResult(jobId)) ?? undefined;
          this.#patch(type, { state: "done", result });
          break;
        }
        if (status.state === "Failed" || status.state === "Cancelled") {
          this.#patch(type, { state: "failed" });
          this.#notification?.peek("danger", {
            data: { message: `Scan ${status.state}${status.error ? `: ${status.error}` : ""}` },
          });
          break;
        }
      }
    } catch (error) {
      this.#patch(type, { state: "failed" });
      this.#notification?.peek("danger", { data: { message: "Scan failed to start." } });
      console.error(error);
    }
  }

  async deleteSelected(type: ScanType): Promise<void> {
    const ids = this.#slices.getValue()[type].selected;
    if (ids.length === 0) {
      return;
    }

    try {
      const result =
        type === "OrphanedMedia"
          ? await this.#repository.deleteMedia(ids, false)
          : await this.#repository.deleteFiles(ids, false);

      const affected = result?.affected ?? 0;
      const errors = result?.errors ?? [];
      this.#notification?.peek(errors.length ? "warning" : "positive", {
        data: {
          message: `${affected} item(s) processed${errors.length ? `, ${errors.length} error(s)` : ""}.`,
        },
      });

      await this.scan(type);
    } catch (error) {
      this.#notification?.peek("danger", { data: { message: "Delete failed." } });
      console.error(error);
    }
  }

  #patch(type: ScanType, patch: Partial<ScanSlice>): void {
    const current = this.#slices.getValue();
    this.#slices.setValue({ ...current, [type]: { ...current[type], ...patch } });
  }
}

export { MediaManagerContext as api };
