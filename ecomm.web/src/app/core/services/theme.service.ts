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
    return this.getTemplateInfo(key).pipe(map((t) => t.sections));
  }

  /** Template sections + whether they're authored (theme-driven) vs the transitional Home fallback. */
  getTemplateInfo(key: string): Observable<{ sections: ThemeSection[]; authored: boolean }> {
    return this.http.get<ApiResponse<{ templateKey: string; authored: boolean; sections: ThemeSection[] }>>(
      `${API_BASE_URL}/storefront/template/${key}`, { params: this.previewParams() }).pipe(
      map((r) => ({ sections: (r.data?.sections ?? []).filter((s) => s.isVisible), authored: r.data?.authored ?? false })),
      catchError(() => of({ sections: [] as ThemeSection[], authored: false })),
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
    const set = (k: string, v: string | null | undefined) => { if (v) root.style.setProperty(k, v); };

    // --- Colour ---
    const primary = settings['PrimaryColor'] || '#2563eb';
    root.style.setProperty('--color-primary', primary);
    root.style.setProperty('--color-primary-dark', this.darken(primary, 0.85));
    set('--color-secondary', settings['SecondaryColor']);
    set('--color-accent', settings['AccentColor'] || settings['PrimaryColor']);

    // --- Typography (families are also web-font-loaded below) ---
    if (settings['Font']) set('--app-font', `'${settings['Font']}', system-ui, sans-serif`);
    if (settings['HeadingFont']) set('--app-heading-font', `'${settings['HeadingFont']}', system-ui, sans-serif`);
    set('--app-font-size', settings['BaseFontSize'] ? this.cssLen(settings['BaseFontSize']) : null);
    set('--app-line-height', settings['LineHeight']);
    set('--app-heading-weight', settings['HeadingWeight']);
    set('--app-heading-spacing', settings['HeadingSpacing']);
    set('--app-heading-transform', settings['HeadingTransform']);
    this.loadFonts([settings['Font'], settings['HeadingFont']]);

    // --- Layout & density ---
    set('--app-container', settings['ContainerWidth'] ? this.cssLen(settings['ContainerWidth']) : null);
    set('--app-section-pad', this.density(settings['Density']));

    // --- Shape (radius scale) + button/card style ---
    const r = this.radiusScale(settings['Radius']);
    root.style.setProperty('--radius-card', r.card);
    root.style.setProperty('--radius-input', r.input);
    root.style.setProperty('--radius-img', r.img);
    root.style.setProperty('--radius-btn', this.buttonRadius(settings['ButtonStyle'], r.btn));
    root.style.setProperty('--btn-radius', this.buttonRadius(settings['ButtonStyle'], r.btn));   // back-compat
    const card = this.cardStyle(settings['CardStyle']);
    root.style.setProperty('--card-border', card.border);
    root.style.setProperty('--card-shadow', card.shadow);
    root.style.setProperty('--card-hover-shadow', card.hover);

    if (settings['Favicon']) this.setFavicon(settings['Favicon']);
  }

  // ---- token mappers ----
  private density(v: string | undefined): string | null {
    return v === 'compact' ? '1.5rem' : v === 'spacious' ? '4rem' : v === 'cozy' ? '2.75rem' : null;
  }
  private radiusScale(v: string | undefined): { card: string; btn: string; input: string; img: string } {
    switch (v) {
      case 'sharp': return { card: '0', btn: '0', input: '0', img: '0' };
      case 'round': return { card: '1.25rem', btn: '0.9rem', input: '0.9rem', img: '1.25rem' };
      default: return { card: '0.75rem', btn: '0.5rem', input: '0.5rem', img: '0.75rem' };   // soft
    }
  }
  private cardStyle(v: string | undefined): { border: string; shadow: string; hover: string } {
    switch (v) {
      case 'shadow': return { border: 'none', shadow: '0 1px 3px rgb(0 0 0 / 0.08)', hover: '0 10px 28px rgb(0 0 0 / 0.12)' };
      case 'elevated': return { border: '1px solid rgb(241 245 249)', shadow: '0 4px 16px rgb(0 0 0 / 0.06)', hover: '0 14px 32px rgb(0 0 0 / 0.12)' };
      case 'flat': return { border: 'none', shadow: 'none', hover: 'none' };
      default: return { border: '1px solid rgb(226 232 240)', shadow: 'none', hover: '0 8px 24px rgb(0 0 0 / 0.08)' };   // bordered
    }
  }

  /** Web fonts a theme may use (allowlisted — we only inject known Google Fonts). */
  private static readonly KNOWN_FONTS = new Set([
    'Inter', 'Poppins', 'Roboto', 'Montserrat', 'Lato', 'Open Sans', 'DM Sans', 'Work Sans', 'Nunito',
    'Playfair Display', 'Cormorant Garamond', 'Lora', 'Oswald', 'Bebas Neue', 'Archivo', 'Space Grotesk',
  ]);
  private readonly loadedFonts = new Set<string>();

  /** Inject a Google-Fonts stylesheet for the theme's fonts (once each). Without this the CSS var alone falls back to system fonts. */
  private loadFonts(families: (string | undefined)[]): void {
    const toLoad = families.filter((f): f is string => !!f && ThemeService.KNOWN_FONTS.has(f) && !this.loadedFonts.has(f));
    if (!toLoad.length) return;
    for (const f of toLoad) this.loadedFonts.add(f);
    const query = toLoad.map((f) => `family=${encodeURIComponent(f)}:wght@400;500;600;700`).join('&');
    const href = `https://fonts.googleapis.com/css2?${query}&display=swap`;
    if (this.doc.querySelector(`link[href="${href}"]`)) return;
    const link = this.doc.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    this.doc.head.appendChild(link);
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

  private buttonRadius(style: string | undefined, fallback = '0.5rem'): string {
    return style === 'pill' ? '9999px' : style === 'square' ? '0' : fallback;
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
