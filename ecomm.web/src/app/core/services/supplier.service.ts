import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { ProductSupplier, ProductSupplierInput, SaveSupplierRequest, Supplier } from '../models/supplier.model';

@Injectable({ providedIn: 'root' })
export class SupplierService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/suppliers`;
  private readonly products = `${API_BASE_URL}/admin/products`;

  list(): Observable<Supplier[]> {
    return this.http.get<ApiResponse<Supplier[]>>(this.base).pipe(map((r) => r.data ?? []));
  }
  create(body: SaveSupplierRequest): Observable<Supplier> {
    return this.http.post<ApiResponse<Supplier>>(this.base, body).pipe(map((r) => r.data as Supplier));
  }
  update(id: number, body: SaveSupplierRequest): Observable<Supplier> {
    return this.http.put<ApiResponse<Supplier>>(`${this.base}/${id}`, body).pipe(map((r) => r.data as Supplier));
  }
  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`);
  }

  forProduct(productId: number): Observable<ProductSupplier[]> {
    return this.http.get<ApiResponse<ProductSupplier[]>>(`${this.products}/${productId}/suppliers`).pipe(map((r) => r.data ?? []));
  }
  setForProduct(productId: number, suppliers: ProductSupplierInput[]): Observable<ProductSupplier[]> {
    return this.http.put<ApiResponse<ProductSupplier[]>>(`${this.products}/${productId}/suppliers`, { suppliers }).pipe(map((r) => r.data ?? []));
  }
}
