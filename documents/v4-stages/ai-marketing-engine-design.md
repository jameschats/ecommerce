# AI Marketing Engine — Complete Design

**Scope:** Everything that helps a merchant get *seen and found* — content, organic social, SEO, and paid ads.
**Not in scope here:** Personalization, recommendations, on-site shopping assistant, dynamic pricing — these belong to the **AI Commerce Engine** (next design doc), which is about *conversion* once a customer is already on the store.

---

## 1. Design Principles

1. **Generate, don't autopilot.** Every AI output is a draft. A human (merchant) approves before anything goes live — copy, images, posts, ads, prices.
2. **Brand Voice is the foundation, not a feature.** Every other module reads from it. Build this first, structurally, even before the modules that consume it.
3. **Don't rebuild what platforms already do better.** Meta/Google already run world-class ad-bidding AI (Advantage+, Performance Max). Our job for Performance Marketing is integration + reporting, not building a competing bidding engine.
4. **One creative pipeline, many outputs.** Product data + Brand Voice → generates descriptions, social captions, ad creative, email copy — from a shared engine, not five disconnected generators.
5. **Credits map to compute cost.** Cheap/frequent (text) ≠ expensive/occasional (image/video) — pricing tiers should reflect actual cost, not be flat.
6. **India-first channel priority.** WhatsApp > Instagram/Facebook > others, reflecting where Indian merchants' customers actually are.

---

## 2. Module Map

```
AI Marketing Engine
├── 2.1 Brand Voice           (foundation — build first)
├── 2.2 Generate               (product copy, ad copy, general content)
├── 2.3 Product Images         (AI image generation/editing)
├── 2.4 Content Library        (versioned asset repository)
├── 2.5 SEO                    (expanded — see section below)
├── 2.6 Social Media Marketing (organic scheduling & posting)
├── 2.7 Campaigns              (email/lifecycle marketing)
└── 2.8 Performance Marketing  (paid ads — integration layer)
```

---

## 2.1 Brand Voice — Foundation Layer

**What it does:** A one-time (and periodically refined) profile that every generation feature reads from, so all AI output — descriptions, captions, ads, emails — sounds consistently like the brand.

**Inputs to capture:**
- Tone descriptors (e.g., playful/premium/minimal/bold) — pick from presets + free text
- Vocabulary do's/don'ts (words to always/never use)
- Sample existing content (if merchant has past product descriptions, social captions, emails) — AI learns from these
- Target audience description (who they're talking to)
- Vertical-specific tone defaults (e.g., Health & Beauty defaults to reassuring/expert tone; Fashion defaults to expressive/trend-aware tone) — pre-seeded per vertical, editable

**Output:** A reusable "voice profile" object referenced by every other Generate call.

**Build priority:** First — everything else is weaker without it.

---

## 2.2 Generate — Content Generation Core

**Sub-features:**
- **Product descriptions** — from title + attributes + category → SEO-friendly, benefit-driven copy in Brand Voice
- **Ad copy** — headline + body variants for paid campaigns (feeds into 2.8)
- **Social captions** — feeds into 2.6
- **Email copy** — feeds into 2.7
- **Bulk generation with review queue** — NOT unattended bulk publish (Tier 3 risk). Generates a batch, merchant reviews/approves/edits before any go live.

**Guardrail (critical):** AI can invent specifications it wasn't given (certifications, technical specs, claims). Enforce:
- A structured "facts" input (only generates from what's provided — no free invention of specs)
- A visible "AI-generated — review before publish" flag on every draft
- Mandatory approval step before publish (no auto-publish toggle in v1, especially for regulated verticals: Electronics, Health & Beauty)

**Credit cost:** Low — text generation is cheap. High frequency allowance even on lower plans.

---

## 2.3 Product Images — AI Image Generation/Editing

**Sub-features:**
- Background removal/replacement (plain → lifestyle/branded background)
- Batch background styling (apply one background style across a product set)
- Image upscaling/cleanup for low-quality supplier photos
- Variant generation (same product, different angles/contexts) — stretch goal, not v1

**Guardrail:** No fabricated product features in generated imagery (e.g., don't add accessories/colors not actually included) — flag and prevent generation that could misrepresent the product.

**Credit cost:** Metered — image generation is compute-heavy. This is the module most likely to need usage-based billing rather than flat allowance.

---

## 2.4 Content Library

**What it does:** Repository of all generated + approved content (descriptions, images, captions, ad creative), versioned and reusable.

**Why it matters:** Without this, every module regenerates from scratch each time — this is what makes Generate/Images/Campaigns *compound* in value over time instead of being a one-off gimmick.

**Features:**
- Tag/search by product, campaign, channel, date
- Version history (see what was generated vs. what was edited/approved)
- Reuse across channels (same approved product description feeds storefront + social + ads, edited per channel as needed)

**Credit cost:** None (storage, not generation) — bundle with plan tier.

---

## 2.5 SEO — Expanded

*(Previously a single "SEO metadata automation" line. This is the real scope.)*

**Sub-features:**
1. **Metadata generation** — meta titles/descriptions, alt text for product images (ties into 2.3)
2. **Bulk keyword research & intent mapping** — across the full catalog, not per-SKU manual work; surfaces long-tail variants (e.g., "waterproof blue running shoes for women") merchants would never find manually at scale
3. **Automated schema markup** — Product, Offer, Review schema auto-generated and deployed per product page (currently completely missing from the draft — this is foundational for search visibility at catalog scale)
4. **Site health monitoring** — broken links, duplicate content, crawl errors, thin product pages — flagged in Merchant Admin, not silently fixed (human review before structural changes)
5. **Content briefs / blog assist** — for merchants running a blog/content section, AI-assisted briefs and drafts feeding the same Generate + Brand Voice pipeline

**Not v1 (flag for future):** AI Search / GEO (Generative Engine Optimization) — tracking brand visibility inside ChatGPT/Perplexity/AI Overviews answers. Real and growing, but early-stage enough to defer past v1.

**Credit cost:** Low-to-moderate — mostly text/data analysis, bundle with plan tier; bulk catalog-wide scans may warrant a metered ceiling on lower plans.

---

## 2.6 Social Media Marketing — Organic Scheduling

**Sub-features:**
- **Catalog-to-post automation** — new/bestselling products auto-drafted into posts (image + AI caption from Brand Voice), merchant approves, then scheduled
- **Multi-platform scheduling calendar** — single calendar view across channels
- **Auto-skip out-of-stock products** in generated post suggestions
- **Shared creative pipeline** with Performance Marketing (2.8) — same AI-generated product imagery/copy can seed both an organic post and a paid ad variant, avoiding duplicate generation work

**Phasing:**
- **V1:** Instagram + Facebook (Meta) + Pinterest scheduling
- **V2:** TikTok + X, video/reel generation
- **V3:** Deeper native integrations, richer content formats

**Credit cost:** Low (mostly reuses Generate + Product Images outputs) — bundle with plan tier.

---

## 2.7 Campaigns — Email / Lifecycle Marketing

**Sub-features:**
- Email campaign copy + subject line generation (first-draft, human sends)
- Basic lifecycle flows: welcome series, abandoned cart, win-back — AI-drafted templates, merchant customizes
- AI-suggested send timing (suggest, don't auto-execute)

**Explicitly NOT v1:**
- Predictive customer scoring / advanced segmentation (this leans toward Commerce Engine — CDP-level personalization, discuss when we design that module)
- Fully autonomous campaign execution

**Credit cost:** Low, bundle with plan tier.

---

## 2.8 Performance Marketing — Paid Ads (Integration Layer)

**Key design decision:** This is **not** a custom AI bidding engine. Meta and Google already run superior, continuously-trained optimization (Advantage+, Performance Max) — building a competing model would be slower, worse, and unnecessary R&D spend.

**What we actually build:**
1. **Ad account connectors** — OAuth link to merchant's Meta Ads and Google Ads accounts
2. **Product feed sync** — push the Products catalog into Meta Catalog Manager / Google Merchant Center automatically (keeps ad platforms in sync with inventory/pricing without manual feed management)
3. **Creative handoff** — AI-generated product images/copy (from 2.2/2.3) made available as ad creative input, feeding Meta/Google's own generative ad tools
4. **Budget & campaign setup wizard** — guided flow to launch an Advantage+ Shopping Campaign / Performance Max campaign without leaving the Merchant Admin
5. **Unified reporting dashboard** — pull ROAS, spend, CPA back from both platforms into one view inside Analytics (2.5 of the master draft), rather than merchants checking two separate ad dashboards
6. **Budget advisor (not autopilot)** — AI suggests budget allocation/reallocation across campaigns based on performance; merchant approves changes — explicitly not autonomous spend control in v1

**Why this scope is right:** It closes almost the entire readiness gap identified earlier — from ~5% to genuinely useful — without trying to out-build Meta/Google's own ML teams.

**Credit cost:** None for the integration itself (Meta/Google charge ad spend directly); platform may take a small reporting/management fee bundled into higher plan tiers.

---

## 3. Cross-Cutting: Credit Model Summary

| Module | Cost profile | Plan mapping |
|---|---|---|
| Brand Voice | One-time setup | All plans |
| Generate (text) | Low, high-frequency | Generous allowance, all plans |
| Product Images | Metered, compute-heavy | Usage-based / higher plans |
| Content Library | Storage only | Bundled |
| SEO | Low-moderate | Bundled; bulk scans metered on lower plans |
| Social Media Marketing | Low (reuses Generate/Images) | Bundled |
| Campaigns | Low | Bundled |
| Performance Marketing | No generation cost (ad spend is separate) | Reporting/management access gated by plan tier |

---

## 4. Guardrails Summary (applies platform-wide)

- Mandatory human review before publish — no module gets an "auto-publish" toggle in v1
- Structured-facts-only generation — AI does not invent specs, certifications, or claims not provided by the merchant
- Visible "AI-generated" flagging in the Content Library / review queue
- Regulated verticals (Electronics, Health & Beauty) get stricter review prompts/warnings before publish

---

## 5. Build Sequence (Recommended)

1. **Brand Voice** (foundation)
2. **Generate** (text) + **Content Library**
3. **Product Images**
4. **SEO — metadata + schema + site health**
5. **Social Media Marketing** V1 (Meta + Pinterest)
6. **Campaigns** (email basics)
7. **Performance Marketing** (Meta + Google connectors, feed sync, reporting)
8. **Social Media Marketing** V2/V3, deeper SEO (GEO tracking), Performance Marketing budget advisor

---

## 6. Readiness Recap (post-design)

| Area | Before this design | After this design |
|---|---|---|
| Content Generation | ~85% | ~95% (formalized guardrails, build sequence) |
| Social Media Marketing | ~70% | ~90% (shared creative pipeline defined) |
| SEO | ~20% | Fully scoped — 5 concrete sub-features, ready to build |
| Performance Marketing | ~5% | Fully scoped as an integration layer — realistic, buildable, no longer a placeholder |

**AI Marketing Engine is now fully designed and ready for the build phase.**

Next: **AI Commerce Engine** — Personalization/Recommendations, Shopping Assistant, Dynamic Pricing.
