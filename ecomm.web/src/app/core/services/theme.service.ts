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

/** One section from the published theme (a shared zone entry or template section). */
export interface ThemeSection {
  id: number;
  sectionType: string;
  kind: string;
  title: string | null;
  settings: string | null;
  blocks: string | null;
  displayOrder: number;
  isVisible: boolean;
}

/** The published theme bundle: global settings + the shared header/footer/announcement zones. */
export interface ThemeBundle {
  themeId: number;
  status: string;
  settings: Record<string, string>;
  header: ThemeSection[];
  footer: ThemeSection[];
  announcement: ThemeSection[];
}

/**
 * Loads the published theme bundle (settings + shared zones) and applies the
 * settings as CSS variables on :root. SSR-safe (sets the variables on the server
 * document too → no flash of default colors). Tailwind's `primary` utilities read
 * these variables. The zone signals (header/footer/announcement) drive the
 * data-driven store chrome; when a zone is empty the app falls back to its
 * built-in chrome, so nothing breaks while themes are being authored.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly http = inject(HttpClient);
  private readonly doc = inject(DOCUMENT);

  /** The store's logo URL (empty = show the text mark). Set from the theme. */
  readonly logo = signal<string | null>(null);
  readonly storeName = signal<string>('');

  /** Published-theme shared zones (empty = use the app's built-in chrome). */
  readonly announcement = signal<ThemeSection[]>([]);
  readonly header = signal<ThemeSection[]>([]);
  readonly footer = signal<ThemeSection[]>([]);

  /**
   * The published theme's section list for a page-type template (product/collection/cart/search/…).
   * Empty when the theme defines no such template → the caller renders its built-in layout (fallback).
   */
  /** Draft-preview token from the URL (?preview=…) — set when an admin previews a theme; '' normally. */
  private previewParams(): Record<string, string> {
    const search = this.doc.defaultView?.location?.search ?? '';
    const m = /[?&]preview=([^&]+)/.exec(search);
    return m ? { preview: decodeURIComponent(m[1]) } : {};
  }

  getTemplate(key: string): Observable<ThemeSection[]> {
    return this.http.get<ApiResponse<{ templateKey: string; sections: ThemeSection[] }>>(
      `${API_BASE_URL}/storefront/template/${key}`, { params: this.previewParams() }).pipe(
      map((r) => (r.data?.sections ?? []).filter((s) => s.isVisible)),
      catchError(() => of([] as ThemeSection[])),
    );
  }

  load(): Observable<void> {
    return this.http.get<ApiResponse<ThemeBundle>>(`${API_BASE_URL}/storefront/theme`, { params: this.previewParams() }).pipe(
      tap((r) => {
        const b = r.data;
        this.apply(b?.settings ?? {});
        this.announcement.set((b?.announcement ?? []).filter((s) => s.isVisible));
        this.header.set((b?.header ?? []).filter((s) => s.isVisible));
        this.footer.set((b?.footer ?? []).filter((s) => s.isVisible));
      }),
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
    // Typography + layout — set only when the theme provides them (additive for the S4 editor).
    if (settings['HeadingFont']) root.style.setProperty('--app-heading-font', `${settings['HeadingFont']}, system-ui, sans-serif`);
    if (settings['BaseFontSize']) root.style.setProperty('--app-font-size', this.cssLen(settings['BaseFontSize']));
    if (settings['ContainerWidth']) root.style.setProperty('--app-container', this.cssLen(settings['ContainerWidth']));
    root.style.setProperty('--btn-radius', this.buttonRadius(settings['ButtonStyle']));
    if (settings['Favicon']) this.setFavicon(settings['Favicon']);
  }

  /** Accepts "16", "16px" or "62rem" → a valid CSS length (bare numbers become px). */
  private cssLen(v: string): string {
    const t = (v || '').trim();
    return /^[0-9.]+$/.test(t) ? `${t}px` : t;
  }

  private setFavicon(href: string): void {
    let link = this.doc.querySelector("link[rel='icon']") as HTMLLinkElement | null;
    if (!link) {
      link = this.doc.createElement('link');
      link.setAttribute('rel', 'icon');
      this.doc.head.appendChild(link);
    }
    link.setAttribute('href', href);
  }

  private buttonRadius(style: string | undefined): string {
    return style === 'pill' ? '9999px' : style === 'square' ? '0' : '0.5rem';
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
