import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import {
  AnalyticsSummary, DeviceBreakdown, GeoBreakdown, GroupProfitRow, NewVsReturning, ProductReportRow,
  ReturnRateRow, SalesPeriodRow, SourceBreakdown, StateBreakdown, TopPage, TrafficPoint, TrafficSummary,
} from '../models/analytics.model';
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
  salesOverTime(from: string, to: string, bucket: 'month' | 'year' | 'day'): Observable<SalesPeriodRow[]> {
    return this.unwrap(this.http.get<ApiResponse<SalesPeriodRow[]>>(
      `${this.base}/sales-over-time${this.range(from, to)}&bucket=${bucket}`));
  }

  // ---------------- Traffic ----------------
  trafficSummary(from: string, to: string): Observable<TrafficSummary> {
    return this.unwrap(this.http.get<ApiResponse<TrafficSummary>>(`${this.base}/traffic-summary${this.range(from, to)}`));
  }
  trafficOverTime(from: string, to: string): Observable<TrafficPoint[]> {
    return this.unwrap(this.http.get<ApiResponse<TrafficPoint[]>>(`${this.base}/traffic-over-time${this.range(from, to)}`));
  }
  trafficByDevice(from: string, to: string): Observable<DeviceBreakdown[]> {
    return this.unwrap(this.http.get<ApiResponse<DeviceBreakdown[]>>(`${this.base}/traffic-by-device${this.range(from, to)}`));
  }
  trafficBySource(from: string, to: string): Observable<SourceBreakdown[]> {
    return this.unwrap(this.http.get<ApiResponse<SourceBreakdown[]>>(`${this.base}/traffic-by-source${this.range(from, to)}`));
  }
  topPages(from: string, to: string): Observable<TopPage[]> {
    return this.unwrap(this.http.get<ApiResponse<TopPage[]>>(`${this.base}/top-pages${this.range(from, to)}`));
  }
  trafficByGeo(from: string, to: string): Observable<GeoBreakdown[]> {
    return this.unwrap(this.http.get<ApiResponse<GeoBreakdown[]>>(`${this.base}/traffic-by-geo${this.range(from, to)}`));
  }
  trafficByState(from: string, to: string): Observable<StateBreakdown[]> {
    return this.unwrap(this.http.get<ApiResponse<StateBreakdown[]>>(`${this.base}/traffic-by-state${this.range(from, to)}`));
  }
  newVsReturning(from: string, to: string): Observable<NewVsReturning> {
    return this.unwrap(this.http.get<ApiResponse<NewVsReturning>>(`${this.base}/new-vs-returning${this.range(from, to)}`));
  }
}
