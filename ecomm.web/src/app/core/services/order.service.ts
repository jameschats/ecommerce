import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { CheckoutQuote, Order, OrderListItem, PlaceOrderResult } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/orders`;
  private readonly adminBase = `${API_BASE_URL}/admin/orders`;

  // ----- customer -----
  quote(addressId?: number | null): Observable<CheckoutQuote> {
    const q = addressId ? `?addressId=${addressId}` : '';
    return this.http.get<ApiResponse<CheckoutQuote>>(`${this.base}/quote${q}`).pipe(map((r) => r.data!));
  }

  place(shippingAddressId: number, billingAddressId?: number | null, notes?: string | null): Observable<PlaceOrderResult> {
    return this.http.post<ApiResponse<PlaceOrderResult>>(this.base, { shippingAddressId, billingAddressId, notes }).pipe(map((r) => r.data!));
  }

  confirm(orderId: number, gatewayPaymentId: string, signature: string): Observable<Order> {
    return this.http.post<ApiResponse<Order>>(`${this.base}/${orderId}/confirm`, { gatewayPaymentId, signature }).pipe(map((r) => r.data!));
  }

  cancel(orderId: number, reason?: string): Observable<Order> {
    return this.http.post<ApiResponse<Order>>(`${this.base}/${orderId}/cancel`, { reason }).pipe(map((r) => r.data!));
  }

  listMine(): Observable<OrderListItem[]> {
    return this.http.get<ApiResponse<OrderListItem[]>>(this.base).pipe(map((r) => r.data!));
  }

  get(orderId: number): Observable<Order> {
    return this.http.get<ApiResponse<Order>>(`${this.base}/${orderId}`).pipe(map((r) => r.data!));
  }

  downloadInvoice(orderId: number, admin = false): void {
    const url = `${admin ? this.adminBase : this.base}/${orderId}/invoice`;
    this.http.get(url, { responseType: 'blob' }).subscribe((blob) => {
      const href = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = href;
      a.download = `invoice-${orderId}.pdf`;
      a.click();
      URL.revokeObjectURL(href);
    });
  }

  // ----- admin -----
  adminList(status?: string, page = 1, pageSize = 20): Observable<PagedResult<OrderListItem>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) params.set('status', status);
    return this.http.get<ApiResponse<PagedResult<OrderListItem>>>(`${this.adminBase}?${params}`).pipe(map((r) => r.data!));
  }

  adminGet(orderId: number): Observable<Order> {
    return this.http.get<ApiResponse<Order>>(`${this.adminBase}/${orderId}`).pipe(map((r) => r.data!));
  }

  adminUpdateStatus(orderId: number, status: string): Observable<Order> {
    return this.http.post<ApiResponse<Order>>(`${this.adminBase}/${orderId}/status`, { status }).pipe(map((r) => r.data!));
  }

  adminCancel(orderId: number, reason?: string): Observable<Order> {
    return this.http.post<ApiResponse<Order>>(`${this.adminBase}/${orderId}/cancel`, { reason }).pipe(map((r) => r.data!));
  }
}
