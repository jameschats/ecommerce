import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { API_BASE_URL } from '../../core/api.config';
import { ApiResponse } from '../../core/models/api-response.model';

interface ProductImagesResponse {
  images: { url: string }[];
}

/**
 * Full-size view of a calendar design, opened from a price-list or product-page thumbnail
 * (design.md §5.1).
 *
 * The price list carries only the primary image URL — adding every image to a 400-row
 * payload would cost far more than it saves. So the lightbox opens *immediately* with the
 * thumbnail's own image, then fetches the rest in the background and reveals navigation
 * only if there is more than one. Opening feels instant either way.
 */
@Component({
  selector: 'app-image-lightbox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-[60] bg-slate-950/85 backdrop-blur-sm flex flex-col"
           role="dialog" aria-modal="true" [attr.aria-label]="title()"
           (click)="close()">

        <!-- Header -->
        <div class="flex items-start gap-4 p-4 text-white shrink-0" (click)="$event.stopPropagation()">
          <div class="min-w-0">
            <p class="font-semibold leading-tight truncate">{{ title() }}</p>
            @if (designNo()) {
              <p class="text-sm text-white/70 font-mono mt-0.5">{{ designNo() }}</p>
            }
          </div>
          <div class="ml-auto flex items-center gap-3 shrink-0">
            @if (images().length > 1) {
              <span class="text-sm text-white/70 tabular-nums">{{ index() + 1 }} / {{ images().length }}</span>
            }
            <button type="button" (click)="close()" aria-label="Close"
                    class="w-9 h-9 grid place-items-center rounded-full bg-white/10 hover:bg-white/20 text-2xl leading-none">×</button>
          </div>
        </div>

        <!-- Stage -->
        <div class="flex-1 min-h-0 relative flex items-center justify-center px-4 pb-2">
          @if (images().length > 1) {
            <button type="button" (click)="prev(); $event.stopPropagation()" aria-label="Previous image"
                    class="absolute left-3 sm:left-6 z-10 w-11 h-11 rounded-full bg-white/10 hover:bg-white/25
                           text-white text-2xl grid place-items-center">‹</button>
            <button type="button" (click)="next(); $event.stopPropagation()" aria-label="Next image"
                    class="absolute right-3 sm:right-6 z-10 w-11 h-11 rounded-full bg-white/10 hover:bg-white/25
                           text-white text-2xl grid place-items-center">›</button>
          }

          <img [src]="current()" [alt]="title()"
               (click)="$event.stopPropagation()"
               class="max-w-full max-h-full object-contain rounded-lg shadow-2xl bg-white" />
        </div>

        <!-- Thumbnail strip, only when there is a choice to make -->
        @if (images().length > 1) {
          <div class="shrink-0 flex justify-center gap-2 p-3 overflow-x-auto" (click)="$event.stopPropagation()">
            @for (img of images(); track img; let i = $index) {
              <button type="button" (click)="index.set(i)" [attr.aria-label]="'Image ' + (i + 1)"
                      class="w-14 h-14 rounded border-2 overflow-hidden shrink-0 transition"
                      [class]="i === index() ? 'border-white' : 'border-transparent opacity-60 hover:opacity-100'">
                <img [src]="img" alt="" class="w-full h-full object-cover" />
              </button>
            }
          </div>
        }

        <p class="text-center text-white/40 text-xs pb-3 shrink-0">
          Press <kbd>Esc</kbd> to close@if (images().length > 1) {<span>, arrow keys to browse</span>}
        </p>
      </div>
    }
  `,
})
export class ImageLightboxComponent {
  private readonly http = inject(HttpClient);

  readonly open = signal(false);
  readonly images = signal<string[]>([]);
  readonly index = signal(0);
  readonly title = signal('');
  readonly designNo = signal('');

  readonly current = computed(() => this.images()[this.index()] ?? '');

  /** Opens straight away on the thumbnail's own image; siblings arrive a moment later. */
  show(productId: number, primaryUrl: string, title: string, designNo: string): void {
    this.images.set(primaryUrl ? [primaryUrl] : []);
    this.index.set(0);
    this.title.set(title);
    this.designNo.set(designNo);
    this.open.set(true);

    this.http
      .get<ApiResponse<ProductImagesResponse>>(`${API_BASE_URL}/catalog/products/${productId}`)
      .subscribe({
        next: (r) => {
          const urls = (r.data?.images ?? []).map((i) => i.url).filter(Boolean);
          if (urls.length > 1) {
            this.images.set(urls);
            // Keep whatever the buyer clicked in view rather than snapping to the first.
            const at = urls.indexOf(primaryUrl);
            this.index.set(at >= 0 ? at : 0);
          }
        },
        // A failed fetch simply means no siblings — the primary image is already showing.
        error: () => {},
      });
  }

  /**
   * Opens directly on a caller-supplied image list — for pages (e.g. product detail) that
   * already have every image loaded, so there is no fetch-then-reveal step: the full set
   * and correct starting index are known up front.
   */
  showWithImages(images: string[], startUrl: string, title: string, designNo: string): void {
    this.images.set(images);
    const at = images.indexOf(startUrl);
    this.index.set(at >= 0 ? at : 0);
    this.title.set(title);
    this.designNo.set(designNo);
    this.open.set(true);
  }

  close(): void {
    this.open.set(false);
  }

  next(): void {
    if (this.images().length > 1) this.index.update((i) => (i + 1) % this.images().length);
  }

  prev(): void {
    if (this.images().length > 1) {
      this.index.update((i) => (i - 1 + this.images().length) % this.images().length);
    }
  }

  handleKey(event: KeyboardEvent): void {
    if (!this.open()) return;
    if (event.key === 'Escape') this.close();
    else if (event.key === 'ArrowRight') this.next();
    else if (event.key === 'ArrowLeft') this.prev();
  }
}
