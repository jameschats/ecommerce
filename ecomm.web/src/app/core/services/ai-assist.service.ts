import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface AiSeo { title: string; metaDescription: string; }

/**
 * Powers the shared ✨ "Improve with AI" affordance. Holds a one-shot `enabled` flag (so buttons hide
 * gracefully when the platform has no AI provider) and the improve/SEO calls, which are metered
 * server-side against the tenant's credit balance.
 */
@Injectable({ providedIn: 'root' })
export class AiAssistService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/ai`;

  readonly enabled = signal(false);
  private loaded = false;

  /** Load the enabled flag once per session (cheap; no DB, no spend). */
  ensureStatus(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.http.get<ApiResponse<{ enabled: boolean }>>(`${this.base}/status`).subscribe({
      next: (r) => this.enabled.set(!!r.data?.enabled),
      error: () => { this.loaded = false; },   // allow a retry if the probe failed
    });
  }

  improve(purpose: string, text: string, context?: string): Observable<string> {
    return this.http.post<ApiResponse<{ text: string }>>(`${this.base}/improve`, { purpose, text, context })
      .pipe(map((r) => r.data?.text ?? ''));
  }

  seo(name: string, description?: string): Observable<AiSeo> {
    return this.http.post<ApiResponse<AiSeo>>(`${this.base}/seo`, { name, description })
      .pipe(map((r) => r.data as AiSeo));
  }
}
