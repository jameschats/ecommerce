import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { combineLatest, switchMap } from 'rxjs';
import { SITE_URL } from '../../../core/api.config';
import { PagedResult } from '../../../core/models/api-response.model';
import { Brand, Category, ProductListItem, ProductQuery } from '../../../core/models/catalog.model';
import { CatalogService } from '../../../core/services/catalog.service';
import { SeoService } from '../../../core/services/seo.service';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';

@Component({
  selector: 'app-product-list',
  imports: [FormsModule, RouterLink, ProductCardComponent],
  templateUrl: './product-list.component.html',
})
export class ProductListComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);

  readonly result = signal<PagedResult<ProductListItem> | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly brands = signal<Brand[]>([]);
  readonly activeCategory = signal<Category | null>(null);
  readonly loading = signal(true);
  /** Placeholder cards shown while products load (keeps layout height stable). */
  readonly skeletons = Array.from({ length: 10 }, (_, i) => i);

  searchText = '';
  sort = '';
  brandId: number | '' = '';

  ngOnInit(): void {
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

              const q: ProductQuery = {
                search: this.searchText || undefined,
                categoryId: cat?.categoryId,
                brandId: this.brandId || undefined,
                sort: this.sort || undefined,
                page: query.get('page') ? +query.get('page')! : 1,
                pageSize: 12,
              };
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

  private applySeo(): void {
    const cat = this.activeCategory();
    const title = cat ? `${cat.name} — CalendarShop` : 'Shop all products — CalendarShop';
    const description = cat?.description ?? `Browse ${cat?.name ?? 'our catalog'} at CalendarShop. Great prices, fast delivery.`;
    this.seo.setMeta({ title, description, url: SITE_URL + this.router.url });
  }

  applyFilters(extra: Record<string, string | number | null> = {}): void {
    const queryParams: Record<string, string | number | null> = {
      search: this.searchText || null,
      brandId: this.brandId || null,
      sort: this.sort || null,
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
