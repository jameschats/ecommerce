# AI-3 — Image Generation

**Goal:** branded marketing images — posters, banners, social creatives — generated from product images + copy. Moderate cost, solid margins (50–75%).

## Scope & checklist — content types (with credit costs)
- [ ] **Product Poster** (20) — branded poster: product + price + offer + logo
- [ ] **Social Story** (20) — vertical 9:16 for Instagram/WhatsApp Stories
- [ ] **Square Post** (20) — 1:1 for IG/FB feed
- [ ] **Banner Ad** (20) — 16:9 for web/email header
- [ ] **Festival Creative** (25) — festival background + product overlay *(calendar in AI-6)*
- [ ] **Background Removal** (10) — remove product bg; replace white/branded
- [ ] **Product Mockup** (30) — product placed on a lifestyle scene

## Supporting scope
- [ ] **Image providers** — `FluxProvider` (best quality-to-cost, default), `StabilityAIProvider` (SDXL), `OpenAIImageProvider` (DALL-E 3, premium). Config-selected via the AI-0 factory.
- [ ] **Templates** — 20+ starter templates; brand kit (logo, colours, fonts) applied automatically.
- [ ] **Size presets** — 1:1, 9:16, 16:9 one-click.
- [ ] **Async generation** — Hangfire job (images take seconds); notify via the SignalR bell when ready.
- [ ] **Credit safety** — debit before generate, refund on provider failure (AI-0), idempotent on `GeneratedContentId`.

## Frontend
Template gallery → pick product + template + size → generate → preview/regenerate → download/save to library. Brand-kit settings per account.

## Gate
Each content type produces a usable branded image from a sample product; background removal works; brand kit is applied; size presets export correctly; credits debit per the table; a failed generation refunds. Generation runs off the request thread with a completion notification.

## Dependencies
AI-0 (providers + credits + async), product images via AI-2 connectors (or upload). Flux/Stability/OpenAI image keys.

**Status:** ⬜ Not started.
