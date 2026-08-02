import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { Brand, BundleComponent, Category, ProductDetail, ProductListItem, ProductQuery } from '../models/catalog.model';

export interface PublicCollectionProduct { productId: number; name: string; slug: string; price: number; primaryImageUrl: string | null; }
export interface PublicCollection {
  collectionId: number; name: string; slug: string; description: string | null; imageUrl: string | null;
  metaTitle: string | null; metaDescription: string | null; products: PublicCollectionProduct[];
}
export interface PublicCollectionSummary { collectionId: number; name: string; slug: string; imageUrl: string | null; productCount: number; }
export interface MenuItem { label: string; url: string; children?: MenuItem[] | null; megaMenu?: MegaMenu | null; }
export interface MegaMenuColumn { heading: string; links: MenuItem[]; }
export interface MegaMenuPromo { imageUrl: string; heading: string; link?: string | null; }
export interface MegaMenu { columns: MegaMenuColumn[]; promo?: MegaMenuPromo | null; }
export interface Menu { handle: string; title: string; items: MenuItem[]; }
export interface StorePolicy { handle: string; title: string; bodyHtml: string | null; hasContent: boolean; }
export interface PolicyLink { handle: string; title: string; }
export interface StoreSeo { title: string | null; description: string | null; image: string | null; }
export interface StoreGate { passwordProtected: boolean; message: string | null; }
export interface ShippingQuote {
  serviceable: boolean; methodId: number | null; methodName: string;
  charge: number; estimatedDays: number | null; message: string | null;
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

  /** All active collections, lightweight — for the "all collections" index page/section. */
  getCollections(): Observable<PublicCollectionSummary[]> {
    return this.http.get<ApiResponse<PublicCollectionSummary[]>>(`${this.base}/collections`).pipe(
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

  /** Rich-shape (swatches/stock) product list for a collection — theme sections sourcing FeaturedProducts by collection. */
  getCollectionMembers(collectionId: number, limit: number): Observable<ProductListItem[]> {
    return this.http.get<ApiResponse<ProductListItem[]>>(`${this.base}/collections/${collectionId}/members`, { params: { limit } }).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  getPolicy(handle: string): Observable<StorePolicy | null> {
    return this.http.get<ApiResponse<StorePolicy>>(`${this.base}/policies/${encodeURIComponent(handle)}`).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  }
  /** A storefront navigation menu by handle (e.g. "main-menu") — merchant-curated top-level nav items. */
  getMenu(handle: string): Observable<Menu | null> {
    return this.http.get<ApiResponse<Menu>>(`${this.base}/navigation/menus/${handle}`).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  }

  getPolicyLinks(): Observable<PolicyLink[]> {
    return this.http.get<ApiResponse<PolicyLink[]>>(`${this.base}/policies`).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  getStoreSeo(): Observable<StoreSeo> {
    return this.http.get<ApiResponse<StoreSeo>>(`${this.base}/storefront/seo`).pipe(
      map((r) => r.data ?? { title: null, description: null, image: null }),
      catchError(() => of({ title: null, description: null, image: null } as StoreSeo)),
    );
  }
  getStoreGate(): Observable<StoreGate> {
    return this.http.get<ApiResponse<StoreGate>>(`${this.base}/storefront/gate`).pipe(
      map((r) => r.data ?? { passwordProtected: false, message: null }),
      catchError(() => of({ passwordProtected: false, message: null } as StoreGate)),
    );
  }
  checkStoreGate(password: string): Observable<boolean> {
    return this.http.post<ApiResponse<{ ok: boolean }>>(`${this.base}/storefront/gate`, { password }).pipe(
      map((r) => r.data?.ok ?? false),
      catchError(() => of(false)),
    );
  }

  getProducts(query: ProductQuery): Observable<PagedResult<ProductListItem>> {
    let params = new HttpParams();
    if (query.search) params = params.set('search', query.search);
    if (query.categoryId != null) params = params.set('categoryId', query.categoryId);
    if (query.brandId != null) params = params.set('brandId', query.brandId);
    if (query.isFeatured != null) params = params.set('isFeatured', query.isFeatured);
    if (query.sort) params = params.set('sort', query.sort);
    if (query.ids?.length) for (const id of query.ids) params = params.append('ids', id);
    if (query.minPrice != null) params = params.set('minPrice', query.minPrice);
    if (query.maxPrice != null) params = params.set('maxPrice', query.maxPrice);
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

  /** PDP delivery/pincode checker — no login/address needed. */
  checkShipping(pincode: string): Observable<ShippingQuote | null> {
    return this.http.get<ApiResponse<ShippingQuote>>(`${this.base}/shipping/check`, { params: { pincode } }).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  }

  /** The real products a bundle/kit is made of — for the PDP "Includes:" list. */
  getBundleItems(productId: number): Observable<BundleComponent[]> {
    return this.http.get<ApiResponse<BundleComponent[]>>(`${this.base}/products/${productId}/bundle-items`).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  /** Products most often bought alongside this one, ranked by real co-purchase frequency. */
  getFrequentlyBoughtTogether(productId: number, take = 3): Observable<ProductListItem[]> {
    return this.http.get<ApiResponse<ProductListItem[]>>(`${this.base}/products/${productId}/frequently-bought-together`, { params: { take } }).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  suggest(q: string): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.base}/suggest`, { params: { q } }).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }
}
