import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface StateMinOrder {
  stateName: string;
  minOrderAmount: number;
}

export interface QuickOrderConfig {
  defaultMinOrderAmount: number;
  packingChargePct: number;
  stateMinOrders: StateMinOrder[];
  states: string[];
  announcementText: string | null;
  priceValidUpto: string | null;
}

export interface QuickOrderQuoteLine {
  productId: number;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  compareAtPrice: number | null;
  lineTotal: number;
  inStock: boolean;
}

export interface QuickOrderQuote {
  lines: QuickOrderQuoteLine[];
  itemCount: number;
  totalUnits: number;
  netTotal: number;
  discountTotal: number;
  subTotal: number;
  minOrderAmount: number;
  packingChargePct: number;
  packingCharges: number;
  roundOff: number;
  overallAmount: number;
  meetsMinimum: boolean;
  warnings: string[];
}

const EMPTY_QUOTE: QuickOrderQuote = {
  lines: [], itemCount: 0, totalUnits: 0, netTotal: 0, discountTotal: 0, subTotal: 0,
  minOrderAmount: 0, packingChargePct: 0, packingCharges: 0, roundOff: 0, overallAmount: 0,
  meetsMinimum: false, warnings: [],
};

/**
 * Talks to the server-side pricing endpoint (design.md §7.1).
 *
 * The table computes totals locally so typing stays instant. This service fetches the
 * *authoritative* numbers — prices re-read from the database, minimum-order rule for the
 * chosen state, packing charge and round-off — and those are what the order form shows.
 */
@Injectable({ providedIn: 'root' })
export class QuickOrderCheckoutService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/quick-order`;

  private config$?: Observable<QuickOrderConfig>;

  getConfig(): Observable<QuickOrderConfig> {
    this.config$ ??= this.http.get<ApiResponse<QuickOrderConfig>>(`${this.base}/config`).pipe(
      map((r) => r.data!),
      catchError(() =>
        of({
          defaultMinOrderAmount: 0, packingChargePct: 0, stateMinOrders: [],
          states: [], announcementText: null, priceValidUpto: null,
        } as QuickOrderConfig),
      ),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.config$;
  }

  quote(lines: { productId: number; quantity: number }[], state: string | null): Observable<QuickOrderQuote> {
    if (!lines.length) return of(EMPTY_QUOTE);
    return this.http
      .post<ApiResponse<QuickOrderQuote>>(`${this.base}/quote`, { lines, state })
      .pipe(
        map((r) => r.data ?? EMPTY_QUOTE),
        catchError(() => of(EMPTY_QUOTE)),
      );
  }
}
