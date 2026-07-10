import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface UploadedMedia {
  mediaFileId: number;
  url: string;
  size: number;
}

export interface MediaFile {
  mediaFileId: number;
  url: string;
  originalName: string | null;
  mimeType: string | null;
  sizeBytes: number | null;
  references: number;
  createdAt: string;
}

/** Uploads image files to the admin media endpoint; returns the stored id + public URL. */
@Injectable({ providedIn: 'root' })
export class MediaService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/media`;

  upload(file: File): Observable<UploadedMedia> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<UploadedMedia>>(this.base, form).pipe(map((r) => r.data as UploadedMedia));
  }

  list(page = 1, pageSize = 24): Observable<PagedResult<MediaFile>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<PagedResult<MediaFile>>>(this.base, { params }).pipe(map((r) => r.data as PagedResult<MediaFile>));
  }
  remove(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`); }
}
