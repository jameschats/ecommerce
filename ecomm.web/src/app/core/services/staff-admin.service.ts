import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { InviteStaffRequest, StaffMember, StaffRoleInfo } from '../models/staff.model';

@Injectable({ providedIn: 'root' })
export class StaffAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/staff`;

  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> {
    return o.pipe(map((r) => r.data as T));
  }

  list(): Observable<StaffMember[]> {
    return this.unwrap(this.http.get<ApiResponse<StaffMember[]>>(this.base));
  }
  roles(): Observable<StaffRoleInfo[]> {
    return this.unwrap(this.http.get<ApiResponse<StaffRoleInfo[]>>(`${this.base}/roles`));
  }
  invite(req: InviteStaffRequest): Observable<StaffMember> {
    return this.unwrap(this.http.post<ApiResponse<StaffMember>>(this.base, req));
  }
  updateRole(userId: number, accessLevel: string): Observable<StaffMember> {
    return this.unwrap(this.http.put<ApiResponse<StaffMember>>(`${this.base}/${userId}/role`, { accessLevel }));
  }
  setStatus(userId: number, status: string): Observable<StaffMember> {
    const params = new HttpParams().set('status', status);
    return this.unwrap(this.http.put<ApiResponse<StaffMember>>(`${this.base}/${userId}/status`, {}, { params }));
  }
  remove(userId: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${userId}`);
  }
}
