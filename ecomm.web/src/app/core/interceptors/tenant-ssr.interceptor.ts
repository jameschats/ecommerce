import { HttpInterceptorFn } from '@angular/common/http';
import { REQUEST, inject } from '@angular/core';

/**
 * SSR-only: (1) stamps X-Forwarded-Host on outgoing API calls with the tenant host from the
 * incoming request, so the server-rendered storefront resolves to the right store (`fetch`
 * forbids overriding the Host header, so we use X-Forwarded-Host — the API honours it); (2)
 * resolves API_BASE_URL's relative '/api' marker (see api.config.ts) into a real absolute URL
 * using that same incoming request's origin — Node's fetch, unlike a browser, has no implicit
 * "current page" to resolve a relative URL against, and the real tenant origin is only known
 * per-request (many tenants share one Node process), not at module-load time. No-op in the
 * browser, where API_BASE_URL is already an absolute, tenant-correct URL from window.location.
 */
export const tenantSsrInterceptor: HttpInterceptorFn = (req, next) => {
  const request = inject(REQUEST, { optional: true });   // Web Request on server, null in browser
  if (!request) return next(req);

  try {
    const incoming = new URL(request.url);
    if (incoming.hostname) req = req.clone({ setHeaders: { 'X-Forwarded-Host': incoming.hostname } });
    if (!/^https?:\/\//i.test(req.url)) req = req.clone({ url: `${incoming.origin}${req.url}` });
  } catch {
    /* ignore malformed request URL */
  }
  return next(req);
};
