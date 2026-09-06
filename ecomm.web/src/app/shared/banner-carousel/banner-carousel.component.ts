import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, PLATFORM_ID, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HomeBanner } from '../../core/models/banner.model';

/**
 * Same Flipkart-style auto-scrolling banner row as the home page, packaged so Order Now,
 * Finished Calendar and About Us can each show their own admin-managed carousel without
 * duplicating the scroll/timer logic. Renders nothing when there are no banners for the page.
 */
@Component({
  selector: 'app-banner-carousel',
  imports: [RouterLink],
  template: `
    @if (banners().length) {
      <section class="page-container pt-6 pb-2">
        <div class="relative group/banner">
          <div #bannerTrack class="flex gap-3 overflow-x-auto snap-x snap-mandatory no-scrollbar scroll-smooth">
            @for (b of banners(); track b.homeBannerId) {
              <a [routerLink]="b.link ?? '/order'"
                 class="snap-start shrink-0 w-[88%] sm:w-[52%] lg:w-[40%] relative rounded-xl overflow-hidden h-[170px] sm:h-[230px] bg-slate-900">
                <img [src]="b.imageUrl" [alt]="b.title ?? ''" class="absolute inset-0 w-full h-full object-cover" />
                @if (b.title || b.subtitle || b.cta) {
                  <div class="absolute inset-0 p-5 sm:p-6 flex flex-col justify-center max-w-[75%] text-white">
                    @if (b.title) { <h3 class="text-lg sm:text-2xl font-bold leading-tight drop-shadow-md">{{ b.title }}</h3> }
                    @if (b.subtitle) { <p class="text-xs sm:text-sm text-white/90 mt-1 line-clamp-2 drop-shadow-md">{{ b.subtitle }}</p> }
                    <!-- Optional per banner: leaving the CTA blank in admin hides this entirely
                         (no empty-but-styled box), rather than always-off or always-on. -->
                    @if (b.cta) { <span class="mt-3 w-fit bg-primary px-3.5 py-1.5 rounded-md text-xs sm:text-sm font-medium">{{ b.cta }}</span> }
                  </div>
                }
              </a>
            }
          </div>

          @if (banners().length > 1) {
            <button type="button" (click)="prev()" aria-label="Previous"
              class="hidden sm:grid absolute left-2 top-1/2 -translate-y-1/2 bg-white/90 hover:bg-white text-slate-700 w-10 h-10 rounded-full place-items-center shadow-md opacity-0 group-hover/banner:opacity-100 transition">‹</button>
            <button type="button" (click)="next()" aria-label="Next"
              class="hidden sm:grid absolute right-2 top-1/2 -translate-y-1/2 bg-white/90 hover:bg-white text-slate-700 w-10 h-10 rounded-full place-items-center shadow-md opacity-0 group-hover/banner:opacity-100 transition">›</button>
          }
        </div>
      </section>
    }
  `,
})
export class BannerCarouselComponent implements OnInit, OnDestroy {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly banners = input.required<HomeBanner[]>();
  readonly currentSlide = signal(0);
  private readonly bannerTrack = viewChild<ElementRef<HTMLDivElement>>('bannerTrack');
  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    if (this.isBrowser && this.banners().length > 1) this.timer = setInterval(() => this.next(), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  next(): void { this.currentSlide.update((i) => (i + 1) % this.banners().length); this.scrollToCurrent(); }
  prev(): void { this.currentSlide.update((i) => (i - 1 + this.banners().length) % this.banners().length); this.scrollToCurrent(); }
  goTo(i: number): void { this.currentSlide.set(i); this.scrollToCurrent(); }

  private scrollToCurrent(): void {
    const track = this.bannerTrack()?.nativeElement;
    const card = track?.children[this.currentSlide()] as HTMLElement | undefined;
    if (track && card) track.scrollTo({ left: card.offsetLeft, behavior: 'smooth' });
  }
}
