import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { AdminTestimonial, SaveTestimonialRequest, Testimonial } from '../models/testimonial.model';

const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '');

@Injectable({ providedIn: 'root' })
export class TestimonialService {
  private readonly http = inject(HttpClient);
  private readonly pub = `${API_BASE_URL}/cms/testimonials`;
  private readonly admin = `${API_BASE_URL}/admin/cms/testimonials`;

  resolvePhoto(url: string | null): string | null {
    if (!url) return url;
    return url.startsWith('/') ? `${API_ORIGIN}${url}` : url;
  }

  // --- Public storefront ---
  getAll(): Observable<Testimonial[]> {
    return this.http.get<ApiResponse<Testimonial[]>>(this.pub).pipe(
      map((r) => (r.data ?? []).map((t) => ({ ...t, photoUrl: this.resolvePhoto(t.photoUrl) }))),
      catchError(() => of([])),
    );
  }

  // --- Admin ---
  listAdmin(): Observable<AdminTestimonial[]> {
    return this.http.get<ApiResponse<AdminTestimonial[]>>(this.admin).pipe(
      map((r) => (r.data ?? []).map((t) => ({ ...t, photoUrl: this.resolvePhoto(t.photoUrl) }))),
    );
  }

  create(body: SaveTestimonialRequest): Observable<AdminTestimonial> {
    return this.http.post<ApiResponse<AdminTestimonial>>(this.admin, body).pipe(map((r) => r.data as AdminTestimonial));
  }

  update(id: number, body: SaveTestimonialRequest): Observable<AdminTestimonial> {
    return this.http.put<ApiResponse<AdminTestimonial>>(`${this.admin}/${id}`, body).pipe(map((r) => r.data as AdminTestimonial));
  }

  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.admin}/${id}`);
  }

  uploadPhoto(id: number, file: File): Observable<unknown> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<unknown>>(`${this.admin}/${id}/photo`, form);
  }
}
