import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface CollectionRule { field: string; op: string; value: string; }
export interface CollectionProduct { productId: number; name: string; slug: string; price: number; primaryImageUrl: string | null; }
export interface AdminCollection {
  collectionId: number; name: string; slug: string; description: string | null; imageUrl: string | null;
  collectionType: string; matchType: string; rules: CollectionRule[];
  metaTitle: string | null; metaDescription: string | null; isActive: boolean; productCount: number;
}
export interface SaveCollection {
  name: string; slug: string | null; description: string | null; imageUrl: string | null;
  collectionType: string; matchType: string; rules: CollectionRule[] | null;
  metaTitle: string | null; metaDescription: string | null; isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class CollectionAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/collections`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  list(): Observable<AdminCollection[]> { return this.unwrap(this.http.get<ApiResponse<AdminCollection[]>>(this.base)); }
  get(id: number): Observable<AdminCollection> { return this.unwrap(this.http.get<ApiResponse<AdminCollection>>(`${this.base}/${id}`)); }
  members(id: number): Observable<CollectionProduct[]> { return this.unwrap(this.http.get<ApiResponse<CollectionProduct[]>>(`${this.base}/${id}/products`)); }
  create(body: SaveCollection): Observable<AdminCollection> { return this.unwrap(this.http.post<ApiResponse<AdminCollection>>(this.base, body)); }
  update(id: number, body: SaveCollection): Observable<AdminCollection> { return this.unwrap(this.http.put<ApiResponse<AdminCollection>>(`${this.base}/${id}`, body)); }
  setMembers(id: number, productIds: number[]): Observable<unknown> { return this.http.put<ApiResponse<unknown>>(`${this.base}/${id}/products`, { productIds }); }
  remove(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`); }
}
