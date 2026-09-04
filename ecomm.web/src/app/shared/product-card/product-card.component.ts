import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductListItem } from '../../core/models/catalog.model';
import { QuickOrderService } from '../../core/services/quick-order.service';
import { WishlistButtonComponent } from '../wishlist-button/wishlist-button.component';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, CurrencyPipe, WishlistButtonComponent],
  template: `
    <a [routerLink]="['/product', product().slug]"
       class="group block bg-white border border-slate-200 rounded-xl overflow-hidden hover:shadow-md transition">
      <div class="relative aspect-square bg-slate-50 overflow-hidden">
        @if (product().primaryImageUrl) {
          <img [src]="product().primaryImageUrl" [alt]="product().name"
               class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" />
        } @else {
          <div class="w-full h-full flex items-center justify-center text-slate-300 text-sm">No image</div>
        }
        @if (discount() > 0 && product().inStock) {
          <span class="absolute top-2 left-2 bg-green-600 text-white text-[11px] font-semibold px-1.5 py-0.5 rounded">{{ discount() }}% off</span>
        }
        @if (!product().inStock) {
          <div class="absolute inset-0 bg-white/60 flex items-center justify-center">
            <span class="bg-slate-800 text-white text-xs font-semibold px-2 py-1 rounded">Out of stock</span>
          </div>
        }
        <div class="absolute top-2 right-2">
          <app-wishlist-button [productId]="product().productId" />
        </div>
      </div>
      <div class="p-3">
        <p class="text-xs text-slate-400">{{ product().brandName ?? product().categoryName }}</p>
        <h3 class="text-sm font-medium text-slate-800 line-clamp-2 min-h-[2.5rem]">{{ product().name }}</h3>
        <div class="mt-1 flex items-baseline gap-2">
          <span class="text-base font-semibold text-slate-900">{{ product().price | currency:'INR':'symbol':'1.2-2' }}</span>
          @if (product().compareAtPrice && product().compareAtPrice! > product().price) {
            <span class="text-xs text-slate-400 line-through">{{ product().compareAtPrice | currency:'INR':'symbol':'1.2-2' }}</span>
          }
        </div>

        <!--
          Adds to the estimate — the site's one basket (see QuickOrderService) — not a
          separate cart. Starts as a one-tap "Add"; once there is a quantity it becomes a
          stepper in place, the same swap Blinkit/Zepto-style grids use, so adjusting a
          quantity never needs a trip to the product page or the price list.

          preventDefault + stopPropagation on every button: the whole card is an <a>, and
          without them a tap here would both add the item and navigate to the product page.
        -->
        @if (qtyInEstimate() === 0) {
          <button type="button" (click)="add($event)" [disabled]="adding() || !product().inStock"
            class="mt-2 w-full h-9 flex items-center justify-center gap-1.5 rounded-lg text-sm font-semibold
                   bg-primary/10 text-primary hover:bg-primary hover:text-white transition
                   disabled:opacity-40 disabled:pointer-events-none">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25"
                 stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg>
            Add
          </button>
        } @else {
          <div class="mt-2 h-9 flex items-center justify-between rounded-lg bg-primary text-white overflow-hidden">
            <button type="button" (click)="dec($event)" aria-label="Decrease quantity"
              class="w-9 h-full grid place-items-center text-lg font-bold hover:bg-primary-dark transition">−</button>
            <span class="text-sm font-semibold tabular-nums">{{ qtyInEstimate() }}</span>
            <button type="button" (click)="inc($event)" aria-label="Increase quantity"
              class="w-9 h-full grid place-items-center text-lg font-bold hover:bg-primary-dark transition">+</button>
          </div>
        }
      </div>
    </a>
  `,
})
export class ProductCardComponent {
  private readonly quickOrder = inject(QuickOrderService);

  product = input.required<ProductListItem>();

  readonly discount = computed(() => {
    const p = this.product();
    if (!p.compareAtPrice || p.compareAtPrice <= p.price) return 0;
    return Math.round((1 - p.price / p.compareAtPrice) * 100);
  });

  readonly qtyInEstimate = computed(() => this.quickOrder.qty(this.product().productId));

  /** True only while the first Add is loading the price list — after that, setQty is synchronous. */
  readonly adding = signal(false);

  add(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.adding.set(true);
    this.quickOrder.addToEstimate(this.product().productId, 1).subscribe({
      next: () => this.adding.set(false),
      error: () => this.adding.set(false),
    });
  }

  inc(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const id = this.product().productId;
    this.quickOrder.setQty(id, this.quickOrder.qty(id) + 1);
  }

  dec(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const id = this.product().productId;
    this.quickOrder.setQty(id, this.quickOrder.qty(id) - 1);
  }
}
