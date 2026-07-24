import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, PLATFORM_ID, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SITE_URL } from '../../core/api.config';
import { SeoService } from '../../core/services/seo.service';
import { QuickOrderTableComponent } from '../order/quick-order-table.component';
import { HomeData } from './home.resolver';

interface HeroSlide { image: string; title: string; subtitle: string; cta: string; link: string; }

/**
 * Home = banner carousel + the quick-order price list (design.md §4).
 *
 * The category tiles, product rails and testimonials that used to sit between them are
 * gone: in Phase 1 the table *is* the storefront, and anything between the banners and
 * the first row of the price list just pushes the working screen below the fold.
 */
@Component({
  selector: 'app-home',
  imports: [RouterLink, QuickOrderTableComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Fallback banners — shown only if the admin has configured none.
  private readonly defaultSlides: HeroSlide[] = [
    { image: 'https://picsum.photos/seed/calbanner1/900/300', title: 'Customizable 2026 Calendars', subtitle: 'Wall, desk & pocket — with your photos, brand & logo.', cta: 'Order now', link: '/order' },
    { image: 'https://picsum.photos/seed/calbanner2/900/300', title: 'Corporate Gifting', subtitle: 'Branded calendars in bulk.', cta: 'Order in bulk', link: '/order' },
    { image: 'https://picsum.photos/seed/calbanner3/900/300', title: 'Desk Calendars', subtitle: 'Elegant picks for any workspace.', cta: 'Browse', link: '/order' },
    { image: 'https://picsum.photos/seed/calbanner4/900/300', title: 'Photo Calendars', subtitle: 'Turn your memories into a year.', cta: 'Create yours', link: '/order' },
    { image: 'https://picsum.photos/seed/calbanner5/900/300', title: 'New-Year Offers', subtitle: 'Up to 30% off select ranges.', cta: 'Grab deals', link: '/order' },
    { image: 'https://picsum.photos/seed/calbanner6/900/300', title: 'Pocket & Tent Calendars', subtitle: 'Handy formats for every desk.', cta: 'Explore', link: '/order' },
  ];
  readonly slides = signal<HeroSlide[]>(this.defaultSlides);
  readonly currentSlide = signal(0);
  private readonly bannerTrack = viewChild<ElementRef<HTMLDivElement>>('bannerTrack');
  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'CalendarShop — Custom 2026 Calendars: Wall, Desk, Pocket & More',
      description: 'Personalized 2026 calendars — wall, desk, tent, pocket, magnet & mouse-pad. Add your photos, brand name and logo. Fast delivery, great prices.',
      url: `${SITE_URL}/`,
    });
    this.seo.setJsonLd([
      {
        '@context': 'https://schema.org', '@type': 'WebSite', name: 'CalendarShop', url: SITE_URL,
        potentialAction: { '@type': 'SearchAction', target: `${SITE_URL}/products?search={search_term_string}`, 'query-input': 'required name=search_term_string' },
      },
      { '@context': 'https://schema.org', '@type': 'Organization', name: 'CalendarShop', url: SITE_URL },
    ]);

    // Preloaded by homeResolver → present on first render (no reflow).
    const data = this.route.snapshot.data['home'] as HomeData | undefined;
    if (data?.banners?.length) {
      this.slides.set(data.banners.map((b) => ({
        image: b.imageUrl ?? '', title: b.title ?? '', subtitle: b.subtitle ?? '',
        cta: b.cta ?? '', link: b.link ?? '/order',
      })));
    }

    if (this.isBrowser) this.timer = setInterval(() => this.next(), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  next(): void { this.currentSlide.update((i) => (i + 1) % this.slides().length); this.scrollToCurrent(); }
  prev(): void { this.currentSlide.update((i) => (i - 1 + this.slides().length) % this.slides().length); this.scrollToCurrent(); }
  goTo(i: number): void { this.currentSlide.set(i); this.scrollToCurrent(); }

  /** Scroll the banner track so the current banner aligns to the left (browser only). */
  private scrollToCurrent(): void {
    const track = this.bannerTrack()?.nativeElement;
    const card = track?.children[this.currentSlide()] as HTMLElement | undefined;
    if (track && card) track.scrollTo({ left: card.offsetLeft, behavior: 'smooth' });
  }
}
