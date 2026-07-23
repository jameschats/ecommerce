# AI Growth — Marketing Content Engine

**Goal:** let a merchant turn one product into a month of ready-to-post marketing — captions, posters, WhatsApp blasts, email campaigns, blog articles — from inside the admin they already use.

> Ship **text first**, sell it, and only then spend money on pixels. Image and video generation are the two places where this product can lose money on every call, and no amount of demo polish changes that.

This doc supersedes the marketing half of [design-v3.md](../design-v3.md) and fills the gap that [v2-ai-store-setup.md](../v2-stages/v2-ai-store-setup.md) parked as *"AI marketing / product-image engine — separate plan."*

Companion module: [ai-support](../ai-support/README.md) — conversations, helpdesk and chatbot. This module wins attention; that one converts and keeps it. They share the brand kit, the entitlement layer (G0), and `AiCreditPricing`.

---

## Goal (the "why")

The commerce platform solves *"I need a store."* It does not solve the problem that actually kills small retailers: **nobody knows the store exists.** A saree seller in Coimbatore can list 200 products in an afternoon and then post to Instagram twice and give up, because writing captions and making posters is a daily grind they have no time or skill for.

That grind is a content-generation problem, and content generation is the one thing an LLM is unambiguously good at. The merchant already has the raw inputs sitting in our database — product names, descriptions, prices, images, categories, festivals, order history. Today those inputs produce a product page and nothing else.

**The bet:** the same product row that renders a PDP can render an Instagram caption, a WhatsApp broadcast, a poster, and a blog article. One click, thirty seconds, a week of content.

---

## Verdict on the idea (read this before building)

Honest assessment, because the pitch as originally framed contains one very good product and one bad business.

| Claim | Verdict | Reasoning |
|---|---|---|
| Merchants need marketing content help | ✅ **Real, acute** | It is the #1 unmet need of every SMB retailer. Not speculative. |
| We can generate it cheaply from data we already hold | ✅ **True for text** | Text is ~73–90% margin and the provider/credit plumbing already ships. |
| It differentiates the commerce platform | ✅ **Strongly** | Shopify has this; the Indian mid-market tools mostly don't bundle it with a store. This is a genuine reason to pick us. |
| Price it as a **separate add-on SKU** | ✅ **Adopt** | Text/image/video have costs an order of magnitude apart; one bundled price mismatches all three. |
| Build it as a **standalone service** (`ecomm.ai`, own DB, own identity) | ⛔ **Reject for now** | See below. Build the connector *interface*, not the second service. |
| Auto-publish to Instagram/YouTube/Facebook/Pinterest | ⏸ **Defer, hard** | See below. Enormous unglamorous cost, near-zero incremental value at v1. |
| Video generation ("one-click reels") | ⏸ **Defer to last** | Worst margin in the product (10–75%), highest expectation, most refund risk. |
| AI writes blog/SEO articles | 🟡 **Blocked** | We generate 1,500-word articles with **nowhere to publish them** — there is no `Articles` entity. Fix the destination first. |

### Why standalone `ecomm.ai` is the wrong move today

[design-v3.md §2](../design-v3.md) specifies a separate service, a separate `ai_engine` database, and a separate GUID identity space bridged to commerce `bigint TenantId`. Every bit of that complexity exists **for one reason only**: to serve merchants who are *not* our tenants.

That market is the single most crowded corner of AI SaaS. Predis.ai is this exact product, India-based and funded. Canva Magic Studio ships it free inside a tool merchants already open daily. Jasper, Copy.ai, Ocoya, Later, and Buffer all overlap. Shopify ships Magic natively. Winning there is a paid-acquisition contest, which is precisely the contest a solo builder cannot enter.

Meanwhile **the tenants we already have cost ₹0 to reach.** The whole advantage of building this is distribution we already own, and the standalone architecture throws that advantage away in exchange for a GUID-bridge and a second database.

**Decision: build it as a slice of `ecomm.api`, scoped to our own tenants — but build the `IPlatformConnector` seam from G1.** The distinction matters. A connector *interface* (`FetchProductsAsync`, Own/CSV implementations now) costs a day and keeps the Shopify/Woo door open. A separate *service* with its own database and GUID identity space costs months and has to be maintained whether or not that market ever materialises. Buy the option, not the building.

If attach rate among our own merchants is high *and* non-tenant merchants ask unprompted, revisit — the provider, credit, and connector layers all transfer. Extracting a proven module later is cheap; maintaining a speculative second service now is not.

### Why auto-publishing is deferred

"One click posts to Instagram" sounds like the product and is actually a compliance project. Instagram Graph API requires Business accounts linked to Facebook Pages, an app review with screencasts, and permissions that expire; YouTube and Pinterest each mean another OAuth app, another review, another token store, another rate limiter. That is months of work carrying permanent platform risk — Meta can revoke an app and take the feature down overnight.

**Generate → merchant reviews → merchant downloads/copies → merchant posts** delivers most of the value at a fraction of the cost, and it keeps a human in the loop before anything hits a real brand's public account. Build the publishing layer when paying merchants ask for it by name.

### The honest risk

The commerce platform is not finished monetizing. Per-plan feature gating **does not exist** — `Plan.MaxProducts`, `Plan.MaxOrders`, and `Plan.Features` are stored and displayed but enforced nowhere. Starting a second product before the first can enforce a paywall is the classic way to end up with two half-products. **G0 therefore includes the entitlement layer**, which the commerce platform needs regardless.

**Overall: build it — as a module, text-first, no auto-publish, gated behind a real entitlement check.**

---

## What already exists (reuse, don't rebuild)

The AI foundation shipped in V2 and is deliberately small. Do not duplicate any of it.

- **Provider abstraction** — `Features/Ai/IAiService.cs`: `AiPrompt` → `AiCompletion`, config-gated by `Ai:Provider` (`None`|`OpenAI`), `NullAiService` when off. Text-only, single method, no factory. Default model `gpt-4.1-mini`, deliberately non-reasoning (a reasoning mini can burn its budget on hidden thought and return empty content in JSON mode).
- **Credit metering** — `Features/Ai/AiCreditService.MeterAsync(feature, action)`: balance check → run → debit + signed `AiUsageLog` row in one save; `AppException(402)` when short; nothing debited if the call throws. Per-action costs in `AiCreditPricing.cs` — **adding a feature is one line.**
- **Credit entities** — `TenantAiCredit` (balance, cycle grant), `AiUsageLog` (signed ledger + real `CostMicros` for margin tuning), `AiCreditPack` (global), migration `172_ai_credits.sql`.
- **Top-ups** — Razorpay end-to-end via `PlatformPaymentGatewayFactory`, signature-verified, with the Angular widget in `admin-ai.component.ts`.
- **Send infrastructure** — `Features/Notifications/INotificationService.SendEmailAsync/SendSmsAsync(code, recipient, tokens)`, admin-editable `NotificationTemplate` rows, every send logged to `NotificationHistory`, pluggable `IEmailSender`/`ISmsSender`. **This is the campaign send layer.** What is missing is only the notion of a *campaign*, an *audience*, and a *schedule*.
- **Segmentation source** — `Features/Customers/` CRM with consent flags for email/SMS/WhatsApp and prebuilt segments (all/paid/repeat/prospect/subscribers).
- **Media** — `Features/Media/IMediaStorage.SaveAsync(...) → StoredFile(Url, ...)`, singleton, local disk today, documented as drop-in swappable for S3/R2.
- **Background work** — no Hangfire, no Quartz. `Features/Subscriptions/SubscriptionLifecycleService.cs` is a `BackgroundService` + `PeriodicTimer` and is **the template to copy**. Its own comment predicts this moment: *"moves to Hangfire in V2-6 when the platform gains more background jobs."*
- **Realtime** — SignalR hub at `/hubs/notifications`, `NotificationFeedService` drives the bell.
- **Frontend patterns** — `shared/ai-assist/ai-assist-button.component.ts` (the ✨ button), `core/services/ai-credit.service.ts`, flat routing in `app.routes.ts`, admin shell nav in `admin-layout.component.ts`.

**Guardrail worth preserving:** every shipped AI feature is *generate → preview → merchant confirms → apply*. Nothing is ever blind-written. Keep that.

---

## Architecture

**A vertical slice, `Features/Growth/`, inside `ecomm.api`.** No new service, no new database, no GUID bridge. New entities implement `ITenantScoped` and inherit tenant isolation free via the global query filter in `EcommerceDbContext`.

Three deliberate departures from the shipped AI slice:

**1. Capability split in the provider layer.** The seam already exists and is sound: `Features/Ai/IAiService.cs` is a config-swapped abstraction (`Ai:Provider`), nothing calls OpenAI directly, and `NullAiService` covers the off state. What is missing is not the interface but a *second implementation* and a *capability split*.

- `IAiService` stays as-is for text — do not widen it.
- Add `IImageAiService` (G4) and, if it ever ships, `IVideoAiService` alongside it, each independently config-gated so image spend can be switched off without touching text.
- Register implementations in a **capability-keyed registry** (`Ai:TextProvider`, `Ai:ImageProvider`) so a second text provider — Gemini Flash and DeepSeek are the obvious cost plays — is a class plus a config value, with no caller changes.
- **Hold off on routing rules and automatic failover** from [design-v3.md §3](../design-v3.md) until two providers are actually live. Failover logic that has never been exercised against a real outage is failover logic that will not work during one. Wire the second provider first, then route.

**2. Reserve-then-settle credits.** The shipped `MeterAsync` debits *after* success on the request thread. That breaks for image and video jobs, where the HTTP request returns long before the provider does. Extend — don't replace — `AiCreditService` with `ReserveAsync(feature, action, idempotencyKey) → reservationId` and `SettleAsync/RefundAsync(reservationId)`. Synchronous text keeps using `MeterAsync` unchanged. `AiUsageLog` is already a signed append-only ledger, so a refund is a positive row; no migration of existing balances is needed. This gets [design-v3.md §5](../design-v3.md)'s safety for async jobs without rewriting a working ledger.

**3. Entitlements become real.** Introduce `IEntitlementService.HasFeatureAsync(key)` reading parsed `Plan.Features` JSON, plus a `[RequiresFeature("growth")]` filter. This is a prerequisite for selling anything as a tier, and the commerce platform needs it regardless of this module.

**Scheduling:** a `GrowthSchedulerService : BackgroundService` modelled on `SubscriptionLifecycleService`. It runs **outside a request**, so every unit of work must be wrapped in `ICurrentTenantService.BeginScope(tenantId)` — without it the global query filters silently target `TenancyOptions.DefaultTenantId` and one tenant's campaign sends another tenant's products. This is the single most dangerous bug available in this module.

**Content lives in the commerce DB** so generated copy can reference real `ProductId`s and campaign results can be attributed against real orders — the thing a standalone `ai_engine` database could never do, and the strongest argument for integration.

---

## Naming

Stages are **G0–G6**. Do **not** reuse `AI-0…AI-6`: that prefix already means two different things — the shipped V2 phases in [v2-ai-store-setup.md](../v2-stages/v2-ai-store-setup.md) and the unbuilt stages in [v3-stages/](../v3-stages/). A third meaning would make the docs unreadable.

Migration band **`240–249`**. The V2 stage bands are reserved through `230–239` (V2-13 reporting) in
[v2-stages/README.md](../v2-stages/README.md) — `190–199` is V2-9 support ticketing and `200–209` is V2-10
observability, so AI work starts after them. [ai-support](../ai-support/README.md) takes `250–259`.

---

## Scope & checklist

### G0. Foundation — entitlements, reservations, scheduler

> **Not all of G0 belongs to AI Growth.** The first two items are shared platform infrastructure that [ai-support](../ai-support/README.md) also needs, and the entitlement layer is something the commerce platform needs regardless of either module. They live here only because this doc was written first. **Whichever module is built first should build the shared items** — marked 🔗 below — and the other module inherits them. Everything unmarked is AI-Growth-specific and should not block support work.

- [x] 🔗 **`IEntitlementService`** ✅ **Built** — `HasFeatureAsync(key)` parsing `Plan.Features` JSON (list *or* flag-object form, failing closed on anything unparseable), `EnsureCanAddProductsAsync` throwing **402 with the plan named**, `RemainingProductSlotsAsync` for bulk paths, and `GET /api/admin/plan-usage`. Enforced on product create and CSV import.
  > **`MaxProducts`, `MaxOrders` and `Features` were advertised on the pricing page and enforced nowhere** — every store had unlimited everything. Two asymmetries are deliberate: products are checked **on creation only**, so a downgrade or a limit correction can never strand a merchant with data they can't edit; and orders are **reported, not blocked**, because refusing a shopper's checkout over a billing ceiling turns a billing conversation into lost revenue and an instant churn reason. Verified live: a store at 10/3 gets 402 on create while its existing catalogue stays fully readable.
- [ ] 🔗 **Brand kit** — per-tenant tone (`friendly`/`premium`/`value`), language, target audience, emoji preference, hashtag set, do-not-say list. **Shared** — feeds marketing prompts here and reply tone in support C3.
- [ ] **Reserve/settle credits** — extend `AiCreditService` with `ReserveAsync`/`SettleAsync`/`RefundAsync`, idempotent on a caller-supplied key so a double-submit never double-charges. *AI-Growth-specific:* only long-running image/video jobs need this. Synchronous text — including every support AI action — keeps using the shipped `MeterAsync` unchanged.
- [ ] **Cycle-renewal grant hook** — close the known gap from `v2-ai-store-setup.md`: `CycleResetAt` is displayed but nothing ever refreshes the balance. Fold into the scheduler sweep.
- [ ] **`GrowthSchedulerService`** — `BackgroundService` + `PeriodicTimer`, per-tick DI scope, iterates due work wrapped in `ICurrentTenantService.BeginScope`. *AI-Growth-specific:* backs bulk generation and scheduled campaigns; support needs no scheduler through C4.
- [ ] **Capability-keyed provider registry** — split config to `Ai:TextProvider` / `Ai:ImageProvider` (keeping `Ai:Provider` honoured as a fallback so nothing shipped breaks), so a second provider is a class plus a config value. Routing and failover stay out until two are live.
- [ ] **Second text provider** — wire Gemini Flash or DeepSeek behind `IAiService` purely to prove the seam holds and to establish a cost floor for high-volume actions like bulk generation.
- [ ] **Super-admin credit-pack editor** — the other gap parked in V2; packs are SQL-seeded only today.

### G1. Text generation — the sellable MVP ✅ **Built** *(migrations `240–242`)*

- [x] **Seven content types**, credit costs in parens: **Instagram Caption** (3) · **Facebook Post** (3) · **WhatsApp Broadcast** (2) · **Email Campaign** (8, subject + body) · **Product Description** (5) · **Google Ads Copy** (5) · **Festival Offer** (3, no product — uses a brief). *Blog Article deferred to G5 (no `Article` destination yet); SEO Meta already ships as `AiImproveService.SeoAsync`.*
- [x] **Content types as data** — each is a record (key, credit cost, prompt shape, product-or-brief). Adding one is a single entry; no `GrowthPromptTemplate` table needed for the MVP.
- [x] **Brand kit** (`GrowthBrandKit`) — tone · language · audience · emoji · hashtags · do-not-say, rendered into every prompt via `BrandKitService.PromptFragmentAsync`. Editable at `/admin/growth/brand-kit`.
- [x] **Language selector** — English · Hindi · Tamil · Telugu · **Hinglish**, overridable per generation. Verified: Hinglish output is genuinely Hinglish ("Jaldi karein, offer limited time ke liye hai!").
- [x] **Generate → review → edit → copy/keep** — never auto-published; every generation saved to `GrowthContents` so nothing is paid for twice.
- [x] **Content library** — `/admin/growth/library`, filterable by type, copy-to-clipboard, editable, discardable.
- [x] **Gated** — `[RequiresFeature("growth")]`; on `growth`/`pro`/`enterprise` plans (migration `242`), Starter shows an upgrade card. Metered via `MeterAsync` (debit on success only), verified: balance 2000→1997 after one 3-credit Instagram caption.
- [ ] **Bulk generate** — select N products → queue one type each. Deferred with the scheduler (async, G0 remainder).
- [ ] **`IPlatformConnector` seam** — `FetchProductsAsync` with `OwnPlatformConnector` (direct DB) and `CsvConnector` implementations. Shopify/Woo deferred, but the interface lands now so adding them later touches no calling code.

### G2. Campaign builder ✅ **Built** *(migration `243`)*

- [x] **Goal picker** — New arrival · Festival · Weekend sale · Back in stock · Clearance → one goal fans out to Instagram + Facebook + WhatsApp + email in a single action. Each goal is a preset brief; the merchant's note is appended.
- [x] **Fan-out = four metered calls** — a campaign debits the four channel costs (16 credits total) and produces four editable `GrowthContent` rows linked by `CampaignId`. Verified live: 2000→1984, four on-brief Hinglish festival posts.
- [x] **Fault-tolerant** — a channel that fails mid-fan-out (credits run out, provider hiccup) is reported per-channel and the successes are kept; the campaign is never lost. Locked by test.
- [x] **`GrowthCampaign`** record + per-channel review/copy UI at `/admin/growth/campaigns`; recent campaigns listed and deletable.
- [ ] **Real sends for owned channels** — email/SMS via `INotificationService`, audience from `Features/Customers/` segments with consent respected. *Deferred — needs SMTP live first, and a scheduler.*
- [ ] **Export pack for unowned channels** — copy-ready caption + hashtags + asset + checklist. *Deferred with images (G4).*

### G3. Content calendar

- [ ] **Month/week grid** — every scheduled and published item across channels, drag to reschedule.
- [ ] **Festival calendar** — 30+ Indian festivals with lead-time nudges (*"Diwali is in 3 weeks — generate your collection campaign"*). This is the moat; global tools do not have it.
- [ ] **Weekly content plan** — generate a 7-day mix (new arrival / styling tip / review / offer / behind-the-scenes) in one action.

### G4. Image generation

**Do not start until G1 is generating revenue.** Costs real money per call.

- [ ] **`IImageAiService`** alongside `IAiService`, config-gated, off by default.
- [ ] **Formats** — Poster (20) · Story 9:16 (20) · Square 1:1 (20) · Festival Creative (25) · Background Removal (10).
- [ ] **Template-composited first** — brand kit + product photo + text overlay rendered server-side is near-free and often *better* than a diffusion model that mangles fabric and text. Treat generative image as the premium path, not the default.
- [ ] **Async via scheduler + SignalR bell**, reserve-then-settle credits, refund on provider failure.
- [ ] **Store to `IMediaStorage`** with a generated-asset library.

### G5. Blog & SEO surface

> Blocked by a real gap: we plan to generate 1,500-word articles and have **nowhere to put them.** `Articles` is deferred in four separate docs. Build the destination or don't build the generator.

- [ ] **`Article` entity + storefront routes** — list, detail, SSR/SEO meta, sitemap entry.
- [ ] **Admin article editor** reusing the CMS page-builder where possible.
- [ ] **AI draft → unpublished** — same pattern as the shipped `AiPageService`.
- [ ] **Internal linking** — articles link to real products.

### G6. Measurement & publishing (revisit gate)

- [ ] **Campaign analytics** — sends, opens, clicks for owned channels; attribute back to orders via coupon codes and UTM landings.
- [ ] **Then, only if merchants ask by name:** evaluate Meta Graph API publishing. Requires app review, Business account linking, token refresh, rate limiting, and an approval queue before anything posts to a real brand account.

---

## Data model

Migrations `240–249`, all tenant-scoped via `ITenantScoped` except where noted.

`GrowthBrandKit` (TenantId, Tone, Language, Audience, Hashtags, DoNotSay, LogoMediaId, PrimaryColor) — one row per tenant. `GrowthPromptTemplate` (ContentType, Version, SystemPrompt, UserPromptTemplate, IsActive) — seeded, admin-editable. `GrowthContent` (ContentType, ProductId?, CampaignId?, Language, Body, Status draft/approved/scheduled/published, MediaId?, CreatedBy) — the content library. `GrowthCampaign` (Goal, Name, Status, ScheduledAt, SegmentKey, CouponId?) — ties a fan-out together. `GrowthCampaignChannel` (CampaignId, Channel, ContentId, ScheduledAt, SentAt, Stats JSON) — per-channel state. `GrowthCreditReservation` (Feature, Credits, IdempotencyKey unique, State reserved/settled/refunded, ContentId?) — backs reserve-then-settle. `GrowthFestival` (**global, not tenant-scoped** — Name, Date, Region, SuggestedGoal) — the festival calendar. `Article` + `ArticleTag` (G5) — the blog destination.

Extends existing: `Plan.Features` JSON gains a `growth` key; `AiCreditPricing` gains the G1/G4 action costs.

---

## Endpoints

All merchant-facing under `[Authorize(Roles="Admin")]` + `[RequiresFeature("growth")]`.

`api/admin/growth/brand-kit` (GET, PUT), `/generate` (POST — type + productId + language), `/content` (GET list, GET/:id, PUT, DELETE), `/content/bulk` (POST), `/campaigns` (GET, POST, GET/:id, PUT, POST/:id/schedule, POST/:id/cancel), `/campaigns/:id/export` (GET — zip of copy + assets), `/calendar` (GET — range), `/festivals` (GET upcoming), `/images` (POST generate, GET status/:id) *(G4)*, `api/admin/articles` (CRUD) *(G5)*, `api/superadmin/ai/packs` (CRUD — closes the V2 gap).

---

## Frontend

New `features/admin/growth/` with standalone components, child routes added to the existing `/admin` block in `app.routes.ts`, a **Marketing** nav group in `admin-layout.component.ts` (Generate · Campaigns · Calendar · Library · Brand Kit), and `core/services/growth.service.ts` following `ai-credit.service.ts`. Reuse `shared/ai-assist/` for inline generation and the existing SignalR bell for async-job completion. A locked-state upsell card renders when `HasFeatureAsync("growth")` is false.

---

## Pricing & go-to-market

**Three modules, priced by how much each actually costs to serve.** The principle: subscribe what is cheap and predictable, meter what is expensive and variable.

| Module | Real cost/merchant/mo | Price | Model |
|---|---|---|---|
| Commerce Platform | infra only | ₹499–₹999 | Subscription |
| **AI Text Marketing** | **₹5–₹20** | ₹299–₹499 | Subscription add-on, generous allowance |
| **AI Image Marketing** | ₹150–₹300 | ₹599–₹999 | Subscription add-on, **hard monthly cap** |
| **AI Video Marketing** | ₹1,000–₹4,000+ | ₹1,999+ | **Usage-metered only.** Never unlimited. |

### The text cost number is not a typo

At the rates already configured in `appsettings.json` (`gpt-4.1-mini`, `InputUsdPerMTok: 0.15`, `OutputUsdPerMTok: 0.60`, `UsdToInr: 88`), the canonical SMB profile — 20 product descriptions + 30 social posts + 10 emails + 20 WhatsApp messages ≈ 80 requests/month, averaging ~1,200 input and ~800 output tokens — costs roughly **₹5/merchant/month**. Allowing for blog articles, retries, and prompt growth, budget **₹20**. That is ~98% margin, not the 73–90% assumed in [design-v3.md §9](../design-v3.md).

**The strategic consequence:** text is too cheap to ration. A stingy text allowance buys ₹15 of margin and costs the habit that makes the product sticky. Be generous with text, and put the meter where the money actually leaves — images and video.

### Structure

- **Text add-on: generous, near-flat.** A high allowance most merchants never approach. Cap exists only to stop scripted abuse, not to shape normal use.
- **Image add-on: subscription with a real cap.** Capped **before** the provider call, never reconciled after. A merchant who wants more buys credits.
- **Video: metered, always.** No unlimited tier at any price. This is the one place where a handful of heavy users can outspend the entire subscription base — the docs' own [v3-stage-5](../v3-stages/v3-stage-5-video-generation.md) rule ("max 50/mo on the highest plan, no exceptions") is correct and non-negotiable.
- **Top-ups** reuse the shipped Razorpay pack flow (Starter 200/₹199, Growth 600/₹499, Pro 1500/₹999) — no new billing code.

### One caution on stacking

₹999 commerce **+** ₹999 AI Marketing doubles the bill for an SMB whose alternative is "post it myself for free." That is a real conversion barrier at the exact moment the merchant has the least trust in the product.

**Recommendation: include a small text allowance in every commerce tier as the on-ramp.** Let merchants taste it, build the weekly habit, hit the ceiling, then upgrade. A paywall in front of an unproven benefit converts far worse than a ceiling reached by a merchant who already relies on the feature. The add-on SKU stays exactly as priced — this only changes where the first dose lives.

### Margin discipline

`AiUsageLog.CostMicros` already records real provider cost per call. Compare credits-charged against cost-incurred per feature from week one and reprice in `AiCreditPricing.cs` — it is a one-line change per action, by design. Any feature whose realised margin drops below 60% gets repriced or capped before it scales.

### Market reach

Keep the standalone service deferred (see *Verdict*), but **build the `IPlatformConnector` seam in G1** — `FetchProductsAsync` for Own/CSV now, Shopify and Woo later. The interface is nearly free today and preserves the Shopify/Woo optionality without paying for a separate service, database, and identity space up front. If non-tenant demand shows up, the connector is the only piece that needed to exist in advance.

---

## Gate

`Ai:Provider=None` leaves every Growth surface cleanly disabled with an explanatory banner and no 500s; a merchant on a non-Growth plan gets 402 + upsell, never a partial render; generating an Instagram caption for a real product returns brand-kit-shaped copy in the chosen language, debits exactly the priced credits, and writes one `AiUsageLog` row; a forced provider error debits nothing and a double-submit with the same idempotency key charges once; a campaign fans out to five channels, sends email only to consented customers in the chosen segment, and records per-channel state; a scheduled campaign fires from the background scheduler under the correct tenant scope — verified by scheduling on tenant A and confirming tenant B's data is untouched; the calendar shows the next Indian festival with a working generate CTA; `dotnet test ecomm.tests` green.

---

## Phasing

**G0 → G1 → stop and sell.** G1 is a complete, chargeable product on its own; do not start G2 until merchants are actually generating content. Then G2 → G3. **G4 (images) and G5 (blog) only after G1 shows revenue.** G6 publishing only on named demand. Video generation ([v3-stage-5](../v3-stages/v3-stage-5-video-generation.md)) stays parked — revisit when image generation has proven both the async pipeline and the margin discipline.

---

## Dependencies

Shipped: `IAiService` + credits (V2 AI-0…AI-6), `INotificationService`, `Features/Customers/` segments + consent, `IMediaStorage`, SignalR bell, `ICurrentTenantService.BeginScope`. Built here: entitlements, reservations, scheduler. Unbuilt elsewhere and **not** required by G0–G3: WhatsApp Cloud API transport (V2-6c/V2-11 — until then WhatsApp is export-only), Hangfire (the `PeriodicTimer` pattern suffices at this scale).

---

## Open items for James

1. **Roadmap conflict** — [design.md §2](../design.md) defines V3 as *marketplace*; [design-v3.md](../design-v3.md) redefines V3 as *AI Growth Engine*. Two docs, one version number. Recommend: keep V3 = marketplace, and treat AI Growth as a **cross-version module** (this doc), not a version.
2. **`document-ai-growth/`** — an empty folder at the repo root, presumably an earlier attempt at this location. This doc lives at `documents/ai-growth/` with the rest of the docs; the root folder should be removed.
3. **`design-v3.md`'s standalone architecture** is rejected here. If you still want the standalone product, that is a business decision worth making explicitly rather than by architecture drift — the counter-argument is in *Verdict* above.

---

**Status:** ⬜ Not started.
