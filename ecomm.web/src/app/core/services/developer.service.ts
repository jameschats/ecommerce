import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import {
  ApiKey, CreateApiKeyRequest, CreatedApiKey,
  CreateWebhookSubscriptionRequest, CreatedWebhookSubscription, WebhookSubscription,
} from '../models/developer.model';

@Injectable({ providedIn: 'root' })
export class ApiKeysService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/api-keys`;

  list(): Observable<ApiKey[]> {
    return this.http.get<ApiResponse<ApiKey[]>>(this.base).pipe(map((r) => r.data ?? []));
  }
  scopes(): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.base}/scopes`).pipe(map((r) => r.data ?? []));
  }
  create(body: CreateApiKeyRequest): Observable<CreatedApiKey> {
    return this.http.post<ApiResponse<CreatedApiKey>>(this.base, body).pipe(map((r) => r.data as CreatedApiKey));
  }
  revoke(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class WebhooksService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/webhooks`;

  list(): Observable<WebhookSubscription[]> {
    return this.http.get<ApiResponse<WebhookSubscription[]>>(this.base).pipe(map((r) => r.data ?? []));
  }
  events(): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.base}/events`).pipe(map((r) => r.data ?? []));
  }
  create(body: CreateWebhookSubscriptionRequest): Observable<CreatedWebhookSubscription> {
    return this.http.post<ApiResponse<CreatedWebhookSubscription>>(this.base, body).pipe(map((r) => r.data as CreatedWebhookSubscription));
  }
  setActive(id: number, isActive: boolean): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/${id}/active`, isActive);
  }
  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`);
  }
}
