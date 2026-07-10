import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface ThemeSummary {
  themeId: number;
  name: string;
  status: string;
  source: string | null;
  isPublished: boolean;
  previewToken: string | null;
  createdAt: string;
}

export interface PrebuiltThemeSummary {
  key: string;
  name: string;
  category: string;
  description: string;
  primaryColor: string;
  secondaryColor: string;
  font: string;
}

@Injectable({ providedIn: 'root' })
export class ThemeLibraryService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/themes`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  list(): Observable<ThemeSummary[]> { return this.unwrap(this.http.get<ApiResponse<ThemeSummary[]>>(this.base)); }
  get(id: number): Observable<ThemeSummary> { return this.unwrap(this.http.get<ApiResponse<ThemeSummary>>(`${this.base}/${id}`)); }
  create(name: string): Observable<ThemeSummary> { return this.unwrap(this.http.post<ApiResponse<ThemeSummary>>(this.base, { name })); }
  duplicate(id: number, name: string | null): Observable<ThemeSummary> { return this.unwrap(this.http.post<ApiResponse<ThemeSummary>>(`${this.base}/${id}/duplicate`, { name })); }
  rename(id: number, name: string): Observable<ThemeSummary> { return this.unwrap(this.http.put<ApiResponse<ThemeSummary>>(`${this.base}/${id}`, { name })); }
  publish(id: number): Observable<unknown> { return this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/publish`, {}); }
  remove(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`); }

  prebuilt(): Observable<PrebuiltThemeSummary[]> { return this.unwrap(this.http.get<ApiResponse<PrebuiltThemeSummary[]>>(`${this.base}/prebuilt`)); }
  install(key: string): Observable<ThemeSummary> { return this.unwrap(this.http.post<ApiResponse<ThemeSummary>>(`${this.base}/install`, { key })); }
}
