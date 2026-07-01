import { css, html, state, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { Slices } from "../../context/media-manager.context.js";
import { formatBytes } from "../../utils/format.js";

interface StatCard {
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

  get #cards(): StatCard[] {
    const media = this._slices?.OrphanedMedia;
    const files = this._slices?.OrphanedFiles;
    const reclaimable =
      (media?.result?.reclaimableBytes ?? 0) + (files?.result?.reclaimableBytes ?? 0);

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
        value: formatBytes(reclaimable),
        loading: media?.state === "scanning" || files?.state === "scanning",
      },
    ];
  }

  override render() {
    return html`
      <div class="grid">
        ${this.#cards.map(
          (card) => html`
            <uui-box class="card">
              <div class="value">
                ${card.loading
                  ? html`<uui-loader-circle></uui-loader-circle>`
                  : card.value}
              </div>
              <div class="label">
                <uui-icon name=${card.icon}></uui-icon>
                <span>${card.label}</span>
              </div>
            </uui-box>
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
      .card {
        --uui-box-default-padding: var(--uui-size-space-5);
      }
      .value {
        font-size: var(--uui-type-h2-size, 2rem);
        font-weight: 700;
        line-height: 1.1;
        min-height: 2rem;
      }
      .label {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        margin-top: var(--uui-size-space-2);
        color: var(--uui-color-text-alt);
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
