import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  CreateCustomerRequest, CustomerDetail, CustomerListItem, CustomerSegment, UpdateCustomerRequest,
} from '../models/customer.model';

@Injectable({ providedIn: 'root' })
export class CustomerAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/customers`;

  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> {
    return o.pipe(map((r) => r.data as T));
  }

  list(search: string, segment: string, page: number, pageSize = 20): Observable<PagedResult<CustomerListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (segment && segment !== 'all') params = params.set('segment', segment);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<CustomerListItem>>>(this.base, { params }));
  }
  segments(): Observable<CustomerSegment[]> {
    return this.unwrap(this.http.get<ApiResponse<CustomerSegment[]>>(`${this.base}/segments`));
  }
  get(id: number): Observable<CustomerDetail> {
    return this.unwrap(this.http.get<ApiResponse<CustomerDetail>>(`${this.base}/${id}`));
  }
  create(req: CreateCustomerRequest): Observable<CustomerDetail> {
    return this.unwrap(this.http.post<ApiResponse<CustomerDetail>>(this.base, req));
  }
  update(id: number, req: UpdateCustomerRequest): Observable<CustomerDetail> {
    return this.unwrap(this.http.put<ApiResponse<CustomerDetail>>(`${this.base}/${id}`, req));
  }
}
