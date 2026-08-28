import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { Cart } from '../models/cart.model';
import { AuthService } from './auth.service';
import { EventService } from './event.service';
import { TokenStorageService } from './token-storage.service';

/**
 * Client cart state. Sends the guest cart token (X-Cart-Token) on every call;
 * for logged-in users the JWT (added by the auth interceptor) wins server-side.
 * On login, the guest cart is merged into the user cart automatically.
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorageService);
  private readonly auth = inject(AuthService);
  private readonly events = inject(EventService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly base = `${API_BASE_URL}/cart`;

  readonly cart = signal<Cart | null>(null);
  readonly itemCount = computed(() => this.cart()?.itemCount ?? 0);
  readonly subtotal = computed(() => this.cart()?.subtotal ?? 0);
  readonly items = computed(() => this.cart()?.items ?? []);

  /** Mini-cart flyout open state (opened by the header cart icon + after add-to-cart). */
  readonly drawerOpen = signal(false);
  openDrawer(): void { this.drawerOpen.set(true); }
  closeDrawer(): void { this.drawerOpen.set(false); }

  private lastUserId: number | null | undefined = undefined;

  constructor() {
    if (this.isBrowser) {
      effect(() => {
        const userId = this.auth.currentUser()?.userId ?? null;
        const prev = this.lastUserId;
        this.lastUserId = userId;
        if (prev === undefined) { this.reload(); return; }      // first run
        if (userId && userId !== prev) { this.mergeThenReload(); } // just logged in
        else { this.reload(); }                                  // logged out / switched
      });
    }
  }

  private headers(): Record<string, string> {
    const token = this.storage.getCartToken();
    return token ? { 'X-Cart-Token': token } : {};
  }

  reload(): void {
    if (!this.isBrowser) return;
    this.http.get<ApiResponse<Cart>>(this.base, { headers: this.headers() })
      .pipe(map((r) => r.data!))
      .subscribe({ next: (c) => this.cart.set(c), error: () => {} });
  }

  add(productId: number, productVariantId: number | null, quantity: number): Observable<Cart> {
    this.events.addToCart(productId, quantity);
    return this.mutate(this.http.post<ApiResponse<Cart>>(`${this.base}/items`, { productId, productVariantId, quantity }, { headers: this.headers() }));
  }

  updateQty(cartItemId: number, quantity: number): Observable<Cart> {
    return this.mutate(this.http.put<ApiResponse<Cart>>(`${this.base}/items/${cartItemId}`, { quantity }, { headers: this.headers() }));
  }

  remove(cartItemId: number): Observable<Cart> {
    const pid = this.items().find((i) => i.cartItemId === cartItemId)?.productId;
    if (pid) this.events.removeFromCart(pid);
    return this.mutate(this.http.delete<ApiResponse<Cart>>(`${this.base}/items/${cartItemId}`, { headers: this.headers() }));
  }

  clear(): Observable<Cart> {
    return this.mutate(this.http.delete<ApiResponse<Cart>>(this.base, { headers: this.headers() }));
  }

  setNotes(notes: string): Observable<Cart> {
    return this.mutate(this.http.put<ApiResponse<Cart>>(`${this.base}/notes`, { notes }, { headers: this.headers() }));
  }

  private mergeThenReload(): void {
    this.http.post<ApiResponse<Cart>>(`${this.base}/merge`, {}, { headers: this.headers() })
      .pipe(map((r) => r.data!))
      .subscribe({ next: (c) => this.cart.set(c), error: () => this.reload() });
  }

  private mutate(req$: Observable<ApiResponse<Cart>>): Observable<Cart> {
    return req$.pipe(map((r) => r.data!), tap((c) => this.cart.set(c)));
  }
}
