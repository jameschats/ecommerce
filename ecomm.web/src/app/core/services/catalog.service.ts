import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { Brand, Category, ProductDetail, ProductListItem, ProductQuery } from '../models/catalog.model';

export interface PublicCollectionProduct { productId: number; name: string; slug: string; price: number; primaryImageUrl: string | null; }
export interface PublicCollection {
  collectionId: number; name: string; slug: string; description: string | null; imageUrl: string | null;
  metaTitle: string | null; metaDescription: string | null; products: PublicCollectionProduct[];
}

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/catalog`;

  private categories$?: Observable<Category[]>;

  getCategories(): Observable<Category[]> {
    // Cached for the session: the header, home and listing pages all need it,
    // so fetch once and replay — avoids a re-fetch (and layout pop-in) per navigation.
    this.categories$ ??= this.http.get<ApiResponse<Category[]>>(`${this.base}/categories`).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.categories$;
  }

  getBrands(): Observable<Brand[]> {
    return this.http.get<ApiResponse<Brand[]>>(`${this.base}/brands`).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  getCollection(slug: string): Observable<PublicCollection | null> {
    return this.http.get<ApiResponse<PublicCollection>>(`${this.base}/collections/${encodeURIComponent(slug)}`).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  }

  getProducts(query: ProductQuery): Observable<PagedResult<ProductListItem>> {
    let params = new HttpParams();
    if (query.search) params = params.set('search', query.search);
    if (query.categoryId != null) params = params.set('categoryId', query.categoryId);
    if (query.brandId != null) params = params.set('brandId', query.brandId);
    if (query.isFeatured != null) params = params.set('isFeatured', query.isFeatured);
    if (query.sort) params = params.set('sort', query.sort);
    const pageSize = query.pageSize ?? 12;
    params = params.set('page', query.page ?? 1).set('pageSize', pageSize);
    return this.http
      .get<ApiResponse<PagedResult<ProductListItem>>>(`${this.base}/products`, { params })
      .pipe(
        map((r) => r.data!),
        catchError(() => of({ items: [], page: query.page ?? 1, pageSize, totalCount: 0, totalPages: 0 })),
      );
  }

  getProductBySlug(slug: string): Observable<ProductDetail | null> {
    return this.http.get<ApiResponse<ProductDetail>>(`${this.base}/products/by-slug/${slug}`).pipe(
      map((r) => r.data),
      catchError(() => of(null)),
    );
  }

  suggest(q: string): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.base}/suggest`, { params: { q } }).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }
}
