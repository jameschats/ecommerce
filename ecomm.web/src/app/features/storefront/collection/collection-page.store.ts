import { Injectable, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { combineLatest, switchMap } from 'rxjs';
import { SITE_URL } from '../../../core/api.config';
import { PagedResult } from '../../../core/models/api-response.model';
import { Brand, Category, Facets, ProductListItem, ProductQuery } from '../../../core/models/catalog.model';
import { CatalogService } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

/** One applied filter, for the removable chip row. */
export interface FilterChip { label: string; remove: () => void; }

/**
 * All state + behaviour for the collection/listing page (also serves search via
 * ?search=). Provided at the CollectionPageComponent level so the (thin) section
 * components can inject it. This is the product-list logic re-homed unchanged.
 */
@Injectable()
export class CollectionPageStore {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly theme = inject(ThemeService);
  private readonly siteUrl = inject(SITE_URL);

  readonly result = signal<PagedResult<ProductListItem> | null>(null);
  readonly facets = signal<Facets | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly brands = signal<Brand[]>([]);
  readonly activeCategory = signal<Category | null>(null);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  /** Placeholder cards shown while products load (keeps layout height stable). */
  readonly skeletons = Array.from({ length: 10 }, (_, i) => i);
  /** Pure UI state, not persisted/URL-driven — resets to grid each visit, same as most sites. */
  readonly viewMode = signal<'grid' | 'list'>('grid');
  setViewMode(mode: 'grid' | 'list'): void { this.viewMode.set(mode); }

  searchText = '';
  sort = '';
  brandId: number | '' = '';
  minPrice: number | '' = '';
  maxPrice: number | '' = '';

  // Multi-value facet selections — mirror the URL (?brandIds=&color=&size=&attr=&inStock=&onSale=&minRating=).
  // Re-read from the query params on every navigation, exactly like the scalar fields above.
  brandIds: number[] = [];
  colors: string[] = [];
  sizes: string[] = [];
  attrs: string[] = []; // "code:value"
  inStock = false;
  onSale = false;
  minRating: number | '' = '';

  /** Driven by CollectionGrid's own authored settings — read once by the host before init() so the
   * very first query already uses the right page size, instead of racing a signal update against it. */
  private pageSize = 12;
  private lastQuery: ProductQuery | null = null;

  /** Wire the route → data pipeline (called once by the host, after it's resolved the template's
   * CollectionGrid settings). */
  init(pageSize = 12): void {
    this.pageSize = pageSize > 0 ? pageSize : 12;
    this.catalog.getBrands().subscribe((b) => this.brands.set(b));

    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(
        switchMap(([params, query]) => {
          this.loading.set(true);
          const slug = params.get('slug');
          return this.catalog.getCategories().pipe(
            switchMap((cats) => {
              this.categories.set(cats);
              const cat = slug ? cats.find((c) => c.slug === slug) ?? null : null;
              this.activeCategory.set(cat);

              this.searchText = query.get('search') ?? '';
              this.sort = query.get('sort') ?? '';
              this.brandId = query.get('brandId') ? +query.get('brandId')! : '';
              this.minPrice = query.get('minPrice') ? +query.get('minPrice')! : '';
              this.maxPrice = query.get('maxPrice') ? +query.get('maxPrice')! : '';
              this.brandIds = query.getAll('brandIds').map(Number).filter((n) => !isNaN(n));
              this.colors = query.getAll('color');
              this.sizes = query.getAll('size');
              this.attrs = query.getAll('attr');
              this.inStock = query.get('inStock') === 'true';
              this.onSale = query.get('onSale') === 'true';
              this.minRating = query.get('minRating') ? +query.get('minRating')! : '';

              const q: ProductQuery = {
                search: this.searchText || undefined,
                categoryId: cat?.categoryId,
                brandId: this.brandId || undefined,
                sort: this.sort || undefined,
                minPrice: this.minPrice || undefined,
                maxPrice: this.maxPrice || undefined,
                brandIds: this.brandIds.length ? this.brandIds : undefined,
                color: this.colors.length ? this.colors : undefined,
                size: this.sizes.length ? this.sizes : undefined,
                attr: this.attrs.length ? this.attrs : undefined,
                inStock: this.inStock || undefined,
                onSale: this.onSale || undefined,
                minRating: this.minRating || undefined,
                page: query.get('page') ? +query.get('page')! : 1,
                pageSize: this.pageSize,
              };
              this.lastQuery = q;
              // Facets reflect the current filter set (server applies per-facet exclusion so multi-select
              // stays usable); load them next to the products so the rail and grid update together.
              return combineLatest([this.catalog.getProducts(q), this.catalog.getFacets(q)]);
            }),
          );
        }),
      )
      .subscribe({
        next: ([res, facets]) => {
          this.result.set(res);
          this.facets.set(facets);
          this.loading.set(false);
          this.applySeo();
        },
        error: () => this.loading.set(false),
      });
  }

  /** "Load more" pagination: appends the next page onto the current results instead of replacing them. */
  loadMore(): void {
    const current = this.result();
    if (!current || !this.lastQuery || this.loadingMore()) return;
    const nextPage = current.page + 1;
    if (nextPage > current.totalPages) return;
    this.loadingMore.set(true);
    this.catalog.getProducts({ ...this.lastQuery, page: nextPage }).subscribe({
      next: (res) => {
        this.result.set({ ...res, items: [...current.items, ...res.items] });
        this.lastQuery = { ...this.lastQuery!, page: nextPage };
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
  }

  private applySeo(): void {
    const cat = this.activeCategory();
    const brand = this.theme.storeName() || 'our store';
    const title = cat ? `${cat.name} — ${brand}` : `Shop all products — ${brand}`;
    const description = cat?.description ?? `Browse ${cat?.name ?? 'our catalog'} at ${brand}. Great prices, fast delivery.`;
    this.seo.setMeta({ title, description, url: this.siteUrl + this.router.url });
  }

  applyFilters(extra: Record<string, string | number | null> = {}): void {
    const queryParams: Record<string, string | number | null> = {
      search: this.searchText || null,
      brandId: this.brandId || null,
      sort: this.sort || null,
      minPrice: this.minPrice || null,
      maxPrice: this.maxPrice || null,
      page: null,
      ...extra,
    };
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  // ---- Facet filters -------------------------------------------------------
  // Each toggle rewrites its own URL param (arrays for multi-select) and resets to page 1; the route
  // pipeline above re-reads the params, re-queries products + facets, and the rail/grid re-render.

  /** Merge facet params into the URL, always resetting pagination. Empty arrays clear the key. */
  private navFacets(params: Record<string, string | number | boolean | string[] | number[] | null>): void {
    const cleaned: Record<string, string | number | boolean | string[] | number[] | null> = { page: null };
    for (const [k, v] of Object.entries(params)) cleaned[k] = Array.isArray(v) && v.length === 0 ? null : v;
    this.router.navigate([], { relativeTo: this.route, queryParams: cleaned, queryParamsHandling: 'merge' });
  }

  private toggle<T>(list: T[], value: T): T[] {
    return list.includes(value) ? list.filter((x) => x !== value) : [...list, value];
  }

  toggleBrand(id: number): void { this.navFacets({ brandIds: this.toggle(this.brandIds, id) }); }
  toggleColor(v: string): void { this.navFacets({ color: this.toggle(this.colors, v) }); }
  toggleSize(v: string): void { this.navFacets({ size: this.toggle(this.sizes, v) }); }
  toggleAttr(code: string, value: string): void { this.navFacets({ attr: this.toggle(this.attrs, `${code}:${value}`) }); }
  isAttrActive(code: string, value: string): boolean { return this.attrs.includes(`${code}:${value}`); }

  setInStock(on: boolean): void { this.navFacets({ inStock: on ? true : null }); }
  setOnSale(on: boolean): void { this.navFacets({ onSale: on ? true : null }); }
  /** Toggle the "n★ & up" rating filter; clicking the active one clears it. */
  setMinRating(n: number): void { this.navFacets({ minRating: this.minRating === n ? null : n }); }

  setPrice(min: number | '' | null, max: number | '' | null): void {
    this.navFacets({ minPrice: min || null, maxPrice: max || null });
  }

  setSort(sort: string): void {
    this.sort = sort;
    this.navFacets({ sort: sort || null });
  }

  /** Any storefront filter active (not counting sort/search) — drives the chip row + "clear all". */
  get hasActiveFilters(): boolean {
    return this.brandIds.length > 0 || this.colors.length > 0 || this.sizes.length > 0 || this.attrs.length > 0
      || this.inStock || this.onSale || this.minRating !== '' || this.minPrice !== '' || this.maxPrice !== '';
  }

  clearAll(): void {
    this.navFacets({
      brandIds: null, color: null, size: null, attr: null,
      inStock: null, onSale: null, minRating: null, minPrice: null, maxPrice: null,
    });
  }

  /** Removable chips for every applied filter — label + its own undo action. */
  get chips(): FilterChip[] {
    const chips: FilterChip[] = [];
    const brandName = (id: number) => this.brands().find((b) => b.brandId === id)?.name ?? `Brand ${id}`;
    for (const id of this.brandIds) chips.push({ label: brandName(id), remove: () => this.toggleBrand(id) });
    for (const c of this.colors) chips.push({ label: c, remove: () => this.toggleColor(c) });
    for (const s of this.sizes) chips.push({ label: `Size: ${s}`, remove: () => this.toggleSize(s) });
    for (const a of this.attrs) {
      const [code, ...rest] = a.split(':');
      const value = rest.join(':');
      const name = this.facets()?.attributes.find((at) => at.code === code)?.name ?? code;
      chips.push({ label: `${name}: ${value}`, remove: () => this.toggleAttr(code, value) });
    }
    if (this.inStock) chips.push({ label: 'In stock', remove: () => this.setInStock(false) });
    if (this.onSale) chips.push({ label: 'On sale', remove: () => this.setOnSale(false) });
    if (this.minRating !== '') chips.push({ label: `${this.minRating}★ & up`, remove: () => this.setMinRating(this.minRating as number) });
    if (this.minPrice !== '' || this.maxPrice !== '') {
      const lo = this.minPrice === '' ? '0' : this.minPrice;
      const hi = this.maxPrice === '' ? '∞' : this.maxPrice;
      chips.push({ label: `₹${lo}–${hi}`, remove: () => this.setPrice(null, null) });
    }
    return chips;
  }

  goToPage(page: number): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { page }, queryParamsHandling: 'merge' });
  }

  get pages(): number[] {
    const r = this.result();
    return r ? Array.from({ length: r.totalPages }, (_, i) => i + 1) : [];
  }
}
