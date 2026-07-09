import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface ShippingMethod {
  shippingMethodId: number;
  name: string;
  description: string | null;
  baseRate: number;
  freeShippingThreshold: number | null;
  estimatedDays: number | null;
  isActive: boolean;
}
export interface SaveShippingMethod {
  name: string;
  description: string | null;
  baseRate: number;
  freeShippingThreshold: number | null;
  estimatedDays: number | null;
  isActive: boolean;
}
export interface ShippingZone {
  shippingZoneId: number;
  name: string;
  pincodeStart: string | null;
  pincodeEnd: string | null;
  rate: number;
  isServiceable: boolean;
}
export interface SaveShippingZone {
  name: string;
  pincodeStart: string | null;
  pincodeEnd: string | null;
  rate: number;
  isServiceable: boolean;
}

@Injectable({ providedIn: 'root' })
export class ShippingAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/shipping`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  listMethods(): Observable<ShippingMethod[]> { return this.unwrap(this.http.get<ApiResponse<ShippingMethod[]>>(`${this.base}/methods`)); }
  createMethod(b: SaveShippingMethod): Observable<ShippingMethod> { return this.unwrap(this.http.post<ApiResponse<ShippingMethod>>(`${this.base}/methods`, b)); }
  updateMethod(id: number, b: SaveShippingMethod): Observable<ShippingMethod> { return this.unwrap(this.http.put<ApiResponse<ShippingMethod>>(`${this.base}/methods/${id}`, b)); }
  deleteMethod(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/methods/${id}`); }

  listZones(): Observable<ShippingZone[]> { return this.unwrap(this.http.get<ApiResponse<ShippingZone[]>>(`${this.base}/zones`)); }
  createZone(b: SaveShippingZone): Observable<ShippingZone> { return this.unwrap(this.http.post<ApiResponse<ShippingZone>>(`${this.base}/zones`, b)); }
  updateZone(id: number, b: SaveShippingZone): Observable<ShippingZone> { return this.unwrap(this.http.put<ApiResponse<ShippingZone>>(`${this.base}/zones/${id}`, b)); }
  deleteZone(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/zones/${id}`); }
}
