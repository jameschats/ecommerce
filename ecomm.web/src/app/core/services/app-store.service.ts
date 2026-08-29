import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface AppListing {
  id: number; name: string; slug: string; description: string | null; iconUrl: string | null;
  category: string | null; requestedScopes: string[]; isEmbedded: boolean; pricingModel: string; installed: boolean;
}
export interface InstalledApp {
  installationId: number; appId: number; name: string; slug: string; iconUrl: string | null;
  grantedScopes: string[]; isEmbedded: boolean; embedUrl: string | null; installedAt: string;
}
export interface AppConfig { installed: boolean; installationId: number | null; settings: Record<string, string | null>; }

@Injectable({ providedIn: 'root' })
export class AppStoreService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/apps`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  store(): Observable<AppListing[]> { return this.unwrap(this.http.get<ApiResponse<AppListing[]>>(this.base)); }
  installed(): Observable<InstalledApp[]> { return this.unwrap(this.http.get<ApiResponse<InstalledApp[]>>(`${this.base}/installed`)); }
  install(slug: string): Observable<InstalledApp> { return this.unwrap(this.http.post<ApiResponse<InstalledApp>>(`${this.base}/${slug}/install`, {})); }
  uninstall(installationId: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/installed/${installationId}`); }
  config(slug: string): Observable<AppConfig> { return this.unwrap(this.http.get<ApiResponse<AppConfig>>(`${this.base}/${slug}/config`)); }
  saveConfig(slug: string, settings: Record<string, string | null>): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/${slug}/config`, settings);
  }
  /** Low Stock Alerts: run the check now (returns how many products are low + sends the email). */
  runLowStock(): Observable<{ lowStockCount: number }> {
    return this.unwrap(this.http.post<ApiResponse<{ lowStockCount: number }>>(`${this.base}/low-stock-alerts/run`, {}));
  }
  /** Sales Digest: send the digest now. */
  runSalesDigest(): Observable<{ orderCount: number }> {
    return this.unwrap(this.http.post<ApiResponse<{ orderCount: number }>>(`${this.base}/sales-digest/run`, {}));
  }
}
