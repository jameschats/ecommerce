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

  // --- MS1: social connections ---
  listConnections(): Observable<SocialConnection[]> {
    return this.http.get<ApiResponse<SocialConnection[]>>(`${this.base}/connections`).pipe(map((r) => r.data as SocialConnection[]));
  }

  startConnect(platform: string): Observable<{ authorizeUrl: string }> {
    return this.http.post<ApiResponse<{ authorizeUrl: string }>>(`${this.base}/connections/${platform}/start`, {})
      .pipe(map((r) => r.data as { authorizeUrl: string }));
  }

  disconnect(platform: string): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/connections/${platform}`);
  }

  // --- MS2 sub-step 1: weekly-plan preferences ---
  getPlanSettings(): Observable<MarketingPlanSettings> {
    return this.http.get<ApiResponse<MarketingPlanSettings>>(`${this.base}/plan/settings`).pipe(map((r) => r.data as MarketingPlanSettings));
  }

  savePlanSettings(settings: MarketingPlanSettings): Observable<MarketingPlanSettings> {
    return this.http.put<ApiResponse<MarketingPlanSettings>>(`${this.base}/plan/settings`, settings).pipe(map((r) => r.data as MarketingPlanSettings));
  }
}

/** Per-channel row in the weekly-plan matrix. Mirrors ChannelPrefDto on the API. */
export interface ChannelPref {
  platform: string;
  displayName: string;
  connected: boolean;
  enabled: boolean;
  allowText: boolean;
  allowPoster: boolean;
  allowVideo: boolean;
}

/** Weekly-plan cadence + channel matrix. Mirrors MarketingPlanSettingsDto on the API. */
export interface MarketingPlanSettings {
  textPerWeek: number;
  postersPerWeek: number;
  videosPerWeek: number;
  weekStartDay: number;
  defaultPostHour: number;
  autoRecur: boolean;
  channels: ChannelPref[];
}

/** A social platform card on the connections page. Mirrors SocialConnectionDto on the API. */
export interface SocialConnection {
  platform: string;
  displayName: string;
  status: 'connected' | 'expired' | 'not_connected' | 'not_configured';
  accountName: string | null;
  configured: boolean;
  connectedAt: string | null;
  expiresAt: string | null;
}
