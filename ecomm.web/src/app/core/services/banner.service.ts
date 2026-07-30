import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { AdminBanner, BannerPage, HomeBanner, SaveBannerRequest } from '../models/banner.model';
import { ApiResponse } from '../models/api-response.model';

/** Origin of the API (API_BASE_URL without the trailing "/api") — used to absolutize
 *  server-relative banner image URLs so they load from the API host, not the SSR host. */
const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '');

@Injectable({ providedIn: 'root' })
export class BannerService {
  private readonly http = inject(HttpClient);
  private readonly pub = `${API_BASE_URL}/cms/banners`;
  private readonly admin = `${API_BASE_URL}/admin/cms/banners`;

  /** Absolutize a server-relative image URL (e.g. /api/cms/banners/1/image); pass external URLs through. */
  resolveImage(url: string | null): string | null {
    if (!url) return url;
    return url.startsWith('/') ? `${API_ORIGIN}${url}` : url;
  }

  // --- Public storefront ---
  getBanners(page: BannerPage): Observable<HomeBanner[]> {
    return this.http.get<ApiResponse<HomeBanner[]>>(this.pub, { params: { page } }).pipe(
      map((r) => (r.data ?? []).map((b) => ({ ...b, imageUrl: this.resolveImage(b.imageUrl) }))),
      catchError(() => of([])),
    );
  }

  // --- Admin ---
  listAdmin(page: BannerPage): Observable<AdminBanner[]> {
    return this.http.get<ApiResponse<AdminBanner[]>>(this.admin, { params: { page } }).pipe(
      map((r) => (r.data ?? []).map((b) => ({ ...b, imageUrl: this.resolveImage(b.imageUrl) }))),
    );
  }

  create(body: SaveBannerRequest): Observable<AdminBanner> {
    return this.http.post<ApiResponse<AdminBanner>>(this.admin, body).pipe(map((r) => r.data as AdminBanner));
  }

  update(id: number, body: SaveBannerRequest): Observable<AdminBanner> {
    return this.http.put<ApiResponse<AdminBanner>>(`${this.admin}/${id}`, body).pipe(map((r) => r.data as AdminBanner));
  }

  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.admin}/${id}`);
  }

  uploadImage(id: number, file: File): Observable<unknown> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<unknown>>(`${this.admin}/${id}/image`, form);
  }
}
