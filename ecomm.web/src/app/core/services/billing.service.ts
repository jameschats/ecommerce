import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface Subscription {
  status: string;
  planId: number;
  planName: string;
  planSlug: string;
  monthlyPrice: number;
  currentPeriodEnd: string | null;
  graceEndsAt: string | null;
  isActive: boolean;
  isInTrial: boolean;
  mandateStatus: string;
  nextChargeAt: string | null;
  paymentMethodSummary: string | null;
  cancelAtPeriodEnd: boolean;
}

export interface PlatformInvoice {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  totalAmount: number;
  billingHistoryId: number;
}

export interface AutoPaySetup {
  active: boolean;
  authUrl: string | null;
  mandateStatus: string;
  nextChargeAt: string | null;
  paymentMethodSummary: string | null;
}

export interface Plan {
  planId: number;
  name: string;
  slug: string;
  monthlyPrice: number;
  maxProducts: number | null;
  maxOrders: number | null;
  aiCredits: number;
  introPriceInr: number | null;
  introMonths: number | null;
}

export interface BillingHistory {
  id: number;
  amount: number;
  status: string;
  billedAt: string;
  periodStart: string | null;
  periodEnd: string | null;
  reference: string | null;
}

export interface CheckoutSession {
  gatewayOrderId: string;
  amount: number;
  currency: string;
  keyId: string | null;   // null => Mock gateway (dev): confirm without the widget
  provider: string;
  planId: number;
  planName: string;
}

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  current(): Observable<Subscription | null> {
    return this.http.get<ApiResponse<Subscription | null>>(`${this.base}/subscription`).pipe(map((r) => r.data ?? null));
  }
  plans(): Observable<Plan[]> { return this.unwrap(this.http.get<ApiResponse<Plan[]>>(`${this.base}/plans`)); }
  history(): Observable<BillingHistory[]> { return this.unwrap(this.http.get<ApiResponse<BillingHistory[]>>(`${this.base}/subscription/billing-history`)); }
  selectPlan(planId: number): Observable<Subscription> {
    return this.unwrap(this.http.post<ApiResponse<Subscription>>(`${this.base}/subscription/select-plan`, { planId }));
  }
  cancel(): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/subscription/cancel`, {}); }
  startCheckout(planId: number): Observable<CheckoutSession> {
    return this.unwrap(this.http.post<ApiResponse<CheckoutSession>>(`${this.base}/subscription/checkout/start`, { planId }));
  }
  confirmCheckout(body: { planId: number; gatewayOrderId: string; paymentId: string; signature: string }): Observable<Subscription> {
    return this.unwrap(this.http.post<ApiResponse<Subscription>>(`${this.base}/subscription/checkout/confirm`, body));
  }
  // P2 — recurring auto-pay
  setupAutoPay(planId: number): Observable<AutoPaySetup> {
    return this.unwrap(this.http.post<ApiResponse<AutoPaySetup>>(`${this.base}/subscription/autopay/setup`, { planId }));
  }
  cancelAutoPay(): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/subscription/autopay/cancel`, {}); }
  // GST tax invoices for the SaaS fee
  invoices(): Observable<PlatformInvoice[]> { return this.unwrap(this.http.get<ApiResponse<PlatformInvoice[]>>(`${this.base}/subscription/invoices`)); }
  invoicePdf(id: number): Observable<Blob> { return this.http.get(`${this.base}/subscription/invoices/${id}/pdf`, { responseType: 'blob' }); }
}
