import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { WishlistButtonComponent } from '../../../shared/wishlist-button/wishlist-button.component';
import { CollectionPageStore } from './collection-page.store';

/** Reads a dynamic section's own authored settings (parsed once) with sane defaults when nothing's
 * authored — same parse-with-fallback pattern static sections use in storefront-section.component.ts. */
function parseSettings(json: string | null | undefined): any {
  if (!json) return {};
  try { return JSON.parse(json) ?? {}; } catch { return {}; }
}

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

/** Collection title + product count + an optional promotional banner (image/overlay/alignment/
 * description override — all authored, none of it existed before this pass). The category showcase
 * that used to be baked in here unconditionally now lives in its own CollectionCategories section
 * below, so it's addable/removable/reorderable/restylable instead of a fixed, non-editable freebie. */
@Component({
  selector: 'app-collection-header',
  imports: [],
  template: `
    <div class="mb-6 relative overflow-hidden rounded-2xl"
         [class.p-8]="settings().bannerImage"
         [style.background-image]="settings().bannerImage ? 'url(' + settings().bannerImage + ')' : null"
         style="background-size: cover; background-position: center;">
      @if (settings().bannerImage) {
        <div class="absolute inset-0" [style.background-color]="settings().overlayColor || 'rgba(15,23,42,0.45)'"></div>
      }
      <div class="relative" [class.text-center]="settings().textAlign === 'center'">
        <h1 class="text-2xl font-bold" [class]="settings().bannerImage ? 'text-white' : 'text-slate-900'">
          {{ store.activeCategory()?.name ?? 'All products' }}
        </h1>
        @if (settings().description || store.activeCategory()?.description) {
          <p class="text-sm mt-1" [class]="settings().bannerImage ? 'text-white/85' : 'text-slate-500'">
            {{ settings().description || store.activeCategory()?.description }}
          </p>
        }
        @if (store.result()) {
          <p class="text-sm mt-1" [class]="settings().bannerImage ? 'text-white/85' : 'text-slate-500'">{{ store.result()!.totalCount }} product(s)</p>
        }
      </div>
    </div>
  `,
})
export class CollectionHeaderComponent {
  readonly store = inject(CollectionPageStore);
  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));
}

/** Shop-by-category tile grid — split out of CollectionHeader so it's a real, independently
 * addable/removable/reorderable section instead of unconditional non-editable markup. Preserves the
 * original root-only behaviour (hidden once a specific category is active) since the `collection`
 * template is shared by every category page — an always-on tile grid would be redundant once a shopper
 * has already drilled into a category. */
@Component({
  selector: 'app-collection-categories',
  imports: [RouterLink],
  template: `
    @if (!store.activeCategory() && store.categories().length) {
      <div class="mb-8">
        @if (settings().heading) { <h2 class="text-lg font-semibold text-slate-800 mb-3">{{ settings().heading }}</h2> }
        @if (settings().style === 'icons') {
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3" [style.--cols]="columns()">
            @for (c of store.categories(); track c.categoryId) {
              <a [routerLink]="['/category', c.slug]" class="block p-4 text-center rounded-xl border border-slate-200 hover:border-primary transition">
                @if (c.imageUrl) { <img [src]="c.imageUrl" [alt]="c.name" class="w-10 h-10 mx-auto object-contain mb-2" loading="lazy" /> }
                <div class="text-sm font-medium text-slate-700">{{ c.name }}</div>
              </a>
            }
          </div>
        } @else {
          <div class="grid grid-cols-[repeat(3,minmax(0,1fr))] md:grid-cols-[repeat(var(--cols),minmax(0,1fr))] gap-4" [style.--cols]="columns()">
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
      </div>
    }
  `,
})
export class CollectionCategoriesComponent {
  readonly store = inject(CollectionPageStore);
  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));
  readonly columns = computed(() => Math.max(2, Number(this.settings().columns) || 6));
}

/** Filter bar + category sidebar + product grid + pagination. Every knob here is now driven by the
 * section's own authored settings (previously declared in the schema but silently ignored — the editor
 * showed working-looking controls for columns/showFilters/showSort that changed nothing on the live
 * storefront). Adds a mobile "Filter & Sort" drawer, since the desktop category sidebar is `hidden
 * md:block` with no mobile equivalent — mobile shoppers previously had no way to browse by category or
 * filter at all on this page. */
@Component({
  selector: 'app-collection-grid',
  imports: [FormsModule, RouterLink, CurrencyPipe, ProductCardComponent, WishlistButtonComponent],
  template: `
    <div class="flex flex-wrap items-center gap-3 mb-6">
      @if (showFilters()) {
        <input type="search" [(ngModel)]="store.searchText" (keyup.enter)="store.applyFilters()" placeholder="Search products…"
          class="hidden md:block flex-1 min-w-[200px] rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />

        <select [(ngModel)]="store.brandId" (ngModelChange)="store.applyFilters()"
          class="hidden md:block rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
          <option [ngValue]="''">All brands</option>
          @for (b of store.brands(); track b.brandId) { <option [ngValue]="b.brandId">{{ b.name }}</option> }
        </select>

        <div class="hidden md:flex items-center gap-1.5">
          <input type="number" min="0" [(ngModel)]="store.minPrice" (keyup.enter)="store.applyFilters()" (blur)="store.applyFilters()"
            placeholder="Min ₹" class="w-24 rounded-lg border border-slate-300 px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
          <span class="text-slate-400 text-sm">–</span>
          <input type="number" min="0" [(ngModel)]="store.maxPrice" (keyup.enter)="store.applyFilters()" (blur)="store.applyFilters()"
            placeholder="Max ₹" class="w-24 rounded-lg border border-slate-300 px-2.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
        </div>
      }

      @if (showSort()) {
        <select [(ngModel)]="store.sort" (ngModelChange)="store.applyFilters()"
          class="hidden md:block rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
          <option value="">Newest</option>
          <option value="bestsellers">Popularity</option>
          <option value="price">Price: low to high</option>
          <option value="price_desc">Price: high to low</option>
          <option value="name">Name</option>
        </select>
      }

      @if (showFilters() || showSort() || showCategorySidebar()) {
        <button type="button" (click)="mobileFilterOpen.set(true)"
          class="md:hidden flex items-center gap-1.5 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 bg-white">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="4" y1="6" x2="20" y2="6"/><line x1="7" y1="12" x2="17" y2="12"/><line x1="10" y1="18" x2="14" y2="18"/></svg>
          Filter &amp; Sort
        </button>
      }

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

    <!-- Mobile filter & sort drawer: everything the desktop sidebar + filter bar offer, in one place. -->
    @if (mobileFilterOpen()) {
      <div class="fixed inset-0 z-50 md:hidden">
        <div class="absolute inset-0 bg-black/40" (click)="mobileFilterOpen.set(false)"></div>
        <div class="absolute inset-x-0 bottom-0 max-h-[85vh] overflow-auto bg-white rounded-t-2xl p-4">
          <div class="flex items-center justify-between mb-4">
            <h2 class="font-semibold text-slate-800">Filter &amp; Sort</h2>
            <button type="button" (click)="mobileFilterOpen.set(false)" class="text-slate-400 text-xl leading-none px-2">×</button>
          </div>

          @if (showCategorySidebar()) {
            <div class="mb-5">
              <h3 class="text-xs font-semibold text-slate-400 uppercase mb-2">Category</h3>
              <ul class="space-y-1.5 text-sm">
                <li><a routerLink="/products" (click)="mobileFilterOpen.set(false)" class="text-slate-600">All</a></li>
                @for (c of store.categories(); track c.categoryId) {
                  <li>
                    <a [routerLink]="['/category', c.slug]" (click)="mobileFilterOpen.set(false)"
                       [class]="store.activeCategory()?.categoryId === c.categoryId ? 'text-primary font-medium' : 'text-slate-600'">
                      {{ c.name }}
                    </a>
                  </li>
                }
              </ul>
            </div>
          }

          @if (showFilters()) {
            <div class="mb-5 space-y-3">
              <h3 class="text-xs font-semibold text-slate-400 uppercase">Filter</h3>
              <input type="search" [(ngModel)]="store.searchText" placeholder="Search products…"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" />
              <select [(ngModel)]="store.brandId" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white">
                <option [ngValue]="''">All brands</option>
                @for (b of store.brands(); track b.brandId) { <option [ngValue]="b.brandId">{{ b.name }}</option> }
              </select>
              <div class="flex items-center gap-1.5">
                <input type="number" min="0" [(ngModel)]="store.minPrice" placeholder="Min ₹" class="w-full rounded-lg border border-slate-300 px-2.5 py-2 text-sm" />
                <span class="text-slate-400 text-sm">–</span>
                <input type="number" min="0" [(ngModel)]="store.maxPrice" placeholder="Max ₹" class="w-full rounded-lg border border-slate-300 px-2.5 py-2 text-sm" />
              </div>
            </div>
          }

          @if (showSort()) {
            <div class="mb-5">
              <h3 class="text-xs font-semibold text-slate-400 uppercase mb-2">Sort</h3>
              <select [(ngModel)]="store.sort" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white">
                <option value="">Newest</option>
                <option value="bestsellers">Popularity</option>
                <option value="price">Price: low to high</option>
                <option value="price_desc">Price: high to low</option>
                <option value="name">Name</option>
              </select>
            </div>
          }

          <button type="button" (click)="store.applyFilters(); mobileFilterOpen.set(false)" class="btn-primary w-full">Apply</button>
        </div>
      </div>
    }

    <div class="flex gap-6">
      @if (showCategorySidebar()) {
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
      }

      <div class="flex-1">
        @if (store.loading()) {
          <div class="grid grid-cols-[repeat(var(--cols-mobile),minmax(0,1fr))] md:grid-cols-[repeat(var(--cols-desktop),minmax(0,1fr))] gap-4"
               [style.--cols-mobile]="columnsMobile()" [style.--cols-desktop]="columnsDesktop()">
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
            <div class="grid grid-cols-[repeat(var(--cols-mobile),minmax(0,1fr))] md:grid-cols-[repeat(var(--cols-desktop),minmax(0,1fr))] gap-4"
                 [style.--cols-mobile]="columnsMobile()" [style.--cols-desktop]="columnsDesktop()">
              @for (p of store.result()!.items; track p.productId) {
                <app-product-card [product]="p" [aspectRatio]="cardAspect()" />
              }
            </div>
          }

          @if (paginationStyle() === 'loadMore') {
            @if (store.result()!.page < store.result()!.totalPages) {
              <div class="flex justify-center mt-8">
                <button type="button" (click)="store.loadMore()" [disabled]="store.loadingMore()"
                  class="border border-slate-300 hover:bg-slate-50 disabled:opacity-50 text-slate-700 font-medium px-6 py-2.5 rounded-lg">
                  {{ store.loadingMore() ? 'Loading…' : 'Load more' }}
                </button>
              </div>
            }
          } @else if (store.result()!.totalPages > 1) {
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
  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));

  readonly mobileFilterOpen = signal(false);
  readonly showFilters = computed(() => this.settings().showFilters !== false);
  readonly showSort = computed(() => this.settings().showSort !== false);
  readonly showCategorySidebar = computed(() => this.settings().showCategorySidebar !== false);
  readonly columnsDesktop = computed(() => Math.max(1, Number(this.settings().columnsDesktop) || 4));
  readonly columnsMobile = computed(() => Math.max(1, Number(this.settings().columnsMobile) || 2));
  readonly cardAspect = computed<'square' | 'portrait'>(() => (this.settings().cardAspect === 'portrait' ? 'portrait' : 'square'));
  readonly paginationStyle = computed<'numbered' | 'loadMore'>(() => (this.settings().paginationStyle === 'loadMore' ? 'loadMore' : 'numbered'));
}
