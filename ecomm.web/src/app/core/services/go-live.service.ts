import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface GoLiveSummary {
  isLive: boolean;
  goneLiveAt: string | null;
  orders: number;
  ordersWorth: number;
  orderLinesAndStatusHistory: number;
  invoices: number;
  payments: number;
  shipments: number;
  reviewsAndCreditNotes: number;
  stockMovementHistory: number;
  cartsAndWishlists: number;
  notifications: number;
  signInSessionsAndOtps: number;
  reservedUnits: number;
  productsWithReservedUnits: number;
  nextInvoicePreview: string;
  customerAccounts: number;
  customerAddresses: number;
  trafficAndSearchHistory: number;
  importJobs: number;
  contactMessages: number;
}

export interface GoLiveResetRequest {
  includeCustomers: boolean;
  includeTraffic: boolean;
  includeImportHistory: boolean;
  includeContactMessages: boolean;
  confirmationText: string;
}

export interface GoLiveResetResult {
  ordersRemoved: number;
  invoicesRemoved: number;
  paymentsRemoved: number;
  shipmentsRemoved: number;
  reviewsAndCreditNotesRemoved: number;
  stockMovementRemoved: number;
  cartsAndWishlistsRemoved: number;
  notificationsRemoved: number;
  signInSessionsAndOtpsRemoved: number;
  reservedUnitsReleased: number;
  customersRemoved: number;
  trafficRemoved: number;
  importJobsRemoved: number;
  contactMessagesRemoved: number;
}

@Injectable({ providedIn: 'root' })
export class GoLiveService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/go-live`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  summary(): Observable<GoLiveSummary> { return this.unwrap(this.http.get<ApiResponse<GoLiveSummary>>(`${this.base}/summary`)); }
  reset(body: GoLiveResetRequest): Observable<GoLiveResetResult> { return this.unwrap(this.http.post<ApiResponse<GoLiveResetResult>>(`${this.base}/reset`, body)); }
  markLive(): Observable<void> { return this.unwrap(this.http.post<ApiResponse<void>>(`${this.base}/mark-live`, {})); }
}
