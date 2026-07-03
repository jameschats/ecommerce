/** SSR / fallback API base (dev). Prod deploy sed-replaces this literal. */
const DEFAULT_API_BASE_URL = 'http://localhost:5080/api';

/**
 * Tenant-aware API base. In the browser we derive it from the current host, so a
 * merchant on {slug}.<domain> talks to their OWN store's API (the API resolves the
 * tenant from the Host header). SSR / non-browser falls back to the default.
 *   - dev:  Angular on :4200 → API on the same host, port 5080
 *   - prod: same-origin `/api` (Nginx routes /api on every subdomain)
 */
function deriveApiBaseUrl(): string {
  if (typeof window === 'undefined' || !window.location) return DEFAULT_API_BASE_URL;
  const { protocol, hostname, port } = window.location;
  if (hostname === 'localhost' || hostname.endsWith('.localhost') || port === '4200') {
    return `${protocol}//${hostname}:5080/api`;
  }
  return `${protocol}//${hostname}/api`;
}

/** Base URL of the ecomm.api backend (tenant-aware in the browser). */
export const API_BASE_URL = deriveApiBaseUrl();

/** Public site origin — used to build canonical / Open Graph URLs for SEO. */
export const SITE_URL = 'http://localhost:4200';

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
