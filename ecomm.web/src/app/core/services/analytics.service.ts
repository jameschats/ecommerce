import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { AbandonedCartRow, AnalyticsSummary, Funnel, GroupProfitRow, ProductReportRow, ReturnRateRow, SalesDashboard } from '../models/analytics.model';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/analytics`;

  private range(from: string, to: string): string {
    return `?from=${from}&to=${to}`;
  }
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> {
    return o.pipe(map((r) => r.data as T));
  }

  summary(): Observable<AnalyticsSummary> {
    return this.unwrap(this.http.get<ApiResponse<AnalyticsSummary>>(`${this.base}/summary`));
  }
  salesDashboard(from: string, to: string): Observable<SalesDashboard> {
    return this.unwrap(this.http.get<ApiResponse<SalesDashboard>>(`${this.base}/sales${this.range(from, to)}`));
  }
  funnel(from: string, to: string): Observable<Funnel> {
    return this.unwrap(this.http.get<ApiResponse<Funnel>>(`${this.base}/funnel${this.range(from, to)}`));
  }
  abandonedCarts(from: string, to: string): Observable<AbandonedCartRow[]> {
    return this.unwrap(this.http.get<ApiResponse<AbandonedCartRow[]>>(`${this.base}/abandoned-carts${this.range(from, to)}`));
  }
  bestSellers(from: string, to: string): Observable<ProductReportRow[]> {
    return this.unwrap(this.http.get<ApiResponse<ProductReportRow[]>>(`${this.base}/best-sellers${this.range(from, to)}`));
  }
  margins(from: string, to: string, order: 'high' | 'low'): Observable<ProductReportRow[]> {
    return this.unwrap(this.http.get<ApiResponse<ProductReportRow[]>>(`${this.base}/margins${this.range(from, to)}&order=${order}`));
  }
  returnRate(from: string, to: string): Observable<ReturnRateRow[]> {
    return this.unwrap(this.http.get<ApiResponse<ReturnRateRow[]>>(`${this.base}/return-rate${this.range(from, to)}`));
  }
  profitByCategory(from: string, to: string): Observable<GroupProfitRow[]> {
    return this.unwrap(this.http.get<ApiResponse<GroupProfitRow[]>>(`${this.base}/profit-by-category${this.range(from, to)}`));
  }
  profitBySupplier(from: string, to: string): Observable<GroupProfitRow[]> {
    return this.unwrap(this.http.get<ApiResponse<GroupProfitRow[]>>(`${this.base}/profit-by-supplier${this.range(from, to)}`));
  }
}
