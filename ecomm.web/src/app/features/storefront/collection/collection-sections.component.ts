import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { WishlistButtonComponent } from '../../../shared/wishlist-button/wishlist-button.component';
import { CollectionPageStore } from './collection-page.store';

/** Breadcrumb trail. Dynamic section, `collection` template — fixes a previously-shipped bug where
 *  this section type was selectable in the theme editor but fell through to the generic product-rail
 *  renderer (same bug shape as Phase A's RelatedProducts fix, mirrors ProductBreadcrumbsComponent). */
@Component({
  selector: 'app-collection-breadcrumbs',
  imports: [RouterLink],
  template: `
    <nav class="text-xs text-slate-400 mb-5">
      <a routerLink="/" class="hover:text-primary">Home</a>
      @if (store.activeCategory(); as cat) {
        / <a routerLink="/products" class="hover:text-primary">All products</a>
        @if (parentCategory(); as parent) {
          / <a [routerLink]="['/category', parent.slug]" class="hover:text-primary">{{ parent.name }}</a>
        }
        / <span class="text-slate-600">{{ cat.name }}</span>
      } @else {
        / <span class="text-slate-600">All products</span>
      }
    </nav>
  `,
})
export class CollectionBreadcrumbsComponent {
  readonly store = inject(CollectionPageStore);
  readonly parentCategory = computed(() => {
    const cat = this.store.activeCategory();
    if (!cat?.parentCategoryId) return null;
    return this.store.categories().find((c) => c.categoryId === cat.parentCategoryId) ?? null;
  });
}

/** Collection title + product count + (on the all-products view) the category showcase. */
@Component({
  selector: 'app-collection-header',
  imports: [RouterLink],
  template: `
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-slate-900">{{ store.activeCategory()?.name ?? 'All products' }}</h1>
      @if (store.result()) {
        <p class="text-sm text-slate-500 mt-1">{{ store.result()!.totalCount }} product(s)</p>
      }
    </div>

    @if (!store.activeCategory() && store.categories().length) {
      <div class="grid grid-cols-3 sm:grid-cols-4 lg:grid-cols-6 gap-4 mb-8">
        @for (c of store.categories(); track c.categoryId) {
          <a [routerLink]="['/category', c.slug]" class="group text-center">
            <div class="aspect-square rounded-2xl overflow-hidden bg-slate-100 border border-slate-200">
              @if (c.imageUrl) { <img [src]="c.imageUrl" [alt]="c.name" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
            </div>
            <p class="mt-2 text-sm font-medium text-slate-700 group-hover:text-primary">{{ c.name }}</p>
          </a>
        }
      </div>
    }
  `,
})
export class CollectionHeaderComponent {
  readonly store = inject(CollectionPageStore);
}

/** Filter bar + category sidebar + product grid + pagination. */
@Component({
  selector: 'app-collection-grid',
  imports: [FormsModule, RouterLink, CurrencyPipe, ProductCardComponent, WishlistButtonComponent],
  template: `
    <div class="flex flex-wrap items-center gap-3 mb-6">
      <input type="search" [(ngModel)]="store.searchText" (keyup.enter)="store.applyFilters()" placeholder="Search products…"
        class="flex-1 min-w-[200px] rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />

      <select [(ngModel)]="store.brandId" (ngModelChange)="store.applyFilters()"
        class="rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
        <option [ngValue]="''">All brands</option>
        @for (b of store.brands(); track b.brandId) { <option [ngValue]="b.brandId">{{ b.name }}</option> }
      </select>

      <div class="flex items-center gap-1.5">
        <input type="number" min="0" [(ngModel)]="store.minPrice" (keyup.enter)="store.applyFilters()" (blur)="store.applyFilters()"
          placeholder="Min ₹" class="w-24 rounded-lg border border-slate-300 px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
        <span class="text-slate-400 text-sm">–</span>
        <input type="number" min="0" [(ngModel)]="store.maxPrice" (keyup.enter)="store.applyFilters()" (blur)="store.applyFilters()"
          placeholder="Max ₹" class="w-24 rounded-lg border border-slate-300 px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
      </div>

      <select [(ngModel)]="store.sort" (ngModelChange)="store.applyFilters()"
        class="rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
        <option value="">Newest</option>
        <option value="bestsellers">Popularity</option>
        <option value="price">Price: low to high</option>
        <option value="price_desc">Price: high to low</option>
        <option value="name">Name</option>
      </select>

      <div class="flex rounded-lg border border-slate-300 overflow-hidden ml-auto">
        <button type="button" (click)="store.setViewMode('grid')" aria-label="Grid view"
          class="px-2.5 py-2" [class]="store.viewMode() === 'grid' ? 'bg-slate-800 text-white' : 'text-slate-500 hover:bg-slate-50'">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>
        </button>
        <button type="button" (click)="store.setViewMode('list')" aria-label="List view"
          class="px-2.5 py-2 border-l border-slate-300" [class]="store.viewMode() === 'list' ? 'bg-slate-800 text-white' : 'text-slate-500 hover:bg-slate-50'">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="18" x2="21" y2="18"/></svg>
        </button>
      </div>
    </div>

    <div class="flex gap-6">
      <aside class="hidden md:block w-48 shrink-0">
        <h2 class="text-xs font-semibold text-slate-400 uppercase mb-2">Categories</h2>
        <ul class="space-y-1 text-sm">
          <li><a routerLink="/products" class="text-slate-600 hover:text-primary">All</a></li>
          @for (c of store.categories(); track c.categoryId) {
            <li>
              <a [routerLink]="['/category', c.slug]"
                 class="hover:text-primary"
                 [class]="store.activeCategory()?.categoryId === c.categoryId ? 'text-primary font-medium' : 'text-slate-600'">
                {{ c.name }}
              </a>
            </li>
          }
        </ul>
      </aside>

      <div class="flex-1">
        @if (store.loading()) {
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            @for (s of store.skeletons; track s) {
              <div class="bg-white border border-slate-200 rounded-xl overflow-hidden animate-pulse">
                <div class="aspect-square bg-slate-100"></div>
                <div class="p-3 space-y-2">
                  <div class="h-2 w-1/3 bg-slate-100 rounded"></div>
                  <div class="h-3 w-3/4 bg-slate-100 rounded"></div>
                  <div class="h-4 w-1/2 bg-slate-100 rounded"></div>
                </div>
              </div>
            }
          </div>
        } @else if (store.result() && store.result()!.items.length === 0) {
          <div class="py-20 text-center text-slate-400">No products found.</div>
        } @else {
          @if (store.viewMode() === 'list') {
            <div class="flex flex-col divide-y divide-slate-200 border-y border-slate-200">
              @for (p of store.result()!.items; track p.productId) {
                <a [routerLink]="['/product', p.slug]" class="flex gap-4 py-4 hover:bg-slate-50 px-2 -mx-2 rounded-lg">
                  <div class="w-24 h-24 shrink-0 bg-slate-50 rounded-lg overflow-hidden">
                    @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" loading="lazy" /> }
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="text-xs text-slate-400">{{ p.brandName ?? p.categoryName }}</p>
                    <h3 class="text-sm font-medium text-slate-800 line-clamp-1">{{ p.name }}</h3>
                    <div class="mt-1 flex items-baseline gap-2">
                      <span class="text-base font-semibold text-slate-900">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</span>
                      @if (p.compareAtPrice && p.compareAtPrice > p.price) {
                        <span class="text-xs text-slate-400 line-through">{{ p.compareAtPrice | currency:'INR':'symbol':'1.0-0' }}</span>
                      }
                    </div>
                    @if (!p.inStock) { <p class="text-[11px] font-medium text-slate-400 mt-1">Out of stock</p> }
                  </div>
                  <div class="shrink-0" (click)="$event.preventDefault(); $event.stopPropagation()">
                    <app-wishlist-button [productId]="p.productId" />
                  </div>
                </a>
              }
            </div>
          } @else {
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
              @for (p of store.result()!.items; track p.productId) {
                <app-product-card [product]="p" />
              }
            </div>
          }

          @if (store.result()!.totalPages > 1) {
            <div class="flex justify-center gap-1 mt-8">
              @for (pg of store.pages; track pg) {
                <button type="button" (click)="store.goToPage(pg)"
                  class="w-9 h-9 rounded-lg text-sm border"
                  [class]="pg === store.result()!.page ? 'bg-primary text-white border-primary' : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'">
                  {{ pg }}
                </button>
              }
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class CollectionGridComponent {
  readonly store = inject(CollectionPageStore);
}
