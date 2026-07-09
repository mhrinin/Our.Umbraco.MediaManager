import { css, html, state, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { ScanSlice, Slices } from "../../context/media-manager.context.js";
import { formatBytes } from "../../utils/format.js";
import "./media-manager-stat-card.element.js";

interface Stat {
  icon: string;
  label: string;
  value: string;
  description: string;
  loading: boolean;
}

const FAILED_VALUE = "—";

@customElement("media-manager-stats")
export class MediaManagerStatsElement extends UmbLitElement {
  @state() private _slices?: Slices;
  @state() private _reclaimableBytes = 0;

  constructor() {
    super();
    this.consumeContext(MEDIA_MANAGER_CONTEXT, (context) => {
      this.observe(context?.slices, (slices) => {
        this._slices = slices;
      });
      // An item can be both unused AND a duplicate copy; the server counts its size once.
      this.observe(context?.reclaimableBytes, (bytes) => {
        this._reclaimableBytes = bytes ?? 0;
      });
    });
  }

  #countStat(slice: ScanSlice | undefined, count: number): string {
    return slice?.state === "failed" ? FAILED_VALUE : `${count}`;
  }

  get #stats(): Stat[] {
    const media = this._slices?.UnusedMedia;
    const files = this._slices?.OrphanedFiles;
    const broken = this._slices?.BrokenMedia;
    const duplicates = this._slices?.Duplicates;
    const cleanupSlices = [media, files, broken, duplicates];
    const scanning = cleanupSlices.some((slice) => slice?.state === "scanning");
    const anyFailed = cleanupSlices.some((slice) => slice?.state === "failed");

    return [
      {
        icon: "icon-picture",
        label: "Unused media",
        value: this.#countStat(media, media?.result?.totalItems ?? 0),
        description: "Media not referenced by any content.",
        loading: media?.state === "scanning",
      },
      {
        icon: "icon-documents",
        label: "Duplicates",
        value: this.#countStat(duplicates, duplicates?.result?.totalItems ?? 0),
        description: "Redundant copies of identical files.",
        loading: duplicates?.state === "scanning",
      },
      {
        icon: "icon-alert",
        label: "Broken media",
        value: this.#countStat(broken, broken?.result?.totalItems ?? 0),
        description: "Media whose file is missing on disk.",
        loading: broken?.state === "scanning",
      },
      {
        icon: "icon-document",
        label: "Orphaned files",
        value: this.#countStat(files, files?.result?.totalItems ?? 0),
        description: "Files on disk with no matching media item.",
        loading: files?.state === "scanning",
      },
      {
        icon: "icon-trash",
        label: "Reclaimable space",
        value: anyFailed ? FAILED_VALUE : formatBytes(this._reclaimableBytes),
        description: "Disk space recovered by cleaning these up.",
        loading: scanning,
      },
    ];
  }

  override render() {
    return html`
      <div class="grid">
        ${this.#stats.map(
          (stat) => html`
            <media-manager-stat-card
              icon=${stat.icon}
              label=${stat.label}
              value=${stat.value}
              description=${stat.description}
              ?loading=${stat.loading}
            ></media-manager-stat-card>
          `,
        )}
      </div>
    `;
  }

  static override styles = [
    css`
      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: var(--uui-size-space-4);
      }
    `,
  ];
}

export default MediaManagerStatsElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-stats": MediaManagerStatsElement;
  }
}
