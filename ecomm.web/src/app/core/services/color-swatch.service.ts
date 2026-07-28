import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { ColorSwatch, SaveColorSwatchRequest } from '../models/color-swatch.model';

@Injectable({ providedIn: 'root' })
export class ColorSwatchService {
  private readonly http = inject(HttpClient);
  private readonly publicBase = `${API_BASE_URL}/catalog/color-swatches`;
  private readonly adminBase = `${API_BASE_URL}/admin/color-swatches`;

  /** Storefront read, shared + cached for the lifetime of the app (small, rarely-changing lookup). */
  private cached$?: Observable<ColorSwatch[]>;
  list(): Observable<ColorSwatch[]> {
    this.cached$ ??= this.http.get<ApiResponse<ColorSwatch[]>>(this.publicBase).pipe(
      map((r) => r.data ?? []),
      shareReplay(1),
    );
    return this.cached$;
  }

  // --- Admin CRUD ---
  adminList(): Observable<ColorSwatch[]> {
    return this.http.get<ApiResponse<ColorSwatch[]>>(this.adminBase).pipe(map((r) => r.data ?? []));
  }
  create(body: SaveColorSwatchRequest): Observable<ColorSwatch> {
    return this.http.post<ApiResponse<ColorSwatch>>(this.adminBase, body).pipe(map((r) => r.data as ColorSwatch));
  }
  update(id: number, body: SaveColorSwatchRequest): Observable<ColorSwatch> {
    return this.http.put<ApiResponse<ColorSwatch>>(`${this.adminBase}/${id}`, body).pipe(map((r) => r.data as ColorSwatch));
  }
  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.adminBase}/${id}`);
  }
}
