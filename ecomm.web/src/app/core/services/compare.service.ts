import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';

/** Client-side "compare products" list (localStorage, no login needed), capped at 4 —
 *  same SSR-safe pattern as RecentlyViewedService, but reactive (a signal, not a plain
 *  getter) since several product cards + a global floating bar must stay in sync live. */
@Injectable({ providedIn: 'root' })
export class CompareService {
  private readonly key = 'ecomm.compare';
  private readonly max = 4;
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly ids = signal<number[]>(this.isBrowser ? this.read() : []);
  readonly isFull = computed(() => this.ids().length >= this.max);

  has(productId: number): boolean {
    return this.ids().includes(productId);
  }

  toggle(productId: number): void {
    if (!this.isBrowser) return;
    const current = this.ids();
    if (current.includes(productId)) this.write(current.filter((id) => id !== productId));
    else if (current.length < this.max) this.write([...current, productId]);
  }

  remove(productId: number): void {
    this.write(this.ids().filter((id) => id !== productId));
  }

  clear(): void {
    this.write([]);
  }

  private write(ids: number[]): void {
    this.ids.set(ids);
    localStorage.setItem(this.key, JSON.stringify(ids));
  }

  private read(): number[] {
    try {
      const raw = localStorage.getItem(this.key);
      const ids = raw ? JSON.parse(raw) : [];
      return Array.isArray(ids) ? ids.slice(0, this.max) : [];
    } catch {
      return [];
    }
  }
}
