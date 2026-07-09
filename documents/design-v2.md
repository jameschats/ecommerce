# E-Commerce Platform — V2 Design (Multi-Tenant SaaS)

> **Project codename:** "Mini Flipkart" → **Commerce Platform (SaaS)**
> **Author/Architect:** James (.NET full-stack, Technical Architect)
> **Status:** V2 design — approved, build next (V1 is complete and live at `https://calendarshop.online`).
> **Source:** Distilled from `design-chat-v2.txt` (multi-tenant platform) and `design-chat-v2-00.txt` (AI Marketing Engine add-on), reconciled against the **actual V1 schema** (66 tables, 39 already tenant-scoped).

> 📌 **Competitive strategy first.** Sections 1–12 are the *technical* build. The **go-to-market wedge, the Shopify/WooCommerce comparison table, the merchant-facing feature scope, and the onboarding design are in §13–§15** — read those to understand *why a merchant chooses us*, then the technical sections for *how we deliver it*.

---

## 1. What V2 Is

V1 is a **single-store** app — one business, one catalog, one set of customers. V2 turns it into a **platform** where many independent merchants each run a fully isolated store on your infrastructure (the Shopify model). You stop selling calendars and start selling *the software*, charging merchants a monthly subscription.

| Dimension | V1 — Single Store | V2 — Commerce Platform |
|---|---|---|
| Who sells? | You (one seller) | Many merchants, each independent |
| Customers belong to | Your platform | Each merchant's store |
| Products belong to | Your catalog | Each merchant's own catalog |
| Storefront | One Angular app | One storefront per merchant (subdomain) |
| Payments | Your Razorpay account | Each merchant's own Razorpay account |
| Your revenue | Product sales | Monthly subscription fees from merchants |
| Admin panel | Manages your store | Super-admin + per-merchant admin |
| Themes | One global theme | Each merchant sets their own branding |
| Email / SMS | Your brand name | Sent under each merchant's brand |

> **The one engineering rule that matters above all others:** *every* database query must be scoped to a `TenantId`. A single data leak between tenants is a catastrophic, trust-ending failure. EF Core's **Global Query Filter** enforces this automatically so no developer ever has to remember it.

---

## 2. Versioning Strategy

| Version | Shape | Status |
|---|---|---|
| **V1** | Single seller, single store | ✅ Complete — stable, in production |
| **V2** | Multi-tenant SaaS — many isolated merchants | **Design approved — build now** |
| **V3** | Full marketplace (many sellers, one storefront) | Parked |

**What V1 already got right — nothing is wasted:**
- The `Tenants` table already exists (currently holds the single default tenant, `TenantId = 1`).
- **39 of 66 tables already have a `TenantId` FK column** — the upgrade is *additive*, not a rewrite.
- Vertical-slice architecture (`Features/Auth`, `Features/Catalog`, …) — tenant scoping is added per slice without restructuring.
- Theme Engine (CSS variables, already per-setting) — V2 just makes it per-tenant.
- Settings Engine + Audit Logs — already tenant-aware in shape; V2 activates enforcement.

---

## 3. ⚠️ Schema Reconciliation (read before any coding)

The design chat proposes `Tenants.TenantId CHAR(36)` (a GUID) with a `Slug` column. **The real V1 table does not look like that:**

```
Tenants (as built in V1)
  TenantId   BIGINT UNSIGNED  PK auto_increment   -- NOT a GUID
  Name       VARCHAR(150)
  Code       VARCHAR(50)      UNIQUE
  IsActive   TINYINT(1)       DEFAULT 1
  CreatedAt / UpdatedAt
```

All 39 tenant-scoped tables reference this **`bigint`** key. Therefore:

- **Do NOT switch `TenantId` to `CHAR(36)`.** That would rewrite 39 FK columns + every index — exactly the "painful rewrite" the versioning strategy exists to avoid.
- **Keep `TenantId BIGINT`.** In C#, `CurrentTenantId` is a **`long`**, not a `Guid`. (Every code snippet in the chat that says `Guid` becomes `long` here.)
- **Extend the existing table** with the columns V2 needs (`Slug`, `DisplayName`, `PlanId`, `TrialEndsAt`, `CustomDomain`, `SuspendedAt`) via a new migration — additive `ALTER TABLE`, no data loss.
- `Code` (already unique) can double as the initial subdomain slug, or add a dedicated `Slug` column and backfill it from `Code`.

This single decision keeps V2 additive.

---

## 4. Scale Targets

| Metric | V1 (done) | V2 (build for this) |
|---|---:|---:|
| Merchant tenants | 1 | 500 |
| Registered users (all tenants) | 100,000 | 5,000,000 |
| Products (all tenants) | 100,000 | 10,000,000 |
| Orders / day (platform-wide) | 2,000 | 100,000 |
| Concurrent users | 500 | 25,000 |

**V2 infrastructure additions:** Redis (now **mandatory**, keys namespaced `tenant:{id}:…`), a CDN, a MySQL read replica, and a background job runner (**Hangfire**) for async work (billing retries, subdomain provisioning, data exports, AI generation jobs).

---

## 5. Tech Stack Changes

| Layer | V1 | V2 change |
|---|---|---|
| Backend | ASP.NET Core .NET 9 modular monolith | Add **tenant-resolution middleware** + `ICurrentTenantService` |
| ORM | EF Core 9 + Pomelo | Add `HasQueryFilter` per tenant-scoped entity |
| Cache | Redis (basic/optional) | Redis **mandatory**, `tenant:{id}:…` namespaced keys |
| Payments | Single Razorpay account | **Razorpay Route** — each merchant's own linked account |
| Email/SMS | Single sender identity | Per-tenant sender name + reply-to |
| Logging | Serilog | Add a **`TenantId` enricher** to every log entry |
| Billing (new) | — | **Razorpay Subscriptions** — you charge merchants monthly |
| Frontend | One Angular app | **Stays one app**, host-aware (storefront · merchant-admin · super-admin) — see [ADR-001](v2-stages/v2-stage-3-super-admin.md#adr-001) |

### Repository layout (target)
```
ecommerce/
├── ecomm.api/
│   ├── Common/
│   │   └── Tenancy/            ← NEW: ICurrentTenantService, CurrentTenantService, TenantResolutionMiddleware
│   ├── Features/
│   │   ├── [all V1 features]   ← unchanged; EF filters + tenant context injected
│   │   ├── Tenants/            ← NEW: merchant onboarding, tenant CRUD
│   │   ├── Subscriptions/      ← NEW: plans, billing, trials, webhooks
│   │   └── SuperAdmin/         ← NEW: platform-wide management APIs
│   └── ecomm.api.csproj
├── ecomm.web/                  ← ONE Angular app, host-aware: storefront + merchant-admin (/admin)
│                                 + super-admin (/superadmin). NOT split into separate apps — see ADR-001.
└── database/migrations/
    ├── 001–099  ← V1, frozen (currently at 029; never modify applied scripts)
    └── 100–199  ← V2 tables + indexes
```
> Migration convention: V1 scripts stay `0xx` and are **frozen**. All V2 work starts at **`100_*.sql`**, leaving headroom for late V1 patches in the `030–099` band.

---

## 6. The 7 Major Engineering Changes (how to build each)

### 6.1 Multi-tenant data isolation — EF Core Global Query Filters
Every entity that has a `TenantId` gets a query filter in `EcommerceDbContext.OnModelCreating`, so every LINQ query silently gets `WHERE TenantId = @current`.

```csharp
// EcommerceDbContext — inject the tenant context
private readonly ICurrentTenantService _tenant;
public EcommerceDbContext(DbContextOptions o, ICurrentTenantService tenant) : base(o)
    => _tenant = tenant;

// In OnModelCreating, for EACH tenant-scoped entity:
modelBuilder.Entity<Product>().HasQueryFilter(p => p.TenantId == _tenant.CurrentTenantId);
modelBuilder.Entity<Order>()  .HasQueryFilter(o => o.TenantId == _tenant.CurrentTenantId);
// … repeat for all 39 tenant-scoped entities.
```
**How to do it safely on this codebase:**
- Add a small integration test that seeds two tenants and asserts tenant A can never read tenant B's rows — **this test is the gate for V2-0** (below). Nothing else ships until it's green.
- On `SaveChanges`, auto-stamp `TenantId` on new entities (override `SaveChangesAsync`) so inserts can't forget it.
- **Audit every raw SQL call.** Global Query Filters only apply to LINQ. Any Dapper/ADO/`FromSqlRaw` must manually add `AND TenantId = @tenantId` and be tagged `// TENANT-SCOPED`. Grep the codebase for `FromSql`, `ExecuteSql`, and the mysql client before shipping.
- Where the super-admin genuinely needs cross-tenant reads, use `IgnoreQueryFilters()` **explicitly** and only in `Features/SuperAdmin`.

### 6.2 Tenant resolution middleware
Resolve the tenant from the subdomain **before any controller runs**, and stash it on the request.

```csharp
// Common/Tenancy/TenantResolutionMiddleware.cs
var host = context.Request.Host.Host;      // sarahs-boutique.calendarshop.online
var slug = host.Split('.')[0];             // "sarahs-boutique"
var tenant = await tenantRepo.GetBySlugAsync(slug);   // cached in Redis
if (tenant is null || !tenant.IsActive) { context.Response.StatusCode = 404; return; }
context.Items["TenantId"] = tenant.TenantId;          // long
await next(context);
```
- `CurrentTenantService` reads `HttpContext.Items["TenantId"]`; throws if absent (fail closed).
- **Cache the slug→tenant lookup in Redis** (`tenant:slug:{slug}`) — it runs on every request.
- API calls from the SPA carry the tenant via the `Host` header (same subdomain), so no extra client work.
- The **JWT also carries a `TenantId` claim**; middleware must verify the token's tenant matches the resolved host tenant (defence in depth against a token replayed against another store).

### 6.3 Merchant onboarding & subscription billing (a whole new domain)
This is **you collecting money from merchants** — entirely separate from merchants collecting money from *their* customers.

| Step | What happens |
|---|---|
| Merchant signs up | `Tenant` row created; 14-day trial starts; subdomain auto-provisioned |
| Trial ends | Billing prompt; plan selection; Razorpay Subscription activated |
| Monthly billing | Razorpay charges merchant; `TenantBillingHistory` row written (via webhook) |
| Payment fails | 3-day grace → store suspended (`SuspendedAt`) → data retained 30 days |
| Merchant cancels | Store deactivated at period end; data export available 30 days |

Build in `Features/Subscriptions`: `Plans` service, `TenantSubscriptions` state machine (`Trial → Active → Suspended → Cancelled`), and a **Razorpay Subscriptions webhook handler** (idempotent — dedupe on `RazorpayPaymentId`). Use Hangfire for the grace-period timers and suspension jobs.

### 6.4 Subdomain storefront per merchant
- `merchant-slug.calendarshop.online` → the existing Angular storefront reads the subdomain on bootstrap, calls `/api/tenant/resolve`, and loads that tenant's theme + catalog.
- **One** storefront deployment; tenant context drives every data fetch (the API is already host-scoped by 6.2).
- **Custom domains (post-GA point release):** merchant points `www.theirbrand.com` via CNAME; resolve through a `TenantDomains` lookup table + on-demand TLS (e.g. Caddy/Nginx + Let's Encrypt wildcard, or per-domain certs).

### 6.5 Per-merchant payment accounts — **Razorpay Route (recommended)**

| Approach | How it works | Verdict |
|---|---|---|
| **Razorpay Route** | Single checkout; Razorpay splits funds to the merchant's linked account; your platform fee deducted at source | ✅ **Recommended** |
| Per-merchant OAuth | Merchant connects their own Razorpay via OAuth; you bill subscription separately | Good for control |
| Platform collects & remits | You collect, remit minus commission, own reconciliation | ❌ Avoid — regulatory/PA-DSS complexity |

Store linked-account info in `TenantPaymentAccounts`. At checkout, the existing payment slice picks the tenant's account + adds the platform commission as a Route transfer.

### 6.6 Two admin panels

| Panel | Who | Can do |
|---|---|---|
| **Super Admin** (`ecomm.superadmin`) | You, the platform owner | All tenants, suspend/activate, manage plans, **impersonate** a merchant for support, platform revenue dashboard (MRR, churn) |
| **Merchant Admin** (`ecomm.merchant-admin`) | Each merchant's staff | Their own products, orders, customers, inventory, themes, coupons, reports — **nothing outside their tenant** |

Merchant Admin is essentially the **V1 admin UI, ported**, with a `TenantId`-scoped JWT. Super Admin runs **unfiltered** queries with an *explicit* tenant scope (`IgnoreQueryFilters` + manual `WHERE`), and every action is audit-logged. Impersonation = mint a short-lived merchant-scoped token, flagged in audit logs.

### 6.7 Per-tenant branding, email & notifications
- Theme Engine values (colors, logo, font) move from global settings to **per-`TenantId`** rows (the `TenantSettings` table).
- Transactional emails send as *"Your order from Sarah's Boutique"* — `TenantSettings.SenderName` + reply-to per tenant.
- Notification templates stored per tenant; merchants customise order-confirmation / shipping copy. (V1 already has `notificationtemplates` — make it tenant-scoped.)

---

## 7. Data Model — V2 Changes

All V1 tables are unchanged. V2 **extends** `Tenants` and adds new tables via migrations `100+`.

```sql
-- 100_tenant_columns.sql — extend the EXISTING bigint-keyed Tenants table (additive)
ALTER TABLE Tenants
  ADD COLUMN Slug         VARCHAR(80)  NULL UNIQUE,   -- subdomain; backfill from Code
  ADD COLUMN DisplayName  VARCHAR(200) NULL,
  ADD COLUMN CustomDomain VARCHAR(255) NULL,          -- post-GA
  ADD COLUMN PlanId       INT          NULL,
  ADD COLUMN TrialEndsAt  DATETIME     NULL,
  ADD COLUMN SuspendedAt  DATETIME     NULL;

-- NEW V2 tables
Plans                 -- Starter / Growth / Pro / Enterprise
  PlanId, Name, MonthlyPrice, MaxProducts, MaxOrders, AiCredits, Features (JSON)

TenantSubscriptions
  TenantId, PlanId, Status (Trial|Active|Suspended|Cancelled),
  CurrentPeriodStart, CurrentPeriodEnd, RazorpaySubscriptionId

TenantBillingHistory
  TenantId, Amount, Status, RazorpayPaymentId, BilledAt, PeriodStart, PeriodEnd

TenantSettings        -- per-tenant config: StoreName, SenderEmail, CurrencyCode, TimeZone…
  TenantId, `Key`, `Value`

TenantPaymentAccounts
  TenantId, Provider (Razorpay|Stripe), AccountId, IsVerified, ConnectedAt

TenantDomains         -- post-GA custom domains
  TenantId, Domain, IsVerified, CertStatus
```
Keep the hand-authored-entity + Fluent-mapping convention (`ToTable(...)`), exactly as V1.

---

## 8. AI Marketing Engine → see V3

A **second, independently sellable product** — the **AI Growth Engine** — generates product descriptions, social/WhatsApp/email content, images, and video reels from product data, and plugs into this platform **and** external ones (Shopify/WooCommerce). It's the reason `Plans.AiCredits` exists in §7.

Its full design (provider abstraction, feature modules, credit ledger, connectors, build stages, pricing) lives in **[design-v3.md](design-v3.md)** — it's a standalone service (`ecomm.ai`) with its own database, not a slice of `ecomm.api`. The **own-platform connector** depends on V2 tenant infrastructure, which is why V3 is built after V2.

> When a customer buys both, a mapping table bridges the commerce `bigint TenantId` (§3) to the AI engine's own account GUID — the two identity spaces stay separate by design.

---

## 9. V2 Priorities

**P0 — nothing works without these:**
1. Tenant resolution middleware
2. EF Core Global Query Filters on every tenant-scoped entity (+ cross-tenant isolation test)
3. `CurrentTenantService`
4. Plans & Subscriptions + Razorpay billing
5. Merchant self-serve onboarding + auto-subdomain provisioning
6. Super Admin panel
7. Merchant Admin portal (V1 admin, scoped to tenant)
8. Per-tenant Razorpay Route
9. Per-tenant Theme Engine
10. Subdomain storefront routing

**P1 — after P0:** custom domains, per-tenant email identity, merchant billing portal, plan-limit enforcement, platform revenue dashboard (MRR/churn), tenant data export (GDPR/DPDP), AI Marketing Engine GA.

---

## 10. V2 Build Stages

| Stage | Scope | Gate |
|---|---|---|
| **V2-0 — Tenant infrastructure** | Activate/extend `Tenants`, `TenantResolutionMiddleware`, `CurrentTenantService`, `HasQueryFilter` on all entities | **Cross-tenant isolation integration tests must pass before any other stage starts** |
| **V2-1 — Plans & onboarding** | Seed Plans (Starter/Growth/Pro), merchant signup, auto-subdomain, Razorpay Subscriptions, trial→paid, billing webhook | Test tenant can sign up + be charged end-to-end |
| **V2-2 — Merchant Admin portal** | New Angular app; merchant JWT carries `TenantId`; all V1 admin screens ported; billing portal | Merchant can only see own data |
| **V2-3 — Super Admin panel** | Store directory + contacts; standing/watchlist/blacklist + periodic review; two-mode impersonation; proactive assist; plans; revenue dashboard | Directory+contacts; audited view-as/impersonate; blacklist blocks re-signup |
| **V2-4 — Per-tenant storefront** | Storefront reads subdomain on bootstrap; `/api/tenant/resolve`; per-tenant SEO + theme | Two subdomains render two brands |
| **V2-5 — Per-merchant payments** | Razorpay Route; merchant connect flow; platform commission at source | Split settlement verified in Razorpay test |
| **V2-6 — Win-a-Merchant** | Migration import (Shopify/Woo/CSV), visual storefront builder, WhatsApp commerce, Indian courier aggregator, abandoned-cart recovery | Import → live in <15 min; builder publishes |
| **V2-7 — Hardening & Scale** | Plan-limit enforcement, Redis tenant-namespaced keys, per-tenant rate limiting, load test w/ 50 tenants, data export | Load + isolation both green |
| **V2-8 — Webhooks & Public API** | Outbound webhooks (order.created, …) + delivery/retry log; scoped public REST API + API tokens; per-tenant management | 3rd-party endpoint receives a signed event; token reads only its tenant |
| **V2-9 — Merchant Support & Ticketing** | In-app tickets → super-admin queue; threaded + internal notes; SLA per plan; correlation-id link to diagnostics | Merchant raises → queue → resolve; own-tenant only |
| **V2-10 — Observability & Diagnostics** | `CorrelationId` + `TenantId` log enricher; per-tenant logs (Seq); transaction inspector; remediation toolkit; `PlatformAccessLog` | Trace an error by correlation id; retry webhook / replay job from the pane |
| **V2-11 — Unified Multi-Channel Notifications** | One dispatcher → in-app / email / SMS / WhatsApp; per-tenant branding + templates; preferences + DPDP/DLT compliance; delivery log | Event fans to right channels under tenant brand; opt-out honoured |
| **V2-12 — Merchant Engagement & Lifecycle** | Super-admin scheduler: anniversaries, festival wishes, milestones, quarterly/annual NPS; merchant health score; frequency caps | Anniversary/festival auto-send; NPS → health score; opt-out honoured |
| **V2-13 — Reporting & Analytics** | Per-merchant report suite (sales, orders, customers, inventory, GST, settlements) + platform-wide reports (MRR, GMV, cohorts, support); export + scheduled | Merchant sees own reports + GST export; super-admin sees platform MRR/GMV/churn |

> These build stages are the source of truth — see [`v2-stages/`](v2-stages/). The dotted "V2.1 / V2.2" labels elsewhere in this doc are older release-milestone names; they map onto the stages above (§13/§14 now reference stage numbers).

---

## 11. Platform Pricing (what you charge merchants)

| Plan | Price/mo | Products | Orders/mo | AI Credits |
|---|---:|---:|---:|---:|
| Starter | ₹499 | 500 | 2,000 | 0 |
| Growth | ₹999 | 5,000 | 2,000 | 500 |
| Pro | ₹1,999 | Unlimited | Unlimited | 2,000 |
| Enterprise | Custom | Unlimited | Unlimited | Custom |

---

## 12. Key Risks & Gotchas

- **Tenant leakage is existential.** Global Query Filters + the auto-stamp on insert + the V2-0 isolation test are non-negotiable. Treat any `IgnoreQueryFilters()` outside `Features/SuperAdmin` as a bug.
- **Raw SQL bypasses filters.** Audit and tag every raw query `// TENANT-SCOPED`.
- **Don't change the PK type.** `TenantId` stays `bigint`; the chat's `CHAR(36)` is not adopted (§3).
- **The admin seeder is now per-tenant.** V1's `AdminUserSeeder` seeds one global admin — V2 must seed a merchant-admin per new tenant at onboarding, not globally.
- **Webhooks must be idempotent.** Razorpay may deliver twice; dedupe on payment/subscription IDs.
- **Redis is mandatory, not optional** — the slug→tenant lookup on every request needs it.
- **Custom domains need on-demand TLS** — plan the cert story before promising the feature (post-GA point release).

---

## 13. Competitive Positioning — Why a Merchant Chooses Us

Competing with Shopify **head-on, globally, on feature count is not winnable** — it's 20 years deep with an 8,000-app ecosystem, POS hardware, and a fulfilment network. WooCommerce is infinitely flexible but is a *developer* product, not a *merchant* one. So we don't fight on their terms. We win a **specific, defensible wedge**:

> **"Shopify for Bharat" — the India-first, AI-native, all-inclusive store builder that costs a fraction and needs *zero paid apps* for what an Indian SMB actually needs.**

Under that framing, our three goals become concrete and honest:
- **More features** → not "more than Shopify," but **"everything an Indian SMB needs, native and free, that Shopify/Woo charge apps + plugins for"** (GST invoicing, COD+OTP, WhatsApp, couriers, AI marketing).
- **Simpler to use** → template → sensible defaults → one-click launch, live in <15 min (§15). WooCommerce loses on complexity; that's our opening.
- **More affordable** → all-inclusive ₹499–₹1,999 vs an effective ₹3,000–₹6,000+/mo on Shopify India once GST + platform transaction fee + 4–6 paid apps stack up.

### The comparison (India SMB, 2026)

Markers: ✅ native/strong · ⚠️ gap we must build · ❌ not offered.

| Capability / cost (India SMB) | Shopify (India) | WooCommerce (self-host) | **Our Platform (V2)** |
|---|---|---|---|
| Entry price / mo | ₹1,499–₹1,994 (Basic) **+ 18% GST** | "Free" core → **₹12k–30k/yr** real (hosting + plugins + dev) | **₹499 all-in** |
| Mid / top tier | Grow ₹5,599 · Advanced ₹22,680 | varies with plugins | ₹999 · ₹1,999 |
| One-time setup | DIY low; agency ₹20k–1L | **₹35k–1.5L** (developer + setup) | **₹0 — guided wizard** |
| **Extra platform txn fee** (on top of gateway) | **2% Basic / 1% Grow / 0.5% Advanced** — Shopify Payments unavailable in India | none | **none** (Razorpay Route) |
| GST-compliant invoicing | ❌ Paid app (~₹200–800/mo) | ❌ Paid plugin ₹2k–5k | ✅ **Native** |
| COD (+ OTP verify) | ❌ Paid app | ⚠️ basic free; verify = paid | ✅ **Native + admin toggle** |
| WhatsApp order updates + marketing | ❌ Paid app | ❌ Paid plugin | ✅ Native (V2-6) |
| Indian couriers (Shiprocket/Delhivery) | ❌ Paid app | ❌ Paid plugin | ✅ Native aggregator (V2-6) |
| AI marketing (copy/image/video) | ❌ Paid apps, per-tool ₹₹₹ | ❌ few/none | ✅ **Bundled add-on (V3)** |
| Managed hosting / security / updates | ✅ Included | ❌ **You own it** | ✅ Included |
| Storefront visual builder | ✅ Best-in-class (sections) | ⚠️ Page builders (Elementor) | ⚠️ **Gap → build (V2-6)** |
| App / extension ecosystem | ✅ 8,000+ | ✅ 50,000+ | ⚠️ Webhooks + public API (V2-8); full app store later |
| Regional language / Hinglish store + copy | ⚠️ limited | ⚠️ DIY | ✅ Roadmap (V3 AI) |
| Multi-channel (IG/FB shop, Google) | ✅ | ⚠️ plugins | ⚠️ Roadmap (Future) |

**Bottom line:** a typical Indian SMB on Shopify pays base **+ 18% GST + platform transaction fee + 4–6 paid apps** (GST, COD, WhatsApp, courier, reviews) ≈ **₹3,000–₹6,000+/mo effective**. WooCommerce is "free" but **₹35k–1.5L to stand up** and you own security + uptime. Our all-inclusive ₹499–₹1,999 wins on price **and** total-cost-of-ownership — *provided we close the three ⚠️ gaps below.*

---

## 14. Merchant-Facing Feature Scope (what actually wins merchants)

The core V2 in §6 (tenancy, onboarding/billing, subdomain storefront, Razorpay Route, two admin panels, per-tenant branding) is **necessary but not sufficient** — it makes us a platform, not a *chosen* one. These merchant-facing features are what pull a merchant off Shopify/Woo. Phased so we ship the wedge fast.

| Feature | Why it wins | Phase |
|---|---|---|
| **Migration/import from Shopify & WooCommerce** | Removes switching cost — this is acquisition oxygen; without it, adoption stalls | **V2-6** |
| **Visual storefront builder** (sections/blocks, live preview) | Merchants expect to *see & rearrange* their store; closes our #1 UX gap vs Shopify | **V2-6** |
| **WhatsApp commerce** (catalog, order updates, abandoned-cart nudges, broadcast) | *The* Indian channel; Shopify is weak/paid here — a genuine wedge | **V2-6** |
| **Indian courier aggregator** (Shiprocket / Delhivery / NimbusPost) | Live rates, label printing, tracking — table-stakes for Indian fulfilment | **V2-6** |
| **Abandoned-cart recovery** (email + WhatsApp flows) | Direct revenue lift; standard elsewhere, paid-app on Shopify | **V2-6** |
| **AI Growth Engine** (copy/image/video, credits) | No competitor bundles it — see [design-v3.md](design-v3.md) | **V3** |
| **Multi-channel** (Instagram/Facebook shops, Google Shopping feed) | Reach buyers where they browse | **Future** |
| **Regional languages + Hinglish** (storefront + AI copy) | India moat; ties to the AI engine | **V3** |
| **Staff accounts + roles/permissions** (per merchant) | Any real merchant has staff with scoped access | **Future** |
| **Gift cards, multi-currency, subscriptions-for-shoppers** | Nice-to-have breadth | Later |
| **App / extension platform** (public API + webhooks for 3rd parties) | Shopify's real long-term moat; not needed to win the wedge, but the entry point for integrations | **V2-8** |

> **Phase legend:** the **Phase** column above uses the build-stage numbers from [`v2-stages/`](v2-stages/). **"Future"** = intended scope with **no stage doc yet** (multi-channel, staff accounts) — planned in principle, not yet decomposed or scheduled.

> **Sequencing rule:** V2 core (§10, stages V2-0…V2-5) makes multi-tenancy safe; **V2-6 is the "win a merchant" release** (migration + visual builder + WhatsApp + couriers + abandoned cart). Do not market against Shopify until V2-6 ships — clean multi-tenancy alone won't convert anyone.

---

## 15. Onboarding & "Simple to Use"

The explicit goal is **a non-technical merchant live in under 15 minutes** — the opposite of WooCommerce's developer-and-hours reality, and simpler than Shopify's "easy signup, then hunt for 6 apps."

**The guided flow:**
1. **Sign up → pick an industry template** (fashion, food, electronics, services, handmade…). The store is **pre-loaded with a demo catalog, theme, and pages** they can edit in place or wipe with one click.
2. **Setup checklist with a progress bar** — add your first product · connect payments (Razorpay in 2 clicks) · set shipping + pincode serviceability · pick logo + theme colours · choose subdomain (or connect a domain) → **Launch**.
3. **Sensible India defaults, pre-filled** so nothing blocks launch: GST mode on (HSN prompts), COD on, a default courier, default email/SMS templates, **AI-generated SEO meta**, and placeholder India legal pages (privacy / returns / T&C) ready to edit.
4. **"Coming from Shopify/WooCommerce?"** — the wizard offers **one-click catalog import** (ties to §14).
5. **One-click "Launch my store"** flips it live on the subdomain; a shareable link + QR appear instantly for WhatsApp/Instagram.
6. **In-context help + a WhatsApp support line** throughout — meet Indian SMBs where they already are.

**Design principle:** every step has a working default, so the merchant can *skip everything and still have a live, sellable store*, then refine later. Simplicity is a feature we **design and test** (target: median time-to-first-product < 5 min, time-to-launch < 15 min), not a claim we assert.

---

*Companion docs: [design.md](design.md) (V1 baseline), [design-v3.md](design-v3.md) (AI Growth Engine add-on). Stage plans: [v2-stages/](v2-stages/), [v3-stages/](v3-stages/). Comparison figures are as of 2026 — re-verify Shopify/Woo pricing before publishing externally.*
