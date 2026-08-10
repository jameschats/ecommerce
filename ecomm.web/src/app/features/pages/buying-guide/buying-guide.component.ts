import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SITE_URL } from '../../../core/api.config';
import { ContentPage } from '../../../core/models/content-page.model';
import { SeoService } from '../../../core/services/seo.service';
import { ContentSectionsComponent } from '../../../shared/content-sections/content-sections.component';

/**
 * How to choose what to order — the page a dealer reads before placing their first order.
 *
 * Entirely admin-editable (055). Every step is a Prose section, so steps can be added,
 * reworded and reordered without a deploy.
 */
@Component({
  selector: 'app-buying-guide',
  imports: [ContentSectionsComponent],
  template: `
    <section class="page-container py-12">
      <div class="max-w-3xl mx-auto">
        <h1 class="text-3xl sm:text-4xl font-bold text-slate-900">{{ page?.title || 'Buying guide' }}</h1>

        @if (page?.sections?.length) {
          <app-content-sections [sections]="page!.sections" />
        } @else {
          <p class="mt-6 text-slate-500">This guide is being written. Please check back shortly.</p>
        }
      </div>
    </section>
  `,
})
export class BuyingGuideComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);

  readonly page = this.route.snapshot.data['page'] as ContentPage | null;

  ngOnInit(): void {
    this.seo.setMeta({
      title: this.page?.metaTitle || 'Buying guide',
      description: this.page?.metaDescription
        ?? 'How to choose the right calendar: order type, size, paper quality and layout.',
      url: `${SITE_URL}/buying-guide`,
    });
  }
}
