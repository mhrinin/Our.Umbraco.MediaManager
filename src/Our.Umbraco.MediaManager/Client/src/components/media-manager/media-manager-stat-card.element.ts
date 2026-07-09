import { css, html, nothing, property, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

/**
 * Presentational stat card (atom): an icon, a label, a headline value and a short description,
 * with a loading state.
 */
@customElement("media-manager-stat-card")
export class MediaManagerStatCardElement extends UmbLitElement {
  @property() icon = "";
  @property() label = "";
  @property() value = "";
  @property() description = "";
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
        ${this.description
          ? html`<div class="description">${this.description}</div>`
          : nothing}
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
        font-weight: 600;
      }
      .description {
        margin-top: var(--uui-size-space-1);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size, 0.8rem);
        line-height: 1.3;
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
