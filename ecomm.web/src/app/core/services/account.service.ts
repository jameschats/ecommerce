import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { Address, Profile, SaveAddressRequest, UpdateProfileRequest } from '../models/account.model';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/account`;

  getProfile(): Observable<Profile> {
    return this.http.get<ApiResponse<Profile>>(`${this.base}/profile`).pipe(map((r) => r.data!));
  }

  updateProfile(body: UpdateProfileRequest): Observable<Profile> {
    return this.http.put<ApiResponse<Profile>>(`${this.base}/profile`, body).pipe(map((r) => r.data!));
  }

  listAddresses(): Observable<Address[]> {
    return this.http.get<ApiResponse<Address[]>>(`${this.base}/addresses`).pipe(map((r) => r.data!));
  }

  createAddress(body: SaveAddressRequest): Observable<Address> {
    return this.http.post<ApiResponse<Address>>(`${this.base}/addresses`, body).pipe(map((r) => r.data!));
  }

  updateAddress(id: number, body: SaveAddressRequest): Observable<Address> {
    return this.http.put<ApiResponse<Address>>(`${this.base}/addresses/${id}`, body).pipe(map((r) => r.data!));
  }

  deleteAddress(id: number): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/addresses/${id}`).pipe(map(() => void 0));
  }

  setDefaultAddress(id: number): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/addresses/${id}/default`, {}).pipe(map(() => void 0));
  }
}
