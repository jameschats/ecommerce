import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { AuthUser } from '../models/auth.model';

/** Persists the JWT session in localStorage. SSR-safe (no-op on the server). */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly accessKey = 'ecomm.access';
  private readonly refreshKey = 'ecomm.refresh';
  private readonly userKey = 'ecomm.user';
  private readonly cartKey = 'ecomm.cart';
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Stable per-browser cart token for guest carts (lazily created). SSR-safe. */
  getCartToken(): string | null {
    if (!this.isBrowser) return null;
    let token = localStorage.getItem(this.cartKey);
    if (!token) {
      token = crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
      localStorage.setItem(this.cartKey, token);
    }
    return token;
  }

  getAccessToken(): string | null {
    return this.isBrowser ? localStorage.getItem(this.accessKey) : null;
  }

  getRefreshToken(): string | null {
    return this.isBrowser ? localStorage.getItem(this.refreshKey) : null;
  }

  getUser(): AuthUser | null {
    if (!this.isBrowser) return null;
    const raw = localStorage.getItem(this.userKey);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  }

  setSession(accessToken: string, refreshToken: string, user: AuthUser): void {
    if (!this.isBrowser) return;
    localStorage.setItem(this.accessKey, accessToken);
    localStorage.setItem(this.refreshKey, refreshToken);
    localStorage.setItem(this.userKey, JSON.stringify(user));
  }

  clear(): void {
    if (!this.isBrowser) return;
    localStorage.removeItem(this.accessKey);
    localStorage.removeItem(this.refreshKey);
    localStorage.removeItem(this.userKey);
  }
}
