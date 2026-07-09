import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface PaymentSettings {
  provider: string;               // Mock | Razorpay
  razorpayKeyId: string | null;
  hasSecret: boolean;
  isEnabled: boolean;
  codEnabled: boolean;
}
export interface UpdatePaymentSettings {
  provider: string;
  razorpayKeyId: string | null;
  razorpayKeySecret: string | null;   // blank = keep existing
  isEnabled: boolean;
  codEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class PaymentAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/payments`;

  get(): Observable<PaymentSettings> {
    return this.http.get<ApiResponse<PaymentSettings>>(`${this.base}/settings`).pipe(map((r) => r.data as PaymentSettings));
  }
  update(body: UpdatePaymentSettings): Observable<PaymentSettings> {
    return this.http.put<ApiResponse<PaymentSettings>>(`${this.base}/settings`, body).pipe(map((r) => r.data as PaymentSettings));
  }
}
