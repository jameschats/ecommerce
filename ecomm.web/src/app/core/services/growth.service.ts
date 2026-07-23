import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface GrowthType {
  key: string; label: string; description: string; credits: number; needsProduct: boolean;
}
export interface BrandKit {
  tone: string; language: string; audience: string | null;
  useEmoji: boolean; hashtags: string | null; doNotSay: string | null;
}
export interface GrowthContent {
  id: number; contentType: string; productId: number | null; language: string;
  title: string | null; body: string; status: string; createdAt: string;
}
export interface GenerateRequest {
  contentType: string; productId?: number | null; language?: string | null; brief?: string | null;
}

@Injectable({ providedIn: 'root' })
export class GrowthService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/growth`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  types(): Observable<GrowthType[]> {
    return this.unwrap(this.http.get<ApiResponse<GrowthType[]>>(`${this.base}/types`));
  }
  brandKit(): Observable<BrandKit> {
    return this.unwrap(this.http.get<ApiResponse<BrandKit>>(`${this.base}/brand-kit`));
  }
  saveBrandKit(kit: BrandKit): Observable<BrandKit> {
    return this.unwrap(this.http.put<ApiResponse<BrandKit>>(`${this.base}/brand-kit`, kit));
  }
  generate(req: GenerateRequest): Observable<GrowthContent> {
    return this.unwrap(this.http.post<ApiResponse<GrowthContent>>(`${this.base}/generate`, req));
  }
  library(contentType?: string, page = 1, pageSize = 20): Observable<PagedResult<GrowthContent>> {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (contentType) q.set('contentType', contentType);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<GrowthContent>>>(`${this.base}/content?${q}`));
  }
  update(id: number, body: string, title: string | null, status: string): Observable<GrowthContent> {
    return this.unwrap(this.http.put<ApiResponse<GrowthContent>>(`${this.base}/content/${id}`, { body, title, status }));
  }
  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/content/${id}`);
  }
}
