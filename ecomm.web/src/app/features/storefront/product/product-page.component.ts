import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ThemeService } from '../../../core/services/theme.service';
import { ProductPageStore } from './product-page.store';
import {
  ProductBreadcrumbsComponent, ProductDescriptionComponent, ProductInfoComponent, ProductReviewsComponent,
} from './product-sections.component';

/** Default `product` layout when the published theme defines no product template. Matches today's page. */
const DEFAULT_PRODUCT_SECTIONS = ['Breadcrumbs', 'ProductInfo', 'ProductDescription', 'ProductReviews'];

/**
 * Section-composed product page (S3). Resolves the product into a page-scoped
 * ProductPageStore, then renders the published theme's `product` template as an
 * ordered list of dynamic sections — falling back to the built-in order above
 * when no template is authored, so behaviour is identical to the previous page.
 */
@Component({
  selector: 'app-product-page',
  imports: [
    RouterLink, ProductBreadcrumbsComponent, ProductInfoComponent, ProductDescriptionComponent, ProductReviewsComponent,
  ],
  providers: [ProductPageStore],
  template: `
    @if (store.loading()) {
      <div class="py-24 text-center text-slate-400">Loading…</div>
    } @else if (store.notFound()) {
      <div class="py-24 text-center">
        <p class="text-slate-500">Sorry, we couldn't find that product.</p>
        <a routerLink="/products" class="text-primary hover:underline font-medium">Back to all calendars</a>
      </div>
    } @else if (store.product()) {
      <section class="page-container py-8">
        @for (type of sectionTypes(); track $index) {
          @switch (type) {
            @case ('Breadcrumbs') { <app-product-breadcrumbs /> }
            @case ('ProductInfo') { <app-product-info /> }
            @case ('ProductDescription') { <app-product-description /> }
            @case ('ProductReviews') { <app-product-reviews /> }
          }
        }
      </section>
    }
  `,
})
export class ProductPageComponent implements OnInit {
  readonly store = inject(ProductPageStore);
  private readonly route = inject(ActivatedRoute);
  private readonly theme = inject(ThemeService);

  readonly sectionTypes = signal<string[]>(DEFAULT_PRODUCT_SECTIONS);

  ngOnInit(): void {
    // Layout is theme-level (same for every product) — load it once.
    this.theme.getTemplate('product').subscribe((sections) => {
      if (sections.length) this.sectionTypes.set(sections.map((s) => s.sectionType));
    });
    // Entity data is per-route.
    this.route.paramMap.subscribe((params) => this.store.load(params.get('slug') ?? ''));
  }
}
