import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface SubmitContactRequest {
  name: string;
  email: string;
  phone?: string | null;
  subject?: string | null;
  body: string;
  sourceUrl?: string | null;
  /** Honeypot — must stay empty. Hidden from real users. */
  website?: string | null;
}

export interface StoreContact {
  storeEmail: string | null;
  storePhone: string | null;
  storeAddress: string | null;
}

export interface ContactMessage {
  contactMessageId: number;
  name: string;
  email: string;
  phone: string | null;
  subject: string | null;
  body: string;
  sourceUrl: string | null;
  status: string;
  createdAt: string;
  handledAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class ContactService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  submit(body: SubmitContactRequest): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/contact`, body);
  }

  getStoreContact(): Observable<StoreContact> {
    return this.unwrap(this.http.get<ApiResponse<StoreContact>>(`${this.base}/catalog/store-contact`));
  }

  list(status?: string, page = 1, pageSize = 20): Observable<PagedResult<ContactMessage>> {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) q.set('status', status);
    return this.unwrap(this.http.get<ApiResponse<PagedResult<ContactMessage>>>(`${this.base}/admin/messages?${q}`));
  }

  setStatus(id: number, status: string): Observable<ContactMessage> {
    return this.unwrap(this.http.put<ApiResponse<ContactMessage>>(`${this.base}/admin/messages/${id}/status?status=${status}`, {}));
  }
}
