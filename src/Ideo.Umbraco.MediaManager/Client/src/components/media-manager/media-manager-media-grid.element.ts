import { css, html, property, customElement, ifDefined } from "@umbraco-cms/backoffice/external/lit";
import { repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbSelectedEvent, UmbDeselectedEvent } from "@umbraco-cms/backoffice/event";
import "@umbraco-cms/backoffice/imaging";
import type { ScanItem } from "../../types.d.js";
import { formatBytes } from "../../utils/format.js";

const IMAGE_EXTENSIONS = new Set(["jpg", "jpeg", "png", "gif", "webp", "avif", "svg", "bmp", "tiff"]);

/**
 * Native-look media card grid (the same uui-card-media + umb-imaging-thumbnail pair the Media
 * section's grid renders). Exposes the umb-table selection contract — a `selection` array plus
 * selected/deselected events read via `event.target.selection` — so the results element handles
 * both views with one handler.
 */
@customElement("media-manager-media-grid")
export class MediaManagerMediaGridElement extends UmbLitElement {
  @property({ attribute: false }) items: ScanItem[] = [];
  @property({ attribute: false }) selection: string[] = [];
  @property({ attribute: false }) hrefFor?: (unique: string) => string | undefined;

  #onCardSelected(event: Event, id: string) {
    // The card's UUISelectableEvent bubbles; without this, the parent's handler would receive the
    // CARD as target (whose .selection is undefined) and wipe the page selection.
    event.stopPropagation();
    this.selection = [...this.selection.filter((selectedId) => selectedId !== id), id];
    this.dispatchEvent(new UmbSelectedEvent(id));
  }

  #onCardDeselected(event: Event, id: string) {
    event.stopPropagation();
    this.selection = this.selection.filter((selectedId) => selectedId !== id);
    this.dispatchEvent(new UmbDeselectedEvent(id));
  }

  #iconFor(path: string | null): string {
    const extension = path?.split(".").pop()?.toLowerCase() ?? "";
    return IMAGE_EXTENSIONS.has(extension) ? "icon-picture" : "icon-document";
  }

  override render() {
    return repeat(
      this.items,
      (item) => item.id,
      (item) => html`
        <uui-card-media
          name=${item.name}
          detail=${formatBytes(item.sizeBytes)}
          href=${ifDefined(this.hrefFor?.(item.id))}
          selectable
          ?selected=${this.selection.includes(item.id)}
          @selected=${(event: Event) => this.#onCardSelected(event, item.id)}
          @deselected=${(event: Event) => this.#onCardDeselected(event, item.id)}
        >
          <umb-imaging-thumbnail
            unique=${item.id}
            alt=${item.name}
            icon=${this.#iconFor(item.path)}
          ></umb-imaging-thumbnail>
        </uui-card-media>
      `,
    );
  }

  static override styles = [
    css`
      :host {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        grid-auto-rows: 180px;
        gap: var(--uui-size-space-5);
      }
    `,
  ];
}

export default MediaManagerMediaGridElement;

declare global {
  interface HTMLElementTagNameMap {
    "media-manager-media-grid": MediaManagerMediaGridElement;
  }
}
