import {
  css,
  html,
  nothing,
  state,
  customElement,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import "@umbraco-cms/backoffice/components";
import type {
  UmbTableColumn,
  UmbTableConfig,
  UmbTableElement,
  UmbTableItem,
} from "@umbraco-cms/backoffice/components";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { MediaManagerContext, ScanSlice } from "../../context/media-manager.context.js";
import type { ScanType } from "../../types.d.js";
import { formatBytes } from "../../utils/format.js";

const MEDIA_COLUMNS: UmbTableColumn[] = [
  { name: "Name", alias: "name" },
  { name: "Path", alias: "path" },
  { name: "Size", alias: "size" },
];

const FILE_COLUMNS: UmbTableColumn[] = [
  { name: "Path", alias: "path" },
  { name: "Size", alias: "size" },
];

@customElement("media-manager-results")
export class MediaManagerResultsElement extends UmbLitElement {
  #context?: MediaManagerContext;

  @state() private _activeTab: ScanType = "OrphanedMedia";
  @state() private _slice?: ScanSlice;

  constructor() {
    super();
    this.consumeContext(MEDIA_MANAGER_CONTEXT, (context) => {
      this.#context = context;
      this.observe(context?.activeTab, (tab) => {
        if (tab) {
          this._activeTab = tab;
        }
      });
      this.observe(context?.slices, (slices) => {
        this._slice = slices?.[this._activeTab];
      });
    });
  }

  override willUpdate(changed: Map<string, unknown>) {
    super.willUpdate(changed);
    // Re-derive the active slice when the tab changes.
    if (changed.has("_activeTab") && this.#context) {
      this._slice = this.#context.getSlices()[this._activeTab];
    }
  }

  get #isMedia(): boolean {
    return this._activeTab === "OrphanedMedia";
  }

  get #config(): UmbTableConfig {
    return { allowSelection: true, hideIcon: false };
  }

  get #columns(): UmbTableColumn[] {
    return this.#isMedia ? MEDIA_COLUMNS : FILE_COLUMNS;
  }

  get #items(): UmbTableItem[] {
    const result = this._slice?.result;
    if (!result) {
      return [];
    }

    if (this.#isMedia) {
      return result.media.map((m) => ({
        id: m.key,
        icon: "icon-picture",
        data: [
          { columnAlias: "name", value: m.name },
          { columnAlias: "path", value: html`<span class="path">${m.path ?? ""}</span>` },
          { columnAlias: "size", value: formatBytes(m.sizeBytes) },
        ],
      }));
    }

    return result.files.map((f) => ({
      id: f.path,
      icon: "icon-document",
      data: [
        { columnAlias: "path", value: html`<span class="path">${f.path}</span>` },
        { columnAlias: "size", value: formatBytes(f.sizeBytes) },
      ],
    }));
  }

  #onSelection(event: Event) {
    const table = event.target as UmbTableElement;
    this.#context?.setSelection(this._activeTab, table.selection ?? []);
  }

  async #deleteSelected() {
    const count = this._slice?.selected.length ?? 0;
    if (count === 0) {
      return;
    }

    await umbConfirmModal(this, {
      headline: `Delete ${count} item(s)`,
      content: this.#isMedia
        ? "The selected media will be moved to the Recycle Bin, where they can be restored."
        : "The selected physical files will be permanently deleted. This cannot be undone.",
      color: "danger",
      confirmLabel: this.#isMedia ? "Move to Recycle Bin" : "Delete permanently",
    });

    await this.#context?.deleteSelected(this._activeTab);
  }

  override render() {
    const slice = this._slice;
    if (!slice || slice.state === "idle" || slice.state === "scanning") {
      return this.#renderScanning(slice?.processed ?? 0);
    }
    if (slice.state === "failed") {
      return html`<uui-box><p>The scan failed. Please try again.</p></uui-box>`;
    }
    return this.#items.length === 0 ? this.#renderEmpty() : this.#renderTable(slice);
  }

  #renderScanning(processed: number) {
    return html`
      <uui-box>
        <div class="scanning">
          <uui-loader-bar></uui-loader-bar>
          <span>Scanning… (${processed} processed)</span>
        </div>
      </uui-box>
    `;
  }

  #renderEmpty() {
    return html`
      <uui-box>
        <umb-empty-state size="small">
          Nothing to clean up here — your ${this.#isMedia ? "media" : "files"} are all in use. 🎉
        </umb-empty-state>
      </uui-box>
    `;
  }

  #renderTable(slice: ScanSlice) {
    const selectedCount = slice.selected.length;
    return html`
      <uui-box>
        <div class="toolbar" slot="header-actions">
          ${selectedCount > 0
            ? html`<span class="selected-count">${selectedCount} selected</span>`
            : nothing}
          <uui-button
            look="primary"
            color="danger"
            label=${this.#isMedia ? "Move to Recycle Bin" : "Delete files"}
            ?disabled=${selectedCount === 0}
            @click=${this.#deleteSelected}
          ></uui-button>
        </div>
        <umb-table
          .config=${this.#config}
          .columns=${this.#columns}
          .items=${this.#items}
          .selection=${slice.selected}
          @selected=${this.#onSelection}
          @deselected=${this.#onSelection}
        ></umb-table>
      </uui-box>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }
      .toolbar {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
      }
      .selected-count {
        color: var(--uui-color-text-alt);
      }
      .scanning {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
      }
      .path {
        color: var(--uui-color-text-alt);
        font-family: var(--uui-font-monospace, monospace);
        font-size: var(--uui-type-small-size, 0.8rem);
        word-break: break-all;
      }
    `,
  ];
}

export default MediaManagerResultsElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-results": MediaManagerResultsElement;
  }
}
