import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { AdminCoupon, SaveCouponRequest } from '../models/coupon.model';

@Injectable({ providedIn: 'root' })
export class CouponService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/coupons`;

  list(): Observable<AdminCoupon[]> {
    return this.http.get<ApiResponse<AdminCoupon[]>>(this.base).pipe(map((r) => r.data ?? []));
  }

  create(body: SaveCouponRequest): Observable<AdminCoupon> {
    return this.http.post<ApiResponse<AdminCoupon>>(this.base, body).pipe(map((r) => r.data as AdminCoupon));
  }

  update(id: number, body: SaveCouponRequest): Observable<AdminCoupon> {
    return this.http.put<ApiResponse<AdminCoupon>>(`${this.base}/${id}`, body).pipe(map((r) => r.data as AdminCoupon));
  }

  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`);
  }
}
