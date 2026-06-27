import { isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SITE_URL } from '../../core/api.config';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { AuthService } from '../../core/services/auth.service';
import { CatalogService } from '../../core/services/catalog.service';
import { CmsService, HomeSection } from '../../core/services/cms.service';
import { SeoService } from '../../core/services/seo.service';
import { ProductCardComponent } from '../../shared/product-card/product-card.component';

interface HeroSlide { image: string; title: string; subtitle: string; cta: string; link: string; }
interface Testimonial { name: string; company: string; rating: number; text: string; }

@Component({
  selector: 'app-home',
  imports: [RouterLink, ProductCardComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly catalog = inject(CatalogService);
  private readonly cms = inject(CmsService);
  private readonly seo = inject(SeoService);
  private readonly auth = inject(AuthService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly isAdmin = this.auth.isAdmin;

  readonly sections = signal<HomeSection[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly featured = signal<ProductListItem[]>([]);
  readonly newest = signal<ProductListItem[]>([]);

  readonly slides: HeroSlide[] = [
    { image: 'https://picsum.photos/seed/calhero1/1600/520', title: 'Customizable 2026 Calendars', subtitle: 'Wall, desk, pocket & more — personalized with your photos, brand name and logo.', cta: 'Shop calendars', link: '/products' },
    { image: 'https://picsum.photos/seed/calhero2/1600/520', title: 'Corporate Gifting Made Easy', subtitle: 'Branded calendars in bulk. Start strong with the right essentials.', cta: 'Order in bulk', link: '/products' },
    { image: 'https://picsum.photos/seed/calhero3/1600/520', title: 'Desk Calendars for Every Workspace', subtitle: 'Smart, elegant desk calendars that look great on any table.', cta: 'Browse desk calendars', link: '/category/desk-calendars' },
  ];
  readonly currentSlide = signal(0);
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

    this.cms.getHomeSections().subscribe((s) => this.sections.set(s));
    this.catalog.getCategories().subscribe((c) => this.categories.set(c));
    this.catalog.getProducts({ isFeatured: true, pageSize: 10 }).subscribe((r) => this.featured.set(r.items));
    this.catalog.getProducts({ pageSize: 10 }).subscribe((r) => this.newest.set(r.items));

    if (this.isBrowser) this.timer = setInterval(() => this.next(), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  next(): void { this.currentSlide.update((i) => (i + 1) % this.slides.length); }
  prev(): void { this.currentSlide.update((i) => (i - 1 + this.slides.length) % this.slides.length); }
  goTo(i: number): void { this.currentSlide.set(i); }

  railItems(type: string): ProductListItem[] {
    return type === 'NewArrivals' ? this.newest() : this.featured();
  }

  star(n: number): string {
    return '★'.repeat(Math.max(0, Math.min(5, n)));
  }
}
