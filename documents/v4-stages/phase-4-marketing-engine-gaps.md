# v4 Phase 4 — AI Marketing Engine: Closing the Gaps

**Goal:** everything the design doc calls Content Generation is already live as Growth (Brand Voice, Generate, Product Images, Campaign builder, basic SEO). This phase is specifically the parts that don't exist yet: hardening the Content Library, the 4 remaining SEO sub-features, and the two genuinely-new integration surfaces — actual social posting/scheduling and Performance Marketing (ad platform connectors).

**Depends on:** Phase 0 (scheduler, for social post scheduling and any recurring feed-sync jobs), Phase 1 (WhatsApp BSP connector, for the WhatsApp Commerce track).
**Blocks:** nothing downstream depends on this phase completing before starting — genuinely parallelizable with Phase 3/5 if there's capacity.

---

## Track A — Content Library + SEO Gaps (no external dependencies, start anytime)

### Scope & checklist
- [x] Harden `GrowthContent` into the real Content Library — done 2026-08-21, except cross-channel reuse tracking (see below)
- [ ] Bulk keyword research & intent mapping across the full catalog (genuinely new AI feature — uses `IAiService`, new credit-metered category)
- [x] ~~Automated schema markup~~ — **already shipped, and not what this roadmap assumed.** See correction below (2026-08-21).
- [ ] Site health monitoring (broken links, duplicate content, crawl errors, thin product pages) — flagged to the merchant, never auto-fixed
- [ ] Content briefs / blog assist, feeding the same Generate + Brand Voice pipeline Growth already uses

### Correction to this roadmap's own premise (2026-08-21)

This section assumed schema markup and sitemap infrastructure didn't exist. Both already do — the same "audit before scoping" pattern that corrected several other phases in this roadmap. Investigated instead of building blind, and found:

- **Product/Offer/BreadcrumbList JSON-LD already ships** — `ProductPageStore.applySeo()` (`ecomm.web/src/app/features/storefront/product/product-page.store.ts`), via an existing `SeoService` (`core/services/seo.service.ts`) that also handles title/meta description/OG tags, sourced from `Product.MetaTitle`/`MetaDescription` (already AI-assistable via the existing `AiImproveService.SeoAsync` / "✨ Improve with AI" button in the product admin — not new work, already there). Same JSON-LD pattern is also used on the homepage and FAQ page.
- **`sitemap.xml` already ships** — `ecomm.api/Features/Seo/SitemapController.cs`, `GET /api/sitemap.xml`, built from live categories + active products.
- **Real gap found and fixed**: the JSON-LD's `Offer.availability` was hardcoded to `https://schema.org/InStock` regardless of actual stock — a genuine bug, not a missing feature. Google Merchant Center treats incorrect availability as a policy violation, not a cosmetic issue. Fixed to read `p.inStock`.
- **Real gap found and fixed**: `robots.txt` genuinely didn't exist anywhere (confirmed — this half of the roadmap's "SEO infra deferred" note was accurate). Added at `ecomm.web/src/server.ts` as an Express route, **not** a `.NET` controller — robots.txt must be served at the literal domain root for crawlers to find it (unlike sitemap.xml, which only needs to be *referenced*, so staying under `/api/` is fine for that one). Built per-request from the actual host header rather than a fixed config value, since every tenant subdomain needs its own correct `Sitemap:` URL — deliberately more correct than the sitemap controller's own single-hardcoded-base-URL approach (`Cors:AngularOrigin` config, defaults to a stale single-tenant value), which is a **pre-existing gap flagged here, not fixed** — out of scope for this pass, worth a follow-up since every tenant's sitemap currently reports the same base URL.
- **Genuinely not done, and lower priority than they looked**: individual `Review` markup (only `AggregateRating` exists — sufficient for Google's rich-result eligibility, `Review` is supplementary) and GTIN/MPN identifiers (no field exists on `Product` for this at all; most Indian D2C sellers don't have registered GTINs anyway, so this would be new schema + admin UI for a field with unclear demand — not built without a real ask).

### Content Library hardening — real findings and what shipped (2026-08-21)

Researched before building (same discipline as the SEO correction above) — `GrowthContent` was a single mutable row per piece: `UpdateAsync` overwrote `Body`/`Title` destructively with no history, `Status` (Draft/Kept/Discarded) conflated workflow stage with edit-provenance, and the actual Angular library screen had **zero filter/search UI** despite the backend already having `ProductId`/`ContentType`/`CreatedAt` columns to filter on. Of the roadmap's three asks:

- **Version history — genuinely missing, now fixed.** New `OriginalBody`/`OriginalTitle` columns (migration 268) snapshot the AI-generated text at creation time and are never touched by `UpdateAsync` — so `Body != OriginalBody` is a real, diffable "this was hand-edited" fact instead of unknowable. `GrowthContentDto` gained `WasEdited` (computed, not stored, so it can never drift from the truth) plus the original text itself, so the library UI can show a "View original AI draft" toggle. Rows from before this migration get `OriginalBody = null`, treated as "unknown," never as a false edited-flag.
- **Search by product/campaign/channel/date — partially real gap, now fixed.** The columns already existed; what was missing was query support (`campaignId`/`from`/`to` added to `LibraryAsync`) and, more importantly, that the frontend never exposed ANY of it — not even the `productId` filter the backend already supported. Added channel/date-range filter controls to the library screen, plus a deep-link from the Campaigns screen's "N pieces" count straight into the library pre-filtered to that campaign. **Free-text tags were deliberately not built** — the roadmap bullet read "tag/search by product/campaign/channel/date," which on inspection describes filtering by those four real dimensions, not a literal tags feature; inventing a separate tags system nobody's asked for would be scope creep this pass didn't need.
- **Cross-channel reuse tracking — genuinely missing, deliberately not built.** There is zero existing scaffolding (no scheduling/publishing entity exists anywhere in the codebase yet — that's Phase 4 Track B, itself not started), and the only "reuse" affordance today is a client-side clipboard-copy button with no server record. Designing this now would mean guessing at a shape Track B's still-undecided scheduling model will later dictate — deferred, not silently skipped.

3 new tests (edit preserves original + flags `WasEdited`, status-only change does not flag as edited, campaign/date-range filtering). Full suite: 367/367 passing. `ng build` clean.

### Design decisions

**Schema markup was never actually an open item** — the "not an AI feature, deterministic templating" framing from the original draft turned out to be correct in principle but moot in practice, since the templating was already built. What was actually open was two concrete bugs/gaps (availability, robots.txt), now closed.

**Site health monitoring is mostly deterministic too** — broken-link and duplicate-content checks are crawl-and-compare, not AI judgment calls. "Thin product page" flagging is the one sub-piece that benefits from an AI content-quality pass; keep that as a small optional layer on top of the deterministic checks, not the foundation of the feature.

**Bulk keyword research and content briefs are the two genuinely new `IAiService` consumers here** — new credit categories in `AiCreditPricing`, same metering pattern every other AI feature already uses (no new abstraction needed).

---

## Track B — Social Media Marketing: Actual Posting & Scheduling

### Scope & checklist
- [ ] Meta OAuth connector (Instagram + Facebook) — merchant links their account, platform gets posting permission
- [ ] Pinterest connector (V1 scope per the design doc)
- [ ] Catalog-to-post automation: new/bestselling products auto-drafted into posts (reusing Growth's existing image + caption generation — not regenerating this capability), merchant approves, then scheduled
- [ ] Multi-platform scheduling calendar UI
- [ ] Auto-skip out-of-stock products in generated post suggestions
- [ ] Scheduled posts fire via **Phase 0's Hangfire**, not a new bespoke scheduler

### Design decisions
- **Content generation is already solved — this track is purely "connect + schedule + publish."** Growth already produces Instagram/Facebook captions and marketing images in the right formats; this track's job is OAuth account linking, a scheduling calendar, and the actual publish API calls at the scheduled time. Don't rebuild caption/image generation.
- **V2/V3 (TikTok, X, video/reel generation, deeper native integrations) explicitly deferred**, per the design doc's own phasing — V1 here is Meta + Pinterest only.

---

## Track C — Performance Marketing + WhatsApp Commerce

### Scope & checklist — Performance Marketing
- [ ] Meta Ads + Google Ads OAuth connectors — **separate credential/permission scope from Track B's Meta connector** (Marketing API vs. Graph API), but share the same account-linking UX pattern so a merchant doesn't learn two different "connect your Meta account" flows
- [ ] Product feed sync — push the Products catalog into Meta Catalog Manager / Google Merchant Center automatically, kept in sync with inventory/pricing (scheduled via Hangfire, not manual re-export)
- [ ] Creative handoff — Growth's existing generated images/copy exposed as ad-creative input to Meta/Google's own generative ad tools (no new generation, a new export/handoff surface)
- [ ] Budget & campaign setup wizard — guided flow to launch an Advantage+ Shopping Campaign / Performance Max campaign without leaving Merchant Admin
- [ ] Unified reporting — ROAS/spend/CPA pulled from both platforms into one view (feeds Phase 6's Analytics)
- [ ] Budget advisor — AI suggests allocation/reallocation, merchant approves; **not** autopilot spend control

### Scope & checklist — WhatsApp Commerce
- [ ] Catalog sync to Meta Commerce Manager/WABA, via **Phase 1's `IWhatsAppProvider`**
- [ ] Conversational checkout — see design decision below
- [ ] GST-compliant invoicing per WhatsApp order
- [ ] Abandoned cart recovery, order confirmation, shipment tracking — all via WhatsApp notifications (Phase 1's router, WhatsApp channel)
- [ ] AI auto-replies for FAQ-shaped product questions — **this is Phase 2's chatbot on the WhatsApp channel it already gained in that phase**, not a new bot
- [ ] Shared team inbox for WhatsApp conversations — **already exists**, this is the `ShopperMerchant`-axis Inbox every other channel already lands in

### Design decisions
- **No custom ad-bidding engine, by explicit design.** Meta/Google already run world-class bidding AI (Advantage+, Performance Max) — the entire value here is integration + reporting, not competing with their ML teams. This bounds Performance Marketing's real scope to exactly the 6 checklist items above, nothing more.
- **WhatsApp orders are real `Order` entities through the existing order pipeline — not a parallel commerce system.** GST tax calculation, invoice PDF generation (QuestPDF), inventory reserve/commit, order status notifications — all of this already exists for storefront checkout. WhatsApp Commerce's actual new work is narrower than it looks: a new **order-creation entry point** that happens to be conversational, feeding the same pipeline every other channel already uses. Framing it this way avoids building a second, parallel invoicing/inventory system by accident.
- **Conversational checkout is the highest-complexity, highest-risk item in this entire phase** — payment collection inside a chat thread, via UPI, needs to trigger the same `IPaymentGateway` flow (Razorpay) already used for storefront checkout, just initiated from a WhatsApp message instead of a web page. Recommend treating this as its own sub-milestone within the track, built and verified *after* catalog sync + notifications + AI auto-replies are already working — those three ship real value on their own and de-risk the BSP integration before payment collection is added on top.
- **The AI auto-reply piece is zero new bot work.** Phase 2 already built a chatbot with a WhatsApp channel (via Phase 1's connector); WhatsApp Commerce's "FAQ-shaped product questions" requirement is that exact same bot, already deployed on that exact same channel. Nothing to build here beyond confirming it's live for shopping-flow contexts too.

---

## Explicitly out of scope for this phase

- **Predictive customer scoring / advanced segmentation** for Campaigns (2.7) — the design doc itself defers this toward the Commerce Engine's future CDP-level personalization work, not v4
- **Fully autonomous campaign execution or ad spend** — Budget advisor suggests, merchant approves, no exceptions
- **AI Search/GEO tracking** (brand visibility inside ChatGPT/Perplexity answers) — real and growing per the design doc, explicitly deferred past v4

---

## Implementation notes

- Track A is the natural starting point — zero external OAuth dependencies, fastest to ship, immediately useful (schema markup alone closes a real gap every live product page has today)
- Tracks B and C's Meta connectors are architecturally related (both OAuth-to-Meta) but functionally separate (Graph API for organic posting vs. Marketing API for ads) — build the account-linking UX as one shared pattern, the actual API integrations as two separate connector implementations
- WhatsApp Commerce's non-checkout pieces (catalog sync, notifications, AI replies) can ship as soon as Phase 1's BSP connector exists; conversational checkout should trail behind, once those are proven live

## Verification

- Schema markup present and valid (Google's Rich Results Test) on every published product page
- Site health check correctly flags a deliberately broken link and a deliberately thin product page, changes nothing automatically
- Connect a test Meta account → catalog-to-post suggestion appears with correct image/caption → merchant approves → post publishes at the scheduled time, out-of-stock items never suggested
- Connect a test Meta Ads / Google Ads account → product feed appears correctly in Catalog Manager/Merchant Center, stays in sync after a price change
- Launch a test Advantage+ Shopping Campaign via the wizard without leaving Merchant Admin; reporting dashboard shows real spend/ROAS pulled back from Meta
- WhatsApp: catalog syncs correctly, an order placed via WhatsApp produces a real `Order` with correct GST and a real invoice PDF, identical in structure to a storefront order
- WhatsApp conversational checkout: a real UPI payment collected through the chat thread correctly completes via the same `IPaymentGateway` flow storefront checkout uses — verified last, after everything else in the track is stable

**Status:** not started. Track A can start immediately in parallel with Phases 3/5.
