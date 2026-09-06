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
  siteNameSize: string;
  logoUrl: string;
  footerLogoUrl: string;
  footerDescription: string;
  announcementText: string;
  priceValidUpto: string;
  /** One source for the contact page, the footer and the storefront's structured data. */
  contactAddress: string;
  contactMobile1: string;
  contactMobile2: string;
  contactLandline1: string;
  contactLandline2: string;
  contactEmail: string;
  contactHours: string;
  contactCity: string;
  /** Footer social icons. Each is shown only when its Enabled flag is true and Url is set. */
  socialFacebookUrl: string;
  socialFacebookEnabled: boolean;
  socialInstagramUrl: string;
  socialInstagramEnabled: boolean;
  socialXUrl: string;
  socialXEnabled: boolean;
  socialLinkedinUrl: string;
  socialLinkedinEnabled: boolean;
}

const EMPTY: SiteBranding = {
  browserTitle: '', faviconUrl: '', siteName: '', siteNameAccent: '', siteNameSize: '',
  logoUrl: '', footerLogoUrl: '', footerDescription: '',
  announcementText: '', priceValidUpto: '',
  contactAddress: '', contactMobile1: '', contactMobile2: '', contactLandline1: '', contactLandline2: '',
  contactEmail: '', contactHours: '', contactCity: '',
  socialFacebookUrl: '', socialFacebookEnabled: false,
  socialInstagramUrl: '', socialInstagramEnabled: false,
  socialXUrl: '', socialXEnabled: false,
  socialLinkedinUrl: '', socialLinkedinEnabled: false,
};

export interface SocialLink {
  label: string;
  url: string;
}

/** One run of plain text, or a run recognized as a URL/domain (href set). */
export interface TextSegment {
  text: string;
  href: string | null;
}

const FOOTER_DESCRIPTION_FALLBACK =
  'Custom 2026 calendars — wall, desk, pocket & more. Personalized with your photos, brand name and logo.';

/** Matches an http(s) URL, a www. address, or a bare domain like "dailycalendarstore.in". */
const URL_PATTERN =
  /((?:https?:\/\/)?(?:www\.)?[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*\.[a-zA-Z]{2,}(?:\/[^\s]*)?)/g;

/**
 * Splits admin-entered text into paragraphs (blank line = new paragraph) and, within each,
 * turns anything URL-shaped into a clickable segment — admin types plain text in the settings
 * textarea, not markup, so a bare "dailycalendarstore.in" should still render as a link.
 */
function linkifyParagraphs(text: string): TextSegment[][] {
  return text
    .split(/\n\s*\n/)
    .map((p) => p.trim())
    .filter((p) => p.length > 0)
    .map((paragraph) =>
      paragraph
        .split(URL_PATTERN)
        .map((part, i): TextSegment => ({
          text: part,
          // split() with a capturing group interleaves the captured matches at odd indices.
          href: i % 2 === 1 ? (part.startsWith('http') ? part : `https://${part}`) : null,
        }))
        .filter((seg) => seg.text.length > 0),
    );
}

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

  /** Replaces the hardcoded "Custom 2026 calendars..." blurb under the footer logo. */
  readonly footerDescription = signal('');

  /**
   * The same text as {@link footerDescription}, pre-split into paragraphs with any URL-shaped
   * text turned into a link — the footer template renders this rather than the raw string so
   * a two-paragraph "About us" with a bare domain in it reads and links correctly.
   */
  readonly footerParagraphs = signal<TextSegment[][]>([]);

  /**
   * Wordmark font size as a CSS length, or null to keep the built-in text-xl. How big the
   * name wants to be depends on the name itself and on whether a logo sits beside it, so
   * it is a setting rather than a fixed class.
   */
  readonly siteNameSize = signal<string | null>(null);

  /**
   * Header announcement. Reuses the QuickOrder.AnnouncementText setting that already
   * existed in admin but was never displayed anywhere, with the price-validity date
   * appended so the two read as one sentence.
   */
  readonly announcement = signal('');

  /**
   * How to reach the shop. One source for the contact page, the footer and the storefront's
   * structured data — the site used to say Chennai on the contact page while its structured
   * data said Madurai, and an AI asked where the shop is could have believed either.
   */
  readonly contact = signal({
    address: '', mobile1: '', mobile2: '', landline1: '', landline2: '', email: '', hours: '', city: '',
  });

  /**
   * Footer social icons, filtered to the ones admin has both enabled and given a URL —
   * the template just renders this list rather than repeating the enabled-and-has-url
   * check per platform.
   */
  readonly socialLinks = signal<SocialLink[]>([]);

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

    // Guard the range: this is written straight into a style binding, and a stray value
    // would otherwise leave the header unreadable with no way to see why.
    const size = Number(b.siteNameSize);
    this.siteNameSize.set(Number.isFinite(size) && size >= 0.75 && size <= 3 ? `${size}rem` : null);
    this.logoUrl.set(b.logoUrl?.trim() ?? '');
    // Blank means "reuse the header logo", so every shop configured before this existed
    // keeps rendering exactly as it did.
    this.footerLogoUrl.set(b.footerLogoUrl?.trim() || (b.logoUrl?.trim() ?? ''));
    this.footerDescription.set(b.footerDescription?.trim() ?? '');
    this.footerParagraphs.set(linkifyParagraphs(b.footerDescription?.trim() || FOOTER_DESCRIPTION_FALLBACK));

    const parts = [b.announcementText?.trim(), b.priceValidUpto?.trim() ? `Prices valid up to ${b.priceValidUpto.trim()}` : '']
      .filter((p) => p);
    this.announcement.set(parts.join(' · '));

    this.contact.set({
      address: b.contactAddress?.trim() ?? '',
      mobile1: b.contactMobile1?.trim() ?? '',
      mobile2: b.contactMobile2?.trim() ?? '',
      landline1: b.contactLandline1?.trim() ?? '',
      landline2: b.contactLandline2?.trim() ?? '',
      email: b.contactEmail?.trim() ?? '',
      hours: b.contactHours?.trim() ?? '',
      city: b.contactCity?.trim() ?? '',
    });

    const links = [
      { label: 'Facebook', url: b.socialFacebookUrl?.trim() ?? '', enabled: b.socialFacebookEnabled },
      { label: 'Instagram', url: b.socialInstagramUrl?.trim() ?? '', enabled: b.socialInstagramEnabled },
      { label: 'X', url: b.socialXUrl?.trim() ?? '', enabled: b.socialXEnabled },
      { label: 'LinkedIn', url: b.socialLinkedinUrl?.trim() ?? '', enabled: b.socialLinkedinEnabled },
    ]
      .filter((l) => l.enabled && l.url)
      .map(({ label, url }): SocialLink => ({ label, url }));
    this.socialLinks.set(links);

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
