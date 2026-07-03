# AI-2 — Platform Connectors (the market multiplier)

**Goal:** let the engine pull products from **any** platform — your own, Shopify, WooCommerce, or a CSV — so the addressable market is every SMB, not just your tenants. This is the single highest-leverage stage.

> The moment Shopify merchants can use your engine, your market multiplies far beyond your own platform. See [design-v3.md §7](../design-v3.md).

## Scope & checklist
- [ ] **`IPlatformConnector`** — `FetchProductsAsync(config)` (+ `PublishContentAsync` reserved for push-back). One interface, many implementations.
- [ ] **`OwnPlatformConnector`** — direct internal call to `ecomm.api` (**depends on V2 tenancy**); real-time inventory sync.
- [ ] **`ShopifyConnector`** — Shopify Admin REST API via OAuth; product pull + webhook inventory sync.
- [ ] **`WooCommerceConnector`** — WooCommerce REST API via API key; product pull + webhook sync.
- [ ] **`CSVConnector`** — manual upload fallback (no dependency — good for the earliest standalone demo).
- [ ] **Synced product store** — `SyncedProducts` (external id, name, description, images, price, category, tags, `LastSyncedAt`); the source for AI generation prompts.
- [ ] **Connected platforms** — `ConnectedPlatforms` with **encrypted** credentials (`CredentialsJson`), `LastSyncAt`, `ProductsSynced`. Encrypt at rest; never log — this is the highest-value secret in the product.
- [ ] **Sync jobs** — Hangfire; scheduled + webhook-triggered; progress + error surfacing.

## Connector capability matrix (target)
| Feature | Own | Shopify | Woo | CSV |
|---|---|---|---|---|
| Pull products | ✔ auto | ✔ OAuth | ✔ API key | ✔ manual |
| Sync inventory | ✔ real-time | ✔ webhook | ✔ webhook | ✗ |
| Push descriptions back | ✔ auto | V2.1 | V2.1 | ✗ |

## Gate
A Shopify store connects via OAuth and its catalog appears in `SyncedProducts`; a WooCommerce store connects via API key; a CSV upload works standalone; credentials are stored encrypted; a webhook updates inventory. AI-1 text generation runs against synced products from each source.

## Dependencies
AI-0 (core), AI-1 (something to generate from the synced data). `OwnPlatformConnector` needs **V2**. Shopify OAuth app + Woo test store.

**Status:** ⬜ Not started. **Highest-leverage stage — unlocks the non-tenant market.**
