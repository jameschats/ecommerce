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
  confirmPlan(id: number): Observable<{ plan: WeekPlan; message: string | null }> {
    return this.http.post<ApiResponse<WeekPlan>>(`${this.base}/plan/${id}/confirm`, {})
      .pipe(map((r) => ({ plan: r.data as WeekPlan, message: r.message })));
  }
  discardPlan(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/plan/${id}`);
  }
  /** Assign channels to an already-generated item and schedule it — no re-generation, no extra credit. */
  scheduleItem(itemId: number, channels: string[]): Observable<{ postsScheduled: number }> {
    return this.http.post<ApiResponse<{ postsScheduled: number }>>(`${this.base}/plan/items/${itemId}/schedule`, { channels })
      .pipe(map((r) => r.data as { postsScheduled: number }));
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

  // --- MS3c: reel rendering ---
  renderReel(productId: number | null, goal: string, platform: string, languageCode: string, includeMusic: boolean): Observable<{ jobId: number }> {
    return this.http.post<ApiResponse<{ jobId: number }>>(`${this.base}/video/render`, { productId, goal, platform, languageCode, includeMusic })
      .pipe(map((r) => r.data as { jobId: number }));
  }
  renderStatus(jobId: number): Observable<RenderStatus> {
    return this.http.get<ApiResponse<RenderStatus>>(`${this.base}/video/render/${jobId}`).pipe(map((r) => r.data as RenderStatus));
  }

  // --- Poster Studio: standalone poster editor ---
  posterOptions(): Observable<PosterEditorOptions> {
    return this.http.get<ApiResponse<PosterEditorOptions>>(`${this.base}/poster/options`).pipe(map((r) => r.data as PosterEditorOptions));
  }
  previewPoster(req: PosterStudioRequest): Observable<{ svg: string }> {
    return this.http.post<ApiResponse<{ svg: string }>>(`${this.base}/poster/preview`, req).pipe(map((r) => r.data as { svg: string }));
  }
  suggestPosterHeadline(productId: number | null, topic: string | null): Observable<{ headline: string }> {
    return this.http.post<ApiResponse<{ headline: string }>>(`${this.base}/poster/suggest-headline`, { productId, topic })
      .pipe(map((r) => r.data as { headline: string }));
  }
  posterBackgroundStyles(): Observable<NamedDescribedCode[]> {
    return this.http.get<ApiResponse<NamedDescribedCode[]>>(`${this.base}/poster/background-styles`).pipe(map((r) => r.data as NamedDescribedCode[]));
  }
  generatePosterBackground(poster: PosterStudioRequest, style: string): Observable<PosterBackgroundResult> {
    return this.http.post<ApiResponse<PosterBackgroundResult>>(`${this.base}/poster/background`, { poster, style })
      .pipe(map((r) => r.data as PosterBackgroundResult));
  }
  /** Reopens an existing poster creative — see PosterDetail.specKind for how to branch: "layers-v1" (a
   *  freeform canvas poster, fully re-editable), "legacy" (made before the canvas editor existed —
   *  view-only from here on), or "none" (a text creative, or nothing salvageable). */
  getPoster(creativeId: number): Observable<PosterDetail> {
    return this.http.get<ApiResponse<PosterDetail>>(`${this.base}/poster/${creativeId}`).pipe(map((r) => r.data as PosterDetail));
  }
  duplicatePoster(creativeId: number): Observable<PosterCreatedResult> {
    return this.http.post<ApiResponse<PosterCreatedResult>>(`${this.base}/poster/${creativeId}/duplicate`, {})
      .pipe(map((r) => r.data as PosterCreatedResult));
  }
  /** Populates a starting draft (template + headline) instead of a blank editor. Costs whatever
   *  suggestPosterHeadline already costs today — never triggers the paid AI background on its own. */
  autoFillPosterDraft(kind: 'org' | 'product', productId: number | null): Observable<AutoFillDraft> {
    return this.http.post<ApiResponse<AutoFillDraft>>(`${this.base}/poster/auto-fill`, { kind, productId })
      .pipe(map((r) => r.data as AutoFillDraft));
  }

  // --- Freeform canvas editor ---
  /** A template's starter layer document, brand-kit colours/font already resolved server-side. */
  templateDocument(templateId: string, format: string): Observable<PosterDocument> {
    return this.http.get<ApiResponse<PosterDocument>>(`${this.base}/poster/templates/${templateId}/document?format=${format}`)
      .pipe(map((r) => r.data as PosterDocument));
  }
  /** Finalizes a freeform poster. `mediaFileId` is the id returned by MediaService.upload() after the
   *  canvas has been exported to PNG client-side — the server resolves it to a URL itself rather than
   *  trusting a client-supplied one. Omit `caption` to have the server write one via AI (credit-metered,
   *  same as the legacy editor) — "Create" is the one metered step regardless of editor. */
  createPosterDocument(document: PosterDocument, mediaFileId: number, caption?: string | null): Observable<PosterCreatedResult> {
    return this.http.post<ApiResponse<PosterCreatedResult>>(`${this.base}/poster`, { document, mediaFileId, caption: caption ?? null })
      .pipe(map((r) => r.data as PosterCreatedResult));
  }
  /** Re-renders (client-side, then re-exports) and overwrites an existing freeform poster in place —
   *  free, no credit spend. */
  updatePosterDocument(creativeId: number, document: PosterDocument, mediaFileId: number, caption: string): Observable<PosterCreatedResult> {
    return this.http.put<ApiResponse<PosterCreatedResult>>(`${this.base}/poster/${creativeId}`, { document, mediaFileId, caption })
      .pipe(map((r) => r.data as PosterCreatedResult));
  }

  // --- Creative Library ---
  listLibrary(type?: 'text' | 'poster'): Observable<LibraryItem[]> {
    const q = type ? `?type=${type}` : '';
    return this.http.get<ApiResponse<LibraryItem[]>>(`${this.base}/library${q}`).pipe(map((r) => r.data as LibraryItem[]));
  }
}

export interface PosterStudioRequest {
  kind: 'org' | 'product';
  productId: number | null;
  headline: string;
  price: number | null;
  cta: string;
  includeLogo: boolean;
  includeName: boolean;
  backgroundImageUrl?: string | null;
  templateId?: string | null;
  font?: string | null;
  headlineScale?: string | null;
  format?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  accentColor?: string | null;
}
export interface PosterCreatedResult { itemId: number; creativeId: number; mediaUrl: string; caption: string; }
export interface PosterTemplateInfo { id: string; name: string; description: string; usesPhoto: boolean; category: string; }
export interface PosterFormatInfo { id: string; label: string; width: number; height: number; }
export interface PosterEditorOptions { templates: PosterTemplateInfo[]; fonts: string[]; backgroundStyles: NamedDescribedCode[]; formats: PosterFormatInfo[]; }
export interface NamedDescribedCode { key: string; label: string; description: string; }
export interface PosterBackgroundResult { url: string; creditsSpent: number; }
export interface PosterDetail {
  creativeId: number; itemId: number; type: string;
  specKind: 'layers-v1' | 'legacy' | 'none';
  document: PosterDocument | null;
  legacyPoster: PosterStudioRequest | null;
  caption: string | null;
  mediaUrl: string | null;
}
export interface AutoFillDraft { templateId: string; headline: string; showPrice: boolean; }

/** Mirrors PosterDocument/PosterLayer on the API — the freeform canvas editor's persisted shape.
 *  Colour/font fields may carry unresolved "{primary}"/"{primaryDark}"/"{secondary}"/"{accent}"/"{font}"
 *  placeholder tokens straight from a template until the server resolves them (see templateDocument());
 *  a document read back via getPoster()/duplicated already has real values, never placeholders. */
export interface PosterDocument {
  specVersion: 'layers-v1';
  format: { width: number; height: number };
  background: { type: 'color' | 'image'; color?: string | null; imageUrl?: string | null };
  layers: PosterLayer[];
  templateId?: string | null;
  kind?: 'org' | 'product' | null;
  productId?: number | null;
}

export type PosterLayerRole = 'headline' | 'price' | 'cta' | 'logo' | 'photo' | 'background' | null;

export interface PosterLayer {
  id: string;
  type: 'text' | 'image' | 'shape';
  x: number; y: number; width: number; height: number;
  rotation: number; opacity: number; zIndex: number;
  role?: PosterLayerRole;
  // text
  text?: string | null; fontFamily?: string | null; fontSize?: number | null; fontWeight?: string | null;
  fontStyle?: string | null; textAlign?: 'left' | 'center' | 'right' | null; color?: string | null;
  lineHeight?: number | null; letterSpacing?: number | null;
  // image
  imageUrl?: string | null; fit?: 'cover' | 'contain' | null; cornerRadius?: number | null;
  // shape
  shapeKind?: 'rect' | 'ellipse' | 'line' | null; fill?: string | null; stroke?: string | null; strokeWidth?: number | null;
}

export interface LibraryItem {
  creativeId: number;
  itemId: number;
  type: string;
  body: string | null;
  mediaUrl: string | null;
  productId: number | null;
  createdAt: string;
  channels: string[];
  editable: boolean;
}

export interface RenderStatus { id: number; status: 'queued' | 'rendering' | 'done' | 'failed'; outputMediaUrl: string | null; error: string | null; }

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
  creativeBody: string | null;
  creativeMediaUrl: string | null;
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
