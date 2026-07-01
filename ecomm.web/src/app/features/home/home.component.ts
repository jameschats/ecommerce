import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, PLATFORM_ID, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SITE_URL } from '../../core/api.config';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { AuthService } from '../../core/services/auth.service';
import { HomeSection } from '../../core/services/cms.service';
import { SeoService } from '../../core/services/seo.service';
import { ProductCardComponent } from '../../shared/product-card/product-card.component';
import { HomeData } from './home.resolver';

interface HeroSlide { image: string; title: string; subtitle: string; cta: string; link: string; }
interface Testimonial { name: string; company: string; rating: number; text: string; }

@Component({
  selector: 'app-home',
  imports: [RouterLink, ProductCardComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly auth = inject(AuthService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly isAdmin = this.auth.isAdmin;

  readonly sections = signal<HomeSection[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly featured = signal<ProductListItem[]>([]);
  readonly newest = signal<ProductListItem[]>([]);

  readonly slides: HeroSlide[] = [
    { image: 'https://picsum.photos/seed/calbanner1/900/300', title: 'Customizable 2026 Calendars', subtitle: 'Wall, desk & pocket — with your photos, brand & logo.', cta: 'Shop calendars', link: '/products' },
    { image: 'https://picsum.photos/seed/calbanner2/900/300', title: 'Corporate Gifting', subtitle: 'Branded calendars in bulk.', cta: 'Order in bulk', link: '/products' },
    { image: 'https://picsum.photos/seed/calbanner3/900/300', title: 'Desk Calendars', subtitle: 'Elegant picks for any workspace.', cta: 'Browse', link: '/category/desk-calendars' },
    { image: 'https://picsum.photos/seed/calbanner4/900/300', title: 'Photo Calendars', subtitle: 'Turn your memories into a year.', cta: 'Create yours', link: '/products' },
    { image: 'https://picsum.photos/seed/calbanner5/900/300', title: 'New-Year Offers', subtitle: 'Up to 30% off select ranges.', cta: 'Grab deals', link: '/products' },
    { image: 'https://picsum.photos/seed/calbanner6/900/300', title: 'Pocket & Tent Calendars', subtitle: 'Handy formats for every desk.', cta: 'Explore', link: '/products' },
  ];
  readonly currentSlide = signal(0);
  private readonly bannerTrack = viewChild<ElementRef<HTMLDivElement>>('bannerTrack');
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly googleRating = 4.5;
  readonly reviewCount = '100+';
  readonly testimonials: Testimonial[] = [
    { name: 'Rathish Radhakrishnan', company: 'Cognizant', rating: 5, text: 'Great job for on-time delivery & all assignments with quality before promised slots. Much appreciated.' },
    { name: 'Suraj Ahamed', company: '', rating: 4, text: 'Supportive team. Handout quality was good and shared with senior management.' },
    { name: 'ONCOSPARK INDIA PVT LTD', company: '', rating: 5, text: 'Excellent print quality. Dedicated services. Thanks for the wonderful job. Keep it up guys.' },
    { name: 'Joy Bose', company: '', rating: 5, text: 'It looks amazing! The photos printed really clear.' },
    { name: 'Priya Menon', company: '', rating: 5, text: 'Loved the desk calendar with photo frame — premium feel and quick delivery.' },
    { name: 'Aakash Gupta', company: 'Innovaegis', rating: 5, text: 'Perpetual desk calendar is a hit in our office. Will reorder next year.' },
  ];
  readonly colA = this.testimonials.filter((_, i) => i % 2 === 0);
  readonly colB = this.testimonials.filter((_, i) => i % 2 === 1);

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

    // Data is preloaded by homeResolver → present on first render (no reflow).
    const data = this.route.snapshot.data['home'] as HomeData | undefined;
    if (data) {
      this.sections.set(data.sections);
      this.categories.set(data.categories);
      this.featured.set(data.featured);
      this.newest.set(data.newest);
    }

    if (this.isBrowser) this.timer = setInterval(() => this.next(), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  next(): void { this.currentSlide.update((i) => (i + 1) % this.slides.length); this.scrollToCurrent(); }
  prev(): void { this.currentSlide.update((i) => (i - 1 + this.slides.length) % this.slides.length); this.scrollToCurrent(); }
  goTo(i: number): void { this.currentSlide.set(i); this.scrollToCurrent(); }

  /** Scroll the banner track so the current banner aligns to the left (browser only). */
  private scrollToCurrent(): void {
    const track = this.bannerTrack()?.nativeElement;
    const card = track?.children[this.currentSlide()] as HTMLElement | undefined;
    if (track && card) track.scrollTo({ left: card.offsetLeft, behavior: 'smooth' });
  }

  railItems(type: string): ProductListItem[] {
    return type === 'NewArrivals' ? this.newest() : this.featured();
  }

  star(n: number): string {
    return '★'.repeat(Math.max(0, Math.min(5, n)));
  }
}
