import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface GeneratedPage { pageId: number; slug: string; sections: number; }

/** AI-5: generate a storefront page (as builder sections) from a prompt. */
@Injectable({ providedIn: 'root' })
export class AiPageService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/ai/page`;

  generate(title: string, prompt: string): Observable<GeneratedPage> {
    return this.http.post<ApiResponse<GeneratedPage>>(this.base, { title, prompt }).pipe(map((r) => r.data as GeneratedPage));
  }
}
