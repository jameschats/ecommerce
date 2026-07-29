import { CurrencyPipe } from '@angular/common';
import { Component, HostListener, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductDetail } from '../../core/models/catalog.model';
import { CartService } from '../../core/services/cart.service';
import { CatalogService } from '../../core/services/catalog.service';
import { QuickViewService } from '../../core/services/quick-view.service';
import { WishlistButtonComponent } from '../wishlist-button/wishlist-button.component';

/**
 * Global quick-view modal (mounted once in app.html, opened from any product card via
 * QuickViewService). Deliberately does NOT reuse ProductPageStore — that store also mutates
 * page <title>/meta tags and fetches reviews, which would be wrong here (a modal shouldn't
 * rewrite the host page's SEO tags). Calls the same underlying CatalogService/CartService the
 * full product page uses, so fetch + add-to-cart behaviour is identical, without the side effects.
 */
@Component({
  selector: 'app-quick-view',
  imports: [CurrencyPipe, RouterLink, WishlistButtonComponent],
  template: `
    @if (svc.slug()) {
      <div class="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4" (click)="svc.close()">
        <div class="bg-white rounded-2xl w-full max-w-2xl max-h-[85vh] overflow-auto p-5" (click)="$event.stopPropagation()">
          <div class="flex justify-end mb-1">
            <button type="button" (click)="svc.close()" class="text-slate-400 hover:text-slate-700 text-xl leading-none">×</button>
          </div>

          @if (loading()) {
            <div class="py-16 text-center text-slate-400">Loading…</div>
          } @else if (!product()) {
            <div class="py-16 text-center text-slate-400">Product not found.</div>
          } @else if (product(); as p) {
            <div class="grid sm:grid-cols-2 gap-6">
              <div class="aspect-square bg-slate-50 rounded-xl overflow-hidden">
                @if (p.images[0]?.url) {
                  <img [src]="p.images[0].url" [alt]="p.name" class="w-full h-full object-cover" />
                } @else {
                  <div class="w-full h-full flex items-center justify-center text-slate-300 text-sm">No image</div>
                }
              </div>
              <div>
                <p class="text-xs text-slate-400">{{ p.brandName ?? p.categoryName }}</p>
                <h2 class="text-xl font-bold text-slate-900">{{ p.name }}</h2>
                <div class="mt-1 flex items-baseline gap-2">
                  <span class="text-lg font-semibold text-slate-900">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</span>
                  @if (p.compareAtPrice && p.compareAtPrice > p.price) {
                    <span class="text-sm text-slate-400 line-through">{{ p.compareAtPrice | currency:'INR':'symbol':'1.0-0' }}</span>
                  }
                </div>
                @if (p.shortDescription) { <p class="text-sm text-slate-600 mt-3 line-clamp-3">{{ p.shortDescription }}</p> }

                @for (g of optionGroups(); track g.name) {
                  <div class="mt-4">
                    <p class="text-sm font-medium text-slate-700 mb-2">{{ g.name }}</p>
                    <div class="flex flex-wrap gap-2">
                      @for (val of g.values; track val) {
                        <button type="button" (click)="selectOption(g.name, val)"
                          class="text-sm border rounded-lg px-3 py-1.5 transition"
                          [class]="selected[g.name] === val ? 'border-primary text-primary bg-primary/5 font-medium' : 'border-slate-300 text-slate-600 hover:border-slate-400'">
                          {{ val }}
                        </button>
                      }
                    </div>
                  </div>
                }

                <div class="flex items-center gap-3 mt-5">
                  <div class="flex items-center border border-slate-300 rounded-lg">
                    <button type="button" (click)="decQty()" class="w-9 h-10 text-slate-600 hover:bg-slate-50">−</button>
                    <span class="w-8 text-center text-sm">{{ qty() }}</span>
                    <button type="button" (click)="incQty()" class="w-9 h-10 text-slate-600 hover:bg-slate-50">+</button>
                  </div>
                  <button type="button" (click)="addToCart()" [disabled]="!p.inStock || adding()"
                    class="bg-primary hover:bg-primary-dark disabled:opacity-50 disabled:cursor-not-allowed text-white font-medium px-6 py-2.5 rounded-lg transition">
                    {{ p.inStock ? 'Add to cart' : 'Out of stock' }}
                  </button>
                  <app-wishlist-button [productId]="p.productId" />
                </div>
                @if (addedMessage()) { <p class="text-sm text-green-600 mt-2">✓ Added to your cart. <a routerLink="/cart" (click)="svc.close()" class="underline font-medium">View cart</a></p> }
                @if (cartError(); as err) { <p class="text-sm text-red-600 mt-2">{{ err }}</p> }

                <a [routerLink]="['/product', p.slug]" (click)="svc.close()" class="block text-sm text-slate-500 hover:text-primary underline mt-4">View full details</a>
              </div>
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class QuickViewComponent {
  readonly svc = inject(QuickViewService);
  private readonly catalog = inject(CatalogService);
  private readonly cart = inject(CartService);

  readonly product = signal<ProductDetail | null>(null);
  readonly loading = signal(false);
  readonly qty = signal(1);
  readonly adding = signal(false);
  readonly addedMessage = signal(false);
  readonly cartError = signal<string | null>(null);
  selected: Record<string, string> = {};

  readonly optionGroups = computed(() => {
    const p = this.product();
    if (!p) return [] as { name: string; values: string[] }[];
    const map = new Map<string, string[]>();
    for (const v of p.variants) {
      for (const o of v.options) {
        const list = map.get(o.optionName) ?? [];
        if (!list.includes(o.optionValue)) list.push(o.optionValue);
        map.set(o.optionName, list);
      }
    }
    return Array.from(map.entries()).map(([name, values]) => ({ name, values }));
  });

  constructor() {
    effect(() => {
      const slug = this.svc.slug();
      if (!slug) { this.product.set(null); return; }
      this.loading.set(true);
      this.product.set(null);
      this.cartError.set(null);
      this.catalog.getProductBySlug(slug).subscribe({
        next: (p) => {
          this.loading.set(false);
          this.product.set(p);
          this.selected = {};
          for (const g of this.optionGroups()) this.selected[g.name] = g.values[0];
          this.qty.set(1);
        },
        error: () => { this.loading.set(false); this.product.set(null); },
      });
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void { this.svc.close(); }

  selectOption(name: string, value: string): void { this.selected = { ...this.selected, [name]: value }; }
  incQty(): void { this.qty.update((q) => Math.min(999, q + 1)); }
  decQty(): void { this.qty.update((q) => Math.max(1, q - 1)); }

  /** Match the selected options to a concrete variant (null for simple products) — mirrors
   *  ProductPageStore.resolveVariantId exactly, but this is local derived state from this
   *  modal's own selection, not shared business logic worth extracting an abstraction for. */
  private resolveVariantId(): number | null {
    const p = this.product();
    if (!p || p.variants.length === 0) return null;
    const match = p.variants.find((v) => v.options.length > 0 && v.options.every((o) => this.selected[o.optionName] === o.optionValue));
    return match?.productVariantId ?? null;
  }

  addToCart(): void {
    const p = this.product();
    if (!p || !p.inStock || this.adding()) return;
    this.adding.set(true);
    this.cartError.set(null);
    this.cart.add(p.productId, this.resolveVariantId(), this.qty()).subscribe({
      next: () => { this.adding.set(false); this.addedMessage.set(true); setTimeout(() => this.addedMessage.set(false), 2500); },
      error: (e) => { this.adding.set(false); this.cartError.set(e?.error?.message ?? 'Could not add to cart.'); },
    });
  }
}
