import { CurrencyPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductListItem } from '../../core/models/catalog.model';
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
          <span class="text-base font-semibold text-slate-900">{{ product().price | currency:'INR':'symbol':'1.0-0' }}</span>
          @if (product().compareAtPrice && product().compareAtPrice! > product().price) {
            <span class="text-xs text-slate-400 line-through">{{ product().compareAtPrice | currency:'INR':'symbol':'1.0-0' }}</span>
          }
        </div>
      </div>
    </a>
  `,
})
export class ProductCardComponent {
  product = input.required<ProductListItem>();

  readonly discount = computed(() => {
    const p = this.product();
    if (!p.compareAtPrice || p.compareAtPrice <= p.price) return 0;
    return Math.round((1 - p.price / p.compareAtPrice) * 100);
  });
}
