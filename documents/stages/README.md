# Build Stages

Sequential, dependency-ordered build plan for V1 (see [`../design.md`](../design.md) §11). Each stage has its own doc with scope, checklist, and status.

| Stage | Title | Status |
|-------|-------|--------|
| [0](stage-0-foundations.md) | Foundations (schema, settings, audit, media) | 🟡 Schema done; cross-cutting code pending |
| [1](stage-1-identity.md) | Identity (auth, RBAC) | ✅ Backend + Angular UI done |
| [2](stage-2-catalog.md) | Catalog (+ import/export) | ✅ Backend + storefront (SSR/SEO) + admin UI done (SEO-fields/image-ZIP deferred) |
| [2.5](stage-2.5-theming-cms.md) | Theming & CMS-lite *(pulled from V1.1)* | ✅ Theme Engine + home-section manager (SSR-verified) |
| [3](stage-3-inventory-search.md) | Inventory & Search | ✅ Inventory (ledger, low-stock, import/export, admin UI) + FULLTEXT search + autocomplete |
| [4](stage-4-shopping.md) | Shopping (cart, addresses) | ✅ Cart (user+guest, merge-on-login, stock check) + profile + address book + storefront/account UI |
| [5](stage-5-checkout-money.md) | Checkout & Money | ✅ Tax (GST/HSN) + shipping (serviceability) + orders (reserve/commit) + payments (Mock/Razorpay) + refund + invoice PDF + admin orders |
| [6](stage-6-postpurchase.md) | Post-purchase & engagement | ⬜ Not started |
| [7](stage-7-differentiators.md) | Differentiators (V1.1) | ⬜ Deferred |
| [8](stage-8-hardening.md) | Hardening | ⬜ Not started |

**Legend:** ✅ done · 🟡 in progress · ⬜ not started

> All 61 tables + 72 FKs already exist (migrations `001`–`015`), since the schema is built for V3 up front. Each stage **implements code** against tables that already exist.
