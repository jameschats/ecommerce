/**
 * Runtime configuration — DEVELOPMENT DEFAULTS.
 *
 * This file is copied verbatim into the build output and is **replaced on the server
 * by the deploy script** with the values for that environment. It is loaded as a plain
 * script before the Angular bundle, so `window.__APP_CONFIG__` is always set before any
 * module reads it.
 *
 * Do not put secrets here. Everything in this file is public — it is served to browsers.
 *
 * See documents/stages-v2/deployment.md.
 */
window.__APP_CONFIG__ = {
  apiBaseUrl: 'http://localhost:5080/api',
  siteUrl: 'http://localhost:4200',
  umamiSrc: '',
  umamiWebsiteId: '',
  umamiDashboardUrl: '',
};
