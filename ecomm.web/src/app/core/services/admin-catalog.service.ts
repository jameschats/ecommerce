import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import {
  AttributeDef,
  ImportJobResult,
  InventoryImportResult,
  InventoryRow,
  InventoryTransaction,
  ProductAttributeInput,
  ProductAttributeValue,
  SaveAttributeRequest,
  SaveBrandRequest,
  SaveCategoryRequest,
  SaveProductRequest,
  SaveVariantRequest,
  StoreSettings,
  VariantInventory,
} from '../models/admin-catalog.model';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { Brand, Category, ProductDetail, ProductListItem, ProductQuery, ProductVariant } from '../models/catalog.model';

@Injectable({ providedIn: 'root' })
export class AdminCatalogService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin`;

  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> {
    return o.pipe(map((r) => r.data as T));
  }

  // --- Categories ---
  listCategories(): Observable<Category[]> {
    return this.unwrap(this.http.get<ApiResponse<Category[]>>(`${this.base}/categories`));
  }
  createCategory(body: SaveCategoryRequest): Observable<Category> {
    return this.unwrap(this.http.post<ApiResponse<Category>>(`${this.base}/categories`, body));
  }
  updateCategory(id: number, body: SaveCategoryRequest): Observable<Category> {
    return this.unwrap(this.http.put<ApiResponse<Category>>(`${this.base}/categories/${id}`, body));
  }
  deleteCategory(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/categories/${id}`);
  }

  // --- Brands ---
  listBrands(): Observable<Brand[]> {
    return this.unwrap(this.http.get<ApiResponse<Brand[]>>(`${this.base}/brands`));
  }
  createBrand(body: SaveBrandRequest): Observable<Brand> {
    return this.unwrap(this.http.post<ApiResponse<Brand>>(`${this.base}/brands`, body));
  }
  updateBrand(id: number, body: SaveBrandRequest): Observable<Brand> {
    return this.unwrap(this.http.put<ApiResponse<Brand>>(`${this.base}/brands/${id}`, body));
  }
  deleteBrand(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/brands/${id}`);
  }

  // --- Products ---
  listProducts(query: ProductQuery): Observable<PagedResult<ProductListItem>> {
    let params = new HttpParams().set('page', query.page ?? 1).set('pageSize', query.pageSize ?? 20);
    if (query.search) params = params.set('search', query.search);
    if (query.categoryId != null) params = params.set('categoryId', query.categoryId);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<ProductListItem>>>(`${this.base}/products`, { params }));
  }
  getProduct(id: number): Observable<ProductDetail> {
    return this.unwrap(this.http.get<ApiResponse<ProductDetail>>(`${this.base}/products/${id}`));
  }
  createProduct(body: SaveProductRequest): Observable<ProductDetail> {
    return this.unwrap(this.http.post<ApiResponse<ProductDetail>>(`${this.base}/products`, body));
  }
  updateProduct(id: number, body: SaveProductRequest): Observable<ProductDetail> {
    return this.unwrap(this.http.put<ApiResponse<ProductDetail>>(`${this.base}/products/${id}`, body));
  }
  deleteProduct(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/products/${id}`);
  }

  // --- Variants ---
  listVariants(productId: number): Observable<ProductVariant[]> {
    return this.unwrap(this.http.get<ApiResponse<ProductVariant[]>>(`${this.base}/products/${productId}/variants`));
  }
  createVariant(productId: number, body: SaveVariantRequest): Observable<ProductVariant> {
    return this.unwrap(this.http.post<ApiResponse<ProductVariant>>(`${this.base}/products/${productId}/variants`, body));
  }
  updateVariant(productId: number, variantId: number, body: SaveVariantRequest): Observable<ProductVariant> {
    return this.unwrap(this.http.put<ApiResponse<ProductVariant>>(`${this.base}/products/${productId}/variants/${variantId}`, body));
  }
  deleteVariant(productId: number, variantId: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/products/${productId}/variants/${variantId}`);
  }
  /** Wix-style bulk generator: one option group per name, its values as a list — every
   *  combination becomes a variant. Additive server-side; safe to call again after adding
   *  a value to an existing option. */
  generateVariants(productId: number, options: { name: string; values: string[] }[]): Observable<ProductVariant[]> {
    return this.unwrap(this.http.post<ApiResponse<ProductVariant[]>>(`${this.base}/products/${productId}/variants/generate`, { options }));
  }

  // --- Attributes ---
  listAttributes(): Observable<AttributeDef[]> {
    return this.unwrap(this.http.get<ApiResponse<AttributeDef[]>>(`${this.base}/attributes`));
  }
  createAttribute(body: SaveAttributeRequest): Observable<AttributeDef> {
    return this.unwrap(this.http.post<ApiResponse<AttributeDef>>(`${this.base}/attributes`, body));
  }
  deleteAttribute(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/attributes/${id}`);
  }
  addAttributeValue(id: number, value: string): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/attributes/${id}/values`, { value });
  }
  deleteAttributeValue(id: number, valueId: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/attributes/${id}/values/${valueId}`);
  }

  // --- Product attributes ---
  getProductAttributes(productId: number): Observable<ProductAttributeValue[]> {
    return this.unwrap(this.http.get<ApiResponse<ProductAttributeValue[]>>(`${this.base}/products/${productId}/attributes`));
  }
  setProductAttributes(productId: number, attributes: ProductAttributeInput[]): Observable<ProductAttributeValue[]> {
    return this.unwrap(
      this.http.put<ApiResponse<ProductAttributeValue[]>>(`${this.base}/products/${productId}/attributes`, { attributes }),
    );
  }

  // --- Import / Export ---
  importProducts(file: File): Observable<ImportJobResult> {
    const form = new FormData();
    form.append('file', file);
    return this.unwrap(this.http.post<ApiResponse<ImportJobResult>>(`${this.base}/products/import`, form));
  }
  exportProducts(): Observable<Blob> {
    return this.http.get(`${this.base}/products/export`, { responseType: 'blob' });
  }
  /** One action across many products — delete (soft), status, category, or a price revision. */
  bulkProducts(body: Record<string, unknown>): Observable<{ affected: number; summary: string }> {
    return this.unwrap(
      this.http.post<ApiResponse<{ affected: number; summary: string }>>(`${this.base}/products/bulk`, body),
    );
  }
  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${this.base}/products/import-template`, { responseType: 'blob' });
  }

  // --- Inventory ---
  listInventory(search: string, lowStockOnly: boolean, page: number): Observable<PagedResult<InventoryRow>> {
    let params = new HttpParams().set('page', page).set('pageSize', 20).set('lowStockOnly', lowStockOnly);
    if (search) params = params.set('search', search);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<InventoryRow>>>(`${this.base}/inventory`, { params }));
  }
  setStock(productId: number, availableQty: number, reorderLevel: number): Observable<InventoryRow> {
    return this.unwrap(this.http.put<ApiResponse<InventoryRow>>(`${this.base}/inventory/${productId}`, { availableQty, reorderLevel }));
  }
  exportInventory(): Observable<Blob> {
    return this.http.get(`${this.base}/inventory/export`, { responseType: 'blob' });
  }
  inventoryTemplate(): Observable<Blob> {
    return this.http.get(`${this.base}/inventory/import-template`, { responseType: 'blob' });
  }
  importInventory(file: File): Observable<InventoryImportResult> {
    const form = new FormData();
    form.append('file', file);
    return this.unwrap(this.http.post<ApiResponse<InventoryImportResult>>(`${this.base}/inventory/import`, form));
  }
  inventoryTransactions(productId: number): Observable<InventoryTransaction[]> {
    return this.unwrap(this.http.get<ApiResponse<InventoryTransaction[]>>(`${this.base}/inventory/${productId}/transactions`));
  }
  variantInventory(productId: number): Observable<VariantInventory[]> {
    return this.unwrap(this.http.get<ApiResponse<VariantInventory[]>>(`${this.base}/inventory/${productId}/variants`));
  }
  setVariantStock(productId: number, variantId: number, availableQty: number, reorderLevel: number): Observable<VariantInventory> {
    return this.unwrap(this.http.put<ApiResponse<VariantInventory>>(`${this.base}/inventory/${productId}/variant/${variantId}`, { availableQty, reorderLevel }));
  }

  // ----- Store settings (tax mode + store identity) -----
  getStoreSettings(): Observable<StoreSettings> {
    return this.unwrap(this.http.get<ApiResponse<StoreSettings>>(`${this.base}/store/settings`));
  }
  updateStoreSettings(body: StoreSettings): Observable<StoreSettings> {
    return this.unwrap(this.http.put<ApiResponse<StoreSettings>>(`${this.base}/store/settings`, body));
  }
}
