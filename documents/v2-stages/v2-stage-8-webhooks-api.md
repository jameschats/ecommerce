# V2-8 — Webhooks & Public API (App Platform)

**Goal:** let external apps and merchant tools **integrate with the platform** — subscribe to events via **outbound webhooks** (like Shopify's `order.created`) and read/write through a **scoped public REST API**. This is the entry point to an app ecosystem. It's Shopify's long-term moat; not required to win the wedge (V2-6), but the foundation for integrations once merchants ask for them.

> Schema foundation already exists in the P2 roadmap — [platform-roadmap-p2.md](../platform-roadmap-p2.md): `ApiApplications`, `ApiTokens`, `Webhooks`, `WebhookDeliveries`.

## Scope & checklist

### 8a. Outbound webhooks (platform → external apps)
- [ ] **Event catalogue** — emit a versioned set: `order.created`, `order.paid`, `order.fulfilled`, `order.cancelled`, `product.created/updated/deleted`, `inventory.updated`, `customer.created`, `refund.created`. Hook into the existing V1 domain events (orders, catalog, inventory) + the SignalR notification points.
- [ ] **Subscriptions** — per tenant: register endpoint URL(s), pick events, get a signing secret. Stored in `Webhooks`.
- [ ] **Signed delivery** — each POST carries an **HMAC-SHA256 signature header** (over the raw body, using the subscription secret) so receivers can verify authenticity — the Shopify `X-Hmac` model.
- [ ] **Async delivery + retries** — deliver via **Hangfire** with exponential backoff (e.g. 5 attempts over ~24h); record every attempt in `WebhookDeliveries` (status, HTTP code, response snippet, attempt count).
- [ ] **Delivery log + replay** — merchant can see deliveries and **re-send** a failed one; auto-disable an endpoint after sustained failures + notify.
- [ ] **At-least-once + ordering note** — receivers must be idempotent (document it); include an event id + timestamp.

### 8b. Public REST API (external apps → platform)
- [ ] **Scoped API tokens** — `ApiApplications` + `ApiTokens` per tenant; **hashed at rest**; scopes (`read_products`, `write_products`, `read_orders`, …); revocable; optional expiry.
- [ ] **Tenant isolation** — API requests resolve the tenant from the token (not the host) and run through the **same EF global query filters** (V2-0). A token can only ever touch its own tenant's data.
- [ ] **Endpoints** — versioned `/api/public/v1/...` for products, orders, inventory, customers (read + scoped writes). Reuse V1 services; enforce scopes per route.
- [ ] **Rate limiting** — per-token + per-tenant quotas (extends the V1 rate limiter); standard `X-RateLimit-*` headers.
- [ ] **Docs** — OpenAPI/Swagger for the public surface; a short "build an app" guide.

### 8c. Merchant admin UI
- [ ] Create/revoke API tokens (show once), manage webhook subscriptions (URL + events + secret), view the delivery log + replay.

## Data model
Migrations `180–189`: `ApiApplications`, `ApiTokens` (hashed), `Webhooks` (subscriptions), `WebhookDeliveries` (attempt log). All tenant-scoped (`TenantId bigint`, per V2-0). Encrypt/ hash all secrets + tokens at rest.

## Security posture
- Tokens hashed (never stored/logged in plaintext); shown to the merchant once.
- Webhook payloads signed; receivers verify HMAC.
- Public API is tenant-scoped by the same query filters — a leak here is the same existential risk as V2-0; include it in the isolation test suite.
- Per-token rate limits so one integration can't starve a tenant.

## Gate
A third-party endpoint receives a **signed** `order.created` within seconds of a test order; a forced 500 on the receiver triggers retries logged in `WebhookDeliveries` and can be replayed; a scoped API token reads/writes **only** its tenant's data and is rejected on out-of-scope calls; revoking a token blocks it immediately; per-token rate limit returns 429 with headers.

## Dependencies
V2-0 (tenancy + isolation tests — **critical**; tokens and webhooks are per-tenant), Hangfire (delivery/retries, from V2-1), V1 order/catalog/inventory events, V2-2 merchant admin (management UI).

## Relationship to a full app store
This stage delivers **integration primitives** (webhooks + API + tokens), not a public app marketplace with OAuth app installs, listing, and billing — that larger ecosystem is deferred until there's partner demand (see [design-v2.md §14](../design-v2.md)).

**Status:** ⬜ Not started. Post-V2-6; build when integration/partner demand appears.
