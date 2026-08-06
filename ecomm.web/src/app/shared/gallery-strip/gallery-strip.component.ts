import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GalleryImage } from '../../core/models/gallery.model';

/**
 * Continuous-scroll photo strip for the home page, below the price list. Horizontal on
 * desktop (reuses the header announcement's marquee-box/marquee-group pattern — two
 * copies of the row sliding left in lockstep), vertical on mobile (reuses the
 * marquee-track/marquee-fade pattern originally built for testimonials). Renders nothing
 * when there are no photos.
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

        <!-- Mobile: vertical continuous scroll -->
        <div class="sm:hidden marquee-fade overflow-hidden h-[70vh]">
          <div class="marquee-track flex flex-col gap-4">
            @for (copy of [0, 1]; track copy) {
              @for (img of images(); track img.galleryImageId) {
                <ng-container [ngTemplateOutlet]="card" [ngTemplateOutletContext]="{ $implicit: img }" />
              }
            }
          </div>
        </div>
      </section>
    }

    <ng-template #card let-img>
      @if (img.link) {
        <a [routerLink]="img.link" class="block shrink-0 w-[82%] sm:w-80 aspect-[2/3] rounded-xl overflow-hidden bg-slate-100">
          <img [src]="img.imageUrl" [alt]="img.title ?? ''" class="w-full h-full object-cover" loading="lazy" />
        </a>
      } @else {
        <div class="shrink-0 w-[82%] sm:w-80 aspect-[2/3] rounded-xl overflow-hidden bg-slate-100">
          <img [src]="img.imageUrl" [alt]="img.title ?? ''" class="w-full h-full object-cover" loading="lazy" />
        </div>
      }
    </ng-template>
  `,
})
export class GalleryStripComponent {
  readonly images = input.required<GalleryImage[]>();
}
