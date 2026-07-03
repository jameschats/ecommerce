# V2-7 — Hardening & Scale

**Goal:** make the platform safe to run at the V2 scale target (500 tenants, 5M users, 100k orders/day, 25k concurrent) and compliant enough to trust with merchant + customer data.

## Scope & checklist
- [ ] **Plan-limit enforcement** — hard-enforce `MaxProducts` / `MaxOrders` / `AiCredits` per plan; graceful upgrade prompts at the boundary (not silent failures).
- [ ] **Redis, tenant-namespaced** — all cache keys `tenant:{id}:…`; catalog/theme/session/slug caches; invalidation on writes. (Redis became mandatory at V2-0 for slug resolution; this generalises it.)
- [ ] **Per-tenant rate limiting** — extend V1's rate limiting to be tenant-aware (a noisy tenant can't starve others); per-plan quotas optional.
- [ ] **Load test** — simulate 50 tenants under concurrent load; verify isolation holds *and* latency targets; find N+1s and hot queries; add indexes / read-replica routing as needed.
- [ ] **MySQL read replica** — route heavy read paths (catalog, search, analytics) to a replica; keep writes on primary.
- [ ] **Tenant data export (DPDP / GDPR)** — a merchant can export their full store (products, orders, customers) on demand and on cancellation (30-day window from V2-1).
- [ ] **Per-tenant observability** — `TenantId` enricher on every Serilog entry; per-tenant error/usage dashboards; alerting.
- [ ] **Backups + retention** — automated DB backups; the 30-day post-cancellation retention window honoured, then purge.
- [ ] **Security pass** — re-run the V1 hardening checklist per-tenant; confirm the `IgnoreQueryFilters` guard; pen-test cross-tenant access; verify encrypted credentials (payments, couriers, WhatsApp, connectors).

## Gate
Load test at 50 tenants passes with **zero cross-tenant leakage** and within latency targets; plan limits enforce correctly at the boundary; a tenant data export produces a complete, tenant-only archive; every log line carries `TenantId`.

## Dependencies
All prior V2 stages. Redis, MySQL replica, monitoring stack.

**Status:** ⬜ Not started.
