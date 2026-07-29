import { InjectionToken, REQUEST, inject, isDevMode } from '@angular/core';

/**
 * Tenant-aware API base. In the browser we derive it from the current host, so a
 * merchant on {slug}.<domain> talks to their OWN store's API (the API resolves the
 * tenant from the Host header).
 *   - dev:  Angular on :4200 → API on the same host, port 5080. Also used, via this exact
 *     literal string, by CalendarShop's build-time `sed` deploy step (documents/deployment.md
 *     §5a) for its own single-tenant prod API base — keep this string intact even though it's
 *     otherwise dead in a real deployed (non-dev) SSR process.
 *   - prod, browser: same-origin `/api` (Nginx routes /api on every subdomain).
 *   - prod, SSR: a relative '/api' marker — the real tenant origin is only known per-request
 *     (many tenants share this one Node process), not at module-load time, so it's resolved
 *     per-request in tenantSsrInterceptor (which already has REQUEST access) rather than here.
 */
function deriveApiBaseUrl(): string {
  if (typeof window !== 'undefined' && window.location) {
    const { protocol, hostname, port } = window.location;
    if (hostname === 'localhost' || hostname.endsWith('.localhost') || port === '4200') {
      return `${protocol}//${hostname}:5080/api`;
    }
    return `${protocol}//${hostname}/api`;
  }
  return isDevMode() ? 'http://localhost:5080/api' : '/api';
}

/** Base URL of the ecomm.api backend (tenant-aware in the browser; relative + interceptor-resolved during SSR). */
export const API_BASE_URL = deriveApiBaseUrl();

/**
 * Public site origin — used to build canonical / Open Graph / JSON-LD URLs for SEO. Unlike
 * API_BASE_URL this can't be resolved via an HTTP interceptor (it's spliced into page metadata,
 * not a request URL), so it's a proper per-request InjectionToken: browser reads window.location,
 * SSR reads the real incoming request (REQUEST, Angular 21's stable per-request DI token) so each
 * tenant's server-rendered pages get their own correct origin, not a shared module-level guess.
 */
export const SITE_URL = new InjectionToken<string>('SITE_URL', {
  providedIn: 'root',
  factory: () => {
    if (typeof window !== 'undefined' && window.location) return window.location.origin;
    const request = inject(REQUEST, { optional: true });
    if (request) { try { return new URL(request.url).origin; } catch { /* fall through */ } }
    return 'http://localhost:4200';
  },
});

/**
 * Umami (privacy-friendly, cookieless web analytics). Both empty = tracking disabled.
 * In prod, set these (deploy replaces this file) to e.g.
 *   UMAMI_SRC = 'https://analytics.calendarshop.online/script.js'
 *   UMAMI_WEBSITE_ID = '<website-id from the Umami dashboard>'
 */
export const UMAMI_SRC = '';
export const UMAMI_WEBSITE_ID = '';

/** Umami dashboard URL — linked from the admin sidebar. Empty = hide the link. */
export const UMAMI_DASHBOARD_URL = '';
