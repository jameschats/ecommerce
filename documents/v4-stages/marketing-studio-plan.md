# Marketing Studio (Social + AI Creative) — vision & plan

**Status: requirements-gathering.** This captures the user's vision verbatim-in-structure. The *plan/design* section is filled in AFTER the open decisions below are answered (user asked to give all inputs first). Goal stated by the user: **make marketing a flagship strength of WavCommerce — "the business should focus on marketing solidly."**

Related: [marketing-engine-v2-plan.md](marketing-engine-v2-plan.md) (email/deliverability/automation backlog), [ai-marketing-engine-design.md](ai-marketing-engine-design.md), M1–M4 shipped (campaigns, festival calendar, blog+AI writer, SEO).

---

## 1. Requirements (from the user)

### Channels to target
Facebook, LinkedIn, Instagram, Pinterest, YouTube, Google Ads, WhatsApp.

### Creative types to generate
- **Text** (short-form) — captions/posts
- **Image / posters**
- **Video** (short-form — Reels/Shorts)
- **Blogs** (already shipped in M3)

### Pages / surfaces the merchant needs
1. **Company profile page** — collect company info: logo, company name, theme colors. (Feeds every creative.)
2. **Connections page** — connect all social accounts; each shows **connected / not-connected** status.
3. **Weekly plan** — a plan for the week: how many text posts, posters, videos.
4. **Scheduler page** — schedule the planned creatives to post to the connected social accounts.
5. **Poster editor** — multi-step editor for image/poster creation.
6. **Video editor** — multi-step editor for video creation.

### Poster types
- **Organization-based** (brand/store promo)
- **Product-based** (specific product promo)

### Branding rules
- Creatives are **company-theme-based** by default (logo, company name, theme colors).
- **Include/exclude toggles** (checkboxes): include logo? include company name? etc.
- Before generating a poster or video, collect **inputs first** (what is this poster/video for? include logo? include company name? goal? platform? etc.) — not a one-click black box.

### Video philosophy (explicit)
- **Do NOT ask an LLM to "edit video" frame-by-frame.** Build a pipeline around specialized APIs/models; the LLM provides the *intelligence* (script/plan), deterministic software does the *assembly*.
- Merchant's happy path: **Product → Goal → Platform → Generate.**
- Pipeline stages:
  1. **LLM → structured video plan** (hook, duration, ordered scenes with visual type + on-screen text). Example scene JSON: `{duration, visual: hero_product|zoom_product|detail_product|multiple_product_images|brand_logo, text}`.
  2. **TTS voiceover** — script → audio. Offer language (English, Tamil, Hindi, Malayalam, Telugu, Kannada) + voice (female/male/young/professional/energetic/traditional). India-first.
  3. **Product motion from photos** — Ken Burns / zoom / pan / transitions over uploaded product photos (deterministic). *(V1)*
  4. **AI image→video** — turn static product photos into moving clips via an external video-gen API. *(V2)*
  5. **AI-generated scenes** — text/image→scene ("young South Indian woman wearing this saree at a wedding reception") preserving the product. *(V3)*
  6. **Animated captions** — from known facts (price, "Pure Silk", "Traditional Zari", "Free Shipping", "Shop Now").
  7. **Music** — licensed library chosen by vertical (festival/fashion/luxury/kids/electronics/food/fitness). **Must be properly licensed — never scrape/generate copyrighted songs.**
  8. **Renderer** — deterministic assembly (scenes + voiceover + music + captions + logo + CTA) → `final_video.mp4` via FFmpeg or a render service.

### Target architecture (user's sketch)
Marketing Engine split into agents/services: **Campaign AI** (strategy), **Content AI** (images/text), **Customer AI** (segmentation) → feed **Video AI** (script / voice / visuals) → **Video Renderer** → MP4/Reel.

### Phasing (user's explicit V1/V2/V3 — be practical, don't build all at once)
- **V1:** product photos + AI script + AI voice + Ken Burns animation + animated captions + licensed music + logo + CTA → 30s reel. Cheapest, most reliable.
- **V2:** add image→AI-video (static photos become moving scenes).
- **V3:** add fully AI-generated advertising scenes (text/image→scene, product-preserving).

---

## 2. Decisions (locked with the user, 2026-08-29)

- **D1. Render infra = HYBRID.** Self-host FFmpeg on a render-worker container for V1 deterministic assembly (posters + photo→reel: no per-render fee, full control). Call external AI-video APIs only for V2 (image→video) and V3 (generated scenes).
- **D2. AI providers = I recommend best-in-class, user opens the accounts + supplies keys.** (Recommendations in §3.6.)
- **D3. Metering = AI credits, per generation.** Reuse `AiCreditPricing`; every image/video/voice generation deducts credits; show a cost estimate before generating.
- **D4. Account readiness = ALL platforms available** (Meta FB+IG, Google Ads+YouTube, LinkedIn, Pinterest, WhatsApp). So sequence by **approval difficulty**, not availability.
- **D5. Publish model = approval gate with per-channel auto opt-in.** Generated → draft → merchant approves → scheduled → published. Merchant can flip individual channels to auto-publish once trusted.
- **D6. Weekly plan = AI proposes a full 7-day mix (tied to the festival calendar + catalog), merchant edits/approves.**
- **Build order:** Foundation → text + posters + scheduler (ship first) → Video V1 → V2 → V3.

---

## 3. Plan / design

### 3.1 Principle
**AI provides the intelligence (strategy, copy, script, scene plan, voice); deterministic software does the assembly (FFmpeg render, template compositing, scheduled publishing).** Never ask an LLM to edit video. Reuse what's already built: Brand Kit, Growth text/image generation, festival calendar (M2), coupons, `CustomerEvent`, `AiCreditPricing`, Hangfire scheduling, `IMediaStorage` (disk+Nginx), multi-tenancy (`ITenantScoped`), and the OAuth-token pattern from the App Store.

### 3.2 Service map (the user's agent sketch, made concrete)
```
Marketing Studio
├── Brand AI          → BrandProfile (logo, name, colors, fonts, handles, include/exclude defaults)
├── Campaign AI       → WeeklyPlanService (7-day mix from festivals + catalog + goals)
├── Content AI        → text (GrowthGenerationService, exists) · PosterService (org/product)
├── Video AI          → VideoPlanService (LLM scene plan) · VoiceService (TTS) ·
│                        CaptionService (facts→timed cues) · MusicService (licensed pick)
├── Video Renderer    → VideoRenderService (FFmpeg job in a render-worker container)
├── Customer AI       → segments (exists; feeds targeting later)
└── Publisher         → ISocialPublisher per platform + ScheduledPost + Hangfire dispatch
```

### 3.3 Data model (new entities → new migrations, all `ITenantScoped` unless noted)
- **BrandProfile** — extend the existing Brand Kit: `LogoMediaId`, `CompanyName`, `Tagline`, `PrimaryColor/Secondary/Accent` (default-pulled from the active Theme), `Font`, per-channel handles, `DefaultIncludeLogo`, `DefaultIncludeName`.
- **SocialConnection** — `Platform` (Facebook|Instagram|LinkedIn|Pinterest|YouTube|GoogleAds|WhatsApp), `Status` (Connected|NotConnected|Expired), `AccessToken`/`RefreshToken` (encrypted), `ExternalAccountId`/`PageId`, `Scopes`, `ExpiresAt`, `ConnectedAt`. Powers the connections page's connected/not-connected state.
- **MarketingCreative** — unified asset: `Type` (Text|Poster|Video), `Subtype` (Org|Product), `Status` (Draft|Approved|Scheduled|Published|Failed), `InputSpec` JSON (the wizard inputs: purpose, includeLogo, includeName, goal, product, platform…), `BrandSnapshot` JSON, `OutputMediaId`, `ProductId?`, credit cost.
- **MusicTrack** — GLOBAL (not tenant-scoped): licensed library, `Vertical`, `Mood`, `Duration`, `MediaId`, `LicenseRef`.
- **ScheduledPost** — `CreativeId`, `Platform`, `ScheduledAt`, `Status`, approval fields, `AutoPublish` (per-channel), `ExternalPostId`, `Error`.
- **WeeklyPlan** — `WeekStart`, generated mix (counts per type/channel), status; expands into draft `MarketingCreative` + `ScheduledPost` rows.

### 3.4 Merchant surfaces (Angular admin, new "Marketing Studio" nav group)
1. **/admin/marketing/brand** — Brand Kit (logo upload, name, colors auto-from-theme + override, include/exclude defaults).
2. **/admin/marketing/connections** — connect/disconnect each platform; live connected/not-connected + token-expiry status.
3. **/admin/marketing/plan** — AI-proposed weekly mix; merchant edits counts/topics, regenerate, approve → fills the calendar.
4. **/admin/marketing/calendar** — scheduler: month/week view, drag to reschedule, approve, per-channel auto-publish toggle, retry failed.
5. **/admin/marketing/poster** — **stepped poster editor**: (1) Type org/product → (2) pick product/topic + goal + platform → (3) toggles (include logo, include name, palette) + prompt → (4) AI drafts copy + layout, live preview → (5) tweak text/template/colors → save/approve/schedule.
6. **/admin/marketing/video** — **stepped video editor** (V1): (1) product + goal + platform → (2) AI scene plan (editable list of scenes: order, duration, on-screen text) → (3) voice (language + gender/style) + music vertical → (4) toggles (logo, name, CTA, price) → (5) render preview → approve/schedule. *(Merchant never "edits frames" — they edit the plan; the renderer assembles.)*
7. **/admin/marketing/library** — all creatives (reuse existing Content Library), filter by type/status/channel.

### 3.5 Video V1 render pipeline (self-hosted, deterministic)
Runs in a **new `render-worker` container** (FFmpeg + the API image) consuming a Hangfire queue so heavy jobs never block the web API.
```
product photos + brand kit + goal + platform
  → VideoPlanService (LLM)        → scenes[] {duration, visual, text}
  → VoiceService (TTS API)        → voiceover.mp3   [credited]
  → CaptionService (facts)        → timed caption cues (ASS/drawtext)
  → MusicService                  → licensed track (ducked under VO)
  → VideoRenderService (FFMPEG):
        zoompan (Ken Burns) per photo · xfade transitions · logo/CTA overlay ·
        burn captions · amix voiceover+music → H.264 mp4, per-platform aspect (9:16 / 1:1 / 16:9)
  → IMediaStorage → MarketingCreative.OutputMediaId
```
- **V2:** swap selected Ken Burns clips for **image→video** clips from an external API (fal.ai/Replicate).
- **V3:** add **text/image→scene** generation (product-preserving), e.g. "saree at a wedding reception."
- Posters use the same worker: an **HTML/CSS brand template rendered headless → PNG** (reuses theme fonts/colors; many templates cheap to add). Lighter fallback: SVG templates → raster.

### 3.6 Provider recommendations (D2 — open these accounts; keys → user-secrets/env)
- **Aggregator to minimise accounts:** **fal.ai** or **Replicate** — one account exposes many image + image→video + some TTS models behind a single key. Strongly recommended as the spine for image/video gen.
- **Image gen / poster backgrounds:** Flux (Black Forest Labs) via fal.ai, or OpenAI `gpt-image-1`, or Google Imagen.
- **Image→video (V2):** Runway Gen-3, Luma Dream Machine, Kling, Google Veo, Pika — all reachable via fal.ai/Replicate.
- **TTS (Indian languages, India-first):** **Sarvam AI** (excellent Hindi/Tamil/Telugu/Kannada/Malayalam) + **ElevenLabs** for premium English/multilingual.
- **Music:** license a small curated royalty-free pack per vertical to start (Epidemic Sound / Artlist / Soundstripe). **No scraping copyrighted songs.**
- **Render:** self-hosted FFmpeg (no vendor).
- **Social APIs:** Meta Graph (FB Page + IG Business — needs business verification + App Review for `*_content_publish`), LinkedIn Posts API, Pinterest API, YouTube Data API (resumable upload, Shorts ≤60s vertical), Google Ads API (separate ads/feed track), WhatsApp via existing BSP code.

### 3.7 Metering (D3)
New `AiCreditPricing` SKUs, each deducted on use with a pre-generation estimate shown: `poster.copy`, `poster.background`, `video.plan`, `tts.per_second`, `video.render.v1` (flat), `video.aivideo.per_second` (V2/V3 external passthrough + margin), `image.gen`. Text reuses existing SKUs.

### 3.8 Milestones (ship order)
- **MS0 — Foundation:** ✅ **shipped 2026-09-01** (commit 089b421, migration 293). Bounded module `Features/MarketingStudio/` with the §3.10 seam: `MarketingBrandProfile` (Marketing* table cluster, no core FKs), `MarketingBrandService` + `IBrandThemeDefaults` port + `BrandThemeDefaults` adapter (only place touching theme tables), `MarketingStudioController` at `/api/marketing/*` gated by `RequiresFeature("marketing_studio")` (granted to all plans). Brand Kit page `/admin/marketing/brand` (logo upload, theme-seeded colour pickers, include/exclude defaults, social handles, live preview) + "Studio brand kit" nav. 5 tests. **Still deferred to later MS0 polish:** `MarketingCreative` entity + metering SKUs (land with MS2/MS3 when creatives exist).
- **MS1 — Connections:** ✅ **shipped 2026-09-01** (commit cbfb096, migration 294). Config-driven OAuth2 framework — one code path for every network (`SocialPlatforms` descriptors + `SocialOptions` client id/secret + central `RedirectBaseUrl`). `SocialConnection` entity (encrypted tokens via IDataProtection). `SocialConnectionService`: status listing, OAuth start (authorize URL + signed, TTL'd `state` carrying the tenant so the anonymous central-host callback attributes via `BeginScope`), generic code exchange, disconnect. `MarketingConnectionsController` at `/api/marketing/connections` (admin+gated list/start/disconnect; anonymous callback → 302 back to page). Connections page `/admin/marketing/connections` (per-network status, Connect→OAuth, Disconnect, return-banner) + nav. 7 tests. **Left for when app keys land (user opens apps):** set `Social:Providers:*` client id/secret + `Social:RedirectBaseUrl` (start LinkedIn/Pinterest/YouTube, Meta after App Review, Google Ads last); per-provider token-exchange quirks (Pinterest Basic auth, Meta long-lived-token swap) + fetch real account name; Hangfire token refresh. WhatsApp stays a BSP embedded-signup card, not this OAuth flow.
- **MS2 — Text + Posters + Scheduler (FIRST SHIPPABLE VALUE):** poster stepped editor (org + product) with headless-template render; text gen (reuse Growth); AI weekly plan; scheduler + Hangfire publisher honoring the approval gate + per-channel auto-publish.
- **MS3 — Video V1:** render-worker container + FFmpeg pipeline; VideoPlan + TTS + captions + music + Ken Burns; video stepped editor.
- **MS4 — Video V2:** image→video via aggregator.
- **MS5 — Video V3 + Google Ads + attribution:** generated scenes; ads/product-feed; tie post→click→order attribution into [marketing-engine-v2-plan.md](marketing-engine-v2-plan.md).

### 3.11 Multi-tenant social auth — one platform app, many merchant connections
**The model (standard SaaS, like Buffer/Hootsuite/Later):** WavCommerce registers **ONE developer app per network** (a Meta app, a LinkedIn app, a Pinterest app, one Google Cloud project for YouTube). That app is the *platform's* identity — it holds the client id/secret and is what goes through **business verification + App Review once, centrally.** Each **merchant connects their own account** under that app via an OAuth "Connect" click; we store a **per-merchant access token** in the tenant-scoped `SocialConnection`. Merchants never create developer apps — they just log into their own page and grant access.

```
WavCommerce (one app per platform: Meta / LinkedIn / Pinterest / Google)
   │  ← business verification + App Review done ONCE, by us
   ├── Merchant A → OAuth connect → token for A's FB Page / IG / LinkedIn Co. Page   (SocialConnection, tenant A)
   ├── Merchant B → OAuth connect → token for B's accounts                           (SocialConnection, tenant B)
   └── …
```

**Per-platform merchant prerequisites (the studio must guide the merchant through these):**
- **Meta:** merchant needs a **Facebook Page** + an **Instagram Business/Creator account linked to that Page** (IG content-publish only works this way). Our app requests `pages_manage_posts`, `instagram_content_publish`, `business_management`. Until App Review is approved, only accounts with a role on our app can connect (fine for testing).
- **LinkedIn:** merchant must be an **admin of the Company Page**; we request `w_organization_social`. Personal-profile posting is restricted.
- **Pinterest:** merchant connects a business account; our app needs Pinterest's standard-access review for production.
- **YouTube:** merchant connects their channel (Google login); `youtube.upload` is a sensitive scope needing Google OAuth verification.

**Two real multi-tenant gotchas to design for:**
1. **Shared per-app / per-project rate limits & quotas.** Some limits are counted against *our* app across *all* merchants — most sharply **YouTube: the Data API upload quota (~10k units/day ≈ ~6 uploads/day) is per our Google project, shared platform-wide.** Mitigation: request quota increases as we scale, and **queue/throttle uploads** so one merchant can't exhaust the shared budget. Meta/LinkedIn are mostly per-user-token limited, easier.
2. **Token lifecycle.** Tokens are per-merchant, encrypted at rest, auto-refreshed via Hangfire (Meta long-lived ~60d, LinkedIn ~60d, Google refresh tokens long-lived), and revoked on disconnect. The connections page reflects `Connected / Expired / Not connected` from this.

**At scale (later):** Meta's **Tech-Provider / System-User** model lets a SaaS manage many client Pages centrally — worth adopting once merchant count is high; not needed for V1.

### 3.10 Extraction seam — "in the monolith now, a separate web app later" (user requirement)
Build the studio as a **self-contained module inside `ecomm.api` today**, but behind seams so it can be lifted into its own service/web app with minimal surgery. Same intent as ADR-001 (super-admin platform-context seam) — don't prematurely split, but don't weld it in either.

Rules that keep the cut cheap:
1. **One bounded vertical slice.** All backend code under `Features/MarketingStudio/` (controllers + services + entities + DTOs); nothing marketing-specific leaks into other slices. Angular: one **lazy-loaded** `marketing` feature area under `/admin/marketing/**`, self-contained (its own routes/state/components) so it can later become a micro-frontend or standalone SPA.
2. **Talk to commerce through interfaces, never direct EF joins into core tables.** Define thin ports the module depends on — `ICatalogReader` (product name/price/description/images by id), `IMediaStore` (already `IMediaStorage`), `IBrandProvider`, `ITenantContext`, `IAiCreditLedger`. Today these are in-process implementations; on extraction each becomes an HTTP client to the commerce API. **No cross-module `Include()`/join** — reference core rows by id only (`ProductId`, `TenantId`).
3. **Own data cluster, loose coupling.** Marketing tables live together (clear `Mkt*`/module grouping) and hold **no hard FKs into core commerce tables** — soft references by id, so the schema can move to a separate database. Keep `MusicTrack` global; everything else tenant-scoped via the shared `TenantId`.
4. **Own API prefix + queue boundary.** Route everything under `/api/marketing/*`. The render worker already runs as a **separate container** talking over a job queue (DB/Hangfire) — keep that boundary strict (no in-process render calls), because it's the natural first thing to split out.
5. **Shared, portable auth.** Reuse the existing JWT + tenant claims; when split, the marketing app validates the **same tokens** (shared signing key / introspection) so multi-tenant identity flows unchanged.
6. **Feature-flagged.** Gate the whole module behind `RequiresFeature("marketing_studio")` so it can be turned on/off and, later, pointed at a separate host without touching core.

Net: extraction later = stand up a new service hosting `Features/MarketingStudio/*`, flip the six ports from in-process to HTTP clients, point the lazy Angular area at the new API, move the `Mkt*` tables to their own DB. No rewrite of core commerce.

## 4. Feasibility & cost review (2026-08-29)

### 4.1 Feasibility, component by component
- 🟢 **Brand kit, connections UI, text gen, poster (template), weekly plan, scheduler, Video V1 (FFmpeg photo-reel)** — standard engineering, high confidence. Reuses existing Growth gen, Hangfire, IMediaStorage, AiCreditPricing.
- 🟡 **Social connectors** — technically routine, but **gated by platform approval, not code**: Meta needs business verification + App Review (weeks) for content-publish; LinkedIn/Pinterest/YouTube are lighter. Mitigation: ship the studio + scheduler with the easy channels first; Meta lands when review clears.
- 🟡 **Render worker at scale** — FFmpeg is reliable, but video render is CPU/RAM heavy; needs a bounded queue and likely a dedicated render box as volume grows.
- 🟡 **Video V2/V3 (AI image→video / generated scenes)** — the models exist, but **product-appearance consistency is genuinely hard**: AI video/scene models drift the product's exact look (pattern, logo, proportions). Honest expectation: great for *mood/lifestyle b-roll*, not for a faithful product hero shot. Keep V1 (real product photos in motion) as the dependable core; treat V2/V3 as accents.
- 🟢 **Music** — feasible with a proper multi-client commercial license (the real dependency is legal, not technical).

### 4.2 Will it actually help merchants?
**Yes — mainly by removing the effort barrier.** Most small merchants market inconsistently or not at all; a system that proposes a week of on-brand posts and publishes them is real value (consistency + speed + professional look). Honest caveats:
- **Organic social reach is limited and declining** — posting alone rarely explodes a business. The compounding wins come from pairing this with **paid ads + retention email** (the [marketing-engine-v2-plan.md](marketing-engine-v2-plan.md) deliverability/attribution work).
- **Quality depends on the merchant's own product photos** and catalog. Garbage in → mediocre out.
- **Without attribution, merchants can't see ROI** and will churn on perceived value — so campaign→click→order attribution (marketing-v2 D) is a near-term must, not optional.
- Net: a strong **differentiator and retention/upsell driver**, not a guaranteed growth machine. Frame it honestly to merchants.

### 4.3 Cost to the merchant (approximate, current market — providers change pricing; treat as ballpark)
Per-generation raw provider cost:
| Item | Raw cost |
|---|---|
| Text / caption | < $0.01 |
| Blog article | ~$0.02–0.05 |
| Poster (existing product photos) | ~$0.01 |
| Poster (AI-generated background) | ~$0.04–0.08 |
| Video V1 — 30s photo-reel (LLM plan + TTS + FFmpeg) | ~$0.10–0.25 |
| Video V2 — 30s with AI footage | ~$1.50–5 |
| Video V3 — 30s generated scenes | ~$2–8 |
| Organic social posting (FB/IG/LinkedIn/Pinterest/YouTube) | $0 (APIs are free; Google Ads = merchant's own ad budget) |

**Typical active merchant, one week** (AI-proposed mix: ~10 text posts, ~5 posters incl. 2 AI-bg, ~3 V1 videos, 1 blog):
- **Raw provider cost ≈ $1/week (~$4/month).**
- **Merchant-facing (credits, ~2.5–3× margin) ≈ $3–6/week (~$12–25/month).**

**Same merchant leaning on AI video (2 V2 reels/week):**
- Raw ≈ $5–9/week (~$20–36/month).
- **Merchant-facing ≈ $15–30/week (~$60–120/month).**

So: **V1-era is a coffee or two a month; AI-video-heavy is a modest subscription-sized add-on.** The credit gate + pre-generation estimate keeps merchants in control.

### 4.4 Platform fixed costs (not per-merchant)
- Aggregator (fal.ai/Replicate), image/video, TTS = **pay-as-you-go, no floor** (passed through as credits).
- **Music license** — the one real fixed cost: a commercial multi-client catalog (~few hundred to low-thousands $/yr) — must be legit.
- **Render infra** — a dedicated render box (~$20–80/mo) once video volume is real.
- Social APIs = free.

### 4.5 Bottom line
Feasible and worth building. **V1 is cheap, reliable, and high-value; V2/V3 are where cost and quality risk concentrate — gate them behind credits and set honest expectations.** The plan's real success condition isn't the creative gen (that's the easy, cheap part) — it's **deliverability + attribution + consistent publishing**, so pair this with the marketing-v2 foundation.

### 3.9 Key risks / dependencies
- **Meta App Review + business verification takes weeks** — build the connector, gate publishing behind approval; LinkedIn/Pinterest/YouTube give earlier wins.
- **Render worker is CPU/RAM heavy** — dedicated container, bounded concurrency, a queue; may need a larger VPS or a separate render box as volume grows.
- **Music licensing must be legitimate** — curated licensed pack, tracked via `MusicTrack.LicenseRef`.
- **Cost control** — hard credit gate + estimate before every paid generation; cache/reuse assets.
- **Token security** — social tokens encrypted at rest; refresh jobs; revoke on disconnect.

---

## 5. MS2 design — weekly plan & scheduler (customization + review + history)

Refined with the user 2026-09-01. Today's calendar is read-only (festivals + scheduled campaigns) — none of the below exists yet; MS2 adds it. Core principle the user set: **nothing is generated one-shot; the merchant customizes, then confirms, then we generate — and they can always see and change the schedule.**

### 5.1 Two gates, never a black box
```
① PLAN PREFERENCES        ② AI PROPOSES (cheap outline, no creatives yet)      ③ CONFIRM → GENERATE
  posts/week per type   →   a draft week: day · type · channel(s) · topic   →   user edits/removes/adds,
  per-channel × type        (just the plan, ~no credits)                         toggles channels, then
  matrix, week start                                                             CONFIRMS → creatives are
                                                                                 generated (credits spent here)
                                                                                        │
                                                                                        ▼
                          ④ SCHEDULER (per-channel posts)     ⑤ PUBLISH per schedule
                            reschedule · approve · skip   →     auto-publish channels go live at time;
                            regenerate · change cadence         others wait for approval (D5 gate)
                                                                        │
                                                                        ▼
                                                              ⑥ JOB HISTORY (runs log)
                                                                published / failed + when + link/error
```
- **Gate 1 (confirm before generate):** the AI first proposes only an *outline* (day/type/channel/topic) — near-zero cost. The merchant reviews, edits, removes, adds, and flips channels, then **confirms**. Only then do we spend credits generating the actual copy/posters. This directly honours "ask before generation."
- **Gate 2 (approval before publish):** per the locked D5 decision — each channel is either auto-publish (goes live at its slot) or waits as "needs approval" in the scheduler.

### 5.2 Customization surfaces (the merchant's controls)
- **Plan Preferences** (`MarketingPlanSettings`, per tenant): how many of each per week — `TextPerWeek`, `PostersPerWeek`, `VideosPerWeek`; `WeekStartDay`; default posting times; `AutoRecur` (auto-draft next week) on/off.
- **Per-channel × per-type matrix** (`MarketingChannelPref`, one row per connected platform): `Enabled` (include this channel at all) + `AllowText` / `AllowPoster` / `AllowVideo` checkboxes. This is the user's "after connecting, uncheck if we don't want a poster for a particular social media." Only *connected* channels appear.
- **Per-item toggles** at review time: include logo / include name (defaults from the Brand Kit), pick/replace the product or topic, change the day/time, choose which of the allowed channels this specific item goes to, remove it, or add an extra item.

### 5.3 Data model (new, all Marketing* cluster, no core FKs)
- `MarketingPlanSettings` (per tenant) — the cadence prefs above.
- `MarketingChannelPref` (per tenant × platform) — the enable + type checkboxes.
- `MarketingPlan` (per week) — `WeekStart`, `Status` (Draft→Confirmed→Active→Done).
- `MarketingPlanItem` — one intended creative: `Day/ScheduledAt`, `Type`, `Topic/ProductId`, `Angle`, include-toggles, `Status` (Proposed→Approved→Generated→Scheduled→Done/Skipped), `CreativeId` once generated.
- `MarketingCreative` — the generated asset (text/poster; video later), reusable across channels.
- `ScheduledPost` — **fan-out: one row per (item × channel)** with `ScheduledAt`, `Status` (PendingApproval | Scheduled | Published | Failed | Skipped), `ExternalPostId`, `Error`. This is both the schedule **and** the job history (query by time). Per-channel rows are what make per-channel toggles, approval, and history clean.
- Publishing is driven by a **Hangfire sweep** that picks up due `Scheduled` posts and calls the channel's publisher (reuses the MS1 `SocialConnection` tokens).

### 5.4 Scheduler screen (`/admin/marketing/calendar`)
- Week/month/list view of `ScheduledPost` grouped by day: each shows type + channel chip + status + time.
- Actions: **reschedule** (change date/time), **approve** (for PendingApproval), **skip**, **regenerate** creative, **edit caption**, open the published post.
- A **cadence panel**: change per-week counts, flip `AutoRecur` (weekly), "generate next week now."
- A **History tab**: chronological runs — published/failed, timestamp, channel, external link or error. (Same `ScheduledPost` data, past + terminal states.)

### 5.5 Recurring / "change to weekly, etc."
`AutoRecur` on → a weekly Hangfire job drafts next week's plan from the saved preferences and leaves it at **Draft** for the merchant to confirm (never auto-generates+publishes unattended — respects Gate 1). Off → the merchant starts each week manually. Cadence/counts are editable any time and apply to the next draft.

### 5.6 Build sub-steps (ship incrementally, test each)
1. `MarketingPlanSettings` + `MarketingChannelPref` + Preferences UI (counts + per-channel×type matrix from connected channels).
2. `MarketingPlan`/`MarketingPlanItem` + AI **outline** proposer + review UI (edit/remove/add/toggle) + **confirm**.
3. Generation on confirm (text + posters reuse MS0 brand kit; credits) → `MarketingCreative` + fan-out `ScheduledPost`.
4. Scheduler screen (reschedule/approve/skip/regenerate) + Hangfire publish sweep (auto-publish vs approval).
5. History tab + `AutoRecur` weekly drafting.
6. (MS3 later) videos become a plan type once the video pipeline exists.
