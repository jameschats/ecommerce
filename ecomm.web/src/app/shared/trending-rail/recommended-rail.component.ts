import { Component, OnInit, inject, input, signal } from '@angular/core';
import { ProductListItem } from '../../core/models/catalog.model';
import { CatalogService } from '../../core/services/catalog.service';
import { EventService } from '../../core/services/event.service';
import { ProductCardComponent } from '../product-card/product-card.component';

/**
 * "Recommended for you" rail (AI Commerce C3). Personalized to the visitor's own recent behaviour; when
 * there isn't enough personal signal yet it falls back to store-wide Trending, and self-hides only when
 * both are empty. This is the cold-start fallback the design doc requires — the shopper never sees an
 * empty personalization slot.
 */
@Component({
  selector: 'app-recommended-rail',
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
export class RecommendedRailComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly events = inject(EventService);

  readonly limit = input(5);
  readonly products = signal<ProductListItem[]>([]);
  readonly heading = signal('🔥 Trending now');

  ngOnInit(): void {
    const vid = this.events.getVisitorId();
    const personalized$ = vid ? this.catalog.getPersonalized(vid, this.limit()) : this.catalog.getTrending(this.limit());
    personalized$.subscribe((picks) => {
      if (picks.length > 0 && vid) {
        this.heading.set('Recommended for you');
        this.products.set(picks);
      } else {
        // Cold start → store-wide trending.
        this.catalog.getTrending(this.limit()).subscribe((t) => { this.heading.set('🔥 Trending now'); this.products.set(t); });
      }
    });
  }
}
