import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface AiPack { packId: number; name: string; credits: number; priceInr: number; }
export interface AiBalance { enabled: boolean; balance: number; cycleResetAt: string | null; packs: AiPack[]; }
export interface AiUsage { id: number; feature: string; credits: number; tokens: number | null; model: string | null; createdAt: string; }
export interface TopUpResult {
  granted: boolean; balance: number;
  gatewayOrderId: string | null; keyId: string | null; amountPaise: number | null; currency: string | null;
}
export interface TopUpVerify { packId: number; gatewayOrderId: string; gatewayPaymentId: string; signature: string; }

/** Merchant AI credits: balance, usage ledger, and buy-credits top-ups (paid to the platform). */
@Injectable({ providedIn: 'root' })
export class AiCreditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/ai`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  balance(): Observable<AiBalance> { return this.unwrap(this.http.get<ApiResponse<AiBalance>>(this.base)); }
  usage(): Observable<AiUsage[]> { return this.unwrap(this.http.get<ApiResponse<AiUsage[]>>(`${this.base}/usage`)); }
  topUp(packId: number): Observable<TopUpResult> { return this.unwrap(this.http.post<ApiResponse<TopUpResult>>(`${this.base}/topup`, { packId })); }
  verify(body: TopUpVerify): Observable<TopUpResult> { return this.unwrap(this.http.post<ApiResponse<TopUpResult>>(`${this.base}/topup/verify`, body)); }
}
