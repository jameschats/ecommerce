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

---

# Part 2 — Complete implementation spec (build-ready)

Grounded in the shipped Track A: scheme `ApiKeyAuthDefaults.Scheme = "ApiKey"`, tokens read from
`Authorization: Bearer …` or `X-Api-Key`, principal carries a `tenant` claim + `scope` claims,
`RequiresScopeAttribute` gates endpoints, and `TenantResolutionMiddleware` cross-checks the `tenant` claim
against the Host. Existing scopes: `products:read, orders:read, inventory:read, inventory:write`
(`ApiKeyService.ValidScopes`). App tokens reuse all of this.

## 2.1 Data model (migrations 287–289)

**`App`** (global — an app is platform-wide, installed into many tenants; NOT `ITenantScoped`):
`AppId, OwnerUserId (developer), Name, Slug, ClientId (public), ClientSecretHash (one-way), Handle,
Description, IconUrl, Category, RedirectUris (csv), RequestedScopes (csv), IsEmbedded (bool),
PricingModel (free|recurring|usage|onetime), Status (draft|in_review|listed|suspended), IsFirstParty (bool),
CreatedAt, UpdatedAt`. Client secret shown once at creation (same one-time-reveal pattern as `ApiKey`).

**`AppInstallation`** (`ITenantScoped` — one per (app, tenant)):
`AppInstallationId, TenantId, AppId, GrantedScopes (csv), AccessTokenHash (offline token, hashed like
ApiKey), TokenPrefix, Status (installed|uninstalled), InstalledByUserId, InstalledAt, UninstalledAt`.
Unique (TenantId, AppId).

**`AppOAuthCode`** (short-lived authorization codes): `Code (hash), AppId, TenantId, Scopes, UserId,
ExpiresAt, RedeemedAt`. TTL ~5 min, single-use.

**`AppCharge`** (billing): `AppChargeId, AppInstallationId, TenantId, Type (recurring|onetime|usage),
Amount, Interval, Status (pending|active|cancelled), CreatedAt`. Reuses `RecordChargeAsync`/Razorpay/D1.

Reuse `WebhookSubscription` with a new nullable `AppInstallationId` column (app-scoped subscriptions);
today's merchant-created subscriptions leave it null.

## 2.2 Scopes
Reuse `ApiKeyService.ValidScopes` and add app-oriented ones as endpoints grow:
`products:write, orders:write, content:write` (blog/CMS), `themes:write`, and a **separately-gated**
`customers:read` (PII — off by default, explicit consent, DPDP posture). Central `ValidScopes` list stays
the single source of truth for both API keys and apps.

## 2.3 Auth — extend the existing handler (no new scheme)
In `ApiKeyAuthenticationHandler.HandleAuthenticateAsync`, after the `ApiKey` hash lookup misses, try an
`AppInstallation` by `AccessTokenHash`. On hit, emit the same claims plus `app_installation_id` and
`app_id`; scopes come from `AppInstallation.GrantedScopes`. Everything downstream (`RequiresScope`,
tenant cross-check, public controllers) works unchanged. Distinguish token kinds by key prefix
(`ApiKeyService.KeyPrefixLiteral` for keys; a new `apptok_` prefix for app tokens).

## 2.4 OAuth 2.0 install flow (endpoints)
1. `GET /admin/apps/{slug}` (App Store listing) → **Install** button.
2. `GET /oauth/authorize?client_id&scope&redirect_uri&state` → renders the **consent screen** in the admin
   (must be an authenticated admin of the tenant). Shows requested scopes in plain language.
3. `POST /oauth/authorize` (merchant approves) → issue an `AppOAuthCode`, redirect to the app's
   `redirect_uri?code&state`.
4. App server calls `POST /oauth/token` `{client_id, client_secret, code, redirect_uri}` → validate →
   create/refresh `AppInstallation`, return `{access_token, scope, token_type:"bearer"}` (offline token).
5. App calls the public API with `Authorization: Bearer apptok_…`.
   Uninstall: `DELETE /admin/apps/installed/{id}` → mark uninstalled, revoke token, fire `app/uninstalled`.

## 2.5 App Store + install UI (Angular)
- Merchant: `/admin/apps` (browse listed apps), `/admin/apps/{slug}` (detail + Install), `/admin/apps/installed`
  (manage/uninstall), and the consent screen. New `AppStoreService`.
- Embedded host: `/admin/apps/{slug}/app` renders the app in a sandboxed iframe (see 2.6).

## 2.6 Embedded apps (App Bridge-lite)
- The admin host page loads the app's URL in an `<iframe sandbox="allow-scripts allow-forms allow-same-origin">`
  with CSP `frame-ancestors 'self'` on the admin origin only.
- A tiny bridge (`postMessage`): host mints a short-lived **session JWT** (claims: tenant, app_installation_id,
  scopes, exp ~2 min, signed by the platform) and posts it to the iframe on request; the app exchanges it for
  a fresh offline call or uses it directly as a bearer for the public API. Bridge also exposes
  `navigate`/`toast`/`resize`. Standalone (non-embedded) apps skip all of this.

## 2.7 App billing + revenue share
- App declares a `PricingModel`; on install (or upgrade) the platform creates an `AppCharge` and bills the
  merchant via the **existing subscription/Razorpay flow** → `RecordChargeAsync` → **GST invoice (D1)**.
- Platform retains a **revenue share** (config: 0% first-party, X% third-party); the developer's share is
  paid out via **Razorpay Route** to the developer's linked account (same primitive as SaaS/marketplace).
- Usage billing: app reports usage via an authenticated endpoint; aggregated into the cycle charge.

## 2.8 Extension points
- **Storefront app blocks:** an installed app registers app-owned section types through `SectionTypeRegistry`
  (tag them with the owning `AppId`); the merchant drops them into the theme via the existing editor —
  Shopify's "theme app extensions" using the section engine already built for AI Commerce rails.
- **Admin UI slots (later):** declared slots where an installed app can add a panel/action.

## 2.9 Review queue (shared primitive)
One generic `SubmissionReview` queue (submitter, type: app|theme|seller-kyc, payloadRef, status
submitted|approved|rejected, reviewer, notes, history) — used by App review, Theme review, and seller KYC.
First-party apps skip review (`IsFirstParty`); third parties go through it.

## 2.10 Phase task checklists + acceptance
- **S1 — App identity + OAuth install:** entities/migrations (2.1); `oauth/authorize` + `oauth/token`;
  extend auth handler (2.3); consent screen. *Accept: a test app completes install → gets `apptok_…` →
  reads products within scope; a scope it wasn't granted returns 403.*
- **S2 — App Store UI + 1–2 first-party apps:** browse/install/uninstall UI; build first-party apps against
  the public API only. *Accept: install/uninstall round-trips; first-party app works with no internal-API
  access.*
- **S3 — Embedded surface:** iframe host + session-token bridge + CSP. *Accept: an embedded app renders in
  the admin and calls the API via a session token; CSP blocks other origins.*
- **S4 — App billing + revenue share:** `AppCharge` + subscription/Route/D1 wire-up. *Accept: installing a
  paid app bills the merchant, produces a GST invoice, and records the dev's payable share.*
- **S5 — Extension points:** app-owned theme section types. *Accept: an installed app's block appears in the
  theme editor and renders on the storefront; uninstalling removes it.*
- **S6 — Developer dashboard + review queue → third parties.** *Accept: a developer submits an app, staff
  approves via the queue, it lists in the App Store.*

## 2.11 Decisions to lock before S1
1. **First-party-only at launch** (recommended) vs open third-party from day one.
2. **Embedded + standalone both** (recommended) vs one.
3. **Revenue-share %** for third-party apps (e.g. 0% first-party, 15% third-party).
4. **Token model:** offline-only (recommended v1) vs offline + online (user-context) tokens.
5. **PII scope (`customers:read`)** — include at launch (gated) or defer (recommended defer, DPDP).

---

**Status:** planned & build-ready. Foundation (Public API Track A) shipped. **Start point: S1** — `App` /
`AppInstallation` entities + the `oauth/authorize` + `oauth/token` flow + auth-handler extension.
Prerequisite for S4 payouts: Razorpay Route (shared with SaaS billing + V3 marketplace).
