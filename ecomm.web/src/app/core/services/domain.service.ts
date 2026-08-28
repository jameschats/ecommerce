import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface DomainStatus {
  domain: string | null;
  verified: boolean;
  verificationPath: string | null;
  verificationToken: string | null;
  cnameTarget: string | null;
  sslStatus: string | null;   // Cloudflare edge-cert status: 'active' once HTTPS is live, else provisioning
}

@Injectable({ providedIn: 'root' })
export class DomainService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/domain`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  get(): Observable<DomainStatus> { return this.unwrap(this.http.get<ApiResponse<DomainStatus>>(this.base)); }
  connect(domain: string): Observable<DomainStatus> { return this.unwrap(this.http.put<ApiResponse<DomainStatus>>(this.base, { domain })); }
  verify(): Observable<DomainStatus> { return this.unwrap(this.http.post<ApiResponse<DomainStatus>>(`${this.base}/verify`, {})); }
  disconnect(): Observable<DomainStatus> { return this.unwrap(this.http.delete<ApiResponse<DomainStatus>>(this.base)); }
}
