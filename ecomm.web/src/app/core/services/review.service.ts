import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { AdminReview, ProductReview, ProductReviews, SubmitReviewRequest } from '../models/review.model';

@Injectable({ providedIn: 'root' })
export class ReviewService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;

  getForProduct(productId: number, page = 1, pageSize = 10): Observable<ProductReviews> {
    return this.http
      .get<ApiResponse<ProductReviews>>(`${this.base}/catalog/products/${productId}/reviews?page=${page}&pageSize=${pageSize}`)
      .pipe(map((r) => r.data as ProductReviews));
  }

  eligibility(productId: number): Observable<{ canReview: boolean; alreadyReviewed: boolean }> {
    return this.http
      .get<ApiResponse<{ canReview: boolean; alreadyReviewed: boolean }>>(`${this.base}/reviews/eligibility/${productId}`)
      .pipe(map((r) => r.data as { canReview: boolean; alreadyReviewed: boolean }));
  }

  submit(body: SubmitReviewRequest): Observable<ProductReview> {
    return this.http.post<ApiResponse<ProductReview>>(`${this.base}/reviews`, body).pipe(map((r) => r.data as ProductReview));
  }

  // --- Admin ---
  listAdmin(status?: string, page = 1, pageSize = 20): Observable<PagedResult<AdminReview>> {
    const q = status ? `&status=${status}` : '';
    return this.http
      .get<ApiResponse<PagedResult<AdminReview>>>(`${this.base}/admin/reviews?page=${page}&pageSize=${pageSize}${q}`)
      .pipe(map((r) => r.data as PagedResult<AdminReview>));
  }

  approve(id: number, approved: boolean): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/admin/reviews/${id}/approve`, { approved });
  }

  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/admin/reviews/${id}`);
  }
}
