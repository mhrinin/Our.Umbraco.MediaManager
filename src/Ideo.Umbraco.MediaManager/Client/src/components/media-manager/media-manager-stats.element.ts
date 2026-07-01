import { css, html, state, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { Slices } from "../../context/media-manager.context.js";
import { formatBytes } from "../../utils/format.js";
import "./media-manager-stat-card.element.js";

interface Stat {
  icon: string;
  label: string;
  value: string;
  loading: boolean;
}

@customElement("media-manager-stats")
export class MediaManagerStatsElement extends UmbLitElement {
  @state() private _slices?: Slices;

  constructor() {
    super();
    this.consumeContext(MEDIA_MANAGER_CONTEXT, (context) => {
      this.observe(context?.slices, (slices) => {
        this._slices = slices;
      });
    });
  }

  get #stats(): Stat[] {
    const media = this._slices?.OrphanedMedia;
    const files = this._slices?.OrphanedFiles;
    const scanning = media?.state === "scanning" || files?.state === "scanning";

    return [
      {
        icon: "icon-picture",
        label: "Orphaned media",
        value: `${media?.result?.media.length ?? 0}`,
        loading: media?.state === "scanning",
      },
      {
        icon: "icon-document",
        label: "Orphaned files",
        value: `${files?.result?.files.length ?? 0}`,
        loading: files?.state === "scanning",
      },
      {
        icon: "icon-trash",
        label: "Reclaimable space",
        value: formatBytes(
          (media?.result?.reclaimableBytes ?? 0) + (files?.result?.reclaimableBytes ?? 0),
        ),
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
