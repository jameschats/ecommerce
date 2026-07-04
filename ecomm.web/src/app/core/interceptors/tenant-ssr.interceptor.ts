import { HttpInterceptorFn } from '@angular/common/http';
import { REQUEST, inject } from '@angular/core';

/**
 * SSR-only: stamps X-Forwarded-Host on outgoing API calls with the tenant host
 * from the incoming request, so the server-rendered storefront resolves to the
 * right store. `fetch` forbids overriding the Host header, so we use
 * X-Forwarded-Host (the API honours it). No-op in the browser, where
 * API_BASE_URL is already derived from window.location.
 */
export const tenantSsrInterceptor: HttpInterceptorFn = (req, next) => {
  const request = inject(REQUEST, { optional: true });   // Web Request on server, null in browser
  if (!request) return next(req);

  try {
    const hostname = new URL(request.url).hostname;       // e.g. acme.localhost
    if (hostname) req = req.clone({ setHeaders: { 'X-Forwarded-Host': hostname } });
  } catch {
    /* ignore malformed request URL */
  }
  return next(req);
};
