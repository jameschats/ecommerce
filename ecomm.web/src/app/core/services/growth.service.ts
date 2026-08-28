import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface GrowthType {
  key: string; label: string; description: string; credits: number; needsProduct: boolean;
}
export interface BrandKit {
  tone: string; language: string; audience: string | null;
  useEmoji: boolean; hashtags: string | null; doNotSay: string | null;
}
export interface GrowthContent {
  id: number; contentType: string; productId: number | null; campaignId: number | null; language: string;
  title: string | null; body: string; status: string; wasEdited: boolean;
  originalTitle: string | null; originalBody: string | null; createdAt: string; updatedAt: string | null;
}
export interface GenerateRequest {
  contentType: string; productId?: number | null; language?: string | null; brief?: string | null;
}

export interface Goal { key: string; label: string; description: string; }
export interface CampaignChannel { channel: string; content: GrowthContent | null; error: string | null; }
export interface Campaign {
  id: number; name: string; goal: string; productId: number | null;
  language: string; status: string; createdAt: string; channels: CampaignChannel[];
}
export interface CampaignSummary { id: number; name: string; goal: string; status: string; createdAt: string; pieces: number; }
export interface CreateCampaignRequest {
  goal: string; name?: string | null; productId: number; brief?: string | null; language?: string | null;
}
export interface CustomerSegment { key: string; label: string; count: number; }
export interface CampaignSendStatus {
  campaignId: number; status: string; channel: string; segment: string | null;
  scheduledAt: string | null; sentAt: string | null;
  recipientCount: number; sentCount: number; failedCount: number;
  eligibleNow: number; hasEmailContent: boolean;
}
export interface BulkJob { queued: number; message: string; }
export interface ImageStyle { key: string; label: string; description: string; }
export interface ImageFormat { key: string; label: string; size: string; }
export interface GeneratedImage { id: number; url: string; costInr: number; createdAt: string; }

@Injectable({ providedIn: 'root' })
export class GrowthService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/growth`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  types(): Observable<GrowthType[]> {
    return this.unwrap(this.http.get<ApiResponse<GrowthType[]>>(`${this.base}/types`));
  }
  brandKit(): Observable<BrandKit> {
    return this.unwrap(this.http.get<ApiResponse<BrandKit>>(`${this.base}/brand-kit`));
  }
  saveBrandKit(kit: BrandKit): Observable<BrandKit> {
    return this.unwrap(this.http.put<ApiResponse<BrandKit>>(`${this.base}/brand-kit`, kit));
  }
  generate(req: GenerateRequest): Observable<GrowthContent> {
    return this.unwrap(this.http.post<ApiResponse<GrowthContent>>(`${this.base}/generate`, req));
  }
  library(filters: {
    contentType?: string | null; productId?: number | null; campaignId?: number | null;
    from?: string | null; to?: string | null; page?: number; pageSize?: number;
  } = {}): Observable<PagedResult<GrowthContent>> {
    const q = new URLSearchParams({ page: String(filters.page ?? 1), pageSize: String(filters.pageSize ?? 20) });
    if (filters.contentType) q.set('contentType', filters.contentType);
    if (filters.productId) q.set('productId', String(filters.productId));
    if (filters.campaignId) q.set('campaignId', String(filters.campaignId));
    if (filters.from) q.set('from', filters.from);
    if (filters.to) q.set('to', filters.to);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<GrowthContent>>>(`${this.base}/content?${q}`));
  }
  update(id: number, body: string, title: string | null, status: string): Observable<GrowthContent> {
    return this.unwrap(this.http.put<ApiResponse<GrowthContent>>(`${this.base}/content/${id}`, { body, title, status }));
  }
  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/content/${id}`);
  }

  // ----- Campaigns (G2) -----
  goals(): Observable<Goal[]> {
    return this.unwrap(this.http.get<ApiResponse<Goal[]>>(`${this.base}/goals`));
  }
  createCampaign(req: CreateCampaignRequest): Observable<Campaign> {
    return this.unwrap(this.http.post<ApiResponse<Campaign>>(`${this.base}/campaigns`, req));
  }
  campaigns(page = 1, pageSize = 20): Observable<PagedResult<CampaignSummary>> {
    return this.unwrap(this.http.get<ApiResponse<PagedResult<CampaignSummary>>>(`${this.base}/campaigns?page=${page}&pageSize=${pageSize}`));
  }
  campaign(id: number): Observable<Campaign> {
    return this.unwrap(this.http.get<ApiResponse<Campaign>>(`${this.base}/campaigns/${id}`));
  }
  removeCampaign(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/campaigns/${id}`);
  }

  // ----- Campaign sends (M1) -----
  segments(): Observable<CustomerSegment[]> {
    return this.unwrap(this.http.get<ApiResponse<CustomerSegment[]>>(`${this.base}/segments`));
  }
  sendPreview(campaignId: number, segment: string | null): Observable<CampaignSendStatus> {
    const q = segment ? `?segment=${encodeURIComponent(segment)}` : '';
    return this.unwrap(this.http.get<ApiResponse<CampaignSendStatus>>(`${this.base}/campaigns/${campaignId}/send${q}`));
  }
  sendCampaign(campaignId: number, segment: string | null, scheduledAt: string | null): Observable<CampaignSendStatus> {
    return this.unwrap(this.http.post<ApiResponse<CampaignSendStatus>>(`${this.base}/campaigns/${campaignId}/send`, { segment, scheduledAt }));
  }
  cancelSend(campaignId: number): Observable<CampaignSendStatus> {
    return this.unwrap(this.http.post<ApiResponse<CampaignSendStatus>>(`${this.base}/campaigns/${campaignId}/cancel-send`, {}));
  }
  /** The copy-paste export pack as a blob (auth header added by the interceptor); caller triggers the download. */
  exportCampaign(campaignId: number): Observable<Blob> {
    return this.http.get(`${this.base}/campaigns/${campaignId}/export`, { responseType: 'blob' });
  }

  // ----- Bulk generate (M1) -----
  bulkGenerate(contentType: string, productIds: number[], language: string | null, brief: string | null): Observable<BulkJob> {
    return this.unwrap(this.http.post<ApiResponse<BulkJob>>(`${this.base}/bulk`, { contentType, productIds, language, brief }));
  }

  // ----- Images (beta) -----
  imageStyles(): Observable<ImageStyle[]> {
    return this.unwrap(this.http.get<ApiResponse<ImageStyle[]>>(`${this.base}/image/styles`));
  }
  imageFormats(): Observable<ImageFormat[]> {
    return this.unwrap(this.http.get<ApiResponse<ImageFormat[]>>(`${this.base}/image/formats`));
  }
  generateImage(productId: number, style: string, format: string | null, brief?: string | null): Observable<GeneratedImage> {
    return this.unwrap(this.http.post<ApiResponse<GeneratedImage>>(`${this.base}/image`, { productId, style, format, brief }));
  }
  recentImages(): Observable<GeneratedImage[]> {
    return this.unwrap(this.http.get<ApiResponse<GeneratedImage[]>>(`${this.base}/image/recent`));
  }
}
