import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { UMAMI_SRC, UMAMI_WEBSITE_ID } from '../api.config';

/**
 * Injects the Umami tracking script once, in the browser only (SSR-safe).
 * No-op unless both UMAMI_SRC and UMAMI_WEBSITE_ID are configured.
 * Umami is cookieless and tracks page views (incl. SPA route changes) automatically.
 */
@Injectable({ providedIn: 'root' })
export class WebAnalyticsService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly doc = inject(DOCUMENT);
  private injected = false;

  init(): void {
    if (this.injected || !isPlatformBrowser(this.platformId)) return;
    if (!UMAMI_SRC || !UMAMI_WEBSITE_ID) return;
    this.injected = true;

    const script = this.doc.createElement('script');
    script.async = true;
    script.defer = true;
    script.src = UMAMI_SRC;
    script.setAttribute('data-website-id', UMAMI_WEBSITE_ID);
    this.doc.head.appendChild(script);
  }
}
