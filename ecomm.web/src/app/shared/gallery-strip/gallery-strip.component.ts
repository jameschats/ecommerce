import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GalleryImage } from '../../core/models/gallery.model';

/**
 * Photo strip for the home page, below the price list. Desktop/tablet gets a continuous
 * horizontal auto-scroll (reuses the header announcement's marquee-box/marquee-group
 * pattern — two copies of the row sliding left in lockstep). Mobile is a plain static
 * stacked list scrolled normally with the page — auto-scrolling a full-width column while
 * the visitor is also trying to scroll the page fought with their thumb, so mobile just
 * doesn't animate. Renders nothing when there are no photos.
 */
@Component({
  selector: 'app-gallery-strip',
  imports: [RouterLink, NgTemplateOutlet],
  template: `
    @if (images().length) {
      <section class="page-container py-8">
        <h2 class="text-lg sm:text-xl font-bold text-slate-900 mb-4">Our Work</h2>

        <!-- Desktop / tablet: horizontal continuous scroll -->
        <div class="hidden sm:block marquee-box overflow-hidden">
          <div class="flex">
            @for (copy of [0, 1]; track copy) {
              <div class="marquee-group flex gap-4 shrink-0 min-w-full pr-4" [attr.aria-hidden]="copy === 1 ? 'true' : null">
                @for (img of images(); track img.galleryImageId) {
                  <ng-container [ngTemplateOutlet]="card" [ngTemplateOutletContext]="{ $implicit: img }" />
                }
              </div>
            }
          </div>
        </div>

        <!-- Mobile: plain stacked list, scrolls with the page -->
        <div class="sm:hidden flex flex-col gap-4">
          @for (img of images(); track img.galleryImageId) {
            <ng-container [ngTemplateOutlet]="card" [ngTemplateOutletContext]="{ $implicit: img }" />
          }
        </div>
      </section>
    }

    <ng-template #card let-img>
      @if (img.link) {
        <a [routerLink]="img.link" class="block w-full sm:w-80 sm:shrink-0 aspect-[2/3] rounded-xl overflow-hidden bg-slate-100">
          <img [src]="img.imageUrl" [alt]="img.title ?? ''" class="w-full h-full object-cover" loading="lazy" />
        </a>
      } @else {
        <div class="w-full sm:w-80 sm:shrink-0 aspect-[2/3] rounded-xl overflow-hidden bg-slate-100">
          <img [src]="img.imageUrl" [alt]="img.title ?? ''" class="w-full h-full object-cover" loading="lazy" />
        </div>
      }
    </ng-template>
  `,
})
export class GalleryStripComponent {
  readonly images = input.required<GalleryImage[]>();
}
