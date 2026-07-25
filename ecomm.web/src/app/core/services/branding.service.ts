import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface SiteBranding {
  browserTitle: string;
  faviconUrl: string;
  siteName: string;
  siteNameAccent: string;
  logoUrl: string;
  footerLogoUrl: string;
  announcementText: string;
  priceValidUpto: string;
}

const EMPTY: SiteBranding = {
  browserTitle: '', faviconUrl: '', siteName: '', siteNameAccent: '',
  logoUrl: '', footerLogoUrl: '',
  announcementText: '', priceValidUpto: '',
};

/**
 * Site identity, configured from admin: browser tab title and favicon, plus the storefront
 * name and header logo.
 *
 * All four used to be baked into the build — the tab title and favicon in index.html, the
 * name as literal markup in app.html — so changing any of them meant a deploy. They are now
 * settings, applied here. Blank means "keep the built-in default", so an unconfigured shop
 * looks exactly as it did before.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly title = inject(Title);
  private readonly doc = inject(DOCUMENT);

  /** The configured title, or '' — SeoService uses it as the suffix for page titles. */
  readonly browserTitle = signal('');

  /**
   * Header/footer wordmark and logo, or '' for the built-in default. Signals rather than
   * plain fields because the header reads them during SSR and again after hydration.
   */
  readonly siteName = signal('');
  readonly logoUrl = signal('');

  /**
   * Tail of the wordmark drawn in the primary colour, joined to siteName with no separator.
   * The built-in mark was two-tone ("Calendar" + "Shop"); a single configurable string has
   * no split point, so the accent lives in its own setting rather than being guessed.
   */
  readonly siteNameAccent = signal('');

  /**
   * Logo for the dark footer, falling back to the header one. The header sits on white and
   * the footer on slate-900, so a single image cannot suit both: remove the white plate that
   * makes it a box in the footer, and dark artwork vanishes there instead.
   */
  readonly footerLogoUrl = signal('');

  /**
   * Header announcement. Reuses the QuickOrder.AnnouncementText setting that already
   * existed in admin but was never displayed anywhere, with the price-validity date
   * appended so the two read as one sentence.
   */
  readonly announcement = signal('');

  private branding$?: Observable<SiteBranding>;

  load(): Observable<SiteBranding> {
    this.branding$ ??= this.http
      .get<ApiResponse<SiteBranding>>(`${API_BASE_URL}/site/branding`)
      .pipe(
        map((r) => r.data ?? EMPTY),
        tap((b) => this.apply(b)),
        catchError(() => of(EMPTY)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.branding$;
  }

  private apply(b: SiteBranding): void {
    this.siteName.set(b.siteName?.trim() ?? '');
    this.siteNameAccent.set(b.siteNameAccent?.trim() ?? '');
    this.logoUrl.set(b.logoUrl?.trim() ?? '');
    // Blank means "reuse the header logo", so every shop configured before this existed
    // keeps rendering exactly as it did.
    this.footerLogoUrl.set(b.footerLogoUrl?.trim() || (b.logoUrl?.trim() ?? ''));

    const parts = [b.announcementText?.trim(), b.priceValidUpto?.trim() ? `Prices valid up to ${b.priceValidUpto.trim()}` : '']
      .filter((p) => p);
    this.announcement.set(parts.join(' · '));

    if (b.browserTitle?.trim()) {
      this.browserTitle.set(b.browserTitle.trim());
      // Works under SSR too — Angular's Title service writes into the rendered document,
      // so the correct tab text is in the server HTML rather than appearing after hydration.
      this.title.setTitle(b.browserTitle.trim());
    }

    if (b.faviconUrl?.trim()) this.applyFavicon(b.faviconUrl.trim());
  }

  private applyFavicon(url: string): void {
    const head = this.doc.head;
    if (!head) return;

    // Replace every existing icon link rather than adding one. Browsers pick among
    // multiple <link rel="icon"> unpredictably, so leaving the original favicon.ico in
    // place would make the change appear to work only sometimes.
    head.querySelectorAll("link[rel~='icon']").forEach((el) => el.remove());

    const link = this.doc.createElement('link');
    link.setAttribute('rel', 'icon');
    link.setAttribute('href', url);
    head.appendChild(link);
  }
}
