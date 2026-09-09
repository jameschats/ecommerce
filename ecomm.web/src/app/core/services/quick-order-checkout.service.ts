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

export interface CustomFieldAnswer {
  productCustomFieldId: number;
  label: string;
  value: string;
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
  variantId: number | null;
  /** e.g. "500 / Red" — null for a plain product with no variant selected. */
  variantLabel: string | null;
  customFields: CustomFieldAnswer[] | null;
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
  /** Amount knocked off by a coupon code, distinct from discountTotal (the price list's own MRP markdown). */
  couponDiscount: number;
  couponCode: string | null;
  /** Why the coupon didn't apply, when couponApplied is false. Null otherwise. */
  couponMessage: string | null;
  couponApplied: boolean;
  roundOff: number;
  overallAmount: number;
  meetsMinimum: boolean;
  warnings: string[];
}

export interface PlacedOrder {
  orderId: number;
  orderNumber: string;
  overallAmount: number;
  status: string;
}

export interface QuickOrderRequestLine {
  productId: number;
  quantity: number;
  variantId?: number | null;
  customFields?: { productCustomFieldId: number; value: string }[];
}

export interface PlaceQuickOrderRequest {
  lines: QuickOrderRequestLine[];
  state: string;
  city: string;
  name: string;
  mobile: string;
  email: string;
  /** Billing address. Also the delivery address unless shipToDifferent is set. */
  address: string;
  /** Trading name, when ordering for a shop rather than as an individual. */
  businessName?: string | null;
  /** The buyer's GST number, printed on their bill. */
  gstin?: string | null;
  /** Transport/lorry the buyer wants the order dispatched by. Optional. */
  transportName?: string | null;
  /** When false, every ship* field below is ignored and the billing address is used. */
  shipToDifferent?: boolean;
  shipName?: string | null;
  shipMobile?: string | null;
  shipAddress?: string | null;
  shipCity?: string | null;
  shipState?: string | null;
  couponCode?: string | null;
}

const EMPTY_QUOTE: QuickOrderQuote = {
  lines: [], itemCount: 0, totalUnits: 0, netTotal: 0, discountTotal: 0, subTotal: 0,
  minOrderAmount: 0, packingChargePct: 0, packingCharges: 0,
  couponDiscount: 0, couponCode: null, couponMessage: null, couponApplied: false,
  roundOff: 0, overallAmount: 0, meetsMinimum: false, warnings: [],
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

  quote(
    lines: QuickOrderRequestLine[],
    state: string | null,
    couponCode?: string | null,
  ): Observable<QuickOrderQuote> {
    if (!lines.length) return of(EMPTY_QUOTE);
    return this.http
      .post<ApiResponse<QuickOrderQuote>>(`${this.base}/quote`, { lines, state, couponCode: couponCode || null })
      .pipe(
        map((r) => r.data ?? EMPTY_QUOTE),
        catchError(() => of(EMPTY_QUOTE)),
      );
  }

  /**
   * Places the order. Errors are deliberately NOT swallowed here — unlike quote(), a
   * failure to place must surface to the buyer rather than degrade to an empty result.
   */
  /**
   * The basket as a printable quotation. Rendered on the server, because the totals include
   * packing and rounding that only the server computes.
   */
  quotePdf(lines: QuickOrderRequestLine[], state?: string, customerName?: string): Observable<Blob> {
    return this.http.post(`${this.base}/quote/pdf`,
      { lines, state: state ?? null, customerName: customerName ?? null },
      { responseType: 'blob' });
  }

  place(req: PlaceQuickOrderRequest): Observable<PlacedOrder> {
    return this.http
      .post<ApiResponse<PlacedOrder>>(`${this.base}/place`, req)
      .pipe(map((r) => r.data!));
  }
}
