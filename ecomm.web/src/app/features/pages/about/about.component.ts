import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { ContentPage } from '../../../core/models/content-page.model';
import { PageBannerData } from '../../../core/resolvers/page-banner.resolver';
import { SeoService } from '../../../core/services/seo.service';
import { BannerCarouselComponent } from '../../../shared/banner-carousel/banner-carousel.component';
import { ContentSectionsComponent } from '../../../shared/content-sections/content-sections.component';

/**
 * Content now comes from admin (055). The banner carousel stays as code because it is driven
 * by the banners feature, which has its own admin screen — duplicating it here as editable
 * content would give two places to change the same thing.
 */
@Component({
  selector: 'app-about',
  imports: [BannerCarouselComponent, ContentSectionsComponent],
  template: `
    <app-banner-carousel [banners]="banners" />

    <section class="page-container py-12">
      <h1 class="text-3xl sm:text-4xl font-bold text-slate-900 max-w-3xl">{{ page?.title || 'About us' }}</h1>

      @if (page?.sections?.length) {
        <app-content-sections [sections]="page!.sections" />
      } @else {
        <p class="mt-6 text-slate-500">This page has not been written yet.</p>
      }
    </section>
  `,
})
export class AboutComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);

  readonly banners = (this.route.snapshot.data['pageBanners'] as PageBannerData | undefined)?.banners ?? [];
  readonly page = this.route.snapshot.data['page'] as ContentPage | null;

  ngOnInit(): void {
    this.seo.setMeta({
      title: this.page?.metaTitle || 'About us',
      description: this.page?.metaDescription ?? 'Who we are and how we print calendars.',
      url: `${SITE_URL}/about`,
    });
  }
}
