import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { ResponsiveImgDirective } from '../../../shared/responsive-img/responsive-img.directive';
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
  imports: [RouterLink, ResponsiveImgDirective],
  template: `
    @if (!store.activeCategory() && topLevel().length) {
      <div class="mb-8">
        @if (settings().heading) { <h2 class="text-lg font-semibold text-slate-800 mb-3">{{ settings().heading }}</h2> }
        @if (settings().style === 'icons') {
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3" [style.--cols]="columns()">
            @for (c of topLevel(); track c.categoryId) {
              <a [routerLink]="['/category', c.slug]" class="block p-4 text-center rounded-xl border border-slate-200 hover:border-primary transition">
                @if (c.imageUrl) { <img [src]="c.imageUrl" [appImgSrc]="c.imageUrl" appImgSizes="40px" [alt]="c.name" class="w-10 h-10 mx-auto object-contain mb-2" loading="lazy" /> }
                <div class="text-sm font-medium text-slate-700">{{ c.name }}</div>
              </a>
            }
          </div>
        } @else {
          <div class="grid grid-cols-[repeat(3,minmax(0,1fr))] md:grid-cols-[repeat(var(--cols),minmax(0,1fr))] gap-4" [style.--cols]="columns()">
            @for (c of topLevel(); track c.categoryId) {
              <a [routerLink]="['/category', c.slug]" class="group text-center">
                <div class="aspect-square rounded-2xl overflow-hidden bg-slate-100 border border-slate-200">
                  @if (c.imageUrl) {
                    <img [src]="c.imageUrl" [appImgSrc]="c.imageUrl" appImgSizes="(min-width: 768px) 16vw, 33vw" [alt]="c.name" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" />
                  } @else {
                    <div class="w-full h-full flex items-center justify-center bg-gradient-to-br from-slate-100 to-slate-200 group-hover:from-primary/10 group-hover:to-primary/20 transition">
                      <span class="text-2xl font-semibold text-slate-400 group-hover:text-primary">{{ c.name.charAt(0) }}</span>
                    </div>
                  }
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
  /** Only shown on "All products" (no active category) — top-level only, so a subcategory never
   *  appears as if it were its own top-level tile alongside its parent. */
  readonly topLevel = computed(() => this.store.categories().filter((c) => !c.parentCategoryId));
}

/** The faceted filter rail — categories + every filter (brand/colour/size/attributes/price/rating/
 * availability), each a collapsible section with live counts sourced from the /facets endpoint. Shared
 * by the desktop sidebar and the mobile drawer so the two never drift. Counts come from the store's
 * `facets()` signal, which the server computes with per-facet exclusion so multi-select stays usable
 * (selecting one colour doesn't zero out the others). Emits `navigated` after any category link so the
 * mobile drawer can close itself. */
@Component({
  selector: 'app-collection-facets',
  imports: [FormsModule, RouterLink],
  template: `
    @if (showCategories()) {
      <section class="border-b border-slate-200 pb-4 mb-4">
        <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-2.5">Category</h3>
        <ul class="space-y-1.5 text-sm">
          <li>
            <a routerLink="/products" (click)="navigated.emit()"
               [class]="!store.activeCategory() ? 'text-primary font-medium' : 'text-slate-600 hover:text-primary'">All products</a>
          </li>
          @for (c of sidebarCategories(); track c.categoryId) {
            <li>
              <a [routerLink]="['/category', c.slug]" (click)="navigated.emit()"
                 [class]="store.activeCategory()?.categoryId === c.categoryId ? 'text-primary font-medium' : 'text-slate-600 hover:text-primary'">
                {{ c.name }}
              </a>
            </li>
          }
        </ul>
      </section>
    }

    @if (showFilters()) {
      <!-- Price -->
      <section class="border-b border-slate-200 pb-4 mb-4">
        <button type="button" (click)="toggle('price')" class="flex w-full items-center justify-between mb-2.5">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wide">Price</span>
          <span class="text-slate-300 text-xs">{{ isOpen('price') ? '▾' : '▸' }}</span>
        </button>
        @if (isOpen('price')) {
          <div class="flex items-center gap-1.5">
            <input type="number" min="0" [(ngModel)]="store.minPrice" (keyup.enter)="applyPrice()"
              [placeholder]="pricePlaceholder(store.facets()?.priceMin)" class="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
            <span class="text-slate-400 text-sm">–</span>
            <input type="number" min="0" [(ngModel)]="store.maxPrice" (keyup.enter)="applyPrice()"
              [placeholder]="pricePlaceholder(store.facets()?.priceMax)" class="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
            <button type="button" (click)="applyPrice()" class="shrink-0 rounded-lg bg-slate-800 text-white text-xs px-2.5 py-2 hover:bg-slate-700">Go</button>
          </div>
        }
      </section>

      <!-- Brand -->
      @if (store.facets()?.brands?.length) {
        <section class="border-b border-slate-200 pb-4 mb-4">
          <button type="button" (click)="toggle('brand')" class="flex w-full items-center justify-between mb-2.5">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wide">Brand</span>
            <span class="text-slate-300 text-xs">{{ isOpen('brand') ? '▾' : '▸' }}</span>
          </button>
          @if (isOpen('brand')) {
            <ul class="space-y-1.5 max-h-56 overflow-auto pr-1">
              @for (b of store.facets()!.brands; track b.brandId) {
                <li>
                  <label class="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
                    <input type="checkbox" [checked]="store.brandIds.includes(b.brandId)" (change)="store.toggleBrand(b.brandId)"
                      class="rounded border-slate-300 text-primary focus:ring-primary" />
                    <span class="flex-1 truncate">{{ b.name }}</span>
                    <span class="text-xs text-slate-400">{{ b.count }}</span>
                  </label>
                </li>
              }
            </ul>
          }
        </section>
      }

      <!-- Colour -->
      @if (store.facets()?.colors?.length) {
        <section class="border-b border-slate-200 pb-4 mb-4">
          <button type="button" (click)="toggle('color')" class="flex w-full items-center justify-between mb-2.5">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wide">Colour</span>
            <span class="text-slate-300 text-xs">{{ isOpen('color') ? '▾' : '▸' }}</span>
          </button>
          @if (isOpen('color')) {
            <div class="flex flex-wrap gap-2">
              @for (c of store.facets()!.colors; track c.value) {
                <button type="button" (click)="store.toggleColor(c.value)" [title]="c.value + ' (' + c.count + ')'"
                  class="flex items-center gap-1.5 rounded-full border pl-1 pr-2.5 py-1 text-xs transition"
                  [class]="store.colors.includes(c.value) ? 'border-primary ring-1 ring-primary text-slate-800' : 'border-slate-300 text-slate-600 hover:border-slate-400'">
                  <span class="w-4 h-4 rounded-full border border-black/10" [style.background-color]="c.hex || '#e2e8f0'"></span>
                  {{ c.value }}
                </button>
              }
            </div>
          }
        </section>
      }

      <!-- Size -->
      @if (store.facets()?.sizes?.length) {
        <section class="border-b border-slate-200 pb-4 mb-4">
          <button type="button" (click)="toggle('size')" class="flex w-full items-center justify-between mb-2.5">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wide">Size</span>
            <span class="text-slate-300 text-xs">{{ isOpen('size') ? '▾' : '▸' }}</span>
          </button>
          @if (isOpen('size')) {
            <div class="flex flex-wrap gap-2">
              @for (s of store.facets()!.sizes; track s.value) {
                <button type="button" (click)="store.toggleSize(s.value)"
                  class="min-w-[2.5rem] rounded-lg border px-2.5 py-1.5 text-xs font-medium transition"
                  [class]="store.sizes.includes(s.value) ? 'border-primary bg-primary/5 text-primary' : 'border-slate-300 text-slate-600 hover:border-slate-400'">
                  {{ s.value }}
                </button>
              }
            </div>
          }
        </section>
      }

      <!-- Attributes (each filterable attribute group) -->
      @for (attr of store.facets()?.attributes ?? []; track attr.code) {
        <section class="border-b border-slate-200 pb-4 mb-4">
          <button type="button" (click)="toggle('attr:' + attr.code)" class="flex w-full items-center justify-between mb-2.5">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wide">{{ attr.name }}</span>
            <span class="text-slate-300 text-xs">{{ isOpen('attr:' + attr.code) ? '▾' : '▸' }}</span>
          </button>
          @if (isOpen('attr:' + attr.code)) {
            <ul class="space-y-1.5 max-h-56 overflow-auto pr-1">
              @for (v of attr.values; track v.value) {
                <li>
                  <label class="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
                    <input type="checkbox" [checked]="store.isAttrActive(attr.code, v.value)" (change)="store.toggleAttr(attr.code, v.value)"
                      class="rounded border-slate-300 text-primary focus:ring-primary" />
                    <span class="flex-1 truncate">{{ v.value }}</span>
                    <span class="text-xs text-slate-400">{{ v.count }}</span>
                  </label>
                </li>
              }
            </ul>
          }
        </section>
      }

      <!-- Rating -->
      @if (hasRatings()) {
        <section class="border-b border-slate-200 pb-4 mb-4">
          <button type="button" (click)="toggle('rating')" class="flex w-full items-center justify-between mb-2.5">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wide">Customer rating</span>
            <span class="text-slate-300 text-xs">{{ isOpen('rating') ? '▾' : '▸' }}</span>
          </button>
          @if (isOpen('rating')) {
            <ul class="space-y-1">
              @for (n of ratingRows; track n) {
                @if (ratingCount(n) > 0) {
                  <li>
                    <button type="button" (click)="store.setMinRating(n)"
                      class="flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-sm transition"
                      [class]="store.minRating === n ? 'bg-primary/5 text-primary' : 'text-slate-600 hover:bg-slate-50'">
                      <span class="text-amber-400 tracking-tight">
                        @for (star of [1,2,3,4,5]; track star) {<span [class.text-slate-200]="star > n">★</span>}
                      </span>
                      <span class="text-xs">&amp; up</span>
                      <span class="ml-auto text-xs text-slate-400">{{ ratingCount(n) }}</span>
                    </button>
                  </li>
                }
              }
            </ul>
          }
        </section>
      }

      <!-- Availability -->
      <section class="pb-1">
        <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-2.5">Availability</h3>
        <label class="flex items-center gap-2 text-sm text-slate-600 cursor-pointer mb-1.5">
          <input type="checkbox" [checked]="store.inStock" (change)="store.setInStock(!store.inStock)"
            class="rounded border-slate-300 text-primary focus:ring-primary" />
          <span class="flex-1">In stock only</span>
          <span class="text-xs text-slate-400">{{ store.facets()?.inStockCount ?? 0 }}</span>
        </label>
        <label class="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
          <input type="checkbox" [checked]="store.onSale" (change)="store.setOnSale(!store.onSale)"
            class="rounded border-slate-300 text-primary focus:ring-primary" />
          <span class="flex-1">On sale</span>
          <span class="text-xs text-slate-400">{{ store.facets()?.onSaleCount ?? 0 }}</span>
        </label>
      </section>
    }
  `,
})
export class CollectionFacetsComponent {
  readonly store = inject(CollectionPageStore);
  /** When false the category list is hidden (host doesn't want a category picker). */
  showCategories = input(true);
  showFilters = input(true);
  /** Fired after a category link is followed — lets the mobile drawer close. */
  readonly navigated = output<void>();

  /** Scoped, not the full flat site tree — showing every category (top-level and nested, alphabetised
   *  together) made a subcategory look identical to an unrelated top-level one, and dumped categories
   *  with zero relevance to the current view into the list. No active category → top-level only.
   *  Active category → its children (to narrow further); if it has none, its siblings instead (lateral
   *  browsing), since the current category itself is already shown in the page title/breadcrumb. */
  readonly sidebarCategories = computed(() => {
    const cats = this.store.categories();
    const active = this.store.activeCategory();
    if (!active) return cats.filter((c) => !c.parentCategoryId);
    const children = cats.filter((c) => c.parentCategoryId === active.categoryId);
    if (children.length) return children;
    return cats.filter((c) => c.parentCategoryId === active.parentCategoryId && c.categoryId !== active.categoryId);
  });

  /** "n★ & up" rows, high to low; RatingCounts is 0-indexed so n maps to ratingCounts[n-1]. */
  readonly ratingRows = [4, 3, 2, 1];
  private readonly collapsed = signal<Set<string>>(new Set());
  isOpen(key: string): boolean { return !this.collapsed().has(key); }
  toggle(key: string): void {
    const next = new Set(this.collapsed());
    next.has(key) ? next.delete(key) : next.add(key);
    this.collapsed.set(next);
  }

  ratingCount(n: number): number { return this.store.facets()?.ratingCounts?.[n - 1] ?? 0; }
  hasRatings(): boolean { return this.ratingRows.some((n) => this.ratingCount(n) > 0); }
  pricePlaceholder(v: number | undefined): string { return v != null ? `₹${Math.round(v)}` : '₹'; }
  applyPrice(): void { this.store.setPrice(this.store.minPrice, this.store.maxPrice); }
}

/** Filter bar + category sidebar + product grid + pagination. Every knob here is now driven by the
 * section's own authored settings (previously declared in the schema but silently ignored — the editor
 * showed working-looking controls for columns/showFilters/showSort that changed nothing on the live
 * storefront). Adds a mobile "Filter & Sort" drawer, since the desktop category sidebar is `hidden
 * md:block` with no mobile equivalent — mobile shoppers previously had no way to browse by category or
 * filter at all on this page. */
@Component({
  selector: 'app-collection-grid',
  imports: [FormsModule, RouterLink, CurrencyPipe, DecimalPipe, ProductCardComponent, WishlistButtonComponent, CollectionFacetsComponent, ResponsiveImgDirective],
  template: `
    <div class="flex flex-wrap items-center gap-3 mb-4">
      @if (showFilters()) {
        <input type="search" [(ngModel)]="store.searchText" (keyup.enter)="store.applyFilters()" placeholder="Search products…"
          class="hidden md:block flex-1 min-w-[200px] rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
      }

      @if (showSort()) {
        <select [(ngModel)]="store.sort" (ngModelChange)="store.applyFilters()"
          class="hidden md:block rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary">
          <option value="">Newest</option>
          <option value="bestsellers">Popularity</option>
          <option value="rating">Avg. customer rating</option>
          <option value="price">Price: low to high</option>
          <option value="price_desc">Price: high to low</option>
          <option value="discount">Discount</option>
          <option value="name">Name</option>
        </select>
      }

      @if (showFilters() || showSort() || showCategorySidebar()) {
        <button type="button" (click)="mobileFilterOpen.set(true)"
          class="md:hidden flex items-center gap-1.5 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 bg-white">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="4" y1="6" x2="20" y2="6"/><line x1="7" y1="12" x2="17" y2="12"/><line x1="10" y1="18" x2="14" y2="18"/></svg>
          Filter &amp; Sort
          @if (store.chips.length) { <span class="ml-0.5 inline-flex items-center justify-center min-w-[1.1rem] h-[1.1rem] rounded-full bg-primary text-white text-[10px] px-1">{{ store.chips.length }}</span> }
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

    <!-- Mobile filter & sort drawer: the full facet rail + sort, in one place. -->
    @if (mobileFilterOpen()) {
      <div class="fixed inset-0 z-50 md:hidden">
        <div class="absolute inset-0 bg-black/40" (click)="mobileFilterOpen.set(false)"></div>
        <div class="absolute inset-x-0 bottom-0 max-h-[88vh] flex flex-col bg-white rounded-t-2xl">
          <div class="flex items-center justify-between px-4 py-3 border-b border-slate-200">
            <h2 class="font-semibold text-slate-800">Filter &amp; Sort</h2>
            <div class="flex items-center gap-3">
              @if (store.hasActiveFilters) {
                <button type="button" (click)="store.clearAll()" class="text-xs text-primary font-medium">Clear all</button>
              }
              <button type="button" (click)="mobileFilterOpen.set(false)" class="text-slate-400 text-2xl leading-none px-1">×</button>
            </div>
          </div>

          <div class="flex-1 overflow-auto px-4 py-4">
            @if (showSort()) {
              <section class="border-b border-slate-200 pb-4 mb-4">
                <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-2.5">Sort by</h3>
                <select [(ngModel)]="store.sort" (ngModelChange)="store.applyFilters()" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white">
                  <option value="">Newest</option>
                  <option value="bestsellers">Popularity</option>
                  <option value="rating">Avg. customer rating</option>
                  <option value="price">Price: low to high</option>
                  <option value="price_desc">Price: high to low</option>
                  <option value="discount">Discount</option>
                  <option value="name">Name</option>
                </select>
              </section>
            }
            <app-collection-facets [showCategories]="showCategorySidebar()" [showFilters]="showFilters()" (navigated)="mobileFilterOpen.set(false)" />
          </div>

          <div class="px-4 py-3 border-t border-slate-200">
            <button type="button" (click)="mobileFilterOpen.set(false)" class="btn-primary w-full">
              Show {{ store.result()?.totalCount ?? 0 }} results
            </button>
          </div>
        </div>
      </div>
    }

    <div class="flex gap-6">
      @if (showFilters() || showCategorySidebar()) {
        <aside class="hidden md:block w-60 shrink-0">
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-sm font-semibold text-slate-800">Filters</h2>
            @if (store.hasActiveFilters) {
              <button type="button" (click)="store.clearAll()" class="text-xs text-primary font-medium hover:underline">Clear all</button>
            }
          </div>
          <app-collection-facets [showCategories]="showCategorySidebar()" [showFilters]="showFilters()" />
        </aside>
      }

      <div class="flex-1 min-w-0">
        <!-- Result count + active-filter chips -->
        <div class="flex flex-wrap items-center gap-2 mb-4 min-h-[1.75rem]">
          @if (!store.loading() && store.result()) {
            <span class="text-sm text-slate-500 mr-1">{{ store.result()!.totalCount }} result{{ store.result()!.totalCount === 1 ? '' : 's' }}</span>
          }
          @for (chip of store.chips; track chip.label) {
            <button type="button" (click)="chip.remove()"
              class="inline-flex items-center gap-1 rounded-full bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs pl-2.5 pr-1.5 py-1 transition">
              {{ chip.label }}
              <span class="text-slate-400 text-sm leading-none">×</span>
            </button>
          }
          @if (store.hasActiveFilters) {
            <button type="button" (click)="store.clearAll()" class="text-xs text-primary font-medium hover:underline ml-1">Clear all</button>
          }
        </div>

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
          <div class="py-20 text-center">
            <svg class="w-12 h-12 mx-auto text-slate-300 mb-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
            <p class="text-slate-600 font-medium">No products match your filters</p>
            <p class="text-sm text-slate-400 mt-1">Try removing a filter or widening your price range.</p>
            @if (store.hasActiveFilters) {
              <button type="button" (click)="store.clearAll()" class="btn-primary mt-4">Clear all filters</button>
            }
          </div>
        } @else {
          @if (store.viewMode() === 'list') {
            <div class="flex flex-col divide-y divide-slate-200 border-y border-slate-200">
              @for (p of store.result()!.items; track p.productId) {
                <a [routerLink]="['/product', p.slug]" class="flex gap-4 py-4 hover:bg-slate-50 px-2 -mx-2 rounded-lg">
                  <div class="w-24 h-24 shrink-0 bg-slate-50 rounded-lg overflow-hidden">
                    @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [appImgSrc]="p.primaryImageUrl" appImgSizes="96px" [alt]="p.name" class="w-full h-full object-cover" loading="lazy" /> }
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="text-xs text-slate-400">{{ p.brandName ?? p.categoryName }}</p>
                    <h3 class="text-sm font-medium text-slate-800 line-clamp-1">{{ p.name }}</h3>
                    @if (p.reviewCount && p.reviewCount > 0) {
                      <div class="mt-1 flex items-center gap-1.5">
                        <span class="inline-flex items-center gap-0.5 rounded bg-green-600 text-white text-[11px] font-semibold px-1.5 py-0.5">
                          {{ p.rating | number:'1.1-1' }}<span class="text-[9px] leading-none">★</span>
                        </span>
                        <span class="text-[11px] text-slate-400">({{ p.reviewCount }})</span>
                      </div>
                    }
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
