# AI-4 — Campaign Builder

**Goal:** turn a single goal into a full multi-channel campaign in one action — the feature that makes the engine feel like a marketing *team*, not a text box.

## Scope & checklist
- [ ] **Goal picker** — product launch, flash sale, festival offer, restock, etc.
- [ ] **Multi-channel generation** — one goal → Instagram caption + Facebook post + WhatsApp message + email (subject + body) + product description update, generated **simultaneously** (reuses AI-1 text; AI-3 images optional).
- [ ] **Review per channel** — edit each asset before it goes out.
- [ ] **Scheduling** — publish now or schedule per channel; Hangfire runs the schedule.
- [ ] **Campaign record** — `AiCampaigns` (goal, channels JSON, `ContentIds` JSON, `ScheduledAt`, status) linking all generated assets.
- [ ] **Basic analytics** — per-campaign: assets generated, credits used, scheduled/published counts; delivery status where the channel reports it.

## Publishing
- Direct publish where a connector supports it (WhatsApp send now; social auto-publish per the connector matrix — Own/Shopify V2, others V2.1).
- Where auto-publish isn't available, produce copy-ready assets + a checklist.

## Frontend
Campaign wizard (goal → generated assets grid → edit each → schedule/publish) + a campaigns dashboard with status and simple analytics.

## Gate
Picking "festival offer" generates all five channel assets from one product in a single flow; each is editable; a campaign schedules and fires via Hangfire; the `AiCampaigns` record links every asset; per-campaign credit usage is reported.

## Dependencies
AI-1 (text), optionally AI-3 (images), AI-2 (connectors for publishing/product context). Hangfire (scheduling), SignalR (status).

**Status:** ⬜ Not started.
