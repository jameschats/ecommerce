# AI Engines — Shipped Reference (Marketing + Commerce)

**Status as of 2026-08-29.** This is the consolidated, authoritative record of what shipped for the two
AI engines, what's deferred, and the concrete action items still open. It supersedes the "not started"
footers in the older module docs ([ai-growth](../ai-growth/README.md), [ai-commerce-engine-design](ai-commerce-engine-design.md)).
Per-phase detail lives in [phase-3](phase-3-commerce-data-personalization.md) and [phase-4](phase-4-marketing-engine-gaps.md);
this doc is the single-page overview.

> **Rule of thumb:** *Marketing gets people to the store; Commerce converts them once they're there.*
> Both are in-platform slices of `ecomm.api` scoped to our own tenants — no standalone service.

---

## 1. AI Marketing Engine ("Growth") — M1–M4 ✅ live

Built on the already-shipped generation core (Brand Voice, 7 content types, campaign fan-out, AI images,
content library, credit metering, plan gating).

| Milestone | What it does | Key code |
|---|---|---|
| **M1 — Sends, scheduling, bulk, export** | Email a generated campaign to a customer **segment** (email-marketing consent enforced on top of every segment), **now or scheduled** via Hangfire under a re-established tenant scope; **bulk generate** one content type across ≤50 products async; **export pack** (`.md`) for channels we don't send to directly. | `Features/Growth/GrowthCampaignSendService.cs`, `GrowthBulkService.cs`; migration `278`; admin Campaigns + Generate screens |
| **M2 — Content + festival calendar (G3)** | Month grid merging **Indian festivals** (global `GrowthFestival`, ~35 occasions 2026 H2–2027, seeded) with the store's scheduled/sent campaigns; **lead-time nudges** ("Diwali in 3 weeks…") and one-click "Generate campaign" prefill. The moat global tools lack. | `Features/Growth/GrowthCalendarService.cs`, entity `GrowthFestival`; migration `279`; admin `/admin/growth/calendar` |
| **M3 — Blog + AI writer (G5)** | The **`Article`** destination the blog writer was blocked on: storefront `/blog` (SEO meta + `BlogPosting` JSON-LD + sitemap), admin editor with a metered **AI first-draft** (`growth-article`, gated behind the growth plan). | `Features/Blog/ArticleService.cs`, `BlogControllers.cs`, entity `Article`; migration `280`; storefront `blog/`, admin `blog/` |
| **M4 — SEO assistant** | The two new `IAiService` consumers Track A names: **bulk keyword ideas** (grounded in the store's real categories) and **content briefs** that hand straight to the blog writer. | `Features/Growth/GrowthSeoService.cs`; admin `/admin/growth/seo` |

**Credit costs added** (`Features/Ai/AiCreditPricing.cs`): `growth-article` 12, `growth-keywords` 2, `growth-brief` 3.

### Deferred (blocked on external accounts, NOT code) — see §3
Social posting (Meta/Instagram, Pinterest), performance marketing (Meta Ads, Google Ads, product-feed
sync), and WhatsApp Commerce. Generation for these channels already works; only the connect/publish
integrations remain, to be built "when a merchant asks by name" per the ai-growth Verdict. Also deferred:
site-health monitoring (deterministic crawl, lower priority).

---

## 2. AI Commerce Engine — C1–C4 ✅ live

The Shopping Assistant (module 3.2) was already shipped (Phase 3 Track A). These four milestones built the
rest of the engine on one shared data layer.

| Milestone | What it does | Key code |
|---|---|---|
| **C1 — Commerce data layer** | Server-side **behavioural event capture** (view / add-to-cart / remove). Public `POST /api/events` → in-memory buffer → **per-minute Hangfire flush**, batched per tenant under `BeginScope` — never a synchronous insert on the storefront hot path. First-party visitor id (localStorage), SSR-safe. **Verified end-to-end on prod.** | `Features/Commerce/CustomerEventService.cs`, `EventController.cs`, entity `CustomerEvent`; migration `281`; `core/services/event.service.ts` |
| **C2 — Trending Now** | A genuinely **computed** trend (unlike manual "Best Sellers"): weighted views + add-to-cart (events) + purchases (orders) over a window, in-stock only. | `ProductService.GetTrendingAsync`; `GET /api/catalog/trending`; `shared/trending-rail/` |
| **C3 — Personalized Picks + Recently Viewed** | Per-visitor picks from category affinity over their own events, gated at ≥2 interactions with **trending cold-start fallback**; server-side recently-viewed. | `ProductService.GetPersonalizedAsync/GetRecentlyViewedAsync`; `GET /api/catalog/personalized` + `/recently-viewed`; `recommended-rail`, `recently-viewed-rail` |
| **C4 — Merchant controls + demand→pricing** | Per-product **pin/exclude** from recommendations (admin toggles), applied across strategies; and the **Dynamic Pricing demand signal** wired to real 7-day event velocity (was hardcoded 0) — closes the Phase-5 gap. Market-based, never per-shopper. | `Products.ExcludeFromRecommendations/PinnedInRecommendations` (migration `282`); `PricingEngineService` demand signal |

**Surfacing:** rails ship two ways — hardcoded on the cart page, and a **`ProductRecommendations` theme
section** (source = trending \| recommended \| recently-viewed; grid/carousel; self-hiding) that merchants
can drop on any page template via the theme editor. `Features/Cms/SectionTypes/SectionTypeRegistry.cs` +
`storefront-section.component.ts`.

**Privacy:** first-party, purpose-limited, no PII in event metadata, per-store `BehaviorTrackingEnabled`
toggle (default on). **Open item:** the India **DPDP Act** consent review before broad rollout — flagged,
not resolved (see §3).

---

## 3. Pending action items (need external setup / a decision — not code)

These are the only things standing between "built" and "fully live for merchants". Each needs YOUR action
or a business decision; the platform-side code is either done or a small wire-up once the account exists.

| # | Item | What's needed from us | What's blocked / why |
|---|---|---|---|
| 1 | **Meta (Instagram + Facebook) organic posting** | A Meta Business account + a reviewed Meta app (Graph API permissions, screencast review). | Social posting Track B. Caption/image generation already done — only OAuth account-linking + scheduled publish remains. |
| 2 | **Pinterest posting** | A Pinterest developer app + OAuth review. | Same as above (Track B, V1 scope). |
| 3 | **Meta Ads + Google Ads** | Ad-account access + Marketing API / Google Ads API apps (separate scope from #1). | Performance-marketing Track C: product-feed sync + campaign wizard + ROAS reporting. |
| 4 | **WhatsApp Commerce** | A live WhatsApp Business (BSP) account — Gupshup funded but templates unapproved; Meta catalog. | Catalog sync + conversational checkout. Highest-risk item; provider code exists but is **unverified against a real account**. |
| 5 | **DPDP consent review (behavioural capture)** | A legal/compliance decision on consent for on-site behaviour tracking under India's DPDP Act. | AI Commerce capture is live but first-party + purpose-limited with an on/off toggle; broad rollout wants this resolved. |
| 6 | **Ops go-live carry-overs** | Change prod admin password (still `Admin@123`), real GSTIN, MSG91 DLT registration, self-host Umami + set `UMAMI_*`. | Pre-existing operational items, unrelated to the AI engines. |

**Security reminder (standing):** rotate the secrets exposed in `/root/deploy-wavcommerce.sh` (GitHub PAT,
Cloudflare token, DB password).

---

## 4. Test / deploy notes
- `ecomm.tests` restored to compiling during this work (accumulated constructor drift fixed); **423 passing**.
  The 3 `GupshupWhatsAppProviderTests` failures are pre-existing (stale vs the GatewayAPI rewrite, item #4).
- All migrations `278–282` applied cleanly on prod. Deploy path is Docker (`/root/deploy-docker.sh`).
- Commits: AI Marketing `4506ef9`→`ec9c624`; AI Commerce `925d352`→`1467198`; theme section `7482ca6`.
