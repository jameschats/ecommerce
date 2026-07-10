import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface NotificationTemplate {
  id: number;
  code: string;
  label: string;
  channel: string;
  subject: string | null;
  body: string | null;
  isActive: boolean;
  updatedAt: string | null;
}

export interface NotificationSender {
  senderName: string | null;
  replyToEmail: string | null;
}

@Injectable({ providedIn: 'root' })
export class NotificationTemplatesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/notification-templates`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  list(): Observable<NotificationTemplate[]> { return this.unwrap(this.http.get<ApiResponse<NotificationTemplate[]>>(this.base)); }
  update(id: number, body: { subject: string | null; body: string | null; isActive: boolean }): Observable<NotificationTemplate> {
    return this.unwrap(this.http.put<ApiResponse<NotificationTemplate>>(`${this.base}/${id}`, body));
  }
  getSender(): Observable<NotificationSender> { return this.unwrap(this.http.get<ApiResponse<NotificationSender>>(`${this.base}/sender`)); }
  updateSender(body: NotificationSender): Observable<NotificationSender> {
    return this.unwrap(this.http.put<ApiResponse<NotificationSender>>(`${this.base}/sender`, body));
  }
}
