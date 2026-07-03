# V2-2 — Merchant Admin Portal

**Goal:** each merchant's staff manage their own store — products, orders, customers, inventory, themes, coupons, reports — and **nothing outside their tenant**. This is the **V1 admin experience, ported and tenant-scoped**.

## Scope & checklist
- [ ] **New Angular app `ecomm.merchant-admin`** — same standalone-components + Tailwind + theme conventions as `ecomm.web`.
- [ ] **Merchant JWT carries a `TenantId` claim** — validated against the host-resolved tenant (V2-0); all API calls are auto-scoped by the global query filters, so screens need no per-query tenant logic.
- [ ] **Port V1 admin screens** — catalog (products/categories/brands/attributes), inventory, orders, reviews, coupons, banners/CMS, theme, store settings, suppliers, analytics, notifications. All already exist in V1 admin; they become per-tenant unchanged (filters do the work).
- [ ] **Merchant billing portal** — current plan, usage vs limits (products/orders/AI credits), invoices (`TenantBillingHistory`), upgrade/downgrade, payment method, cancel.
- [ ] **Merchant onboarding checklist** surfaced here post-launch (finish setup, connect payments, import catalog).

## Not in scope
Cross-tenant anything (that's Super Admin, V2-3). Merchant admins can never see other tenants or platform revenue.

## Data model
No new tables — reuses V1 admin schema (now tenant-scoped) + `TenantSettings`/`TenantSubscriptions` for the billing portal.

## Gate
Logged in as merchant A's admin: every screen shows only A's data; attempting a B-owned resource id 404s; the billing portal reflects A's real plan + usage; no endpoint returns cross-tenant data (covered by V2-0 CI tests + a merchant-admin smoke suite).

## Dependencies
V2-0 (tenancy), V2-1 (plans/subscriptions for the billing portal). Reuses the entire V1 admin feature set.

**Status:** ⬜ Not started.
