import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { AppNotification } from '../models/notification.model';
import { AuthService } from './auth.service';
import { TokenStorageService } from './token-storage.service';

const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '');

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly storage = inject(TokenStorageService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly base = `${API_BASE_URL}/notifications`;

  readonly notifications = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);
  readonly hasUnread = computed(() => this.unreadCount() > 0);

  private connection: HubConnection | null = null;

  constructor() {
    // Connect/refresh when signed in (browser only); tear down on sign-out.
    effect(() => {
      if (!this.isBrowser) return;
      if (this.auth.isAuthenticated()) {
        this.loadUnreadCount();
        this.connect();
      } else {
        this.disconnect();
        this.notifications.set([]);
        this.unreadCount.set(0);
      }
    });
  }

  // ---- REST ----
  loadUnreadCount(): void {
    this.http.get<ApiResponse<number>>(`${this.base}/unread-count`).subscribe({
      next: (r) => this.unreadCount.set(r.data ?? 0),
      error: () => {},
    });
  }

  load(): Observable<AppNotification[]> {
    return this.http.get<ApiResponse<AppNotification[]>>(this.base).pipe(map((r) => r.data ?? []));
  }

  markRead(id: number): void {
    const before = this.notifications();
    this.notifications.set(before.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
    this.recomputeUnread();
    this.http.post<ApiResponse<unknown>>(`${this.base}/${id}/read`, {}).subscribe({ error: () => {} });
  }

  markAllRead(): void {
    this.notifications.set(this.notifications().map((n) => ({ ...n, isRead: true })));
    this.unreadCount.set(0);
    this.http.post<ApiResponse<unknown>>(`${this.base}/read-all`, {}).subscribe({ error: () => {} });
  }

  private recomputeUnread(): void {
    this.unreadCount.set(this.notifications().filter((n) => !n.isRead).length);
  }

  // ---- Real-time (SignalR) ----
  private async connect(): Promise<void> {
    if (this.connection) return;
    const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');
    const conn = new HubConnectionBuilder()
      .withUrl(`${API_ORIGIN}/hubs/notifications`, { accessTokenFactory: () => this.storage.getAccessToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on('notification', (n: AppNotification) => {
      this.notifications.update((list) => [n, ...list].slice(0, 30));
      if (!n.isRead) this.unreadCount.update((c) => c + 1);
    });

    this.connection = conn;
    try { await conn.start(); } catch { this.connection = null; }
  }

  private disconnect(): void {
    this.connection?.stop().catch(() => {});
    this.connection = null;
  }
}
