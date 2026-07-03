# V2-6 — Win-a-Merchant

**Goal:** the features that actually pull a merchant off Shopify/WooCommerce. V2-0…V2-5 make us a *correct* platform; **this stage makes us a *chosen* one.** See [design-v2.md §14](../design-v2.md).

> **Marketing gate:** do not run head-to-head campaigns against Shopify/Woo until this ships. Clean multi-tenancy alone converts no one.

## Scope & checklist

### 6a. Merchant migration / import *(acquisition oxygen)*
- [ ] **Shopify import** — connect via OAuth (or CSV export), pull products (variants, images), customers, orders → `SyncedProducts` → create native products.
- [ ] **WooCommerce import** — REST API key; same mapping.
- [ ] **CSV fallback** — the universal path (reuses V1's product import); good for the earliest demo.
- [ ] Import runs as a **Hangfire** job with progress + a mapping report (what came in, what needs review).

### 6b. Visual storefront builder *(closes the #1 UX gap — hardest item)*
- [ ] **Section/block model** — a page = ordered sections (hero, product grid, banner, rich text, testimonials, featured collection…), each with editable settings; stored per tenant.
- [ ] **Live-preview editor** — drag to reorder, edit settings, see changes; publish. This is a real engineering effort — scope an MVP set of ~8 sections first.
- [ ] Builds on the V1 Theme Engine + home-section manager (already section-aware) rather than starting cold.

### 6c. WhatsApp commerce *(the Indian channel)*
- [ ] Order updates + shipping notifications via WhatsApp (Cloud API / provider).
- [ ] Abandoned-cart nudges; broadcast campaigns; catalog share link.
- [ ] Per-tenant WhatsApp sender config in `TenantSettings`.

### 6d. Indian courier aggregator
- [ ] Integrate Shiprocket / Delhivery / NimbusPost: live rates at checkout, label printing, AWB + tracking → feeds the V1 shipments module.
- [ ] Per-tenant courier credentials (encrypted) + default courier.

### 6e. Abandoned-cart recovery
- [ ] Detect abandoned carts; scheduled email + WhatsApp recovery flows (Hangfire); simple recovery analytics.

## Data model
Migrations `140–169`: `SyncedProducts`, `ConnectedPlatforms` (encrypted credentials), storefront `Sections`/`Blocks` (per tenant), `AbandonedCarts`, WhatsApp + courier config in `TenantSettings`. Encrypt all third-party credentials at rest.

## Gate
- A merchant imports a real Shopify/Woo catalog and is live in **< 15 min** (ties to the onboarding target).
- The visual builder can rearrange + publish a homepage without code.
- WhatsApp order-update fires on a test order; courier returns live rates + a label; an abandoned cart triggers a recovery message.

## Dependencies
V2-0…V2-5 (a working platform to import *into* and sell *from*). Hangfire. Provider accounts (Shopify OAuth app, WhatsApp, courier).

**Status:** ⬜ Not started. **This is the release that wins merchants.**
