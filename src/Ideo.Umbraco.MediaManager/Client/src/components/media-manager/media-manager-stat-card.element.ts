import { css, html, property, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

/**
 * Presentational stat card (atom): an icon, a label and a headline value, with a loading state.
 */
@customElement("media-manager-stat-card")
export class MediaManagerStatCardElement extends UmbLitElement {
  @property() icon = "";
  @property() label = "";
  @property() value = "";
  @property({ type: Boolean }) loading = false;

  override render() {
    return html`
      <uui-box>
        <div class="value">
          ${this.loading ? html`<uui-loader-circle></uui-loader-circle>` : this.value}
        </div>
        <div class="label">
          <uui-icon name=${this.icon}></uui-icon>
          <span>${this.label}</span>
        </div>
      </uui-box>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }
      uui-box {
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

export default MediaManagerStatCardElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-stat-card": MediaManagerStatCardElement;
  }
}
