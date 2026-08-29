# App / Software Marketplace (Shopify App Store style) — Thorough Plan

**Goal:** a **software marketplace** where apps (and themes) extend a merchant's store — the **Shopify App
Store** model, *not* the multi-seller commerce marketplace (that's the separate
[marketplace-plan.md](marketplace-plan.md), V3). A developer builds an app; a merchant discovers it in an
in-admin App Store, installs it (granting scopes), and the app extends their store via the public API,
webhooks, and embedded UI — optionally charging the merchant through the platform.

This extends **Phase 6 Track B (App Marketplace)** and **Track C (Theme Store)**, whose foundation —
**Track A, the Public API — already shipped** (`Features/PublicApi/`: scoped `ApiKey` auth, public
Products/Orders/Inventory DTOs+controllers, webhook subscriptions + delivery, OpenAPI doc, `/admin/developer`).

---

## 1. How Shopify's App Store actually works

An app is an **independent web app hosted by the developer** — Shopify hosts the *listing*, the *install
flow*, the *API*, and the *billing*, not the app's code. The pieces:

| Piece | What it does |
|---|---|
| **Partners dashboard** | Where developers register apps, get API credentials (client id/secret), declare requested **scopes**, redirect URLs, webhooks. |
| **OAuth 2.0 install** | Merchant clicks Install → authorizes the requested scopes → the app receives a **per-shop access token** (offline token for background, online token for user-context). Distinct from a merchant's own API key. |
| **Admin API (GraphQL/REST) + scopes** | The app reads/writes store data *only within granted scopes*. |
| **App Bridge (embedded apps)** | The app renders **inside the Shopify admin** as an iframe; a JS bridge + **session tokens** (short-lived JWTs) authenticate each request and provide navigation/UI. |
| **Extensions** | Ways to inject app functionality without the merchant touching code: **theme app extensions** (app blocks in the storefront theme), **checkout UI extensions**, **admin UI extensions**, **Shopify Functions** (custom backend logic). |
| **Billing API** | Apps charge merchants — recurring (`AppSubscription`), one-time, or **usage-based** — and Shopify **collects it on the merchant's Shopify bill and pays the developer**, taking a **revenue share** (0% on the first $1M/yr per dev, 15% after). |
| **App review + App Store listing** | Shopify reviews public apps before listing; listing has pricing, screenshots, categories, reviews. |
| **App types** | **Public** (App Store), **custom** (single merchant), **unlisted**. Embedded vs standalone. |
| **Lifecycle webhooks** | `app/uninstalled`, scope changes, GDPR/data-request webhooks (mandatory for public apps). |

The crucial design idea: **the platform is the trust + billing + auth broker.** Merchants trust one install
flow and one bill; developers get distribution + collections; the platform takes a cut and enforces scopes
and review.

---

## 2. What this platform already has (Track A)
- **Scoped `ApiKey` auth** (`ApiKeyAuthentication`, `ApiKeyService`) + public DTOs
  (`PublicProductDto`/`PublicOrderDto`/…) deliberately separate from internal admin DTOs (a stable public
  contract — the single most important decision, already made right).
- **Public controllers**: Products/Orders/Inventory.
- **Webhooks**: `WebhookSubscription` + `WebhookDispatchService` (order/product/inventory events fire).
- **OpenAPI doc** + merchant self-service at `/admin/developer` (create/revoke keys, manage webhooks).

**Gap vs an app marketplace:** today's model is *"a merchant generates their own API key for their own
integration."* An app ecosystem needs *"a third-party app registers once, and each merchant installs it via
OAuth, which mints a per-(app, tenant) scoped token."* That OAuth-install + app-identity layer is the core
new work — everything downstream (API, webhooks, DTOs) is reused.

---

## 3. Architecture — what to build on top of Track A

### 3a. New entities
- **`App`** (developer-owned app definition): name, developer/owner, `ClientId`/`ClientSecret`, redirect
  URIs, requested `Scopes`, webhook endpoints, listing metadata (description, icon, category, pricing
  model), `Status` (draft/in-review/listed/suspended), `IsFirstParty`.
- **`AppInstallation`** (per-tenant install): AppId, TenantId, granted `Scopes`, `AccessToken`
  (offline, encrypted), InstalledAt, Status (installed/uninstalled). This is distinct from `ApiKey`.
- **`AppCharge`** (billing): AppInstallationId, type (recurring/one-time/usage), amount, status — reuses the
  subscription/Razorpay rails.
- Reuse `WebhookSubscription` but scope subscriptions to an `AppInstallationId`.

### 3b. OAuth 2.0 install flow (the core new work)
1. Merchant clicks **Install** on an app's App Store listing → redirect to the app's auth URL with the
   requested scopes.
2. The app redirects back to the platform's **authorize** endpoint; the merchant admin sees a consent
   screen ("This app wants to: read products, write orders…") → approves.
3. Platform issues an authorization code → app exchanges it (client id/secret) for a **per-(app,tenant)
   access token** with the granted scopes. `AppInstallation` created.
4. The app now calls the **existing public API** with that token; `ApiKeyAuthentication` is extended (or a
   sibling `AppTokenAuthentication`) to resolve app tokens → tenant + scopes.

### 3c. Embedded app surface (App Bridge-lite)
- Apps render **inside the merchant admin** in an iframe at `/admin/apps/{appId}`; a small JS bridge posts
  **session tokens** (short-lived JWTs signed by the platform) the app exchanges for API calls, plus
  navigation/toast helpers. Strict CSP/`frame-ancestors` for the embed.
- Standalone (non-embedded) apps are also allowed (just OAuth + API, no iframe).

### 3d. Extension points (inject without code)
- **Storefront app blocks** — reuse the **theme section system** (`SectionTypeRegistry` +
  `storefront-section.component`): an installed app can register app-owned section types the merchant drops
  into their theme (this is exactly Shopify's "theme app extensions"). The section system already built for
  AI Commerce rails is the hook.
- **Admin UI extension points** — declared slots in the admin where an installed app can add a panel/action
  (later; start with full embedded apps).

### 3e. App billing + payouts
- Apps charge merchants via the platform's **existing subscription/Razorpay infra** (recurring/usage);
  charges flow through the same idempotent `RecordChargeAsync` → **GST invoice (D1)**. Platform takes a
  **revenue share**; pay developers their share via **Razorpay Route** (the same split-settlement primitive
  the SaaS-billing and V3-marketplace plans use — build once, reuse everywhere).

### 3f. Review/approval + developer dashboard
- Per phase-6's insight, build **one generic submission/review queue** shared by App review, Theme review,
  and seller KYC. A **developer dashboard** (manage apps, credentials, installs, webhooks, billing, listing).

---

## 4. Phasing
1. **S1 — App identity + OAuth install.** `App` + `AppInstallation` entities; OAuth authorize/token
   endpoints + consent screen; extend auth to resolve app tokens → tenant+scopes. Reuse Track A's scopes.
2. **S2 — App Store UI + first-party app(s).** In-admin App Store (browse/install/uninstall/manage
   permissions); build **1–2 real first-party apps** through the public API alone (per phase-6's
   "first-party first" decision) to prove the surface before any third party.
3. **S3 — Embedded app surface.** Iframe host + session-token bridge (App Bridge-lite) + CSP.
4. **S4 — App billing + revenue share** (reuse subscription/Route/D1).
5. **S5 — Extension points** — storefront app blocks via the theme section system; admin UI slots.
6. **S6 — Developer dashboard + review queue → open to third parties** (the deferred step in phase-6).

### Theme Store (Track C) — same pattern
A merchant-facing **Theme Store** (browse/install/customize themes) reuses: the theme engine (already
built), the same "first-party first" curation, the same submission/review queue, and app billing for paid
themes. Plan it as a sibling track after S1–S4.

---

## 5. Security & governance
- **Scopes enforced** on every app call (reuse Track A's scope model); consent screen shows exactly what's
  granted; scope changes require re-consent.
- **Customer PII** stays behind a dedicated, separately-gated scope (phase-6/DPDP posture) — not in the
  default scopes.
- **App tokens** encrypted at rest (DataProtection, like other secrets); `app/uninstalled` webhook revokes.
- **Embedded-app CSP** (`frame-ancestors` limited to the admin origin; each app sandboxed).
- **App review** before public listing (first-party apps skip; third parties go through the review queue).
- **Data-handling obligations** for public apps (data-request/erasure webhooks — the DPDP/GDPR analogue of
  Shopify's mandatory privacy webhooks).

---

## 6. Shopify → this platform mapping
| Shopify | This platform |
|---|---|
| Partners dashboard | Developer dashboard (S6) |
| OAuth install + per-shop token | `AppInstallation` + OAuth flow (S1) |
| Admin GraphQL API + scopes | Public API Track A (shipped) + app-token auth |
| App Bridge / embedded apps | Iframe host + session-token bridge (S3) |
| Theme app extensions | App-owned theme section types via `SectionTypeRegistry` (S5) |
| Billing API + revenue share | Subscription/Razorpay + Route payouts + D1 invoices (S4) |
| App review + App Store listing | Review queue + in-admin App Store (S2/S6) |
| App/uninstalled + privacy webhooks | `WebhookDispatchService` + lifecycle/privacy events |

---

## 7. Recommended start & decisions
- **Start S1 (OAuth install) — it's the one genuinely-new primitive; everything else reuses Track A.**
- **Decision:** first-party-only at launch (per phase-6) vs open third-party submission from the start
  (recommend first-party-only; prove the surface, then open with the review queue).
- **Decision:** embedded-only, standalone-only, or both (recommend both; standalone is trivial once OAuth
  exists, embedded is S3).
- **Decision:** revenue-share % for third-party apps (Shopify: 0% then 15%).

**Status:** planned. Foundation (Public API Track A) shipped. Next primitive: S1 OAuth app-install.
