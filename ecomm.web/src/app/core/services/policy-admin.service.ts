import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface StorePolicy { handle: string; title: string; bodyHtml: string | null; hasContent: boolean; }

@Injectable({ providedIn: 'root' })
export class PolicyAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/policies`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  list(): Observable<StorePolicy[]> { return this.unwrap(this.http.get<ApiResponse<StorePolicy[]>>(this.base)); }
  save(handle: string, title: string, bodyHtml: string): Observable<StorePolicy> {
    return this.unwrap(this.http.put<ApiResponse<StorePolicy>>(`${this.base}/${handle}`, { title, bodyHtml }));
  }
}
