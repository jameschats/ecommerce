import { Component, OnInit, inject, input, signal } from '@angular/core';
import { ProductListItem } from '../../core/models/catalog.model';
import { CatalogService } from '../../core/services/catalog.service';
import { EventService } from '../../core/services/event.service';
import { ProductCardComponent } from '../product-card/product-card.component';

/** "Recently viewed" rail (AI Commerce C3) — the visitor's own server-side view history. Self-hides when empty. */
@Component({
  selector: 'app-recently-viewed-rail',
  imports: [ProductCardComponent],
  template: `
    @if (products().length > 0) {
      <section class="mt-10">
        <h2 class="text-lg font-bold text-slate-900 mb-4">{{ heading() }}</h2>
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
          @for (p of products(); track p.productId) { <app-product-card [product]="p" /> }
        </div>
      </section>
    }
  `,
})
export class RecentlyViewedRailComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly events = inject(EventService);

  readonly heading = input('Recently viewed');
  readonly limit = input(5);
  /** Product id to omit (e.g. the current product on a PDP). */
  readonly excludeId = input<number | null>(null);
  readonly products = signal<ProductListItem[]>([]);

  ngOnInit(): void {
    const vid = this.events.getVisitorId();
    if (!vid) return;
    this.catalog.getRecentlyViewed(vid, this.limit() + 1).subscribe((p) => {
      const ex = this.excludeId();
      this.products.set(p.filter((x) => x.productId !== ex).slice(0, this.limit()));
    });
  }
}
