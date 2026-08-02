import { Injectable, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { combineLatest, switchMap } from 'rxjs';
import { SITE_URL } from '../../../core/api.config';
import { PagedResult } from '../../../core/models/api-response.model';
import { Brand, Category, ProductListItem, ProductQuery } from '../../../core/models/catalog.model';
import { CatalogService } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

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

              const q: ProductQuery = {
                search: this.searchText || undefined,
                categoryId: cat?.categoryId,
                brandId: this.brandId || undefined,
                sort: this.sort || undefined,
                minPrice: this.minPrice || undefined,
                maxPrice: this.maxPrice || undefined,
                page: query.get('page') ? +query.get('page')! : 1,
                pageSize: this.pageSize,
              };
              this.lastQuery = q;
              return this.catalog.getProducts(q);
            }),
          );
        }),
      )
      .subscribe({
        next: (res) => {
          this.result.set(res);
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

  goToPage(page: number): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { page }, queryParamsHandling: 'merge' });
  }

  get pages(): number[] {
    const r = this.result();
    return r ? Array.from({ length: r.totalPages }, (_, i) => i + 1) : [];
  }
}
