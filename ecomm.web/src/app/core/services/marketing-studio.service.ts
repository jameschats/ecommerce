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

  // --- MS2 sub-step 2: weekly plan (propose / review / confirm) ---
  proposePlan(weekStart?: string): Observable<WeekPlan> {
    return this.http.post<ApiResponse<WeekPlan>>(`${this.base}/plan/propose`, { weekStart: weekStart ?? null }).pipe(map((r) => r.data as WeekPlan));
  }
  getCurrentPlan(): Observable<WeekPlan | null> {
    return this.http.get<ApiResponse<WeekPlan | null>>(`${this.base}/plan/current`).pipe(map((r) => r.data ?? null));
  }
  updatePlanItem(id: number, patch: Partial<PlanItem>): Observable<PlanItem> {
    return this.http.put<ApiResponse<PlanItem>>(`${this.base}/plan/items/${id}`, patch).pipe(map((r) => r.data as PlanItem));
  }
  removePlanItem(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/plan/items/${id}`);
  }
  confirmPlan(id: number): Observable<WeekPlan> {
    return this.http.post<ApiResponse<WeekPlan>>(`${this.base}/plan/${id}/confirm`, {}).pipe(map((r) => r.data as WeekPlan));
  }
  discardPlan(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/plan/${id}`);
  }

  // --- MS2 sub-step 4: scheduler ---
  listScheduled(status?: string): Observable<ScheduledPost[]> {
    const q = status ? `?status=${encodeURIComponent(status)}` : '';
    return this.http.get<ApiResponse<ScheduledPost[]>>(`${this.base}/scheduler${q}`).pipe(map((r) => r.data as ScheduledPost[]));
  }
  approvePost(id: number): Observable<ScheduledPost> {
    return this.http.post<ApiResponse<ScheduledPost>>(`${this.base}/scheduler/${id}/approve`, {}).pipe(map((r) => r.data as ScheduledPost));
  }
  approveAllPosts(): Observable<{ approved: number }> {
    return this.http.post<ApiResponse<{ approved: number }>>(`${this.base}/scheduler/approve-all`, {}).pipe(map((r) => r.data as { approved: number }));
  }
  reschedulePost(id: number, scheduledAt: string): Observable<ScheduledPost> {
    return this.http.put<ApiResponse<ScheduledPost>>(`${this.base}/scheduler/${id}/reschedule`, { scheduledAt }).pipe(map((r) => r.data as ScheduledPost));
  }
  skipPost(id: number): Observable<ScheduledPost> {
    return this.http.post<ApiResponse<ScheduledPost>>(`${this.base}/scheduler/${id}/skip`, {}).pipe(map((r) => r.data as ScheduledPost));
  }
  deletePost(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/scheduler/${id}`);
  }

  // --- MS3a: voiceover (Sarvam TTS) ---
  voiceOptions(): Observable<VoiceOptions> {
    return this.http.get<ApiResponse<VoiceOptions>>(`${this.base}/voice/options`).pipe(map((r) => r.data as VoiceOptions));
  }
  voicePreview(text: string, languageCode: string, speaker: string | null): Observable<{ audioUrl: string }> {
    return this.http.post<ApiResponse<{ audioUrl: string }>>(`${this.base}/voice/preview`, { text, languageCode, speaker })
      .pipe(map((r) => r.data as { audioUrl: string }));
  }

  // --- MS3b: video reel plan ---
  videoOptions(): Observable<VideoOptions> {
    return this.http.get<ApiResponse<VideoOptions>>(`${this.base}/video/options`).pipe(map((r) => r.data as VideoOptions));
  }
  videoPlan(productId: number | null, goal: string, platform: string): Observable<VideoPlan> {
    return this.http.post<ApiResponse<VideoPlan>>(`${this.base}/video/plan`, { productId, goal, platform }).pipe(map((r) => r.data as VideoPlan));
  }
}

export interface NamedProduct { id: number; name: string; }
export interface VideoOptions { goals: NamedCode[]; platforms: NamedCode[]; products: NamedProduct[]; }
export interface VideoScene { durationSeconds: number; visual: string; text: string; }
export interface VideoPlan { hook: string; durationSeconds: number; aspect: string; musicVertical: string; narration: string; scenes: VideoScene[]; }

export interface NamedCode { code: string; name: string; }
export interface VoiceOptions { enabled: boolean; languages: NamedCode[]; speakers: NamedCode[]; }

/** A per-channel scheduled post (also a job-history row). Mirrors ScheduledPostDto on the API. */
export interface ScheduledPost {
  id: number;
  planItemId: number;
  platform: string;
  scheduledAt: string;
  status: 'pending_approval' | 'scheduled' | 'published' | 'failed' | 'skipped';
  type: string;
  topic: string;
  preview: string | null;
  mediaUrl: string | null;
  externalPostId: string | null;
  error: string | null;
  publishedAt: string | null;
}

/** One proposed creative in the weekly plan. Mirrors PlanItemDto on the API. */
export interface PlanItem {
  id: number;
  scheduledAt: string;
  type: 'text' | 'poster';
  productId: number | null;
  topic: string;
  angle: string | null;
  channels: string[];
  includeLogo: boolean;
  includeName: boolean;
  status: string;
}

/** A week's proposed plan. Mirrors PlanDto on the API. */
export interface WeekPlan {
  id: number;
  weekStart: string;
  status: string;
  items: PlanItem[];
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
