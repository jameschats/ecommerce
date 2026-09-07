import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface UploadedMedia {
  mediaFileId: number;
  url: string;
  size: number;
}

/** Uploads image files to the admin media endpoint; returns the stored id + public URL. */
@Injectable({ providedIn: 'root' })
export class MediaService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/media`;

  /** @param watermark Set only for product photos — see IMediaStorage.SaveAsync on the API side. */
  upload(file: File, watermark = false): Observable<UploadedMedia> {
    const form = new FormData();
    form.append('file', file);
    if (watermark) form.append('watermark', 'true');
    return this.http.post<ApiResponse<UploadedMedia>>(this.base, form).pipe(map((r) => r.data as UploadedMedia));
  }
}
