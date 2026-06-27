# Stage 8 — Hardening

**Goal:** make V1 production-ready.

## Scope & checklist
- [ ] Caching with **Redis** (catalog, settings, hot reads)
- [ ] Performance passes (query/index review, N+1 checks)
- [ ] Monitoring/observability (Serilog already in; add metrics/dashboards as needed)
- [ ] Automated tests (unit + integration; auth, checkout, tax/shipping math)
- [ ] Secrets moved to user-secrets/env (JWT key, DB password, Razorpay, Google, SMS)
- [ ] Rate limiting (auth/OTP endpoints) + security headers + HTTPS
- [ ] **SEO infra** (see [design.md](../design.md) §13): `sitemap.xml` (from active products/categories) + `robots.txt`; Core Web Vitals pass (image optimization, CDN)
- [ ] Deployment to Hostinger/Azure (container; CI/CD)

## Dependencies
All prior stages.

## Notes
- Later phases (post-V1): microservices split, Kafka events, Elasticsearch, CDN, read replicas, multi-region.

**Status:** ⬜ Not started.
