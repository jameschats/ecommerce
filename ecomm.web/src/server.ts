import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');

/**
 * Hostnames this server will render for. Angular 20+ rejects unknown hosts with a
 * 400 to prevent SSRF, so a deployed site must declare its own hostname or every
 * request fails.
 *
 * Resolved at runtime from `SITE_URL` (already set per environment for the same reason
 * `config.js` exists) plus an optional `ALLOWED_HOSTS` override, rather than from
 * `angular.json` — a build-time list would mean rebuilding the app per domain, which is
 * exactly the coupling we removed when replacing the `sed` step.
 */
function resolveAllowedHosts(): string[] {
  const hosts = new Set(['localhost', '127.0.0.1']);

  for (const h of (process.env['ALLOWED_HOSTS'] ?? '').split(',')) {
    const trimmed = h.trim();
    if (trimmed) hosts.add(trimmed);
  }

  const siteUrl = process.env['SITE_URL'];
  if (siteUrl) {
    try {
      hosts.add(new URL(siteUrl).hostname);
    } catch {
      console.warn(`[ssr] SITE_URL is not a valid URL: "${siteUrl}"`);
    }
  }

  return [...hosts];
}

const allowedHosts = resolveAllowedHosts();
console.log(`[ssr] allowedHosts: ${allowedHosts.join(', ')}`);

const app = express();
const angularApp = new AngularNodeAppEngine({
  allowedHosts,
  // Safe here specifically because this process binds to loopback and is only ever
  // reachable through nginx, which sets these headers itself. It would NOT be safe on a
  // publicly-bound port, where a client could forge them.
  trustProxyHeaders: true,
});

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = Number(process.env['PORT'] ?? 4000);
  // Bind to loopback by default. Without an explicit host Express listens on 0.0.0.0,
  // which exposes SSR directly on its port — bypassing nginx's TLS, security headers and
  // rate limiting — and silently ignores the HOST set in the systemd unit.
  const host = process.env['HOST'] ?? '127.0.0.1';
  app.listen(port, host, (error?: Error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://${host}:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
