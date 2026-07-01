import {
  css,
  html,
  nothing,
  state,
  customElement,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { MediaManagerContext } from "../../context/media-manager.context.js";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { Slices } from "../../context/media-manager.context.js";
import type { ScanType } from "../../types.d.js";
import "../media-manager/media-manager-stats.element.js";
import "../media-manager/media-manager-results.element.js";

@customElement("media-manager-dashboard")
export class MediaManagerDashboardElement extends UmbLitElement {
  #context = new MediaManagerContext(this);

  @state() private _slices?: Slices;
  @state() private _activeTab: ScanType = "OrphanedMedia";

  constructor() {
    super();
    this.provideContext(MEDIA_MANAGER_CONTEXT, this.#context);
    this.observe(this.#context.slices, (slices) => {
      this._slices = slices;
    });
    this.observe(this.#context.activeTab, (tab) => {
      this._activeTab = tab;
    });
  }

  override firstUpdated() {
    this.#context.scanAll();
  }

  get #scanning(): boolean {
    return (
      this._slices?.OrphanedMedia.state === "scanning" ||
      this._slices?.OrphanedFiles.state === "scanning"
    );
  }

  #count(type: ScanType): number | undefined {
    const slice = this._slices?.[type];
    if (slice?.state !== "done") {
      return undefined;
    }
    return type === "OrphanedMedia"
      ? slice.result?.media.length ?? 0
      : slice.result?.files.length ?? 0;
  }

  #renderTab(type: ScanType, label: string) {
    const count = this.#count(type);
    return html`
      <uui-tab
        label=${label}
        ?active=${this._activeTab === type}
        @click=${() => this.#context.setActiveTab(type)}
      >
        ${label}${count === undefined ? nothing : html` <uui-badge>${count}</uui-badge>`}
      </uui-tab>
    `;
  }

  override render() {
    return html`
      <div class="dashboard">
        <div class="header">
          <div class="titles">
            <h1>Media Manager</h1>
            <p>Find and safely remove unused media and orphaned files.</p>
          </div>
          <uui-button
            look="outline"
            label="Rescan"
            ?disabled=${this.#scanning}
            @click=${() => this.#context.scanAll()}
          >
            <uui-icon name="icon-sync"></uui-icon>
            Rescan
          </uui-button>
        </div>

        <media-manager-stats></media-manager-stats>

        <uui-tab-group>
          ${this.#renderTab("OrphanedMedia", "Orphaned media")}
          ${this.#renderTab("OrphanedFiles", "Orphaned files")}
        </uui-tab-group>

        <media-manager-results></media-manager-results>
      </div>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
        height: 100%;
        overflow: auto;
        background: var(--uui-color-background);
      }
      .dashboard {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-5);
        padding: var(--uui-size-layout-1);
        max-width: 1400px;
        margin: 0 auto;
      }
      .header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: var(--uui-size-space-4);
      }
      .titles h1 {
        margin: 0;
      }
      .titles p {
        margin: var(--uui-size-space-1) 0 0;
        color: var(--uui-color-text-alt);
      }
      uui-tab-group {
        --uui-tab-divider: var(--uui-color-divider);
      }
    `,
  ];
}

export default MediaManagerDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-dashboard": MediaManagerDashboardElement;
  }
}
