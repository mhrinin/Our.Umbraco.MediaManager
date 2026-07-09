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
import type { MediaManagerTab, ScanType } from "../../types.d.js";
import "../media-manager/media-manager-stats.element.js";
import "../media-manager/media-manager-results.element.js";
import "../media-manager/media-manager-report.element.js";
import "../media-manager/media-manager-export.element.js";

@customElement("media-manager-dashboard")
export class MediaManagerDashboardElement extends UmbLitElement {
  #context = new MediaManagerContext(this);

  @state() private _slices?: Slices;
  @state() private _activeTab: MediaManagerTab = "UnusedMedia";
  @state() private _isScanning = false;

  constructor() {
    super();
    this.provideContext(MEDIA_MANAGER_CONTEXT, this.#context);
    this.observe(this.#context.slices, (slices) => {
      this._slices = slices;
    });
    this.observe(this.#context.activeTab, (tab) => {
      this._activeTab = tab;
    });
    this.observe(this.#context.isScanning, (isScanning) => {
      this._isScanning = isScanning;
    });
  }

  override firstUpdated() {
    this.#context.scanAll();
  }

  #count(type: MediaManagerTab): number | undefined {
    if (type === "StorageReport" || type === "Export") {
      return undefined;
    }
    const slice = this._slices?.[type];
    if (slice?.state !== "done") {
      return undefined;
    }
    return slice.result?.totalItems ?? 0;
  }

  #renderActivePanel() {
    switch (this._activeTab) {
      case "StorageReport":
        return html`<media-manager-report></media-manager-report>`;
      case "Export":
        return html`<media-manager-export></media-manager-export>`;
      default:
        return html`<media-manager-results></media-manager-results>`;
    }
  }

  #renderTab(type: MediaManagerTab, label: string) {
    const count = this.#count(type);
    return html`
      <uui-tab
        label=${label}
        ?active=${this._activeTab === type}
        @click=${() => this.#context.setActiveTab(type)}
      >
        <span class="tab-label">
          ${label}
          ${count === undefined
            ? nothing
            : html`<span class="count ${count > 0 ? "has-items" : ""}">${count}</span>`}
        </span>
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
            ?disabled=${this._isScanning}
            @click=${() => this.#context.scanAll()}
          >
            <uui-icon name="icon-sync"></uui-icon>
            Rescan
          </uui-button>
        </div>

        <media-manager-stats></media-manager-stats>

        <uui-tab-group>
          ${this.#renderTab("UnusedMedia", "Unused media")}
          ${this.#renderTab("Duplicates", "Duplicates")}
          ${this.#renderTab("BrokenMedia", "Broken media")}
          ${this.#renderTab("OrphanedFiles", "Orphaned files")}
          ${this.#renderTab("StorageReport", "Storage report")}
          ${this.#renderTab("Export", "Export")}
        </uui-tab-group>

        ${this.#renderActivePanel()}
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
        align-items: center;
        justify-content: space-between;
        gap: var(--uui-size-space-4);
        flex-wrap: wrap;
      }
      .titles h1 {
        margin: 0;
        font-size: var(--uui-type-h3-size, 1.5rem);
        line-height: 1.2;
        font-weight: 700;
      }
      .titles p {
        margin: var(--uui-size-space-2) 0 0;
        color: var(--uui-color-text-alt);
        line-height: 1.4;
        max-width: 60ch;
      }
      uui-tab-group {
        --uui-tab-divider: var(--uui-color-divider);
      }
      .tab-label {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }
      .count {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-width: 1.25rem;
        height: 1.25rem;
        padding: 0 var(--uui-size-space-1);
        border-radius: 1rem;
        font-size: 0.75rem;
        font-weight: 700;
        line-height: 1;
        background: var(--uui-color-surface-alt);
        color: var(--uui-color-text-alt);
      }
      .count.has-items {
        background: var(--uui-color-danger);
        color: var(--uui-color-danger-contrast);
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
