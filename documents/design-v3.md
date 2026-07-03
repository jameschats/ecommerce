# AI Growth Engine — V3 Design (Standalone AI-Marketing SaaS)

> **Project codename:** "Mini Flipkart" → **AI Growth Engine**
> **Author/Architect:** James (.NET full-stack, Technical Architect)
> **Status:** V3 design — approved. Build **after V2 is stable** (the own-platform connector depends on V2 tenant infrastructure).
> **Source:** Distilled from `design-chat-v3.txt`, reconciled against the V1/V2 architecture. Supersedes the AI-Marketing sketch in [design-v2.md](design-v2.md) §8.

---

## 1. What It Is

A **standalone SaaS add-on** that gives any e-commerce business AI-powered marketing — product descriptions, social content, email campaigns, promotional images, and video reels — generated in seconds from their product data.

**The key strategic point:** it is **not tied to your own commerce platform.** It connects to Shopify, WooCommerce, Magento, or your own platform through a **connector**. One product, many markets — you don't build a separate tool per platform.

| Product | Includes | Price/mo |
|---|---|---|
| **Commerce Only** | Your e-commerce platform (V1/V2) | ₹499–₹1,999 |
| **AI Growth Engine Only** | AI marketing — connects to *any* platform | ₹799–₹1,999 |
| **Commerce + AI Bundle** | Full stack | ₹1,199–₹2,999 |

### Why this wins in India specifically
Global AI-marketing tools (Jasper, Copy.ai, AdCreative.ai) don't understand **Hinglish**, **Indian festivals** (Diwali, Navratri, Eid, Pongal, Onam), **WhatsApp-first** marketing, or **GST-aware** copy. You do — and that's the moat (see §9 AI-6).

---

## 2. How V3 Relates to V1/V2

| | V1 (Commerce) | V2 (Platform) | **V3 (AI Growth Engine)** |
|---|---|---|---|
| Product | Single store | Multi-tenant SaaS | **Standalone AI marketing** |
| Runs as | `ecomm.api` | same + tenancy | **new `ecomm.ai` service** |
| Customers | Your shoppers | Merchants on your platform | **Any merchant, on any platform** |
| Depends on | — | V1 schema | **V2 tenant infra** (for own-platform connector only) |

The AI engine is a **separate service with its own database** (`ai_engine`). It reuses the V1 architectural patterns (provider abstraction, vertical slices, `ApiResponse<T>` envelope, config-selected providers) but does **not** share the commerce transactional schema.

> **Identity reconciliation:** the chat's AI schema uses `TenantId CHAR(36)`. That is **correct here and does not conflict with V2's `bigint TenantId`** — because the AI engine serves external (Shopify/Woo) merchants too, it maintains its **own account identity space** (a GUID). A small mapping table links a commerce `bigint TenantId` → an AI-engine account GUID when a customer uses both products. Keep the two identity spaces separate on purpose.

---

## 3. Architecture — Provider Abstraction

You **never** call OpenAI, Gemini, or any provider directly from business logic. Every capability goes through an interface; the implementation behind it swaps without touching feature code — exactly the pattern V1 already uses for `IPaymentProvider` / `IEmailSender` / `ISmsSender` / `IMediaStorage`.

```csharp
public interface ITextAIProvider  { Task<string>               GenerateAsync(TextGenerationRequest  r); }
public interface IImageAIProvider { Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest r); }
public interface IVideoAIProvider { Task<VideoGenerationResult> GenerateAsync(VideoGenerationRequest r); }
```

**Registered implementations (config-selected, like `Payments:Provider`):**

| Capability | Providers |
|---|---|
| **Text** | `gemini` (Flash — cheapest, high volume) · `openai` (GPT-4o-mini bulk / GPT-4o quality) · `claude` (Haiku bulk / Sonnet quality) · `deepseek` (ultra-cheap) · `local` (self-hosted Llama, zero API cost) |
| **Image** | `flux` (best quality-to-cost) · `stability` (SDXL) · `openai` (DALL-E 3 — premium) |
| **Video** | `invideo` (slideshow→video, India-based, affordable) · `kling` (good quality, lower cost) · `runway` (Gen-3 — premium) |

**Provider selection by use case (the default routing table):**

| Use case | Default provider | Reason |
|---|---|---|
| Product descriptions, captions | Gemini 1.5 Flash | Cheapest per token; fine for marketing copy |
| Blog articles, long-form | GPT-4o-mini or Claude Haiku | Better coherence over length |
| High-quality copy (Pro tier) | GPT-4o or Claude Sonnet | Best quality; metered |
| Image posters, banners | Flux.1 or Stable Diffusion 3 | Good quality-to-cost |
| Video reels | InVideo AI or Kling | Affordable slideshow pipeline |
| Premium video | Runway Gen-3 | Highest quality; heavily metered |

**How to build it:** a `IAiProviderFactory` resolves the right provider per `(capability, useCase, planTier)`, reading a routing config. Every call records the actual provider, model, token counts, and **provider cost in paise** to `AiUsageLogs` (§6) so margins are observable per generation. Add automatic **failover** (if Gemini errors, fall back to the next cheapest text provider) since availability varies.

---

## 4. Feature Modules

### Module 1 — AI Text Marketing
| Content type | Generates | Credits |
|---|---|---:|
| Product Description | SEO description w/ features, benefits, keywords | 5 |
| Instagram Caption | Caption + CTA + 30 hashtag options | 3 |
| Facebook Post | Story-driven copy; organic or ad variant | 3 |
| WhatsApp Campaign | Short punchy message w/ offer; bulk-send ready | 2 |
| Email Campaign | Subject + preview + body; personalisation tokens | 8 |
| Blog Article | 800–1,500 word SEO article w/ headings | 20 |
| Google Ads Copy | Headlines (30 char) + descriptions (90 char) | 5 |
| Festival Offer Post | Diwali/Navratri/Eid/Pongal/Onam themed | 3 |
| SEO Meta Tags | Title tag + meta description | 4 |

*Cost ₹30–₹100/customer/mo · price ₹299–₹499 — healthy margin even at Starter.*

### Module 2 — AI Image Marketing
| Content type | Generates | Credits |
|---|---|---:|
| Product Poster | Branded poster: product + price + offer + logo | 20 |
| Social Story | Vertical 9:16 for IG/WhatsApp Stories | 20 |
| Square Post | 1:1 for IG/FB feed | 20 |
| Banner Ad | 16:9 for web/email header | 20 |
| Festival Creative | Festival background + product overlay | 25 |
| Background Removal | Remove bg; replace white/branded | 10 |
| Product Mockup | Product on a lifestyle scene | 30 |

*Cost ₹150–₹300/customer/mo (~20–30 images) · price ₹599–₹999.*

### Module 3 — AI Video Marketing
| Content type | Credits |
|---|---:|
| Product Reel (15s) — images + AI voiceover + music + captions | 100 |
| Product Reel (30s) | 180 |
| Slideshow Video — images + text overlays + transitions + music | 60 |
| Festival Video — animated greeting w/ brand + offer | 80 |

> ⚠️ **Video is the margin killer.** 20 reels/mo burns ~2,000 credits costing ₹1,000–₹2,500 against a ₹1,999 plan. **Always enforce credit caps on video. No plan ever includes unlimited video.**

### Module 4 — Campaign Builder
Pick a goal (product launch, flash sale, festival offer) → AI generates **all channels at once**: Instagram caption + Facebook post + WhatsApp message + email subject + product description update. Review each → schedule or publish immediately.

---

## 5. Credit System

No plan is unlimited. **Credits** are the unit of consumption; when they run out, merchants buy top-ups or upgrade.

| AI Plan | Monthly Credits | ~Text | ~Images | ~Videos | Price |
|---|---:|---:|---:|---:|---:|
| Text Only | 300 | ~60 | 0 | 0 | ₹299 |
| Text + Image Starter | 800 | ~80 | ~200 | 0 | ₹599 |
| Text + Image Growth | 2,000 | ~200 | ~500 | 0 | ₹999 |
| Pro (Text + Image + Video) | 5,000 | ~300 | ~80 | ~20 | ₹1,999 |

**Top-up packs:** Small 500 → ₹199 · Medium 1,500 → ₹499 · Large 5,000 → ₹1,299.

### Credit ledger — append-only, never update a row
```sql
AiCreditLedger
  LedgerId        BIGINT PK AUTO_INCREMENT
  TenantId        CHAR(36) FK              -- AI-engine account id (see §2)
  TransactionType ENUM('Allocation','Debit','TopUp','Refund','Expiry')
  Credits         INT           -- positive = add, negative = deduct
  BalanceAfter    INT           -- running balance, denormalised for speed
  ReferenceId     VARCHAR(100)  -- GeneratedContentId, TopUpOrderId, …
  CreatedAt       DATETIME
-- Balance = latest BalanceAfter for the TenantId. Credits expire at period end; no rollover on base plan.
```
**How to build it safely:**
- **Debit *before* generation, refund on failure.** Reserve credits, call the provider, and write a `Refund` row if the provider errors — so a failed generation never charges the merchant.
- **Idempotent debits:** key each debit to a `ReferenceId` (the `GeneratedContentId`) so retries don't double-charge.
- The ledger is the **source of truth**; `AiSubscriptions.CreditsUsedThisPeriod` is a fast cache reconciled from it.

---

## 6. Data Model — AI Schema (`ai_engine`)

Separate database/schema — keeps AI concerns cleanly isolated from commerce data.

```sql
AiSubscriptions
  TenantId, PlanId, Status, CurrentPeriodStart, CurrentPeriodEnd,
  MonthlyCredits, TopUpCredits, CreditsUsedThisPeriod

AiCreditLedger        -- see §5

AiUsageLogs           -- per-generation cost/observability
  TenantId, Provider, ContentType, CreditsUsed, ProviderCostInPaise,
  DurationMs, PromptTokens, CompletionTokens, GeneratedContentId, CreatedAt

AiGeneratedContent
  ContentId (GUID), TenantId, ContentType, SourceProductId, SourcePlatform,
  PromptUsed, GeneratedText, MediaUrl, Provider, ModelUsed, CreditsUsed,
  Status (Draft|Published|Archived), PublishedAt, PublishedChannel, CreatedAt

AiPromptTemplates
  TemplateId, TenantId (NULL = global default), ContentType, Name,
  PromptTemplate, Variables (JSON), Language, IsActive

AiCampaigns
  CampaignId, TenantId, Name, Goal, ScheduledAt, Status,
  Channels (JSON), ContentIds (JSON array)

SyncedProducts
  TenantId, SourcePlatform, ExternalProductId, Name, Description,
  ImageUrls (JSON), Price, Category, Tags, LastSyncedAt

ConnectedPlatforms
  TenantId, Platform, CredentialsJson (ENCRYPTED), IsActive,
  LastSyncAt, ProductsSynced, ConnectedAt
```
> `ConnectedPlatforms.CredentialsJson` holds merchant OAuth tokens / API keys — **encrypt at rest** (envelope encryption; never log). This is the highest-value secret in the whole product.

---

## 7. Platform Connectors — The Market Multiplier

This is what makes the engine **bigger than an add-on to your own platform**.

```csharp
public interface IPlatformConnector {
    Task<IEnumerable<SyncedProduct>> FetchProductsAsync(ConnectorConfig config);
    Task PublishContentAsync(ContentPublishRequest request);   // future
}
// OwnPlatformConnector → direct internal call to ecomm.api (needs V2 tenancy)
// ShopifyConnector     → Shopify Admin REST API via OAuth
// WooCommerceConnector → WooCommerce REST API via API key
// CSVConnector         → manual CSV upload fallback
```

| Feature | Own Platform | Shopify | WooCommerce | CSV |
|---|---|---|---|---|
| Pull products | ✔ Automatic | ✔ OAuth | ✔ API Key | ✔ Manual |
| Sync inventory | ✔ Real-time | ✔ Webhook | ✔ Webhook | ✗ |
| Push descriptions back | ✔ Auto | V2.1 | V2.1 | ✗ |
| Auto-publish social posts | V2 | V2.1 | V2.1 | ✗ |

> **Strategic note:** the moment Shopify merchants can use your engine (AI-2), your addressable market multiplies far beyond your own tenants — this is the single highest-leverage stage.

---

## 8. Build Stages — AI Engine

> Build **after Commerce Platform V2 is stable.** The own-platform connector depends on V2 tenant infrastructure.

| Stage | Scope |
|---|---|
| **AI-0 — Foundation** | New `ecomm.ai` project; provider-abstraction interfaces; credit-system tables; first text provider (Gemini Flash); cost tracking; AI-plan management in Super Admin |
| **AI-1 — Text Generation** *(ship first, prove the model)* | Product descriptions, social captions, WhatsApp/email copy, hashtags; language selector (EN/Hindi/Tamil/Telugu). **This is the MVP — ship it, charge for it, validate demand before images/video.** |
| **AI-2 — Platform Connectors** *(the multiplier)* | OwnPlatform (internal), Shopify (OAuth), WooCommerce (API key), CSV fallback |
| **AI-3 — Image Generation** | Poster/banner generator, 20+ templates, background removal, social presets (1:1, 9:16, 16:9) |
| **AI-4 — Campaign Builder** | Multi-channel wizard, schedule per channel, basic analytics |
| **AI-5 — Video Generation** | Slideshow→video, short Reels, festival videos. **Hard credit gates — max 50 videos/mo on the highest plan, no exceptions.** |
| **AI-6 — Indian Market Differentiators** | Festival calendar (30+ festivals, auto-suggest campaigns), Hinglish mode, regional languages (Tamil/Telugu/Kannada/Marathi), WhatsApp broadcast format, GST-aware pricing copy |

Use **Hangfire** (introduced in V2) for async generation jobs, especially image/video which can take seconds-to-minutes — generate off the request thread, notify via the existing SignalR bell when ready.

---

## 9. Cost vs Pricing

| Plan | Your cost | Price | Gross margin |
|---|---|---:|---:|
| Text Only (₹299) | ₹30–₹80 | ₹299 | 73–90% |
| Text + Image Starter (₹599) | ₹190–₹300 | ₹599 | 50–68% |
| Text + Image Growth (₹999) | ₹250–₹400 | ₹999 | 60–75% |
| Pro w/ Video (₹1,999) | ₹500–₹1,800 | ₹1,999 | 10–75% |

Text and image margins are **solid and predictable**. The Pro plan with video has **high variance** — 50 videos can cost ₹1,500–₹2,500 against ₹1,999. **The credit system is what keeps this profitable.** Sell extra video credit packs at ₹100–₹150 per 10 videos.

---

## 10. Key Risks & Gotchas

- **Video margin is existential** — enforce hard credit caps; never bundle unlimited video (§4, §8 AI-5).
- **Debit before generate, refund on failure; idempotent on `GeneratedContentId`** — never charge for a failed or double-submitted job (§5).
- **Provider failover** — text/image/video providers all have outages; the factory must fall back to the next-cheapest working provider and log the substitution.
- **Encrypt connector credentials at rest** — `ConnectedPlatforms.CredentialsJson` is OAuth tokens/API keys; envelope-encrypt, never log (§6).
- **Cost observability per generation** — write `ProviderCostInPaise` to `AiUsageLogs` on every call so margin erosion is visible before it hurts.
- **Separate identity space** — AI-engine account GUID ≠ commerce `bigint TenantId`; bridge with a mapping table (§2).
- **Ship text first (AI-1) and charge** before building the expensive image/video tiers — validate willingness to pay.

---

## 11. How to Achieve This on This Codebase

1. **New project `ecomm.ai`** (sibling of `ecomm.api`), same conventions: vertical slices under `Features/` (`TextGen`, `ImageGen`, `VideoGen`, `Campaigns`, `Connectors`, `Credits`, `AiPlans`), `ApiResponse<T>` envelope, Serilog, `AppException`.
2. **Provider abstraction** mirrors V1's `IPaymentProvider` pattern — interfaces + config-selected implementations (`Ai:Text:Provider`, `Ai:Image:Provider`, `Ai:Video:Provider`) + an `IAiProviderFactory` for use-case routing.
3. **Own database `ai_engine`** — new numbered migrations in a dedicated folder (e.g. `database/ai-migrations/`), same forward-only + `__schema_migrations` recording pattern.
4. **Credit ledger** as the transactional core (append-only) — the one piece that must be bullet-proof; wrap debit+generate+refund in a unit of work.
5. **Async jobs via Hangfire** (from V2) for image/video; surface completion through the existing **SignalR** notification bell.
6. **Frontend**: a new `ecomm.ai-web` Angular app (or a module inside merchant-admin), reusing the Tailwind + theme conventions.
7. **Connectors** as `IPlatformConnector` implementations; start with `OwnPlatformConnector` (needs V2) + `CSVConnector` (no dependency — good for the earliest demo), then Shopify/Woo.

---

*Companion docs: [design.md](design.md) (V1), [design-v2.md](design-v2.md) (multi-tenant platform). Stage plans: [v2-stages/](v2-stages/), [v3-stages/](v3-stages/).*
