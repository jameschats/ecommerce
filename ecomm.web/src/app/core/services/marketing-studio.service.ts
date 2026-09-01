import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

/** The Marketing Studio visual brand kit (MS0). Mirrors MarketingBrandDto on the API. */
export interface MarketingBrand {
  companyName: string | null;
  tagline: string | null;
  logoUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  font: string | null;
  includeLogoByDefault: boolean;
  includeNameByDefault: boolean;
  instagramHandle: string | null;
  facebookHandle: string | null;
  linkedInHandle: string | null;
  pinterestHandle: string | null;
  youTubeHandle: string | null;
  whatsAppNumber: string | null;
  websiteUrl: string | null;
}

/** Talks to the Marketing Studio module (`/api/marketing/*`). Its own service so the studio stays a
 *  self-contained frontend area, ready to point at a separate app later (plan §3.10). */
@Injectable({ providedIn: 'root' })
export class MarketingStudioService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/marketing`;

  getBrand(): Observable<MarketingBrand> {
    return this.http.get<ApiResponse<MarketingBrand>>(`${this.base}/brand`).pipe(map((r) => r.data as MarketingBrand));
  }

  saveBrand(brand: MarketingBrand): Observable<MarketingBrand> {
    return this.http.put<ApiResponse<MarketingBrand>>(`${this.base}/brand`, brand).pipe(map((r) => r.data as MarketingBrand));
  }
}
