/**
 * Runtime configuration.
 *
 * These values are resolved **once, synchronously, at module load** from whichever
 * source is available:
 *   * Browser — `window.__APP_CONFIG__`, set by `/config.js` (a plain script loaded
 *     before the Angular bundle, so it is always in place before any module reads it).
 *   * SSR / Node — `process.env`.
 *   * Neither — the localhost defaults below, for `ng serve`.
 *
 * Why synchronous rather than an async fetch: some modules capture these at import time
 * (e.g. `banner.service.ts` and `notification.service.ts` derive `API_ORIGIN` at module
 * top level). An async loader would leave those holding the default value. Reading a
 * global that is already set avoids the ordering problem entirely, and means none of the
 * ~26 files importing these constants need to change.
 *
 * Deploy writes `/config.js` per environment — see documents/stages-v2/deployment.md.
 * This replaces the old build-time `sed` substitution, where a skipped rewrite silently
 * pointed production at localhost.
 */

export interface RuntimeConfig {
  apiBaseUrl?: string;
  siteUrl?: string;
  umamiSrc?: string;
  umamiWebsiteId?: string;
  umamiDashboardUrl?: string;
}

declare global {
  interface Window {
    __APP_CONFIG__?: RuntimeConfig;
  }
}

function readConfig(): RuntimeConfig {
  if (typeof window !== 'undefined' && window.__APP_CONFIG__) {
    return window.__APP_CONFIG__;
  }

  const proc = (globalThis as { process?: { env?: Record<string, string | undefined> } }).process;
  if (proc?.env) {
    return {
      apiBaseUrl: proc.env['API_BASE_URL'],
      siteUrl: proc.env['SITE_URL'],
      umamiSrc: proc.env['UMAMI_SRC'],
      umamiWebsiteId: proc.env['UMAMI_WEBSITE_ID'],
      umamiDashboardUrl: proc.env['UMAMI_DASHBOARD_URL'],
    };
  }

  return {};
}

const cfg = readConfig();

/** Base URL of the ecomm.api backend. */
export const API_BASE_URL = cfg.apiBaseUrl || 'http://localhost:5080/api';

/** Public site origin — used to build canonical / Open Graph URLs for SEO. */
export const SITE_URL = cfg.siteUrl || 'http://localhost:4200';

/**
 * Umami (privacy-friendly, cookieless web analytics). Both empty = tracking disabled.
 * In prod set these in `/config.js`, e.g.
 *   umamiSrc: 'https://analytics.calendarshop.online/script.js'
 *   umamiWebsiteId: '<website-id from the Umami dashboard>'
 */
export const UMAMI_SRC = cfg.umamiSrc || '';
export const UMAMI_WEBSITE_ID = cfg.umamiWebsiteId || '';

/** Umami dashboard URL — linked from the admin sidebar. Empty = hide the link. */
export const UMAMI_DASHBOARD_URL = cfg.umamiDashboardUrl || '';

/**
 * Fail loudly on the misconfiguration this file exists to prevent: a deployed site
 * still pointing at localhost because `/config.js` was not written for this environment.
 * The old `sed` approach failed silently here; a half-working site that cannot reach its
 * API is harder to diagnose than an obvious banner saying exactly what is wrong.
 */
if (typeof window !== 'undefined' && typeof document !== 'undefined') {
  const servedFromLocalhost = /^(localhost|127\.0\.0\.1|\[::1\])$/.test(window.location.hostname);
  const pointsAtLocalhost = /localhost|127\.0\.0\.1/.test(API_BASE_URL);

  if (!servedFromLocalhost && pointsAtLocalhost) {
    const message =
      `Configuration error: the site is served from ${window.location.origin} but API_BASE_URL is ` +
      `"${API_BASE_URL}". /config.js was not written for this environment — see ` +
      `documents/stages-v2/deployment.md.`;

    console.error(`[config] ${message}`);

    document.addEventListener('DOMContentLoaded', () => {
      const banner = document.createElement('div');
      banner.textContent = message;
      banner.style.cssText =
        'position:fixed;inset:0 0 auto 0;z-index:2147483647;background:#b91c1c;color:#fff;' +
        'padding:12px 16px;font:14px/1.5 system-ui,sans-serif;text-align:center';
      document.body.prepend(banner);
    });
  }
}
