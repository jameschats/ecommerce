import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { ProductListItem } from '../models/catalog.model';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class WishlistService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${API_BASE_URL}/wishlist`;

  /** Product ids currently wishlisted — drives the heart toggle state everywhere. */
  readonly ids = signal<Set<number>>(new Set());
  readonly count = computed(() => this.ids().size);

  constructor() {
    // Load ids whenever the user signs in; clear on sign-out.
    effect(() => {
      if (this.auth.isAuthenticated()) this.loadIds();
      else this.ids.set(new Set());
    });
  }

  has(productId: number): boolean {
    return this.ids().has(productId);
  }

  private loadIds(): void {
    this.http.get<ApiResponse<number[]>>(`${this.base}/ids`).subscribe({
      next: (r) => this.ids.set(new Set(r.data ?? [])),
      error: () => {},
    });
  }

  list(): Observable<ProductListItem[]> {
    return this.http.get<ApiResponse<ProductListItem[]>>(this.base).pipe(map((r) => r.data ?? []));
  }

  add(productId: number): Observable<unknown> {
    this.ids.update((s) => new Set(s).add(productId)); // optimistic
    return this.http.post<ApiResponse<unknown>>(`${this.base}/${productId}`, {});
  }

  remove(productId: number): Observable<unknown> {
    this.ids.update((s) => { const n = new Set(s); n.delete(productId); return n; }); // optimistic
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${productId}`);
  }
}
