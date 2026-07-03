# V2-0 — Tenant Infrastructure

**Goal:** make every query provably tenant-scoped, so no merchant can ever see another's data. This stage is the **gate** — nothing else in V2 starts until its isolation tests are green.

> ⚠️ **Keep `TenantId` as `BIGINT`.** The design chat proposes a `CHAR(36)` GUID; the real V1 schema uses a `bigint` PK on 39 tables. `CurrentTenantId` is a **`long`** in C#. See [design-v2.md §3](../design-v2.md).

## Scope & checklist
- [ ] **`Common/Tenancy/` module** — `ICurrentTenantService` (`long CurrentTenantId`, fail-closed if unresolved) + `CurrentTenantService` reading `HttpContext.Items["TenantId"]`.
- [ ] **`TenantResolutionMiddleware`** — resolve tenant from the request `Host` subdomain before any controller; 404 on unknown/inactive tenant; stash `TenantId` on `HttpContext.Items`. Cache slug→tenant in Redis (`tenant:slug:{slug}`).
- [ ] **EF Core Global Query Filters** — `HasQueryFilter(e => e.TenantId == _tenant.CurrentTenantId)` on **all 39 tenant-scoped entities** in `EcommerceDbContext.OnModelCreating`.
- [ ] **Auto-stamp `TenantId` on insert** — override `SaveChangesAsync`; new tenant-scoped entities get `CurrentTenantId` so inserts can't forget it.
- [ ] **Raw-SQL audit** — grep for `FromSql*`, `ExecuteSql*`, and the mysql client; every hit gets `AND TenantId = @tenantId` + a `// TENANT-SCOPED` tag. Global filters do **not** cover raw SQL.
- [ ] **JWT ↔ host check** — the token's `TenantId` claim must match the host-resolved tenant (block a token replayed against another store).
- [ ] **`IgnoreQueryFilters()` policy** — allowed **only** in `Features/SuperAdmin`; anywhere else is a bug.
- [ ] **Seed a second tenant** in dev so isolation is testable (V1's default tenant is `TenantId = 1`).

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

**Status:** ⬜ Not started. **This stage gates all of V2.**
