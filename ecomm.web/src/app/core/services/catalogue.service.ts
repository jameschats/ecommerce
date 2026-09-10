import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { Catalogue, CatalogueUpdateRequest } from '../models/catalogue.model';

@Injectable({ providedIn: 'root' })
export class CatalogueService {
  private readonly http = inject(HttpClient);
  private readonly pub = `${API_BASE_URL}/cms/catalogues`;
  private readonly admin = `${API_BASE_URL}/admin/cms/catalogues`;

  // --- Public storefront ---
  list(): Observable<Catalogue[]> {
    return this.http.get<ApiResponse<Catalogue[]>>(this.pub).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  // --- Admin ---
  listAdmin(): Observable<Catalogue[]> {
    return this.http.get<ApiResponse<Catalogue[]>>(this.admin).pipe(map((r) => r.data ?? []));
  }

  upload(file: File): Observable<Catalogue> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<Catalogue>>(this.admin, form).pipe(map((r) => r.data as Catalogue));
  }

  update(id: number, body: CatalogueUpdateRequest): Observable<Catalogue> {
    return this.http.put<ApiResponse<Catalogue>>(`${this.admin}/${id}`, body).pipe(map((r) => r.data as Catalogue));
  }

  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.admin}/${id}`);
  }
}
