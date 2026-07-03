# V2-12 — Merchant Engagement & Lifecycle (Super-Admin Scheduler)

**Goal:** proactively nurture the merchant relationship to **reduce churn** and deepen loyalty — scheduled, automated, super-admin-configurable outreach (anniversaries, festival wishes, milestones) plus recurring **feedback loops** (quarterly/annual NPS) that feed a merchant health signal. Retention is the #1 SaaS lever; this is the customer-success engine.

## Mode
A **super-admin scheduler** that runs recurring / occasion-based campaigns via **Hangfire**, sending through the **V2-11 notification dispatcher** (email + WhatsApp + in-app), personalised (optionally by the **V3 AI engine**), and respecting per-merchant preferences + frequency caps.

## Scope & checklist

### 12a. Scheduled engagement campaigns
- [ ] **Store anniversary** — N years since signup → congratulatory message (+ optional loyalty perk / account credit).
- [ ] **Festival wishes** — Diwali, New Year, Eid, Pongal, Onam, Holi… fired on the festival date; reuses the **V3 AI festival calendar** (30+ Indian festivals) and can AI-generate Hinglish/regional copy.
- [ ] **Milestone celebrations** — first sale · 100th order · ₹1L GMV · plan upgrade → congratulate (data from analytics).
- [ ] **Re-engagement** — inactive merchant (no logins/orders in N days) → check-in nudge (also a churn signal).
- [ ] **Renewal / relationship reminders** — plan renewal, trial ending (relationship-framed; billing mechanics stay in V2-1).

### 12b. Recurring feedback (quarterly / yearly)
- [ ] **Scheduled NPS / CSAT surveys** — cadence configurable (monthly/quarterly/annual); sent via the dispatcher.
- [ ] **Response capture → health signal** — store responses; **detractors auto-flagged** to super-admin (link to a support ticket V2-9 / diagnostics V2-10); **promoters** prompted for a testimonial / referral.
- [ ] **Merchant health score** — aggregate login recency, order volume, NPS, ticket volume, payment health → a churn-risk signal on the super-admin tenant view.

### 12c. Campaign management (super-admin UI)
- [ ] Create/schedule campaigns; **target segments** (all / by plan / by tenure / by health / by region); preview + test-send; pause/resume.
- [ ] Sent + response analytics per campaign (delivered, opened where available, responded, NPS distribution).

### 12d. Guardrails (don't spam merchants)
- [ ] **Frequency caps** — a max engagement-message rate per merchant; transactional notifications (orders/billing) are never throttled, only relationship/marketing ones.
- [ ] **Opt-out + consent** — these are relationship/marketing messages → honour per-merchant opt-out + **DPDP** consent (separate from transactional, per V2-11).

## Data model
Migrations `220–229`, tenant-aware:
```
EngagementCampaigns   CampaignId, Name, Type (Anniversary|Festival|Feedback|Milestone|ReEngagement|Renewal),
                      Schedule (cron/occasion), Channels, TargetSegment (JSON), TemplateId,
                      Status, CreatedByAdminId, CreatedAt
MerchantFeedback      FeedbackId, TenantId, CampaignId, Type (NPS|CSAT|Survey), Score, Comment,
                      Sentiment, CreatedAt
MerchantHealthScore   TenantId, Score, Signals (JSON: loginRecency, orderVolume, nps, tickets,
                      paymentHealth), UpdatedAt
```
Reuse: V2-11 templates/deliveries/preferences, V3 festival calendar, analytics aggregates.

## Endpoints
- **Super-admin:** `GET/POST/PUT /api/superadmin/engagement/campaigns`, `POST .../{id}/pause|resume`, `GET .../{id}/analytics`, `GET /api/superadmin/tenants/{id}/health`.
- **Merchant/public:** `POST /api/engagement/feedback` (survey response), `GET /api/engagement/feedback/{token}` (survey link).

## Gate
An anniversary campaign auto-sends on a test merchant's signup-date anniversary via WhatsApp + email under platform brand; a festival campaign fires on the festival date to all opted-in merchants; a quarterly NPS survey sends → a response records → the health score updates → a detractor is flagged to super-admin; opt-out + frequency caps suppress correctly; transactional sends remain unaffected.

## Dependencies
V2-11 (notification dispatcher + channels + preferences), V2-3 (super admin), V2-9 (feedback→ticket link), V2-10 (health surfaced in diagnostics), Hangfire (scheduling), analytics (milestones/health). Optional: V3 AI (personalised/festival copy — best paired once V3 ships).

**Status:** ⬜ Not started. Churn-reduction / customer-success layer; pairs with V2-11 + V3.
