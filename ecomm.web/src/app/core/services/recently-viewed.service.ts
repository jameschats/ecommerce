import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';

/** Tracks recently-viewed product IDs in the browser (most-recent-first, capped, deduped).
 *  No account/login needed — pure localStorage, same SSR-safe pattern as TokenStorageService. */
@Injectable({ providedIn: 'root' })
export class RecentlyViewedService {
  private readonly key = 'ecomm.recentlyViewed';
  private readonly max = 12;
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Record a product view — moves it to the front if already present, trims to `max`. */
  record(productId: number): void {
    if (!this.isBrowser) return;
    const ids = this.getIds().filter((id) => id !== productId);
    ids.unshift(productId);
    localStorage.setItem(this.key, JSON.stringify(ids.slice(0, this.max)));
  }

  /** Most-recent-first. Empty (never SSR'd) on the server. */
  getIds(): number[] {
    if (!this.isBrowser) return [];
    try {
      const raw = localStorage.getItem(this.key);
      const ids = raw ? JSON.parse(raw) : [];
      return Array.isArray(ids) ? ids : [];
    } catch {
      return [];
    }
  }
}
