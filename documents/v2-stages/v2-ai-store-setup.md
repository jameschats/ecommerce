# Plan — Frictionless, AI-assisted store setup

## Goal (the "why")
Make onboarding + catalog + content setup so fast and painless that merchants *love* the platform and
recommend it. Every painful step gets a fast path — template, import, migrate, or AI. AI is a **first-class,
metered utility available consistently across the whole platform**, not a siloed feature.

## What already exists (reuse, don't rebuild)
- **Excel import/export** — `ProductImportService` (`Template()`, `ImportAsync(xlsx)`, `ExportAsync()`, header→field
  mapping, attribute columns, slug/brand upsert). The on-ramps build on this.
- **`Plan.AiCredits`** column already on the Plan entity.
- **Onboarding provisioning** — signup already creates tenant+admin+trial + default shipping/COD/published theme.
- **Admin-home setup checklist** (`admin-home.component.ts`, "Set up your store 0/5").
- **CMS Pages + page builder**, **Theme engine**, **Razorpay** (per-tenant) for credit top-ups.

## Review of the ideas (validated + refined)
| Idea | Verdict | Notes / refinement |
|---|---|---|
| Minimal onboarding | ✅ core | Fewest steps to "live". Lead with the **instant sample store** so a merchant sees a real store in one click. |
| 1. Template download → fill → upload | ✅ mostly built | Add an **"Improve descriptions with AI"** pass. |
| 2. Upload any file → AI converts to our format | ✅ high value | LLM maps their columns → our schema; **preview + confirm** before import (never blind-import). |
| 3. Import Shopify/Woo/Wix exports | ✅ | Known-ish schemas → **format presets**, AI fallback for odd columns. |
| 4. AI sample catalog by prompt/template | ✅ the "wow" | Store-type presets (bazaar/electronics/shoes/apparel/fashion/food/burgers…) + free prompt → generate categories+products (desc/price/curated images) → seed store-wide → **editable + downloadable**. |
| AI page creation | ✅ | Prompt → page-builder sections (About/Contact/FAQ/policies/landing). |
| AI credits metering + buy screen | ✅ required | Meter every AI call, deduct from plan allowance, top-up via Razorpay. |
| AI marketing / product images | ⏸ parked | Separate, larger plan (image generation). |
| P3/P4 super-admin | ⏸ parked | Resume later. |

## Architecture

### AI provider layer (`Features/Ai/`)
- **`IAiService`** with typed operations, provider-agnostic: `ImproveTextAsync`, `GenerateCatalogAsync`,
  `MapColumnsAsync`, `GeneratePageAsync`. Config-gated provider (`Ai:Provider` = None|OpenAI), mirrors the
  Payments/Shiprocket pattern. JSON-mode / structured output for catalog + mapping.
- **Billing model (DECISION):** *Platform-pays* — one app-level OpenAI key; merchants spend **credits**. Simplest
  for merchants (no keys, no OpenAI account). Alt: per-tenant BYO key. → Recommend platform-pays.

### Credits & metering
- New tables (migration ≥172): **`TenantAiCredit`** (balance, cycle reset), **`AiUsageLog`** (feature, tokens,
  credits, cost, userId, createdAt). Cycle grant = `Plan.AiCredits` on renewal (hook into the subscription
  lifecycle sweep). **`AiCreditService`**: `Balance()`, `TryDebit(feature, credits)`, `Grant()`, `TopUp()`.
- **Credits are abstract + predictable** (not raw tokens): e.g. improve-text = 1, page = 5, sample catalog = 25.
  A wrapper checks balance → runs the AI op → logs usage → debits. Out-of-credits → typed error + buy CTA.
- **Merchant screen:** `/admin/ai` — balance, usage history, **Buy credits** (Razorpay top-up packs).

### "AI everywhere" (the platform-wide ease)
One reusable Angular affordance — a small **✨ "AI" button** component (`ai-assist`) that takes a context
(text + purpose) and returns a suggestion inline. Reused on: product description + SEO, category description,
page content, theme copy, email templates. One endpoint `POST /api/admin/ai/improve` behind the credit wrapper.

## Phasing (each: migration in V2 band, build + tests green, committed)
- **AI-0 — Foundation.** `IAiService` + OpenAI provider (config-gated) + credits model (entities/migration,
  `AiCreditService`, debit wrapper) + `/admin/ai` screen (balance/history/buy via Razorpay). *Everything depends
  on this.*
- **AI-1 — AI text everywhere.** Shared `ai-assist` button + `/ai/improve` endpoint → product/category desc, SEO.
  Highest-frequency, lowest-risk. Immediate "this is delightful" moment.
- **AI-2 — Sample-catalog generator.** Store-type presets + free prompt → categories+products (+curated images)
  → seed → editable + Excel-download. The onboarding wow; also offered in the setup checklist.
- **AI-3 — Bring-your-own-file import.** AI column-mapping over the Excel importer; CSV support; preview+confirm.
- **AI-4 — Platform migration.** Shopify/Woo/Wix export presets (+ AI fallback mapping).
- **AI-5 — AI page creation.** Prompt → page-builder sections.
- **AI-6 — Onboarding polish.** Weave the sample store + on-ramps into a crisp guided setup; trim steps to the
  minimum; "you're live" moment.

## Guardrails / principles
- **Never blind-apply AI output** to a live catalog: generate → **preview** → merchant confirms.
- **Idempotent + reversible** imports (sample data tagged so it can be cleared, like the acme seed).
- **Credits checked before spend**; every spend logged; costs visible to the merchant.
- **Consistent affordance** everywhere (one component, one mental model) — the "easy to use across the whole
  platform" requirement.
- Provider swappable; if `Ai:Provider=None`, AI buttons hide gracefully (platform still fully usable).

## Decisions (LOCKED 2026-07-17)
1. **Billing model:** ✅ **Platform-pays + credits** — one app-level OpenAI key; merchants spend abstract credits,
   buy more via Razorpay. No merchant keys.
2. **Provider:** ✅ **OpenAI**, behind a swappable `IAiService` (config-gated `Ai:Provider`). Confirmed 2026-07-17
   after an OpenAI-vs-Claude cost/quality review: OpenAI's nano/mini tier is 5–20× cheaper than the cheapest Claude
   (Haiku) — decisive for platform-pays, high-volume bulk ops (catalog gen, column mapping, SEO). Design a **two-tier**
   usage from day one: cheap model (GPT-5 mini/nano) for bulk; a "polish" path can later route to Claude for hero copy
   via the swappable interface. Both providers have native structured-JSON + prompt caching, so capabilities are even.
6. **Credit top-up packs (seed):** ✅ ₹199 = 200 credits, ₹499 = 600, ₹999 = 1500 — **editable in admin**. Confirmed 2026-07-17.
3. **Credits unit:** ✅ **Abstract per-action credits** (improve=1, page=5, sample catalog=25, …) — platform absorbs
   token variance.
4. **Sample-catalog images:** ✅ **Curated stock** (live-verified, like the themes). AI image-gen = parked engine.
5. **Start scope:** ✅ **AI-0 → AI-1** first.

## Cost/margin note
Platform funds the OpenAI bill. Per-action credit costs must be set with margin over real token cost (e.g. an
improve-text call is fractions of a ₹; a sample catalog is a few ₹) so a plan's monthly credit grant + top-up
packs stay profitable. Track real `AiUsageLog.cost` to tune credit pricing.
