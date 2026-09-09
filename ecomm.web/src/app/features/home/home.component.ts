import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SITE_URL } from '../../core/api.config';
import { HomeBanner } from '../../core/models/banner.model';
import { ContentPage } from '../../core/models/content-page.model';
import { GalleryImage } from '../../core/models/gallery.model';
import { Testimonial } from '../../core/models/testimonial.model';
import { BrandingService } from '../../core/services/branding.service';
import { SeoService } from '../../core/services/seo.service';
import { BannerCarouselComponent } from '../../shared/banner-carousel/banner-carousel.component';
import { ContentSectionsComponent } from '../../shared/content-sections/content-sections.component';
import { GalleryStripComponent } from '../../shared/gallery-strip/gallery-strip.component';
import { TestimonialSectionComponent } from '../../shared/testimonial-section/testimonial-section.component';
import { QuickOrderTableComponent } from '../order/quick-order-table.component';
import { GalleryData } from './gallery.resolver';
import { HomeData } from './home.resolver';
import { TestimonialsData } from './testimonials.resolver';

/**
 * Home = banner carousel + "New designs" gallery + the About Us write-up + the quick-order
 * price list + a second gallery + testimonials (design.md §4, revised).
 *
 * The category tiles and product rails that used to sit here are still gone — in Phase 1
 * the table *is* the storefront — but the write-up and testimonials came back by request,
 * reusing the same admin-editable content as the About Us page rather than a second copy.
 */
@Component({
  selector: 'app-home',
  imports: [QuickOrderTableComponent, GalleryStripComponent, BannerCarouselComponent, ContentSectionsComponent, TestimonialSectionComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly branding = inject(BrandingService);

  private readonly galleryData = this.route.snapshot.data['gallery'] as GalleryData | undefined;
  private readonly secondGalleryData = this.route.snapshot.data['secondGallery'] as GalleryData | undefined;
  readonly newDesigns: GalleryImage[] = this.galleryData?.images ?? [];
  readonly newDesignsTitle = this.galleryData?.title ?? 'New designs';
  readonly secondGallery: GalleryImage[] = this.secondGalleryData?.images ?? [];
  readonly secondGalleryTitle = this.secondGalleryData?.title ?? 'Our Work';
  readonly testimonials: Testimonial[] = (this.route.snapshot.data['testimonials'] as TestimonialsData | undefined)?.items ?? [];

  /**
   * Its own admin-editable content (Pages CMS, slug `home-about`) — seeded as a copy of the
   * About Us page's write-up (migration 060) but independently editable from here on, since
   * the shop wants the two free to diverge. No Cta section here: the price list right below
   * already serves that purpose on the home page.
   */
  private readonly homeAboutPage = this.route.snapshot.data['homeAbout'] as ContentPage | null;
  readonly aboutSections = (this.homeAboutPage?.sections ?? []).filter((s) => s.sectionType !== 'Cta');
  readonly aboutHeading = this.homeAboutPage?.title || 'Who we are?';

  // Fallback banners — shown only if the admin has configured none.
  private readonly defaultBanners: HomeBanner[] = [
    { homeBannerId: -1, imageUrl: 'https://picsum.photos/seed/calbanner1/900/300', title: 'Customizable 2026 Calendars', subtitle: 'Wall, desk & pocket — with your photos, brand & logo.', cta: 'Order now', link: '/order' },
    { homeBannerId: -2, imageUrl: 'https://picsum.photos/seed/calbanner2/900/300', title: 'Corporate Gifting', subtitle: 'Branded calendars in bulk.', cta: 'Order in bulk', link: '/order' },
    { homeBannerId: -3, imageUrl: 'https://picsum.photos/seed/calbanner3/900/300', title: 'Desk Calendars', subtitle: 'Elegant picks for any workspace.', cta: 'Browse', link: '/order' },
    { homeBannerId: -4, imageUrl: 'https://picsum.photos/seed/calbanner4/900/300', title: 'Photo Calendars', subtitle: 'Turn your memories into a year.', cta: 'Create yours', link: '/order' },
    { homeBannerId: -5, imageUrl: 'https://picsum.photos/seed/calbanner5/900/300', title: 'New-Year Offers', subtitle: 'Up to 30% off select ranges.', cta: 'Grab deals', link: '/order' },
    { homeBannerId: -6, imageUrl: 'https://picsum.photos/seed/calbanner6/900/300', title: 'Pocket & Tent Calendars', subtitle: 'Handy formats for every desk.', cta: 'Explore', link: '/order' },
  ];
  readonly banners = signal<HomeBanner[]>(this.defaultBanners);

  ngOnInit(): void {
    // Admin-authored title/description win over these fallbacks — same rule the product
    // page follows (product-detail.component.ts). Read from the same load() the app shell
    // already triggers (shareReplay'd, so this is normally an instant replay, not a second
    // request), rather than the branding signals directly: apply() runs inside that
    // pipeline before any subscriber sees the value, so there is no race to get this early.
    this.branding.load().subscribe((b) => {
      const siteName = (b.siteName?.trim() || '') + (b.siteNameAccent?.trim() || '') || 'CalendarShop';

      this.seo.setMeta({
        title: b.browserTitle?.trim()
          || 'CalendarShop — Custom 2026 Calendars: Wall, Desk, Pocket & More',
        description: b.homeMetaDescription?.trim()
          || 'Personalized 2026 calendars — wall, desk, tent, pocket, magnet & mouse-pad. Add your photos, brand name and logo. Fast delivery, great prices.',
        // Ignored by Google, still read by some AI crawlers — cheap to emit.
        keywords: 'sivakasi daily calendar manufacturer, lotus calendar senthaamarai press, '
          + 'daily calendar store online, buy daily calendar sivakasi, daily calendar mount board wholesale',
        url: `${SITE_URL}/`,
      });

      // City/address read from the same Settings the contact page and footer use — this used
      // to hardcode "Madurai" while the real shop (Settings → Contact) said Sivakasi, so an AI
      // asked where the shop is could have believed either. See branding.service.ts's `contact`
      // doc comment for the earlier Chennai/Madurai version of the same bug.
      const city = b.contactCity?.trim().split('|')[0]?.trim() || 'Sivakasi';
      const streetAddress = b.contactAddress?.trim().split('\n')[0]?.trim();

      const logo = b.logoUrl?.trim() || b.footerLogoUrl?.trim();
      const phone = b.contactMobile1?.trim();

      this.seo.setJsonLd([
        {
          '@context': 'https://schema.org', '@type': 'WebSite', name: siteName, url: SITE_URL,
          potentialAction: { '@type': 'SearchAction', target: `${SITE_URL}/products?search={search_term_string}`, 'query-input': 'required name=search_term_string' },
        },
        // The legal/trading identity behind the storefront — separate from the Store below,
        // which describes the shop a buyer sees. url/logo/phone/address all read from the same
        // Settings the rest of the page uses, rather than repeating literals that could drift.
        {
          '@context': 'https://schema.org',
          '@type': 'Organization',
          name: 'Senthaamarai Press',
          alternateName: ['Lotus Calendars', siteName],
          url: SITE_URL,
          ...(logo ? { logo } : {}),
          foundingDate: '1961',
          founder: { '@type': 'Person', name: 'S. Balusamy' },
          address: {
            '@type': 'PostalAddress',
            ...(streetAddress ? { streetAddress } : {}),
            addressLocality: city,
            addressRegion: 'Tamil Nadu',
            addressCountry: 'IN',
          },
          ...(phone ? {
            contactPoint: {
              '@type': 'ContactPoint', telephone: phone, contactType: 'customer service',
              areaServed: 'IN', availableLanguage: ['Tamil', 'English'],
            },
          } : {}),
        },
        // LocalBusiness rather than a bare Organization: an AI asked "who sells wholesale
        // calendars in Sivakasi" can only answer from data that says where the shop is and
        // what it sells. Name and URL alone answer nothing.
        {
          '@context': 'https://schema.org',
          '@type': 'Store',
          name: siteName,
          url: SITE_URL,
          description:
            'Wholesale calendar printing — wall, desk, tent and pocket calendars, panchangam '
            + 'and cake calendars, sold to dealers and shops by design number at trade rates.',
          address: {
            '@type': 'PostalAddress',
            ...(streetAddress ? { streetAddress } : {}),
            addressLocality: city,
            addressRegion: 'Tamil Nadu',
            addressCountry: 'IN',
          },
          areaServed: 'IN',
          currenciesAccepted: 'INR',
          paymentAccepted: 'UPI, Bank transfer',
        },
      ]);
    });

    // Preloaded by homeResolver → present on first render (no reflow).
    const data = this.route.snapshot.data['home'] as HomeData | undefined;
    if (data?.banners?.length) this.banners.set(data.banners);
  }
}
