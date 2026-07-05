import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface ThemeDto {
  themeId: number;
  name: string;
  settings: Record<string, string>;
}

/**
 * Loads the active theme and applies it as CSS variables on :root.
 * SSR-safe (sets the variables on the server document too → no flash of
 * default colors). Tailwind's `primary` utilities read these variables.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly http = inject(HttpClient);
  private readonly doc = inject(DOCUMENT);

  /** The store's logo URL (empty = show the text mark). Set from the theme. */
  readonly logo = signal<string | null>(null);
  readonly storeName = signal<string>('');

  load(): Observable<void> {
    return this.http.get<ApiResponse<ThemeDto>>(`${API_BASE_URL}/theme`).pipe(
      tap((r) => this.apply(r.data?.settings ?? {})),
      map(() => void 0),
      catchError(() => of(void 0)),
    );
  }

  apply(settings: Record<string, string>): void {
    this.logo.set(settings['Logo'] || null);
    if (settings['StoreName']) this.storeName.set(settings['StoreName']);
    const root = this.doc.documentElement;
    const primary = settings['PrimaryColor'] || '#2563eb';
    root.style.setProperty('--color-primary', primary);
    root.style.setProperty('--color-primary-dark', this.darken(primary, 0.85));
    if (settings['SecondaryColor']) root.style.setProperty('--color-secondary', settings['SecondaryColor']);
    if (settings['Font']) root.style.setProperty('--app-font', `${settings['Font']}, system-ui, sans-serif`);
  }

  private darken(hex: string, factor: number): string {
    const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex.trim());
    if (!m) return hex;
    const clamp = (n: number) => Math.max(0, Math.min(255, Math.round(n)));
    const r = clamp(parseInt(m[1], 16) * factor);
    const g = clamp(parseInt(m[2], 16) * factor);
    const b = clamp(parseInt(m[3], 16) * factor);
    return `#${[r, g, b].map((n) => n.toString(16).padStart(2, '0')).join('')}`;
  }
}
