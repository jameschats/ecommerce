# AI-1 — Text Generation (the MVP)

**Goal:** ship the cheapest, highest-margin capability first — text — and **charge for it** to validate demand before building images or video.

> **This is the MVP. Ship it, price it, prove willingness to pay, then expand.** Text margins are 73–90% ([design-v3.md §9](../design-v3.md)).

## Scope & checklist — content types (with credit costs)
- [ ] **Product Description** (5) — SEO description w/ features, benefits, keywords
- [ ] **Instagram Caption** (3) — caption + CTA + 30 hashtag options
- [ ] **Facebook Post** (3) — story-driven, organic or ad variant
- [ ] **WhatsApp Campaign** (2) — short punchy offer, bulk-send ready
- [ ] **Email Campaign** (8) — subject + preview + body + personalisation tokens
- [ ] **Blog Article** (20) — 800–1,500 word SEO article w/ headings
- [ ] **Google Ads Copy** (5) — headlines (30ch) + descriptions (90ch)
- [ ] **SEO Meta Tags** (4) — title tag + meta description
- [ ] **Festival Offer Post** (3) — themed copy *(full festival calendar lands in AI-6)*

## Supporting scope
- [ ] **Prompt templates** — `AiPromptTemplates` (global defaults + per-account overrides), variables per content type.
- [ ] **Language selector** — English / Hindi / Tamil / Telugu (full regional + Hinglish in AI-6).
- [ ] **Generate → review → save/edit** — output lands in `AiGeneratedContent` (`Status: Draft`); merchant edits before use.
- [ ] **Source product context** — pull product fields (from CSV or, later, a connector) into the prompt so copy is grounded in real product data.
- [ ] **Provider routing** — Flash for bulk; GPT-4o-mini / Claude Haiku for long-form; GPT-4o / Claude Sonnet for the Pro quality tier (metered). See the [design-v3.md §3](../design-v3.md) routing table.

## Frontend
A generation UI (pick content type → fill/confirm product context → language → generate → edit → copy/save), a content library (`AiGeneratedContent` history), and a credit-balance widget.

## Gate
Each content type generates coherent, on-brand copy grounded in a sample product; credits debit correctly per the table; a merchant can edit + save; language selector produces Hindi/Tamil/Telugu output. Priced and behind a paywall (an AI plan or top-up required).

## Dependencies
AI-0 (providers + credits). Product data via CSV now; live connectors in AI-2.

**Status:** ⬜ Not started.
