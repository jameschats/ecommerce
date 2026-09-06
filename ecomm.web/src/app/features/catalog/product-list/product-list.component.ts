import { isPlatformBrowser } from '@angular/common';
import {
  Component, ElementRef, HostListener, OnDestroy, OnInit, PLATFORM_ID, effect, inject, signal, viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, combineLatest, of, switchMap } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';
import { SITE_URL } from '../../../core/api.config';
import { Brand, Category, ProductListItem, ProductQuery } from '../../../core/models/catalog.model';
import { CatalogService } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';

@Component({
  selector: 'app-product-list',
  imports: [FormsModule, RouterLink, ProductCardComponent],
  templateUrl: './product-list.component.html',
})
export class ProductListComponent implements OnInit, OnDestroy {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly items = signal<ProductListItem[]>([]);
  readonly page = signal(1);
  readonly totalPages = signal(0);
  readonly totalCount = signal(0);
  readonly categories = signal<Category[]>([]);
  readonly brands = signal<Brand[]>([]);
  readonly activeCategory = signal<Category | null>(null);

  /** Skeleton grid on a fresh search/filter/category change. */
  readonly loading = signal(true);
  /** Spinner strip at the bottom while a further page is being appended. */
  readonly loadingMore = signal(false);
  readonly hasMore = signal(false);

  /** Placeholder cards shown while products load (keeps layout height stable). */
  readonly skeletons = Array.from({ length: 10 }, (_, i) => i);

  searchText = '';
  sort = '';
  brandId: number | '' = '';

  /** The current filters, without page — loadMore() reuses this to fetch the next page. */
  private baseQuery: Omit<ProductQuery, 'page'> = { pageSize: 24 };

  private readonly destroyed$ = new Subject<void>();

  /**
   * Search-as-you-type. Keystrokes push here rather than calling applyFilters directly, so
   * a burst of typing produces one request 400ms after the last keystroke instead of one
   * request per character — the box updated the URL and re-queried on every keystroke
   * otherwise, which raced itself and searched the API for "p", "pa", "pan", ... "panchaga".
   * distinctUntilChanged drops the extra request when a keystroke and its correction (e.g.
   * typing then deleting a character back to the same text) cancel out.
   */
  private readonly searchInput$ = new Subject<string>();

  /**
   * Autosuggest, driven by the same keystrokes as searchInput$ but on its own faster
   * debounce — a suggestion list should feel immediate, while the actual product search
   * stays throttled to avoid hammering the API. Backed by /api/catalog/suggest, which
   * already existed server-side (product-name prefix match) but had no caller.
   */
  private readonly suggestQuery$ = new Subject<string>();
  readonly suggestions = signal<string[]>([]);
  readonly showSuggestions = signal(false);

  private readonly searchWrapper = viewChild<ElementRef<HTMLElement>>('searchWrapper');

  /**
   * Infinite scroll. The sentinel sits just below the grid; once it is roughly 400px from
   * entering the viewport, the next page is fetched and appended — the reader never sees a
   * "page 2" click, they just keep scrolling. Watched with an effect rather than in ngOnInit
   * because the sentinel signal has no value until Angular has actually rendered it, and it
   * disappears and reappears (a fresh search has none until its first page loads, the last
   * page of results has none at all), so the observer has to be re-attached whenever the
   * element changes rather than wired up once.
   */
  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');
  private io?: IntersectionObserver;

  constructor() {
    effect(() => {
      const el = this.sentinel()?.nativeElement;
      this.io?.disconnect();
      if (!el || !isPlatformBrowser(this.platformId)) return;
      this.io = new IntersectionObserver(
        (entries) => { if (entries[0]?.isIntersecting) this.loadMore(); },
        { rootMargin: '400px' },
      );
      this.io.observe(el);
    });
  }

  ngOnInit(): void {
    this.catalog.getBrands().subscribe((b) => this.brands.set(b));

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroyed$))
      .subscribe(() => this.applyFilters());

    this.suggestQuery$
      .pipe(
        debounceTime(200),
        distinctUntilChanged(),
        switchMap((q) => (q.trim().length >= 2 ? this.catalog.suggest(q.trim()) : of([]))),
        takeUntil(this.destroyed$),
      )
      .subscribe((names) => {
        this.suggestions.set(names);
        this.showSuggestions.set(names.length > 0);
      });

    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(
        switchMap(([params, query]) => {
          this.loading.set(true);
          this.items.set([]);
          this.hasMore.set(false);
          const slug = params.get('slug');
          return this.catalog.getCategories().pipe(
            switchMap((cats) => {
              this.categories.set(cats);
              const cat = slug ? cats.find((c) => c.slug === slug) ?? null : null;
              this.activeCategory.set(cat);

              this.searchText = query.get('search') ?? '';
              this.sort = query.get('sort') ?? '';
              this.brandId = query.get('brandId') ? +query.get('brandId')! : '';

              this.baseQuery = {
                search: this.searchText || undefined,
                categoryId: cat?.categoryId,
                brandId: this.brandId || undefined,
                sort: this.sort || undefined,
                pageSize: 24,
              };
              return this.catalog.getProducts({ ...this.baseQuery, page: 1 });
            }),
          );
        }),
      )
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.page.set(res.page);
          this.totalPages.set(res.totalPages);
          this.totalCount.set(res.totalCount);
          this.hasMore.set(res.page < res.totalPages);
          this.loading.set(false);
          this.applySeo();
        },
        error: () => this.loading.set(false),
      });
  }

  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
    this.io?.disconnect();
  }

  /** Closes the suggestion dropdown on any click outside the search box + its panel. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.searchWrapper()?.nativeElement.contains(event.target as Node)) {
      this.showSuggestions.set(false);
    }
  }

  private applySeo(): void {
    const cat = this.activeCategory();
    // Category.Description (admin-editable, Catalog → Categories) drives the fallback
    // description when set — same admin-wins-over-derived rule as the product page.
    const title = cat
      ? `${cat.name} | Lotus Wholesale Calendar Materials — Senthaamarai Press`
      : 'Shop all products — Lotus Daily Calendars | Senthaamarai Press';
    const description = cat?.description?.trim()
      || `Buy ${cat?.name ?? 'our catalog'} wholesale from Lotus, Senthaamarai Press, Sivakasi. Factory-direct rates, pan-India delivery.`;
    this.seo.setMeta({ title, description, url: SITE_URL + this.router.url });

    // Matches the visible breadcrumb in the template above — product-detail.component.ts
    // sets the same shape one level deeper.
    const trail = [
      { '@type': 'ListItem', position: 1, name: 'Home', item: `${SITE_URL}/` },
      { '@type': 'ListItem', position: 2, name: 'All calendars', item: `${SITE_URL}/products` },
    ];
    if (cat) trail.push({ '@type': 'ListItem', position: 3, name: cat.name, item: SITE_URL + this.router.url });
    this.seo.setJsonLd([{ '@context': 'https://schema.org', '@type': 'BreadcrumbList', itemListElement: trail }]);
  }

  /** Bound to the search box's (input) event — feeds both the live filter and the suggest dropdown. */
  onSearchInput(): void {
    this.searchInput$.next(this.searchText);
    this.suggestQuery$.next(this.searchText);
  }

  selectSuggestion(name: string): void {
    this.searchText = name;
    this.showSuggestions.set(false);
    this.applyFilters();
  }

  applyFilters(): void {
    const queryParams: Record<string, string | number | null> = {
      search: this.searchText || null,
      brandId: this.brandId || null,
      sort: this.sort || null,
    };
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  /**
   * Appends the next page onto the current list. No-op while a fetch is already in flight or
   * the last page has already been loaded — the intersection observer can fire more than once
   * in quick succession while the sentinel stays in view during a fast scroll.
   */
  loadMore(): void {
    if (this.loadingMore() || !this.hasMore()) return;
    this.loadingMore.set(true);
    this.catalog.getProducts({ ...this.baseQuery, page: this.page() + 1 }).subscribe({
      next: (res) => {
        this.items.update((items) => [...items, ...res.items]);
        this.page.set(res.page);
        this.totalPages.set(res.totalPages);
        this.hasMore.set(res.page < res.totalPages);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
  }
}
