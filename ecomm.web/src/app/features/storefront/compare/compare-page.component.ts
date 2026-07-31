import { CurrencyPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, switchMap } from 'rxjs';
import { ProductDetail } from '../../../core/models/catalog.model';
import { CartService } from '../../../core/services/cart.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { CompareService } from '../../../core/services/compare.service';

/** Side-by-side comparison of the products in the client-side compare list (see CompareService). */
@Component({
  selector: 'app-compare-page',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <div class="max-w-6xl mx-auto px-4 py-8">
      <div class="flex items-center justify-between mb-6">
        <h1 class="text-2xl font-bold text-slate-900">Compare products</h1>
        @if (products().length) {
          <button type="button" (click)="compare.clear()" class="text-sm text-slate-500 hover:text-slate-700 underline">Clear all</button>
        }
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (!products().length) {
        <div class="bg-white rounded-xl border border-slate-200 p-10 text-center">
          <p class="text-slate-500 mb-1">No products to compare yet.</p>
          <a routerLink="/products" class="text-primary hover:underline text-sm">Browse products and tap the compare icon on up to 4.</a>
        </div>
      } @else {
        <div class="overflow-x-auto">
          <table class="w-full border-collapse min-w-[640px]">
            <tbody>
              <tr>
                <td class="w-32"></td>
                @for (p of products(); track p.productId) {
                  <td class="p-3 align-top">
                    <div class="relative w-32">
                      <button type="button" (click)="remove(p.productId)" aria-label="Remove from compare"
                        class="absolute -top-1 -right-1 w-6 h-6 rounded-full bg-white border border-slate-200 text-slate-400 hover:text-slate-700 text-sm shadow-sm z-10">×</button>
                      <a [routerLink]="['/product', p.slug]" class="block w-32 h-32 rounded-lg overflow-hidden bg-slate-50 border border-slate-200 mb-2">
                        @if (p.images[0]) { <img [src]="p.images[0].url" [alt]="p.name" class="w-full h-full object-cover" /> }
                      </a>
                      <a [routerLink]="['/product', p.slug]" class="text-sm font-medium text-slate-800 hover:text-primary line-clamp-2">{{ p.name }}</a>
                    </div>
                  </td>
                }
              </tr>
              <tr class="border-t border-slate-200">
                <td class="p-3 text-sm font-medium text-slate-500">Price</td>
                @for (p of products(); track p.productId) {
                  <td class="p-3 text-sm font-semibold text-slate-900">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</td>
                }
              </tr>
              <tr class="border-t border-slate-200 bg-slate-50/50">
                <td class="p-3 text-sm font-medium text-slate-500">Brand</td>
                @for (p of products(); track p.productId) {
                  <td class="p-3 text-sm text-slate-700">{{ p.brandName ?? '—' }}</td>
                }
              </tr>
              <tr class="border-t border-slate-200">
                <td class="p-3 text-sm font-medium text-slate-500">Category</td>
                @for (p of products(); track p.productId) {
                  <td class="p-3 text-sm text-slate-700">{{ p.categoryName }}</td>
                }
              </tr>
              <tr class="border-t border-slate-200 bg-slate-50/50">
                <td class="p-3 text-sm font-medium text-slate-500">Availability</td>
                @for (p of products(); track p.productId) {
                  <td class="p-3 text-sm" [class]="p.inStock ? 'text-green-600' : 'text-red-600'">{{ p.inStock ? 'In stock' : 'Out of stock' }}</td>
                }
              </tr>
              @for (name of attributeNames(); track name) {
                <tr class="border-t border-slate-200">
                  <td class="p-3 text-sm font-medium text-slate-500">{{ name }}</td>
                  @for (p of products(); track p.productId) {
                    <td class="p-3 text-sm text-slate-700">{{ attrValue(p, name) }}</td>
                  }
                </tr>
              }
              <tr class="border-t border-slate-200">
                <td class="p-3"></td>
                @for (p of products(); track p.productId) {
                  <td class="p-3">
                    <button type="button" (click)="addToCart(p)" [disabled]="!p.inStock || adding() === p.productId"
                      class="w-full text-sm bg-primary hover:bg-primary-dark disabled:opacity-50 text-white py-2 rounded-lg transition">
                      {{ p.inStock ? (adding() === p.productId ? 'Adding…' : 'Add to cart') : 'Out of stock' }}
                    </button>
                  </td>
                }
              </tr>
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class ComparePageComponent {
  private readonly catalog = inject(CatalogService);
  private readonly cart = inject(CartService);
  readonly compare = inject(CompareService);

  readonly products = signal<ProductDetail[]>([]);
  readonly loading = signal(true);
  readonly adding = signal<number | null>(null);

  readonly attributeNames = computed(() => {
    const names = new Set<string>();
    for (const p of this.products()) for (const a of p.attributes) names.add(a.attributeName);
    return Array.from(names);
  });

  constructor() {
    effect(() => {
      const ids = this.compare.ids();
      if (ids.length === 0) {
        this.products.set([]);
        this.loading.set(false);
        return;
      }
      this.loading.set(true);
      this.catalog.getProducts({ ids, pageSize: ids.length }).pipe(
        switchMap((r) => forkJoin(r.items.map((i) => this.catalog.getProductBySlug(i.slug)))),
      ).subscribe({
        next: (details) => {
          const byId = new Map(details.filter((d): d is ProductDetail => !!d).map((d) => [d.productId, d]));
          this.products.set(ids.map((id) => byId.get(id)).filter((d): d is ProductDetail => !!d));
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    });
  }

  remove(productId: number): void {
    this.compare.remove(productId);
  }

  attrValue(p: ProductDetail, name: string): string {
    const a = p.attributes.find((x) => x.attributeName === name);
    return a ? (a.value ?? a.valueText ?? '—') : '—';
  }

  addToCart(p: ProductDetail): void {
    if (!p.inStock || this.adding()) return;
    this.adding.set(p.productId);
    this.cart.add(p.productId, null, 1).subscribe({
      next: () => this.adding.set(null),
      error: () => this.adding.set(null),
    });
  }
}
