import { css, html, state, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "@umbraco-cms/backoffice/components";
import { MEDIA_MANAGER_CONTEXT } from "../../context/media-manager.context-token.js";
import type { MediaManagerContext, ScanSlice } from "../../context/media-manager.context.js";
import { exportDownloadUrl } from "../../services/media-manager.repository.js";
import { formatBytes } from "../../utils/format.js";

@customElement("media-manager-export")
export class MediaManagerExportElement extends UmbLitElement {
  #context?: MediaManagerContext;
  @state() private _slice?: ScanSlice;

  constructor() {
    super();
    this.consumeContext(MEDIA_MANAGER_CONTEXT, (context) => {
      this.#context = context;
      this.observe(context?.slices, (slices) => {
        this._slice = slices?.Export;
      });
    });
  }

  #startExport() {
    this.#context?.scan("Export");
  }

  #download(jobId: string, token: string) {
    // A plain href on the button gets intercepted by the backoffice SPA router (the download
    // attribute is not forwarded to uui-button's internal anchor), which just changes the URL.
    // Assigning location directly issues a real request; the attachment response means the
    // browser starts the download and never leaves the page.
    window.location.href = exportDownloadUrl(jobId, token);
  }

  override render() {
    const slice = this._slice;
    if (slice?.state === "scanning") {
      return this.#renderScanning(slice.processed);
    }
    if (slice?.state === "failed") {
      return this.#renderFailed();
    }
    if (slice?.state === "done" && slice.result?.export) {
      return this.#renderDone(slice.result.jobId, slice.result.export);
    }
    return this.#renderIdle();
  }

  #renderIdle() {
    return html`
      <uui-box>
        <div class="state">
          <uui-icon name="icon-download-alt" class="lead-icon"></uui-icon>
          <p class="explain">
            Builds a ZIP of the entire media filesystem, preserving folder structure, so it can be
            extracted into another environment's media folder or uploaded to blob storage.
            The regenerable image cache is excluded. Large libraries produce large archives —
            the export runs in the background and the download supports resuming.
          </p>
          <uui-button look="primary" label="Create export" @click=${this.#startExport}>
            Create export
          </uui-button>
        </div>
      </uui-box>
    `;
  }

  #renderScanning(processed: number) {
    return html`
      <uui-box>
        <div class="state">
          <uui-loader-circle></uui-loader-circle>
          <span>Creating export… (${processed} files added)</span>
        </div>
      </uui-box>
    `;
  }

  #renderFailed() {
    return html`
      <uui-box>
        <div class="state">
          <uui-icon name="icon-alert" class="failed-icon"></uui-icon>
          <span>The export failed.</span>
          <uui-button look="secondary" label="Retry" @click=${this.#startExport}>Retry</uui-button>
        </div>
      </uui-box>
    `;
  }

  #renderDone(jobId: string, exportInfo: NonNullable<NonNullable<ScanSlice["result"]>["export"]>) {
    return html`
      <uui-box>
        <div class="done">
          <div class="summary">
            <div class="size">${formatBytes(exportInfo.zipSizeBytes)}</div>
            <div class="meta">
              ${exportInfo.fileCount} file(s) · created ${new Date(exportInfo.createdUtc).toLocaleString()}
            </div>
          </div>

          ${exportInfo.skippedCount > 0
            ? html`
                <div class="skipped">
                  <strong>${exportInfo.skippedCount} file(s) could not be read and were skipped:</strong>
                  <ul>
                    ${exportInfo.errors.map((error) => html`<li>${error}</li>`)}
                  </ul>
                </div>
              `
            : null}

          <div class="actions">
            <uui-button
              look="primary"
              label="Download ZIP"
              @click=${() => this.#download(jobId, exportInfo.downloadToken)}
            >
              <uui-icon name="icon-download-alt"></uui-icon>
              Download ZIP
            </uui-button>
            <uui-button look="secondary" label="Re-export" @click=${this.#startExport}>
              Re-export
            </uui-button>
          </div>

          <p class="hint">The export stays available until you create a new one or the site restarts.</p>
        </div>
      </uui-box>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }
      .state {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-5);
        text-align: center;
        color: var(--uui-color-text-alt);
      }
      .lead-icon {
        font-size: 2rem;
      }
      .failed-icon {
        font-size: 2rem;
        color: var(--uui-color-danger);
      }
      .explain {
        margin: 0;
        max-width: 60ch;
        line-height: 1.5;
      }
      .done {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-4);
        padding: var(--uui-size-space-3);
      }
      .size {
        font-size: var(--uui-type-h2-size, 2rem);
        font-weight: 700;
        line-height: 1.1;
      }
      .meta {
        color: var(--uui-color-text-alt);
        margin-top: var(--uui-size-space-1);
      }
      .actions {
        display: flex;
        gap: var(--uui-size-space-3);
      }
      .skipped {
        padding: var(--uui-size-space-3);
        border-left: 4px solid var(--uui-color-warning);
        background: var(--uui-color-warning-emphasis, rgba(251, 213, 27, 0.1));
      }
      .skipped ul {
        margin: var(--uui-size-space-2) 0 0;
        padding-left: var(--uui-size-space-5);
        font-family: var(--uui-font-monospace, monospace);
        font-size: var(--uui-type-small-size, 0.8rem);
      }
      .hint {
        margin: 0;
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size, 0.9rem);
      }
    `,
  ];
}

export default MediaManagerExportElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-export": MediaManagerExportElement;
  }
}
