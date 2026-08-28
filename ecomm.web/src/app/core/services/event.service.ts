import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, NgZone, PLATFORM_ID, inject } from '@angular/core';
import { API_BASE_URL } from '../api.config';

interface QueuedEvent { type: string; productId?: number | null; metadata?: string | null; }

/**
 * Storefront behavioural-event capture (AI Commerce data layer). Buffers events client-side and flushes
 * them in small batches — never one request per interaction — so it stays off the critical path. A
 * first-party visitor id (localStorage) groups a visitor's events before they log in; it is sent to our
 * own API only, never to any third party. No-ops entirely during SSR.
 */
@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly url = `${API_BASE_URL}/events`;

  private queue: QueuedEvent[] = [];
  private visitorId = '';
  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    if (!this.isBrowser) return;
    this.visitorId = this.ensureVisitorId();
    // Flush periodically and when the tab is hidden/closed (best-effort via sendBeacon).
    this.zone.runOutsideAngular(() => {
      this.timer = setInterval(() => this.flush(false), 8000);
      window.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') this.flush(true); });
      window.addEventListener('pagehide', () => this.flush(true));
    });
  }

  /** The first-party visitor id, for per-visitor recommendation calls. Empty when storage is unavailable. */
  getVisitorId(): string { return this.visitorId; }

  view(productId: number): void { this.push({ type: 'view', productId }); }
  search(term: string): void { const t = term?.trim(); if (t) this.push({ type: 'search', metadata: t.slice(0, 120) }); }
  addToCart(productId: number, quantity: number): void { this.push({ type: 'add-to-cart', productId, metadata: `qty:${quantity}` }); }
  removeFromCart(productId: number): void { this.push({ type: 'remove-from-cart', productId }); }

  private push(e: QueuedEvent): void {
    if (!this.isBrowser || !this.visitorId) return;
    this.queue.push(e);
    if (this.queue.length >= 20) this.flush(false);
  }

  private flush(useBeacon: boolean): void {
    if (!this.queue.length) return;
    const batch = this.queue.splice(0, this.queue.length);
    const body = { sessionId: this.visitorId, events: batch };
    try {
      if (useBeacon && navigator.sendBeacon) {
        navigator.sendBeacon(this.url, new Blob([JSON.stringify(body)], { type: 'application/json' }));
        return;
      }
    } catch { /* fall through to http */ }
    // HttpClient path also carries the auth token (interceptor), so a logged-in visitor is attributed.
    this.http.post(this.url, body).subscribe({ error: () => { /* analytics-grade: drop on failure */ } });
  }

  private ensureVisitorId(): string {
    try {
      let id = localStorage.getItem('ecomm_vid');
      if (!id) {
        id = (crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`).slice(0, 64);
        localStorage.setItem('ecomm_vid', id);
      }
      return id;
    } catch {
      return '';   // private mode / storage blocked → capture simply disabled
    }
  }
}
