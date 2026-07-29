import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SITE_URL } from '../../core/api.config';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { AuthService } from '../../core/services/auth.service';
import { BuilderSection, HomeSection } from '../../core/services/cms.service';
import { SeoService } from '../../core/services/seo.service';
import { ThemeService } from '../../core/services/theme.service';
import { ProductCardComponent } from '../../shared/product-card/product-card.component';
import { StorefrontSectionComponent } from '../storefront/storefront-section.component';
import { toBuilderSection } from '../storefront/section-slot';
import { HomeData } from './home.resolver';

interface HeroSlide { image: string; title: string; subtitle: string; cta: string; link: string; }
interface Testimonial { name: string; company: string; rating: number; text: string; }

/** Section types the visual builder owns but the curated home doesn't render itself. */
const BUILDER_NATIVE = new Set(['Hero', 'RichText', 'ImageWithText', 'CtaNewsletter']);

@Component({
  selector: 'app-home',
  imports: [RouterLink, ProductCardComponent, StorefrontSectionComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly auth = inject(AuthService);
  private readonly theme = inject(ThemeService);
  private readonly siteUrl = inject(SITE_URL);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly isAdmin = this.auth.isAdmin;
  /** This store's name (from the theme/tenant) — for headings + SEO, no hardcoded brand. */
  readonly storeName = computed(() => this.theme.storeName() || 'our store');

  readonly sections = signal<HomeSection[]>([]);
  /** The published theme's `index` sections (rendered via the section engine) — empty unless the theme authored one. */
  readonly themeSections = signal<BuilderSection[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly featured = signal<ProductListItem[]>([]);
  readonly newest = signal<ProductListItem[]>([]);

  /**
   * Render the home from the visual builder when the merchant has configured builder-native
   * sections (Hero/RichText/…) or added real content. Otherwise keep the curated storefront
   * home (its slides/testimonials are content-managed below). This keeps existing stores
   * untouched while new stores get their own builder-driven home.
   */
  readonly useBuilder = computed(() => this.sections().some((s) => BUILDER_NATIVE.has(s.sectionType) || this.hasContent(s)));
  readonly builderSections = computed<BuilderSection[]>(() => this.sections().map((s) => ({
    pageSectionId: s.pageSectionId, pageId: s.pageId ?? 0, sectionType: s.sectionType, title: s.title,
    settings: s.settings ?? null, blocks: s.blocks ?? null, displayOrder: s.displayOrder, isVisible: s.isVisible,
  })));

  // Fallback banners — shown only if the admin has configured none. Generic (brand-agnostic).
  private readonly defaultSlides: HeroSlide[] = [
    { image: 'https://picsum.photos/seed/wcbanner1/900/300', title: 'Welcome to our store', subtitle: 'Great products at great prices, delivered fast.', cta: 'Shop now', link: '/products' },
    { image: 'https://picsum.photos/seed/wcbanner2/900/300', title: 'New arrivals', subtitle: 'Fresh picks added regularly.', cta: 'Browse', link: '/products' },
    { image: 'https://picsum.photos/seed/wcbanner3/900/300', title: 'Shop by category', subtitle: 'Find exactly what you need.', cta: 'Explore', link: '/products' },
  ];
  readonly slides = signal<HeroSlide[]>(this.defaultSlides);
  readonly currentSlide = signal(0);
  private readonly bannerTrack = viewChild<ElementRef<HTMLDivElement>>('bannerTrack');
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly googleRating = 4.5;
  readonly reviewCount = '100+';
  // Generic placeholder testimonials (brand-agnostic) — only rendered by a curated Testimonials section.
  readonly testimonials: Testimonial[] = [
    { name: 'Ananya R.', company: '', rating: 5, text: 'Great quality and quick delivery. Exactly as described — will order again!' },
    { name: 'Suraj A.', company: '', rating: 4, text: 'Smooth experience from order to doorstep. Friendly support too.' },
    { name: 'Meera K.', company: '', rating: 5, text: 'Loved the packaging and the product. Highly recommend.' },
    { name: 'Joy B.', company: '', rating: 5, text: 'Fair prices and it arrived faster than I expected.' },
    { name: 'Priya M.', company: '', rating: 5, text: 'Premium feel and quick delivery. Very happy with my purchase.' },
    { name: 'Aakash G.', company: '', rating: 5, text: 'A hit in our office. Will reorder next time.' },
  ];
  readonly colA = this.testimonials.filter((_, i) => i % 2 === 0);
  readonly colB = this.testimonials.filter((_, i) => i % 2 === 1);

  ngOnInit(): void {
    // Merchant-configured store SEO (Preferences) overrides the defaults when present.
    const storeSeo = (this.route.snapshot.data['home'] as HomeData | undefined)?.seo;
    const name = this.theme.storeName() || 'Online store';
    this.seo.setMeta({
      title: storeSeo?.title || `${name} — Shop online`,
      description: storeSeo?.description || `Shop ${name} — great products, fair prices and fast delivery.`,
      image: storeSeo?.image || undefined,
      url: `${this.siteUrl}/`,
    });
    this.seo.setJsonLd([
      {
        '@context': 'https://schema.org', '@type': 'WebSite', name, url: this.siteUrl,
        potentialAction: { '@type': 'SearchAction', target: `${this.siteUrl}/products?search={search_term_string}`, 'query-input': 'required name=search_term_string' },
      },
      { '@context': 'https://schema.org', '@type': 'Organization', name, url: this.siteUrl },
    ]);

    // Data is preloaded by homeResolver → present on first render (no reflow).
    const data = this.route.snapshot.data['home'] as HomeData | undefined;
    if (data) {
      this.themeSections.set((data.themeIndex ?? []).map(toBuilderSection));
      this.sections.set(data.sections);
      this.categories.set(data.categories);
      this.featured.set(data.featured);
      this.newest.set(data.newest);
      if (data.banners?.length) {
        this.slides.set(data.banners.map((b) => ({
          image: b.imageUrl ?? '', title: b.title ?? '', subtitle: b.subtitle ?? '',
          cta: b.cta ?? '', link: b.link ?? '/products',
        })));
      }
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

  railItems(type: string): ProductListItem[] {
    return type === 'NewArrivals' ? this.newest() : this.featured();
  }

  /** True if the section carries real builder content (settings keys with a value, or any blocks). */
  private hasContent(s: HomeSection): boolean {
    try {
      const blocks = s.blocks ? JSON.parse(s.blocks) : [];
      if (Array.isArray(blocks) && blocks.length) return true;
      const settings = s.settings ? JSON.parse(s.settings) : {};
      return Object.values(settings).some((v) => v !== null && v !== '' && v !== undefined);
    } catch { return false; }
  }

  star(n: number): string {
    return '★'.repeat(Math.max(0, Math.min(5, n)));
  }
}
