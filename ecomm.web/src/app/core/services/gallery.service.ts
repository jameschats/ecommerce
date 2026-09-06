import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { AdminGalleryImage, GalleryImage, GallerySection, SaveGalleryImageRequest } from '../models/gallery.model';
import { ApiResponse } from '../models/api-response.model';

/** Origin of the API (API_BASE_URL without the trailing "/api") — used to absolutize
 *  server-relative gallery image URLs so they load from the API host, not the SSR host. */
const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '');

@Injectable({ providedIn: 'root' })
export class GalleryService {
  private readonly http = inject(HttpClient);
  private readonly pub = `${API_BASE_URL}/cms/gallery`;
  private readonly admin = `${API_BASE_URL}/admin/cms/gallery`;

  resolveImage(url: string | null): string | null {
    if (!url) return url;
    return url.startsWith('/') ? `${API_ORIGIN}${url}` : url;
  }

  // --- Public storefront ---
  getImages(section: GallerySection): Observable<GalleryImage[]> {
    return this.http.get<ApiResponse<GalleryImage[]>>(this.pub, { params: { section } }).pipe(
      map((r) => (r.data ?? []).map((g) => ({ ...g, imageUrl: this.resolveImage(g.imageUrl) }))),
      catchError(() => of([])),
    );
  }

  // --- Admin ---
  listAdmin(section: GallerySection): Observable<AdminGalleryImage[]> {
    return this.http.get<ApiResponse<AdminGalleryImage[]>>(this.admin, { params: { section } }).pipe(
      map((r) => (r.data ?? []).map((g) => ({ ...g, imageUrl: this.resolveImage(g.imageUrl) }))),
    );
  }

  create(body: SaveGalleryImageRequest): Observable<AdminGalleryImage> {
    return this.http.post<ApiResponse<AdminGalleryImage>>(this.admin, body).pipe(map((r) => r.data as AdminGalleryImage));
  }

  update(id: number, body: SaveGalleryImageRequest): Observable<AdminGalleryImage> {
    return this.http.put<ApiResponse<AdminGalleryImage>>(`${this.admin}/${id}`, body).pipe(map((r) => r.data as AdminGalleryImage));
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
