import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { API_BASE_URL } from '../api.config';

const VISITOR_COOKIE = 'dcs_vid';
const SESSION_KEY = 'dcs_sid';
const VISITOR_COOKIE_DAYS = 365;

/**
 * First-party page-view beacon (Features/Analytics on the API side — no third-party analytics
 * involved). VisitorId is a persistent cookie so the admin can see new-vs-returning; SessionId
 * lives in sessionStorage so it naturally resets per browser tab/visit. Fire-and-forget: a
 * failed beacon call must never affect the page.
 */
@Injectable({ providedIn: 'root' })
export class TrackingService {
  private readonly http = inject(HttpClient);
  private readonly doc = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly endpoint = `${API_BASE_URL}/analytics/track`;

  track(path: string): void {
    if (!this.isBrowser) return;
    const visitorId = this.getOrCreateVisitorId();
    const sessionId = this.getOrCreateSessionId();
    const referrer = this.doc.referrer || null;
    this.http.post(this.endpoint, { visitorId, sessionId, path, referrer }).subscribe({ error: () => {} });
  }

  private getOrCreateVisitorId(): string {
    const existing = this.readCookie(VISITOR_COOKIE);
    if (existing) return existing;
    const id = crypto.randomUUID();
    this.writeCookie(VISITOR_COOKIE, id, VISITOR_COOKIE_DAYS);
    return id;
  }

  private getOrCreateSessionId(): string {
    const existing = sessionStorage.getItem(SESSION_KEY);
    if (existing) return existing;
    const id = crypto.randomUUID();
    sessionStorage.setItem(SESSION_KEY, id);
    return id;
  }

  private readCookie(name: string): string | null {
    const match = this.doc.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  }

  private writeCookie(name: string, value: string, days: number): void {
    const maxAge = days * 24 * 60 * 60;
    this.doc.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAge}; SameSite=Lax`;
  }
}
