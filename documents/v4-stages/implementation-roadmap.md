# v4 — Implementation Roadmap

**Purpose:** Ties the five v4 design docs (`ai-commerce-engine-design.md`, `ai-marketing-engine-design.md`, `auth-notifications-design.md`, `helpdesk-livechat-chatbot-design.md`, `commerce-platform-feature-draft.md`) and `pending-tasks.md` to the **actual current codebase**, and sequences what's left to build. Produced by auditing the real code, not re-deriving scope from the design docs alone — several areas assumed a blanker slate than what actually exists.

---

## 0. Read this first: the docs assume less exists than it does

The single biggest correction this roadmap makes: **the AI Marketing Engine is largely already built**, under the name **"Growth"** (`Features/Growth/*`), and **Support/Helpdesk has a real unified backend already**, exactly matching the "one backend model" principle the helpdesk doc asks for. Building either from scratch would be a real regression. Section 1 is the full ground-truth audit — read it before estimating effort on anything below, since "design a Brand Voice system" and "wire the existing BrandKit into three new surfaces" are very different sizes of work.

---

## 1. Ground Truth — What Already Exists

### 1.1 Auth (`auth-notifications-design.md` §2)

| Design doc asks for | Status |
|---|---|
| Customer mobile OTP + email/Google secondary | ✅ Built. `Features/Auth/` — 3 toggleable `AuthProvider` rows per tenant (Email+Password, Mobile OTP, Google), enforced server-side (`RequireEnabledProviderAsync`) |
| Merchant/Super Admin email+password primary | ✅ Built, same provider system |
| JWT with roles/claims, refresh rotation | ✅ Built. `JwtTokenService` — HMAC-SHA256, role + per-permission `perm` claims, hashed rotating refresh tokens |
| Account lockout | ✅ Built. 5 attempts / 15-min lockout on `User` |
| Password reset + email verification | ✅ Built (inline in `AuthService`, not a separate module) |
| **2FA for Merchant/Super Admin/Staff** | ❌ **Absent entirely.** No TwoFactor/MFA code anywhere in the API. This is the one real gap in auth. |

### 1.2 Notifications (`auth-notifications-design.md` §3)

| Design doc asks for | Status |
|---|---|
| Template system, delivery history | ✅ Built. `NotificationTemplates` (admin-editable per code+channel) + `NotificationHistory` (every send logged) |
| "One notification engine, many channels" channel router | 🟡 **Not what exists.** Today it's a flat `SendEmailAsync`/`SendSmsAsync` dispatcher (`INotificationService`), config-selected provider per channel (`Email:Provider`, `Sms:Provider`) — no generic `SendAsync(channel, ...)` abstraction, no fallback-chain concept |
| WhatsApp channel | ❌ Absent. Zero WhatsApp send capability anywhere (one unrelated label string in Growth's channel list) |
| Push (FCM/APNs) | ❌ Absent — expected, no mobile app exists yet |
| Consent/opt-in/preference model | ❌ Absent. No consent, DND, or unsubscribe fields anywhere in the schema |

### 1.3 Real-time infra

✅ SignalR already live: `NotificationHub` backs the notification bell (customer + admin) **and** shopper↔merchant conversation messages (`ConversationRealtime`). This is real, reusable infrastructure for a livechat widget — not a from-scratch build.

### 1.4 AI infrastructure (underpins both AI Marketing and AI Commerce docs)

✅ **Excellent existing foundation** — the exact abstraction pattern this roadmap will reuse everywhere new AI capability is added:
- `IAiService` (text) / `IImageAiService` (images) — provider-agnostic, config-selected (`Ai:Provider=None|OpenAI` etc.), currently only OpenAI wired
- `AiCreditService` + `AiUsageLog` — real per-tenant credit ledger, atomic metering (failed call ⇒ no debit), tracks actual provider cost (`CostMicros`) for margin visibility, per-feature pricing constants
- This is already exactly the "Credits map to compute cost" principle the marketing doc asks for (§1.5) — it's built, just needs new feature keys as new AI capabilities ship

### 1.5 AI Marketing Engine (`ai-marketing-engine-design.md`) ≈ "Growth" today

| Design doc module | Status |
|---|---|
| **2.1 Brand Voice** (foundation) | ✅ Built. `BrandKitService`/`GrowthBrandKit` — tone, language (incl. Hinglish), audience, emoji/hashtag prefs, do-not-say list. Already injected into every generation call. |
| **2.2 Generate** (product copy, ad copy, social captions, email copy) | ✅ Built. `GrowthGenerationService` — generates Instagram/Facebook/WhatsApp/email/product-description/festival/Google-Ads copy, all brand-voice-aware |
| **2.3 Product Images** | ✅ Built, and arguably ahead of spec. `GrowthImageService` (new marketing/lifestyle images, 4 styles × platform formats) + `CatalogImageService` (bulk backfill of missing catalog photos) |
| **2.4 Content Library** | 🟡 Partial. `GrowthContent` stores generated content with edit/status — versioning and explicit cross-channel reuse tracking need verification/hardening, not a new build |
| **2.5 SEO** (metadata gen) | 🟡 Partial. `AiImproveService.SeoAsync` generates title+meta description. Missing: bulk keyword research, schema markup automation, site health monitoring, content briefs (items 2–5 of the doc's 5 sub-features) |
| **2.6 Social Media Marketing** (scheduling/posting) | 🟡 Content generation exists (Growth); **actual posting/scheduling to Meta/Pinterest does not** — this is genuinely new work (OAuth connectors + scheduler) |
| **2.7 Campaigns** (email/lifecycle) | 🟡 `GrowthCampaignService` fans one goal out to 4 channels in one action — good foundation, but no lifecycle automation (welcome/abandoned-cart/win-back flows) yet |
| **2.8 Performance Marketing** (Meta/Google Ads integration) | ❌ Absent. No ad-account connectors, no feed sync, no ROAS reporting |

**Net:** this is not a "build the AI Marketing Engine" project — it's "formalize Content Library + SEO's remaining 3 sub-features + build the two genuinely-new integration surfaces (Social scheduling, Performance Marketing)."

### 1.6 Helpdesk / Livechat / Chatbot (`helpdesk-livechat-chatbot-design.md`)

| Design doc asks for | Status |
|---|---|
| **"One backend model, many surfaces"** (§1, principle 1) | ✅ **Already exactly this.** `SupportTicket`/`SupportMessage`/`SupportTicketActivity` shared across a `ConversationAxis` (`MerchantPlatform` and `ShopperMerchant`) — Contact form, Inbox, and merchant↔platform escalation all land in the same model already. The doc's own §10 build-sequence item #1 ("unified backend ticket/conversation model — foundation") is **done**. |
| AI-drafted replies grounded in real data | ✅ Built, and matches the doc's guardrail almost verbatim. `SupportDraftService` grounds drafts in the actual thread + order/shipment/tracking + published policies, explicitly refuses to invent facts, merchant-in-the-loop only |
| Reviews moderation | ✅ Built (`ReviewController`/`ReviewService`) |
| FAQ / Knowledge base as **structured, bot-readable** content | ❌ Absent — this specific shape (not just a static page) is net-new |
| Chatbot core (grounded answering) | ❌ Absent entirely |
| Livechat widget | ❌ Absent (no storefront/admin chat UI found), though the SignalR infra it would sit on (§1.3) already exists |
| WhatsApp channel for the bot | ❌ Absent (depends on Phase 1's WhatsApp work below) |

**Net:** the foundation (unified ticket model + grounded-drafting pattern) is done. What's missing is genuinely new: FAQ-as-structured-data, the chatbot itself, and the livechat widget UI.

### 1.7 AI Commerce Engine (`ai-commerce-engine-design.md`)

| Design doc module | Status |
|---|---|
| Shared commerce data layer (§6, step 1 — "foundation") | ❌ **The real gap.** No server-side browsing/session event capture exists. `RecentlyViewedService` is frontend-only `localStorage`, capped at 12, never reaches the backend. |
| Personalization — non-personalized fallbacks (Best Sellers/Trending) | 🟡 Partial via existing rule-based `RelatedProducts` and `FrequentlyBoughtTogether` (real order-history co-purchase logic, not ML) — a reasonable cold-start fallback already, but no "Personalized Picks" tier exists since there's no browsing-history capture to personalize from |
| AI Shopping Assistant | ❌ Absent. Per the helpdesk doc's own §7, this should **converge with the support chatbot** — build once. |
| Dynamic Pricing | ❌ Absent, as expected — the docs themselves flag this as highest-risk/last |

### 1.8 Super Admin (`commerce-platform-feature-draft.md` §1.1)

Nearly everything **listed as confirmed** is built: store directory + lifecycle + Health Score + KPIs, platform-wide analytics, revenue tracking, billing/invoicing, plan management, AI credit administration, platform payment config, helpdesk escalation queue, announcements, blocklist (signup-time only — no store/IP blocking), staff (flat role, explicitly deferred RBAC), audit log, impersonation. This entire layer needs no re-architecture — only the items already flagged as unconfirmed in the doc's own "gaps" list are genuinely absent:

- ❌ Merchant onboarding & KYC/verification (onboarding today is self-serve setup wizard only, no review/approval step)
- ❌ Theme marketplace management (themes are merchant-self-service only)
- ❌ App/plugin marketplace (nothing exists)
- ❌ Global configuration & policy management
- ❌ Security & fraud monitoring
- 🟡 "Livechat with merchants" is really ticket-reply, not presence-based live chat (same underlying gap as Helpdesk §1.6)

### 1.9 Infrastructure / Portability (`pending-tasks.md` item 12)

This is the item the user's "no compromise… must port to Azure/AWS" instruction maps to directly. Audited for real coupling, not assumed:

| Concern | Finding |
|---|---|
| Media/file storage | ✅ **Already portable by design.** `IMediaStorage` interface, `LocalDiskStorage` is the only implementation today, explicitly documented as swap-ready for S3/R2 with zero caller changes. No action needed until migration day. |
| Database | ✅ Portable. Pomelo MySQL over TCP, no VPS-specific SQL observed — a connection-string swap to RDS/Azure Database for MySQL should work as-is. |
| Secrets/config | 🟡 Standard `appsettings` + env-var override, no plaintext secrets committed (per existing guardrail). Fine as-is; swap to Key Vault/Secrets Manager is a migration-day concern, not a now concern. |
| DataProtection key ring (encrypts per-tenant secrets like Razorpay keys) | 🟡 **Persisted to local disk.** Fine on one instance; would break if horizontally scaled to multiple instances without a shared key store (Azure Blob/AWS-backed persistence). Not urgent at current scale, but a real blocker the moment a second app instance is added. |
| Scheduled/recurring work | 🟠 **Real gap.** `SubscriptionLifecycleService` is an in-process `BackgroundService` timer — and its own comment already says *"moves to Hangfire in V2-6 when the platform gains more background jobs."* v4 is exactly that moment: WhatsApp broadcasts, social-post scheduling, dynamic-pricing sweeps, lifecycle email/SMS reminders are all new recurring work. Running duplicate in-process timers across multiple instances is harmless for an idempotent status sweep, but would be a real bug (duplicate sends, duplicate spend) for anything in that new list. |
| Containerization | 🔴 **Absent.** No `Dockerfile`/`docker-compose` anywhere in the repo. Every cloud target (ECS, AKS, Azure Container Apps, App Service containers) wants an image. This is the cheapest thing to fix now and the most expensive to retrofit later — do it before the codebase gets much bigger, not at migration time. |
| Real-time (SignalR) backplane | 🟡 In-memory today — fine at one instance, needs a Redis backplane the moment there's more than one API instance (same trigger point as the DataProtection key ring, worth doing together). |

**Bottom line:** the app is in better shape for portability than "no compromise" implies — the two genuinely valuable actions *right now*, cheaply, are containerizing and introducing a real job scheduler. Everything else (Redis backplane, shared DataProtection store, Key Vault) is correctly deferred until a second instance is actually needed — building them now would be the premature-abstraction CLAUDE.md already warns against.

---

## 2. Architecture Decisions for v4

These apply across every phase below, not to one feature:

1. **Every new external capability follows the existing `IAiService`/`IMediaStorage`/`IPaymentGateway` pattern**: a narrow interface, config-selected implementation, no caller-side knowledge of the concrete provider. Concretely, this means introducing `IWhatsAppProvider` (BSP-backed), `IPushNotificationProvider` (FCM/APNs), and generalizing notifications into a real `INotificationChannel` router — not four bespoke integrations.
2. **WhatsApp is one integration, not two.** Both design docs (Notifications §3.3, Commerce-platform §"WhatsApp Commerce") explicitly call this out — one BSP (Gupshup/Interakt/Zoko) connection serves transactional notifications AND WhatsApp Commerce AND the chatbot's WhatsApp channel. Build the BSP connector once in Phase 1, reuse it three times.
3. **Shopping Assistant and Support Chatbot are one build, not two.** The helpdesk doc says this directly (§7) — plan and build them together in Phase 2/3, don't build a chatbot then a separate assistant then merge them.
4. **Containerize before the next major feature wave, not after.** Phase 0.
5. **Introduce a real scheduler (Hangfire, DB-backed — already on MySQL, no new infra dependency) before adding any new recurring job**, replacing the ad-hoc `BackgroundService` pattern going forward. Existing sweep can migrate opportunistically, not urgently.
6. **No personalized/individual-based pricing or messaging, ever** — the Commerce Engine doc's exclusion is a hard legal/ethical line, not a v1-only guardrail; encode it in code review checklists for that module specifically.
7. **Mandatory human-in-the-loop stays non-negotiable** for every AI generation surface (already how Growth/SupportDraftService work today) — extend the same posture to every new AI feature, no "auto-publish" toggles in v4.

---

## 3. Phased Roadmap

Sequenced by genuine technical dependency (what must exist before the next thing can be built correctly), cross-referencing each design doc's own build-sequence section rather than overriding it — deviations from a doc's own sequence are called out explicitly.

### Phase 0 — Portability Foundation
*Do this first: cheap now, expensive to retrofit once more services/state exist.*
- Containerize API (Dockerfile) and Angular SSR app (Dockerfile); `docker-compose.yml` for local dev + VPS parity (MySQL can stay bare-metal or containerize too — either works)
- Introduce Hangfire with MySQL storage; migrate the subscription lifecycle sweep to it as the first job (low-risk validation of the new pattern before anything higher-stakes depends on it)
- No other infra changes needed yet (Redis backplane / shared DataProtection store / Key Vault are correctly deferred — see §1.9)

### Phase 1 — Notification Router + WhatsApp Foundation + 2FA
*Unlocks WhatsApp for three later phases (Commerce, Marketing, Helpdesk) — build once, here.*
- Refactor the flat email/SMS dispatcher into a real `INotificationChannel` router (per `auth-notifications-design.md` §6 build sequence, step 4) — template + history + consent shared across channels, not per-channel
- Add consent/opt-in/preference fields to the user identity model (§4 of that doc) — required before any WhatsApp/SMS marketing send, and before DLT/DND compliance work can be meaningful
- Build the WhatsApp BSP connector as one new `IWhatsAppProvider` implementation plugged into the router
- 2FA for Super Admin (mandatory) + Merchant Admin (default-on, per doc's own recommendation) — independent of the above, can run in parallel
- Preference center UI (customer-facing opt-in/out, merchant-facing compliance view) — last, once the model exists to drive it

### Phase 2 — Helpdesk: FAQ, Chatbot, Livechat
*Foundation (unified ticket model) already exists — this phase is the genuinely-new 3 pieces.*
- FAQ/Knowledge base as structured, bot-readable content (not a static page)
- Chatbot core — grounded retrieval over catalog + orders + FAQ, same "never invent, hand off when ungrounded" posture as the existing `SupportDraftService`
- Livechat widget (storefront), bot-first response, reusing the existing `NotificationHub`/`ConversationRealtime` SignalR infra rather than a new real-time system
- Escalation → existing ticket model (already built; this phase only needs to wire the trigger and context handoff)
- WhatsApp channel for the bot, reusing Phase 1's BSP connector

### Phase 3 — Commerce Data Layer + Personalization + Shopping Assistant
- Server-side browsing/session event capture — the real missing foundation the AI Commerce doc calls for as its own step 1; this also directly feeds the platform Analytics work in Phase 6
- Personalization strategies: keep existing FBT/RelatedProducts as the cold-start fallback (already good), add "Personalized Picks" once the event data above has enough volume
- Shopping Assistant, **merged into Phase 2's chatbot** as one assistant with two capability sets — do not build a second bot

### Phase 4 — AI Marketing Engine: Close the Gaps
*Most of this already ships as Growth — this phase is specifically the parts that don't exist yet.*
- Formalize Content Library (versioning, explicit cross-channel reuse — verify/harden `GrowthContent`, don't rebuild)
- SEO: remaining sub-features — bulk keyword research, automated schema markup, site health monitoring, content briefs
- Social Media Marketing — actual posting/scheduling (Meta + Pinterest OAuth connectors, per the doc's own V1 channel scope), built on Phase 0's scheduler
- WhatsApp Commerce — catalog sync + conversational checkout, reusing Phase 1's BSP connector
- Performance Marketing — Meta/Google Ads account connectors, product feed sync, unified ROAS reporting (feeds into Phase 6's Analytics)

### Phase 5 — Dynamic Pricing
*Last, per the AI Commerce doc's own reasoning: highest legal/trust risk, needs the most order-history data.*
- Approval-mode only at launch (per doc's explicit v1 recommendation)
- Hard floor/ceiling, full audit log, change-frequency limits — all non-negotiable per the doc, not phased in later
- Depends on Phase 3's data layer for demand signals

### Phase 6 — Platform Surfaces (pending-tasks.md items 6–11, Super Admin gaps)
*`pending-tasks.md` already sequenced these with real dependency notes — respected here rather than re-derived:*
- Public API + webhooks (item 6) — must come before App Marketplace and Theme Store, since both need something stable to plug into
- App Marketplace: 1–2 first-party apps first, to validate the base before any third-party submission process (item 7)
- Theme Store as its own designed surface (item 9)
- Migration/onboarding tooling from Shopify/Zoho/Instamojo/Dukaan (item 8)
- Analytics scope (item 10) + Reports (item 11) — designed together since Reports overlaps Import/Export infra, and Analytics now has real data to work with post-Phase 3
- Remaining Super Admin gaps: merchant KYC/onboarding review, fraud monitoring, granular staff RBAC, global config/policy management

### Explicitly deferred, not forgotten
- **Vertical-specific features** (the 10–12 vertical list) — premature before v4's core surfaces stabilize; revisit once verticals are actually finalized (the doc itself flags this as pending)
- **Mobile apps** — depend on Push notifications (Phase 1) and should follow, not lead, that work

---

## 4. Decisions Made (2026-08-21)

Resolved by discussion — recorded here so they don't need re-litigating in each phase's detailed plan:

1. **Dynamic Pricing ships in v4** — approval-mode only, Phase 5, hard floor/ceiling + full audit log, exactly per the design doc's own v1 recommendation. *(was open question 1)*
2. **Competitor-price signal is skipped for v1.** Dynamic Pricing launches on inventory + demand + seasonality signals only; no build-vs-buy decision needed yet since the signal itself isn't in v1 scope. Revisit if/when it's actually wanted. *(was open question 2)*
3. **WhatsApp BSP vendor — still open, needs a comparison pass** before Phase 1 starts (Gupshup vs. Interakt vs. Zoko: India pricing, WABA onboarding speed, API quality/docs). SMS is already decided in code (MSG91). Email ESP (SendGrid/SES) is a low-stakes swap, not blocking. *(was open question 3, partially resolved)*
4. **Merchant Admin 2FA is default-on with opt-out at launch**, tightened to mandatory later once onboarding volume and support capacity are known. Super Admin 2FA remains mandatory, unaffected. *(was open question 4)*
5. **Chatbot never auto-approves refunds** — every refund/return request always goes to a human, no threshold-based exception. Consistent with the "no auto-publish/auto-approve" posture used everywhere else in v4. *(was open question 5)*
6. **Solo-seller fallback: bot attempts full resolution first**, only queues what it genuinely can't solve — rather than "escalating" into a ticket a solo merchant may not see for a day. Standard escalation triggers (frustration, judgment calls) still apply when a human is actually available. *(was open question 6)*
7. **App Marketplace: first-party apps first, third-party process later** — matches Phase 6's existing sequencing, unchanged. *(was open question 7)*
8. **Verticals list left as-is (12 items), revisit at Phase 6** — no forced narrowing now. *(was open question 8)*

---

## 5. Summary Table — Effort Reality Check

| Area | Design doc implies | Actual remaining work |
|---|---|---|
| AI Marketing Engine | Build 8 modules from scratch | Close ~4 gaps in an already-shipping system (Growth) |
| Helpdesk foundation | Build unified ticket model first | **Already done** — build the 3 genuinely-new pieces (FAQ, bot, livechat UI) |
| AI Commerce Engine | Build 3 modules + shared data layer | Shared data layer is the real gap; Personalization has a working fallback already; Assistant merges with Phase 2's bot; Dynamic Pricing is genuinely from scratch |
| Auth | Build login flows | Login is done; only 2FA is missing |
| Notifications | Build "one engine, many channels" | Two channels exist but need re-architecting into a router; two channels (WhatsApp, Push) are net-new |
| Super Admin | Build the full surface | Nearly all built; gaps are exactly what the doc itself already flagged as unconfirmed |
| Infrastructure | "Start cheap, migrate later" | Storage/DB already portable; containerizing + a real scheduler are the two concrete actions needed now |
