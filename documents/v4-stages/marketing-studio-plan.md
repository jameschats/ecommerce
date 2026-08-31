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
- **MS0 — Foundation:** BrandProfile + Brand Kit page; `MarketingCreative`; metering SKUs; Marketing Studio nav.
- **MS1 — Connections:** `SocialConnection` + OAuth. Sequence by approval ease: **LinkedIn + Pinterest + YouTube first**, then **Meta (IG+FB)** once App Review clears, **Google Ads** last (own track). Connections page with live status + token refresh (Hangfire).
- **MS2 — Text + Posters + Scheduler (FIRST SHIPPABLE VALUE):** poster stepped editor (org + product) with headless-template render; text gen (reuse Growth); AI weekly plan; scheduler + Hangfire publisher honoring the approval gate + per-channel auto-publish.
- **MS3 — Video V1:** render-worker container + FFmpeg pipeline; VideoPlan + TTS + captions + music + Ken Burns; video stepped editor.
- **MS4 — Video V2:** image→video via aggregator.
- **MS5 — Video V3 + Google Ads + attribution:** generated scenes; ads/product-feed; tie post→click→order attribution into [marketing-engine-v2-plan.md](marketing-engine-v2-plan.md).

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

### 3.9 Key risks / dependencies
- **Meta App Review + business verification takes weeks** — build the connector, gate publishing behind approval; LinkedIn/Pinterest/YouTube give earlier wins.
- **Render worker is CPU/RAM heavy** — dedicated container, bounded concurrency, a queue; may need a larger VPS or a separate render box as volume grows.
- **Music licensing must be legitimate** — curated licensed pack, tracked via `MusicTrack.LicenseRef`.
- **Cost control** — hard credit gate + estimate before every paid generation; cache/reuse assets.
- **Token security** — social tokens encrypted at rest; refresh jobs; revoke on disconnect.
