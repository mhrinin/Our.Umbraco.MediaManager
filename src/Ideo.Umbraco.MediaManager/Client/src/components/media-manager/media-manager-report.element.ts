import { css, html, state, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "@umbraco-cms/backoffice/components";
import type { UmbTableColumn, UmbTableConfig, UmbTableItem } from "@umbraco-cms/backoffice/components";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { MediaManagerContext, ScanSlice } from "../../context/media-manager.context.js";
import { formatBytes } from "../../utils/format.js";

const TABLE_CONFIG: UmbTableConfig = { allowSelection: false, hideIcon: false };

const TYPE_COLUMNS: UmbTableColumn[] = [
  { name: "Media type", alias: "type" },
  { name: "Items", alias: "count" },
  { name: "Size", alias: "size" },
];

const LARGEST_COLUMNS: UmbTableColumn[] = [
  { name: "Name", alias: "name" },
  { name: "Path", alias: "path" },
  { name: "Size", alias: "size" },
];

@customElement("media-manager-report")
export class MediaManagerReportElement extends UmbLitElement {
  #context?: MediaManagerContext;
  @state() private _slice?: ScanSlice;

  constructor() {
    super();
    this.consumeContext(MEDIA_MANAGER_CONTEXT, (context) => {
      this.#context = context;
      this.observe(context?.slices, (slices) => {
        this._slice = slices?.StorageReport;
      });
    });
  }

  get #typeItems(): UmbTableItem[] {
    return (this._slice?.result?.report?.byType ?? []).map((type) => ({
      id: type.typeAlias,
      icon: type.icon,
      data: [
        { columnAlias: "type", value: type.typeAlias },
        { columnAlias: "count", value: `${type.count}` },
        { columnAlias: "size", value: formatBytes(type.bytes) },
      ],
    }));
  }

  get #largestItems(): UmbTableItem[] {
    return (this._slice?.result?.report?.largest ?? []).map((item) => ({
      id: item.id,
      icon: "icon-picture",
      data: [
        { columnAlias: "name", value: item.name },
        { columnAlias: "path", value: html`<span class="path">${item.path ?? ""}</span>` },
        { columnAlias: "size", value: formatBytes(item.sizeBytes) },
      ],
    }));
  }

  override render() {
    const slice = this._slice;
    if (!slice || slice.state === "idle" || slice.state === "scanning") {
      return html`
        <uui-box>
          <div class="state">
            <uui-loader-circle></uui-loader-circle>
            <span>Generating storage report… (${slice?.processed ?? 0} processed)</span>
          </div>
        </uui-box>
      `;
    }

    const report = slice.result?.report;
    if (slice.state === "failed" || !report) {
      return html`
        <uui-box>
          <div class="state">
            <span>The report could not be generated.</span>
            <uui-button look="secondary" label="Retry" @click=${() => this.#context?.scan("StorageReport")}>
              Retry
            </uui-button>
          </div>
        </uui-box>
      `;
    }

    return html`
      <div class="report">
        <uui-box>
          <div class="totals">
            <div>
              <div class="total-value">${formatBytes(report.totalBytes)}</div>
              <div class="total-label">across ${report.totalCount} media files</div>
            </div>
            <uui-button
              look="secondary"
              label="Refresh"
              @click=${() => this.#context?.scan("StorageReport")}
            >
              <uui-icon name="icon-sync"></uui-icon>
              Refresh
            </uui-button>
          </div>
        </uui-box>

        <uui-box headline="By media type">
          <umb-table .config=${TABLE_CONFIG} .columns=${TYPE_COLUMNS} .items=${this.#typeItems}></umb-table>
        </uui-box>

        <uui-box headline="Largest files">
          <umb-table .config=${TABLE_CONFIG} .columns=${LARGEST_COLUMNS} .items=${this.#largestItems}></umb-table>
        </uui-box>
      </div>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }
      .report {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-5);
      }
      .state {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-4);
        color: var(--uui-color-text-alt);
      }
      .totals {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--uui-size-space-4);
      }
      .total-value {
        font-size: var(--uui-type-h2-size, 2rem);
        font-weight: 700;
        line-height: 1.1;
      }
      .total-label {
        color: var(--uui-color-text-alt);
        margin-top: var(--uui-size-space-1);
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

export default MediaManagerReportElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-report": MediaManagerReportElement;
  }
}
