/** Base URL of the ecomm.api backend (dev). */
export const API_BASE_URL = 'http://localhost:5080/api';

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
