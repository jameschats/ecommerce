# V2 Build Stages — Multi-Tenant SaaS Platform

Sequential, dependency-ordered build plan for **V2** (see [`../design-v2.md`](../design-v2.md)). Each stage has its own doc with scope, checklist, gate, and status. Migrations for V2 live in `database/migrations/` numbered **`100+`** (V1 stays `0xx`, frozen).

| Stage | Title | Migrations | Status |
|---|---|---|---|
| [V2-0](v2-stage-0-tenant-infra.md) | **Tenant Infrastructure** — isolation, resolution, query filters | 100–109 | ⬜ Not started |
| [V2-1](v2-stage-1-plans-onboarding.md) | Plans & Merchant Onboarding — signup, subdomain, Razorpay Subscriptions | 110–119 | ⬜ |
| [V2-2](v2-stage-2-merchant-admin.md) | Merchant Admin Portal — new Angular app, tenant-scoped V1 admin | — | ⬜ |
| [V2-3](v2-stage-3-super-admin.md) | Super Admin Panel — tenant mgmt, impersonation, revenue dashboard | 120–129 | ⬜ |
| [V2-4](v2-stage-4-storefront.md) | Per-Tenant Storefront — subdomain resolution, per-tenant theme/SEO | — | ⬜ |
| [V2-5](v2-stage-5-payments.md) | Per-Merchant Payments — Razorpay Route, commission at source | 130–139 | ⬜ |
| [V2-6](v2-stage-6-win-a-merchant.md) | **Win-a-Merchant** — migration import, visual builder, WhatsApp, couriers, abandoned-cart | 140–169 | ⬜ |
| [V2-7](v2-stage-7-hardening.md) | Hardening & Scale — plan limits, Redis, rate limiting, load test, data export | 170–179 | ⬜ |

**Legend:** ✅ done · 🟡 in progress · ⬜ not started

## Two hard rules

1. **V2-0 is a gate, not just a stage.** Cross-tenant isolation integration tests **must be green before any other stage starts** (see [design-v2.md §6.1](../design-v2.md)). A single tenant data leak is an existential, trust-ending failure.
2. **Don't market against Shopify until V2-6 ships.** V2-0…V2-5 make us a *correct* platform; **V2-6 is the release that actually pulls a merchant off Shopify/Woo** (migration + visual builder + WhatsApp + couriers + abandoned-cart). Clean multi-tenancy alone converts no one — see [design-v2.md §14](../design-v2.md).

## Sequencing rationale
Build the isolated platform (V2-0 → V2-5: tenancy → billing → both admin apps → storefront → payments), *then* the merchant-acquisition features (V2-6), *then* harden + scale (V2-7). The **AI Growth Engine** ([design-v3.md](../design-v3.md), stages in [v3-stages/](../v3-stages/)) is built **after** V2 is stable — its own-platform connector depends on this tenant infrastructure.

> V1 already ships GST/HSN, COD, invoice PDF, coupons, reviews, wishlist, theme engine, analytics — V2 makes all of it **per-tenant**, additively (39 of 66 tables already carry `TenantId`).
