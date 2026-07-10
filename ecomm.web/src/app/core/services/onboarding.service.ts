import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { OnboardingResult, PlanOption, SignupRequest } from '../models/onboarding.model';

@Injectable({ providedIn: 'root' })
export class OnboardingService {
  private readonly http = inject(HttpClient);

  plans(): Observable<PlanOption[]> {
    return this.http.get<ApiResponse<PlanOption[]>>(`${API_BASE_URL}/plans`).pipe(map((r) => r.data ?? []));
  }

  slugAvailable(slug: string): Observable<boolean> {
    return this.http
      .get<ApiResponse<boolean>>(`${API_BASE_URL}/onboarding/slug-available/${encodeURIComponent(slug)}`)
      .pipe(map((r) => r.data ?? false));
  }

  /** A unique Shopify-style store address derived from the name (e.g. "cafe24-a3k9"). */
  suggestSlug(name: string): Observable<string> {
    return this.http
      .get<ApiResponse<string>>(`${API_BASE_URL}/onboarding/suggest-slug`, { params: { name } })
      .pipe(map((r) => r.data ?? ''));
  }

  signup(req: SignupRequest): Observable<OnboardingResult> {
    return this.http
      .post<ApiResponse<OnboardingResult>>(`${API_BASE_URL}/onboarding/signup`, req)
      .pipe(map((r) => r.data as OnboardingResult));
  }
}
