import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface CatalogPreset { key: string; label: string; }
export interface CatalogStatus { enabled: boolean; sampleProducts: number; presets: CatalogPreset[]; }
export interface GenProduct { name: string; shortDescription: string; description: string; price: number; tags: string | null; imageUrl: string; attributes: Record<string, string> | null; }
export interface GenCategory { name: string; description: string; subcategories: GenCategory[] | null; products: GenProduct[]; }
export interface GeneratedCatalog { storeType: string; categories: GenCategory[]; }
export interface GenerateRequest { presetKey?: string | null; prompt?: string | null; categories: number; productsPerCategory: number; }
export interface SeedResult { categories: number; products: number; }

/** AI sample-catalog generator (AI-2): generate a preview, add it to the store, download it, or clear it. */
@Injectable({ providedIn: 'root' })
export class AiCatalogService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/ai/catalog`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  status(): Observable<CatalogStatus> { return this.unwrap(this.http.get<ApiResponse<CatalogStatus>>(this.base)); }
  generate(req: GenerateRequest): Observable<GeneratedCatalog> { return this.unwrap(this.http.post<ApiResponse<GeneratedCatalog>>(`${this.base}/generate`, req)); }
  seed(catalog: GeneratedCatalog): Observable<SeedResult> { return this.unwrap(this.http.post<ApiResponse<SeedResult>>(`${this.base}/seed`, catalog)); }
  clear(): Observable<{ removed: number }> { return this.unwrap(this.http.post<ApiResponse<{ removed: number }>>(`${this.base}/clear`, {})); }
  export(catalog: GeneratedCatalog): Observable<Blob> { return this.http.post(`${this.base}/export`, catalog, { responseType: 'blob' }); }
}
