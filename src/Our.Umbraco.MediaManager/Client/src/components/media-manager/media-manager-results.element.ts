import { css, html, nothing, state, customElement } from "@umbraco-cms/backoffice/external/lit";
import type { UUIPaginationElement } from "@umbraco-cms/backoffice/external/uui";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UmbModalRouteRegistrationController } from "@umbraco-cms/backoffice/router";
import { UMB_WORKSPACE_MODAL } from "@umbraco-cms/backoffice/workspace";
import {
  UMB_MEDIA_ENTITY_TYPE,
  UMB_EDIT_MEDIA_WORKSPACE_PATH_PATTERN,
} from "@umbraco-cms/backoffice/media";
import "@umbraco-cms/backoffice/components";
import type {
  UmbTableColumn,
  UmbTableConfig,
  UmbTableElement,
  UmbTableItem,
} from "@umbraco-cms/backoffice/components";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import { PAGE_SIZE } from "../../context/media-manager.context.js";
import type { MediaManagerContext, ScanSlice } from "../../context/media-manager.context.js";
import type { ScanType } from "../../types.d.js";
import { formatBytes } from "../../utils/format.js";
import "./media-manager-media-grid.element.js";
import type { MediaManagerMediaGridElement } from "./media-manager-media-grid.element.js";

type ModalRouteBuilder = (params: Record<string, string | number> | null) => string;

const COLUMNS: UmbTableColumn[] = [
  { name: "Name", alias: "name" },
  { name: "Path", alias: "path" },
  { name: "Size", alias: "size" },
];

@customElement("media-manager-results")
export class MediaManagerResultsElement extends UmbLitElement {
  #context?: MediaManagerContext;

  @state() private _activeTab: ScanType = "UnusedMedia";
  @state() private _slice?: ScanSlice;
  @state() private _mediaEditBuilder?: ModalRouteBuilder;

  constructor() {
    super();

    // Register a modal route so a media item can be opened (and inspected) in a workspace
    // overlay without leaving the dashboard.
    new UmbModalRouteRegistrationController(this, UMB_WORKSPACE_MODAL)
      .addUniquePaths(["unique"])
      .onSetup((params) => ({
        data: { entityType: UMB_MEDIA_ENTITY_TYPE, preset: { unique: params.unique } },
      }))
      .observeRouteBuilder((builder) => {
        this._mediaEditBuilder = builder;
      });

    this.consumeContext(MEDIA_MANAGER_CONTEXT, (context) => {
      this.#context = context;
      this.observe(context?.activeTab, (tab) => {
        // Only cleanup tabs have result tables; report/export render their own panels.
        if (tab && tab !== "StorageReport" && tab !== "Export") {
          this._activeTab = tab;
          this._slice = context?.getSlices()[tab];
        }
      });
      this.observe(context?.slices, (slices) => {
        this._slice = slices?.[this._activeTab];
      });
    });
  }

  get #isMedia(): boolean {
    // Orphaned files are physical files; every other scan targets media nodes.
    return this._activeTab !== "OrphanedFiles";
  }

  get #isGridView(): boolean {
    // Unused media and duplicates exist as viewable media — show them as native preview cards.
    // Broken media has no file to preview and orphaned files are not media: they keep the table.
    return this._activeTab === "UnusedMedia" || this._activeTab === "Duplicates";
  }

  get #config(): UmbTableConfig {
    return { allowSelection: true, hideIcon: false };
  }

  get #total(): number {
    return this._slice?.result?.totalItems ?? 0;
  }

  get #pageCount(): number {
    return Math.max(1, Math.ceil(this.#total / PAGE_SIZE));
  }

  get #pageItems(): UmbTableItem[] {
    return (this._slice?.pageItems ?? []).map((item) => ({
      id: item.id,
      icon: this.#isMedia ? "icon-picture" : "icon-document",
      data: [
        // Media names link to the workspace overlay; physical files have nowhere to open.
        { columnAlias: "name", value: this.#isMedia ? this.#renderName(item.id, item.name) : item.name },
        { columnAlias: "path", value: html`<span class="path">${item.path ?? ""}</span>` },
        { columnAlias: "size", value: formatBytes(item.sizeBytes) },
      ],
    }));
  }

  #mediaHref = (unique: string): string | undefined =>
    this._mediaEditBuilder
      ? this._mediaEditBuilder({ unique }) +
        UMB_EDIT_MEDIA_WORKSPACE_PATH_PATTERN.generateLocal({ unique })
      : undefined;

  #renderName(key: string, name: string) {
    const href = this.#mediaHref(key);
    if (!href) {
      return name;
    }
    return html`<uui-button look="link" compact label=${name} href=${href}>${name}</uui-button>`;
  }

  #onSelection(event: Event) {
    const view = event.target as UmbTableElement | MediaManagerMediaGridElement;
    const tableSelection = view.selection ?? [];

    // Any manual (de)selection drops the "all selected" mode back to explicit ids.
    if (this._slice?.allSelected) {
      this.#context?.setSelection(this._activeTab, tableSelection);
      return;
    }

    // The table only knows the current page (its select-all replaces the selection with the
    // visible items), so off-page selections are merged back in here.
    const pageIds = new Set(this.#pageItems.map((item) => item.id));
    const offPage = (this._slice?.selected ?? []).filter((id) => !pageIds.has(id));
    this.#context?.setSelection(this._activeTab, [...offPage, ...tableSelection]);
  }

  #onPageChange(event: Event) {
    this.#context?.loadPage(this._activeTab, (event.target as UUIPaginationElement).current);
  }

  async #deleteSelected() {
    const slice = this._slice;
    const count = slice?.allSelected ? this.#total : slice?.selected.length ?? 0;
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
      return this.#renderFailed();
    }
    return this.#total === 0 ? this.#renderEmpty() : this.#renderResults(slice);
  }

  #renderScanning(processed: number) {
    return html`
      <uui-box>
        <div class="state">
          <uui-loader-circle></uui-loader-circle>
          <span>Scanning… (${processed} processed)</span>
        </div>
      </uui-box>
    `;
  }

  #renderFailed() {
    return html`
      <uui-box>
        <div class="state">
          <uui-icon name="icon-alert" class="failed-icon"></uui-icon>
          <span>The scan failed.</span>
          <uui-button
            look="secondary"
            label="Retry"
            @click=${() => this.#context?.scan(this._activeTab)}
          >
            Retry
          </uui-button>
        </div>
      </uui-box>
    `;
  }

  #renderEmpty() {
    return html`
      <uui-box>
        <div class="state">
          <uui-icon name="icon-check" class="empty-icon"></uui-icon>
          <span>Nothing to clean up here.</span>
        </div>
      </uui-box>
    `;
  }

  #renderSummary(slice: ScanSlice) {
    if (slice.allSelected) {
      return `All ${this.#total} selected`;
    }
    return slice.selected.length > 0 ? `${slice.selected.length} selected` : `${this.#total} item(s)`;
  }

  #renderResults(slice: ScanSlice) {
    const total = this.#total;
    const selectedCount = slice.allSelected ? total : slice.selected.length;
    const pageIds = new Set(this.#pageItems.map((item) => item.id));
    const viewSelection = slice.allSelected
      ? [...pageIds]
      : slice.selected.filter((id) => pageIds.has(id));
    return html`
      <uui-box class="results">
        <div class="toolbar">
          <span class="summary">${this.#renderSummary(slice)}</span>
          <div class="actions">
            ${!slice.allSelected && selectedCount < total
              ? html`
                  <uui-button
                    look="secondary"
                    compact
                    label="Select all ${total}"
                    @click=${() => this.#context?.selectAll(this._activeTab)}
                  >
                    Select all ${total}
                  </uui-button>
                `
              : nothing}
            ${selectedCount > 0
              ? html`
                  <uui-button
                    look="secondary"
                    compact
                    label="Clear selection"
                    @click=${() => this.#context?.setSelection(this._activeTab, [])}
                  >
                    Clear selection
                  </uui-button>
                `
              : nothing}
            <uui-button
              look="primary"
              color="danger"
              label=${this.#isMedia ? "Move to Recycle Bin" : "Delete files"}
              ?disabled=${selectedCount === 0}
              @click=${this.#deleteSelected}
            >
              ${this.#isMedia ? "Move to Recycle Bin" : "Delete files"}
            </uui-button>
          </div>
        </div>
        ${this.#isGridView
          ? html`
              <media-manager-media-grid
                .items=${slice.pageItems}
                .selection=${viewSelection}
                .hrefFor=${this.#mediaHref}
                @selected=${this.#onSelection}
                @deselected=${this.#onSelection}
              ></media-manager-media-grid>
            `
          : html`
              <umb-table
                .config=${this.#config}
                .columns=${COLUMNS}
                .items=${this.#pageItems}
                .selection=${viewSelection}
                @selected=${this.#onSelection}
                @deselected=${this.#onSelection}
              ></umb-table>
            `}
        ${this.#pageCount > 1
          ? html`
              <uui-pagination
                .current=${slice.page}
                .total=${this.#pageCount}
                @change=${this.#onPageChange}
              ></uui-pagination>
            `
          : nothing}
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
        justify-content: space-between;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-4);
      }
      .summary {
        color: var(--uui-color-text-alt);
      }
      .actions {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
      }
      uui-pagination {
        display: block;
        margin-top: var(--uui-size-space-4);
      }
      .state {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-4);
        text-align: center;
        color: var(--uui-color-text-alt);
      }
      .empty-icon {
        font-size: 2rem;
        color: var(--uui-color-positive);
      }
      .failed-icon {
        font-size: 2rem;
        color: var(--uui-color-danger);
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
