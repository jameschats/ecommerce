import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface MenuItem { label: string; url: string; children?: MenuItem[]; }
export interface Menu { handle: string; title: string; items: MenuItem[]; }
export interface UrlRedirect { urlRedirectId: number; fromPath: string; toPath: string; }

@Injectable({ providedIn: 'root' })
export class NavigationAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/navigation`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  listMenus(): Observable<Menu[]> { return this.unwrap(this.http.get<ApiResponse<Menu[]>>(`${this.base}/menus`)); }
  saveMenu(handle: string, items: MenuItem[]): Observable<Menu> { return this.unwrap(this.http.put<ApiResponse<Menu>>(`${this.base}/menus/${handle}`, { items })); }

  listRedirects(): Observable<UrlRedirect[]> { return this.unwrap(this.http.get<ApiResponse<UrlRedirect[]>>(`${this.base}/redirects`)); }
  createRedirect(fromPath: string, toPath: string): Observable<UrlRedirect> { return this.unwrap(this.http.post<ApiResponse<UrlRedirect>>(`${this.base}/redirects`, { fromPath, toPath })); }
  deleteRedirect(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/redirects/${id}`); }
}
