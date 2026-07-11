import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

/** A store's fulfillment method + (when Shiprocket) its connection. Password is write-only. */
export interface ShiprocketSettings {
  method: 'Self' | 'Shiprocket';
  email: string | null;
  hasPassword: boolean;
  pickupPincode: string | null;
  pickupLocation: string | null;
  isVerified: boolean;
  connectedAt: string | null;
}

export interface UpdateShiprocketSettings {
  method: 'Self' | 'Shiprocket';
  email?: string | null;
  password?: string | null;
  pickupPincode?: string | null;
  pickupLocation?: string | null;
}

@Injectable({ providedIn: 'root' })
export class FulfillmentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/shipping/shiprocket/settings`;

  getSettings(): Observable<ShiprocketSettings> {
    return this.http.get<ApiResponse<ShiprocketSettings>>(this.base).pipe(map((r) => r.data!));
  }

  updateSettings(req: UpdateShiprocketSettings): Observable<ShiprocketSettings> {
    return this.http.put<ApiResponse<ShiprocketSettings>>(this.base, req).pipe(map((r) => r.data!));
  }
}
