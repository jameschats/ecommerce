import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface CheckoutSettings {
  contactMethod: 'email' | 'phone';
  requirePhone: boolean;
  tippingEnabled: boolean;
  tipPresets: string | null;
  itemLimit: number;
  selfServeCancel: boolean;
  selfServeReturns: boolean;
}

@Injectable({ providedIn: 'root' })
export class CheckoutSettingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/checkout-settings`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  get(): Observable<CheckoutSettings> { return this.unwrap(this.http.get<ApiResponse<CheckoutSettings>>(this.base)); }
  update(body: CheckoutSettings): Observable<CheckoutSettings> { return this.unwrap(this.http.put<ApiResponse<CheckoutSettings>>(this.base, body)); }
}
