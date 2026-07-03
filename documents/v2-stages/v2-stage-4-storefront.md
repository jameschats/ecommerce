# V2-4 — Per-Tenant Storefront

**Goal:** one Angular storefront deployment serves every merchant — each subdomain renders that merchant's own theme, catalog, and SEO. Two subdomains = two brands, same code.

## Scope & checklist
- [ ] **Subdomain resolution on bootstrap** — the existing `ecomm.web` SSR reads the request host, calls `GET /api/tenant/resolve` (or receives tenant context server-side), and loads that tenant's theme + catalog. API is already host-scoped by V2-0, so data fetching needs no client tenant logic.
- [ ] **Per-tenant theme** — Theme Engine values (colours, logo, font) move from global settings to per-`TenantId` (`TenantSettings`); SSR injects the right CSS variables before hydration.
- [ ] **Per-tenant SEO** — canonical/OG/meta per tenant + per product; `SITE_URL` becomes the resolved tenant host; per-tenant `sitemap.xml`/`robots.txt`.
- [ ] **Redis slug cache** — the host→tenant + theme lookup runs on every request; cache it (`tenant:slug:{slug}`, `tenant:{id}:theme`), invalidate on theme/settings change.
- [ ] **Per-tenant transactional identity** — emails/SMS send as the merchant's brand (`TenantSettings.SenderName`, reply-to); notification templates per tenant.
- [ ] **404 / suspended handling** — unknown or suspended tenant renders a clean "store unavailable" page, not a stack trace.

## Custom domains (V2.1 — flagged, not built here)
Merchant points `www.theirbrand.com` via CNAME; resolve through a `TenantDomains` lookup + **on-demand TLS** (per-domain certs / wildcard). Plan the cert story before promising it.

## Data model
No new core tables (uses `TenantSettings`, extended `Tenants`). `TenantDomains` reserved for V2.1.

## Gate
Two seeded tenants on two subdomains each render their own logo/colours/products, correct per-tenant canonical URLs, and correct sender identity in emails. View-source shows real per-tenant SSR HTML. Suspended tenant shows the unavailable page.

## Dependencies
V2-0 (host resolution), V2-2 (theme/settings managed by merchants). Redis.

**Status:** ⬜ Not started.
