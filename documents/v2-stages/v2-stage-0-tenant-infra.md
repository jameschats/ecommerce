# V2-0 — Tenant Infrastructure

**Goal:** make every query provably tenant-scoped, so no merchant can ever see another's data. This stage is the **gate** — nothing else in V2 starts until its isolation tests are green.

> ⚠️ **Keep `TenantId` as `BIGINT`.** The design chat proposes a `CHAR(36)` GUID; the real V1 schema uses a `bigint` PK on 39 tables. `CurrentTenantId` is a **`long`** in C#. See [design-v2.md §3](../design-v2.md).

## Scope & checklist
- [x] **`Common/Tenancy/` module** — `ICurrentTenantService` (`long CurrentTenantId`) + `CurrentTenantService` (request `HttpContext.Items` → ambient `BeginScope` override → configured default tenant). Fail-open to default so startup/jobs never break; requests fail-closed via the middleware.
- [x] **`TenantResolutionMiddleware`** — resolves tenant from the `Host` subdomain (apex/www/dev → default; `{slug}.{BaseDomain}` → lookup; unknown/inactive/suspended → 404). Cached in `IMemoryCache` *(Redis is the multi-instance target, noted)*.
- [x] **EF Core Global Query Filters** — `HasQueryFilter(e => e.TenantId == _tenant.CurrentTenantId)` applied via a reflection loop to **all 32 `ITenantScoped` entities** in `EcommerceDbContext`. (`Role`/`Permission` are platform-global; `Tenant`'s id is its PK — all excluded.)
- [x] **Auto-stamp `TenantId` on insert** — `SaveChanges`/`SaveChangesAsync` overrides stamp `CurrentTenantId` on every added `ITenantScoped` row (overwrites a wrong id → no cross-tenant write).
- [x] **Raw-SQL audit** — **clean**: no `FromSql*`/`ExecuteSql*` in source, so the global filters cover 100% of data access.
- [x] **JWT ↔ host check** — the token's `tenant` claim must equal the resolved host tenant, else 403 (blocks a token replayed against another store).
- [x] **`IgnoreQueryFilters()` policy** — documented + used only in the super-admin path / tests; enforced by the isolation suite.
- [x] **~145 hardcoded `TenantId==1` refs** rerouted to the current tenant (`_db.CurrentTenantId`) so multi-tenant reads/writes resolve correctly; V1 (tenant 1) behaviour unchanged.
- [x] **`CorrelationId` middleware + `TenantId` Serilog enricher** (pulled forward from V2-10) — correlation id per request on every log line + the `ApiResponse` error envelope + `X-Correlation-Id` header.
- [ ] **Live second tenant + subdomain config in dev** — deferred to V2-1 onboarding (isolation is proven via the in-memory test gate; no subdomains/second store exist yet). Redis-backed slug cache also deferred to V2-7 scale.

## Data model
No new tables. Migration `100_tenant_columns.sql` extends the existing `Tenants` (additive `ALTER`): `Slug`, `DisplayName`, `CustomDomain`, `PlanId`, `TrialEndsAt`, `SuspendedAt`; backfill `Slug` from `Code`. Add indexes on `Tenants.Slug`.

## The gate — cross-tenant isolation tests (must pass before V2-1)
- Seed tenant A + tenant B, each with products/orders/customers.
- Assert: acting as A, **every** repository/endpoint returns **only** A's rows (products, orders, cart, inventory, coupons, reviews, notifications, media…).
- Assert: a direct `GET /api/.../{id}` for a B-owned id, as A, returns 404 — not B's data.
- Assert: an insert as A stamps `TenantId = A` automatically.
- Assert: a JWT for A used against B's subdomain is rejected.
- These run in CI on every commit for the rest of V2.

## Dependencies
V1 schema (Tenants + 39 `TenantId` columns already exist). Redis (now mandatory) for the slug cache.

**Status:** ✅ **Core complete on branch `v2-tenant-infra`** — query-filter isolation + auto-stamp + tenant resolution + JWT↔host check + correlation-id tracing; **29 tests green** (incl. the cross-tenant isolation gate). V1/tenant-1 behaviour unchanged (login + catalog + admin verified at runtime). *Deferred to V2-1:* a live second tenant + subdomain wiring. **This stage gates all of V2 — gate is green.**
