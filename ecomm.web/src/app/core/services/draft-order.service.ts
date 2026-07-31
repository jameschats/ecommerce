import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface DraftLineInput { productId: number; variantId: number | null; quantity: number; }
export interface CreateDraftOrder { customerUserId: number; lines: DraftLineInput[]; couponCode: string | null; notes: string | null; }
export interface DraftOrderLine {
  productId: number; variantId: number | null; name: string; variantLabel: string | null;
  quantity: number; unitPrice: number; lineTotal: number; taxAmount: number; isFreeGift: boolean;
}
export interface DraftOrder {
  orderId: number; orderNumber: string; status: string; customerUserId: number;
  customerName: string | null; customerEmail: string | null;
  subtotal: number; discountAmount: number; taxAmount: number; shippingAmount: number; totalAmount: number;
  couponCode: string | null; notes: string | null; lines: DraftOrderLine[]; createdAt: string;
}
export interface DraftOrderListItem { orderId: number; orderNumber: string; customerName: string | null; totalAmount: number; createdAt: string; }

@Injectable({ providedIn: 'root' })
export class DraftOrderService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/draft-orders`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  list(): Observable<DraftOrderListItem[]> { return this.unwrap(this.http.get<ApiResponse<DraftOrderListItem[]>>(this.base)); }
  get(id: number): Observable<DraftOrder> { return this.unwrap(this.http.get<ApiResponse<DraftOrder>>(`${this.base}/${id}`)); }
  create(req: CreateDraftOrder): Observable<DraftOrder> { return this.unwrap(this.http.post<ApiResponse<DraftOrder>>(this.base, req)); }
  convert(id: number, paymentMethod: string): Observable<{ orderId: number }> {
    const params = new HttpParams().set('paymentMethod', paymentMethod);
    return this.unwrap(this.http.post<ApiResponse<{ orderId: number }>>(`${this.base}/${id}/convert`, {}, { params }));
  }
  remove(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`); }
}
