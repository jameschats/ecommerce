import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface ProductPricingControls {
  productId: number; name: string; price: number; minPrice: number | null; maxPrice: number | null; priceLocked: boolean;
}
export interface PricingSeasonRule {
  id: number; name: string; startDate: string; endDate: string; biasPercent: number; categoryId: number | null;
}
export interface PriceSuggestion {
  id: number; productId: number; productName: string; oldPrice: number; suggestedPrice: number;
  inventorySignalPercent: number; demandSignalPercent: number; seasonalitySignalPercent: number;
  reason: string | null; status: string; suggestedAt: string; approvedAt: string | null; appliedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class PricingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/pricing`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  listControls(categoryId?: number | null): Observable<ProductPricingControls[]> {
    const q = categoryId ? `?categoryId=${categoryId}` : '';
    return this.unwrap(this.http.get<ApiResponse<ProductPricingControls[]>>(`${this.base}/controls${q}`));
  }
  setControls(productId: number, minPrice: number | null, maxPrice: number | null, priceLocked: boolean): Observable<ProductPricingControls> {
    return this.unwrap(this.http.put<ApiResponse<ProductPricingControls>>(`${this.base}/controls/${productId}`, { minPrice, maxPrice, priceLocked }));
  }
  bulkBounds(categoryId: number | null, floorPercent: number, ceilingPercent: number): Observable<{ updated: number }> {
    return this.unwrap(this.http.post<ApiResponse<{ updated: number }>>(`${this.base}/controls/bulk-bounds`, { categoryId, floorPercent, ceilingPercent }));
  }

  listSeasonRules(): Observable<PricingSeasonRule[]> {
    return this.unwrap(this.http.get<ApiResponse<PricingSeasonRule[]>>(`${this.base}/season-rules`));
  }
  saveSeasonRule(req: { name: string; startDate: string; endDate: string; biasPercent: number; categoryId: number | null }): Observable<PricingSeasonRule> {
    return this.unwrap(this.http.post<ApiResponse<PricingSeasonRule>>(`${this.base}/season-rules`, req));
  }
  removeSeasonRule(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/season-rules/${id}`);
  }

  generateNow(): Observable<{ generated: number }> {
    return this.unwrap(this.http.post<ApiResponse<{ generated: number }>>(`${this.base}/generate`, {}));
  }
  listSuggestions(status?: string | null, page = 1, pageSize = 20): Observable<PagedResult<PriceSuggestion>> {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) q.set('status', status);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<PriceSuggestion>>>(`${this.base}/suggestions?${q}`));
  }
  approve(id: number): Observable<PriceSuggestion> {
    return this.unwrap(this.http.post<ApiResponse<PriceSuggestion>>(`${this.base}/suggestions/${id}/approve`, {}));
  }
  reject(id: number): Observable<PriceSuggestion> {
    return this.unwrap(this.http.post<ApiResponse<PriceSuggestion>>(`${this.base}/suggestions/${id}/reject`, {}));
  }
}
