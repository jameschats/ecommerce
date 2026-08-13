import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ProductListItem } from '../../core/models/catalog.model';
import { ColorSwatchService } from '../../core/services/color-swatch.service';
import { QuickViewService } from '../../core/services/quick-view.service';
import { CompareButtonComponent } from '../compare-button/compare-button.component';
import { WishlistButtonComponent } from '../wishlist-button/wishlist-button.component';
import { ResponsiveImgDirective } from '../responsive-img/responsive-img.directive';

const MAX_SWATCHES_SHOWN = 5;

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, CurrencyPipe, DecimalPipe, WishlistButtonComponent, CompareButtonComponent, ResponsiveImgDirective],
  template: `
    <a [routerLink]="['/product', product().slug]" data-prod-card
       class="group block overflow-hidden sf-card"
       [class.shrink-0]="carouselItem()" [class.w-44]="carouselItem()" [class.sm:w-52]="carouselItem()" [class.snap-start]="carouselItem()">
      <div class="relative bg-slate-50 overflow-hidden" [class]="aspectRatio() === 'portrait' ? 'aspect-[3/4]' : 'aspect-square'">
        @if (product().primaryImageUrl) {
          <img [src]="product().primaryImageUrl" [appImgSrc]="product().primaryImageUrl" appImgSizes="(min-width: 768px) 25vw, 50vw" [alt]="product().name"
               class="w-full h-full object-cover transition"
               [class.group-hover:opacity-0]="!!product().secondaryImageUrl" loading="lazy" />
        } @else {
          <div class="w-full h-full flex items-center justify-center text-slate-300 text-sm">No image</div>
        }
        @if (product().secondaryImageUrl) {
          <img [src]="product().secondaryImageUrl" [appImgSrc]="product().secondaryImageUrl" appImgSizes="(min-width: 768px) 25vw, 50vw" [alt]="product().name"
               class="absolute inset-0 w-full h-full object-cover opacity-0 group-hover:opacity-100 transition" loading="lazy" />
        }
        <div class="absolute top-2 left-2 flex flex-col gap-1 items-start">
          @if (product().isFeatured) {
            <span class="bg-amber-500 text-white text-[11px] font-semibold px-1.5 py-0.5 rounded">Bestseller</span>
          }
          @if (discount() > 0 && product().inStock) {
            <span class="bg-green-600 text-white text-[11px] font-semibold px-1.5 py-0.5 rounded">{{ discount() }}% off</span>
          }
          @if (isNew()) {
            <span class="bg-slate-900 text-white text-[11px] font-semibold px-1.5 py-0.5 rounded">New</span>
          }
        </div>
        @if (!product().inStock) {
          <div class="absolute inset-0 bg-white/60 flex items-center justify-center">
            <span class="bg-slate-800 text-white text-xs font-semibold px-2 py-1 rounded">Out of stock</span>
          </div>
        }
        <div class="absolute top-2 right-2 flex flex-col gap-1.5">
          <app-wishlist-button [productId]="product().productId" />
          <app-compare-button [productId]="product().productId" />
        </div>
        <button type="button" (click)="$event.preventDefault(); $event.stopPropagation(); quickView.open(product().slug)"
          class="absolute inset-x-0 bottom-0 bg-black/60 text-white text-xs font-medium text-center py-1.5 opacity-0 group-hover:opacity-100 transition">
          Quick view
        </button>
      </div>
      <div class="p-3">
        <p class="text-xs text-slate-400">{{ product().brandName ?? product().categoryName }}</p>
        <h3 class="text-sm font-medium text-slate-800 line-clamp-2 min-h-[2.5rem]">{{ product().name }}</h3>
        @if (reviewCount() > 0) {
          <div class="mt-1 flex items-center gap-1.5">
            <span class="inline-flex items-center gap-0.5 rounded bg-green-600 text-white text-[11px] font-semibold px-1.5 py-0.5">
              {{ rating() | number:'1.1-1' }}<span class="text-[9px] leading-none">★</span>
            </span>
            <span class="text-[11px] text-slate-400">({{ reviewCount() }})</span>
          </div>
        }
        <div class="mt-1 flex items-baseline gap-2">
          <span class="text-base font-semibold text-slate-900">{{ product().price | currency:'INR':'symbol':'1.0-0' }}</span>
          @if (product().compareAtPrice && product().compareAtPrice! > product().price) {
            <span class="text-xs text-slate-400 line-through">{{ product().compareAtPrice | currency:'INR':'symbol':'1.0-0' }}</span>
          }
        </div>
        @if (swatches().length) {
          <div class="flex items-center gap-1 mt-2">
            @for (s of swatches(); track s.name) {
              <span class="h-3.5 w-3.5 rounded-full border border-slate-300 shrink-0"
                    [style.background-color]="s.hex ?? '#e5e7eb'" [title]="s.name"></span>
            }
            @if (extraColorCount() > 0) {
              <span class="text-[11px] text-slate-400">+{{ extraColorCount() }}</span>
            }
          </div>
        }
        @if (product().inStock && product().isLowStock) {
          <p class="text-[11px] font-medium text-amber-600 mt-1.5">Only {{ product().availableQty }} left</p>
        }
      </div>
    </a>
  `,
})
export class ProductCardComponent {
  product = input.required<ProductListItem>();
  aspectRatio = input<'square' | 'portrait'>('square');
  /** Fixed-width snap card for a horizontal scroller (home page product carousels) instead of a
   *  grid cell — same `data-prod-card` attribute either way, so the carousel's arrow-scroll math
   *  (offsetWidth-based, see storefront-section.component.ts) finds it regardless of context. */
  carouselItem = input(false);
  readonly quickView = inject(QuickViewService);

  private readonly swatchSvc = inject(ColorSwatchService);
  private readonly allSwatches = toSignal(this.swatchSvc.list(), { initialValue: [] });

  readonly rating = computed(() => this.product().rating ?? 0);
  readonly reviewCount = computed(() => this.product().reviewCount ?? 0);

  readonly discount = computed(() => {
    const p = this.product();
    if (!p.compareAtPrice || p.compareAtPrice <= p.price) return 0;
    return Math.round((1 - p.price / p.compareAtPrice) * 100);
  });

  /** "New" badge for products created in the last 30 days. */
  readonly isNew = computed(() => {
    const created = new Date(this.product().createdAt).getTime();
    return Number.isFinite(created) && Date.now() - created <= 30 * 24 * 60 * 60 * 1000;
  });

  /** Resolves each variant colour to a hex dot (or a neutral fallback if unmapped), capped for card width. */
  readonly swatches = computed(() => {
    const lookup = new Map(this.allSwatches().map((s) => [s.name.toLowerCase(), s.hexCode]));
    return this.product().colorOptions.slice(0, MAX_SWATCHES_SHOWN)
      .map((name) => ({ name, hex: lookup.get(name.toLowerCase()) ?? null }));
  });

  readonly extraColorCount = computed(() => Math.max(0, this.product().colorOptions.length - MAX_SWATCHES_SHOWN));
}
