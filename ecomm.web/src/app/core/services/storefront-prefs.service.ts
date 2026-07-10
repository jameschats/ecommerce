import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface StorefrontPreferences {
  seoTitle: string | null;
  seoDescription: string | null;
  seoImage: string | null;
  passwordEnabled: boolean;
  password: string | null;
  passwordMessage: string | null;
}

@Injectable({ providedIn: 'root' })
export class StorefrontPrefsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/storefront-preferences`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  get(): Observable<StorefrontPreferences> { return this.unwrap(this.http.get<ApiResponse<StorefrontPreferences>>(this.base)); }
  update(body: StorefrontPreferences): Observable<StorefrontPreferences> { return this.unwrap(this.http.put<ApiResponse<StorefrontPreferences>>(this.base, body)); }
}
