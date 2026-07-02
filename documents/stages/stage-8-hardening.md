# Stage 8 — Hardening

**Goal:** make V1 production-ready — security, reliability, tests, performance.

## Scope & checklist
- [x] **Rate limiting** (auth/OTP endpoints) + **security headers** + HTTPS/HSTS
- [x] **Login lockout** (5 failed attempts → 15-min lock; `Users.FailedLoginCount`/`LockoutEndUtc`, migration 026)
- [x] **Forwarded headers** (real client IP + scheme behind Nginx)
- [x] **Health readiness probe** (`/api/health/ready`, DB ping) + **response compression** (Brotli/Gzip)
- [x] **Output caching** for anonymous storefront reads (catalog/home/banners, 60s)
- [x] Performance: query/index review (schema already indexes hot paths; no migration) + N+1 check (projections are query-batched)
- [x] **Automated tests** (xUnit): GST math, coupon engine, password hashing, slug — 25 passing
- [x] Secrets in user-secrets/env (done earlier — `/etc/ecomm/api.env`)
- [x] **SEO infra**: dynamic `sitemap.xml` (API, from live categories + products) + static `robots.txt`
- [ ] Operational (owner): change prod admin password, real GSTIN, wire SMTP/MSG91, Nginx security headers for the storefront

## Implementation
- **Security (`Program.cs`):** `UseForwardedHeaders`, `AddRateLimiter` (per-IP "auth" fixed-window 10/min
  on login/register/otp/forgot), a response-headers middleware (nosniff, X-Frame-Options DENY,
  Referrer-Policy), `UseHsts` in prod. Login lockout in `AuthService.LoginAsync`.
- **Reliability:** `DatabaseHealthCheck` → `/api/health/ready`; `AddResponseCompression` (Brotli+Gzip).
- **Performance:** `AddOutputCache` "public" policy on `CatalogController`, CMS home, banners
  (authenticated requests bypass; nothing user-specific is cached).
- **Tests:** `ecomm.tests` (EF Core InMemory) — `dotnet test ecomm.tests/ecomm.tests.csproj`.

## Deploy notes
- Apply **migration 026** (guarded loop handles it).
- Storefront HTML (Node SSR) is separate from the API — set security headers + gzip for it at **Nginx**
  (`add_header` X-Frame-Options/X-Content-Type-Options/Referrer-Policy/HSTS; `gzip on`), and a CSP tuned
  to allow Razorpay (`checkout.razorpay.com`) + Google GSI (`accounts.google.com`) + image hosts.

## Verification
Rate limit (11th request → 429), login lockout (5 fails lock the account), security headers present,
`/api/health/ready` → 200, responses Brotli-compressed, output cache serves stale within TTL, 25/25 tests pass.

**Status:** ✅ Security · reliability · tests · performance. Deferred: SEO infra; operational go-live items.
