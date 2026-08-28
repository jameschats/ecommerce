import { Component, OnInit, inject, input, signal } from '@angular/core';
import { ProductListItem } from '../../core/models/catalog.model';
import { CatalogService } from '../../core/services/catalog.service';
import { ProductCardComponent } from '../product-card/product-card.component';

/**
 * "Trending now" rail (AI Commerce C2) — products ranked by recent demand velocity. Self-hiding: renders
 * nothing until the store has enough recent activity to compute a trend (cold-start falls back to showing
 * no rail rather than an empty box). Drop it anywhere a horizontal product rail fits.
 */
@Component({
  selector: 'app-trending-rail',
  imports: [ProductCardComponent],
  template: `
    @if (products().length > 0) {
      <section class="mt-10">
        <h2 class="text-lg font-bold text-slate-900 mb-4">{{ heading() }}</h2>
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
          @for (p of products(); track p.productId) {
            <app-product-card [product]="p" />
          }
        </div>
      </section>
    }
  `,
})
export class TrendingRailComponent implements OnInit {
  private readonly catalog = inject(CatalogService);

  readonly heading = input('🔥 Trending now');
  readonly limit = input(5);
  readonly products = signal<ProductListItem[]>([]);

  ngOnInit(): void {
    this.catalog.getTrending(this.limit()).subscribe((p) => this.products.set(p));
  }
}
