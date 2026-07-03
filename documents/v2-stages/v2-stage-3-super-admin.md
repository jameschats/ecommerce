# V2-3 — Super Admin Panel

**Goal:** you, the platform owner, manage all tenants, plans, and platform revenue — the **only** place cross-tenant queries are allowed.

## Scope & checklist
- [ ] **New Angular app `ecomm.superadmin`** — separate deployment; platform-owner auth (not a tenant user).
- [ ] **Tenant management** — list/search all tenants, view detail, **suspend/activate**, change plan, extend trial.
- [ ] **Cross-tenant reads via explicit scope** — `IgnoreQueryFilters()` is permitted **only** in `Features/SuperAdmin`, always with an explicit `WHERE TenantId = …` when targeting one tenant. Every action audit-logged.
- [ ] **Impersonation** — mint a short-lived, merchant-scoped token to support a merchant; flagged in `AuditLogs` (who impersonated whom, when, why).
- [ ] **Plan management** — CRUD `Plans`, pricing, limits, feature flags.
- [ ] **Platform revenue dashboard** — MRR, active/trial/suspended/churned counts, churn rate, revenue by plan, new signups — aggregated **across** tenants (unfiltered, super-admin only). Reuses the V1 analytics patterns at platform scope.

## Security posture
- Super-admin app is the highest-value target: separate origin, strong auth (consider 2FA), IP allowlist optional, every mutating action + every impersonation audit-logged and reviewable.
- A bug that leaks `IgnoreQueryFilters` into a tenant-facing path is critical — lint/guard for it.

## Data model
Migrations `120–129` as needed: a `PlatformAdmins` table (or reuse `Users` with a platform role), impersonation audit fields on `AuditLogs`. Revenue dashboard reads `TenantSubscriptions` + `TenantBillingHistory`.

## Endpoints
`/api/superadmin/tenants` (GET/PUT suspend|activate|plan), `/api/superadmin/tenants/{id}/impersonate` (POST), `/api/superadmin/plans` (CRUD), `/api/superadmin/revenue` (MRR/churn).

## Gate
Super admin can suspend a tenant (its storefront + admin immediately 404), impersonate and land in that merchant's admin (audit row written), and see accurate MRR/churn across seeded tenants. No tenant user can reach any `/api/superadmin/*` route.

## Dependencies
V2-0 (tenancy + the `IgnoreQueryFilters` policy), V2-1 (subscriptions/billing data for revenue).

**Status:** ⬜ Not started.
