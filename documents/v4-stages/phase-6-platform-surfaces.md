# v4 Phase 6 — Platform Surfaces

**Goal:** the pending-tasks.md items (6–11) plus the Super Admin gaps this roadmap's own audit confirmed are genuinely missing. Sequenced per that doc's own dependency notes, not re-derived — it already got the ordering right.

**Depends on:** Phase 3 (Analytics needs real event data to be more than a dashboard over nothing), Phase 0 (webhooks need reliable retry/backoff, same scheduler as everything else).
**Blocks:** nothing — this is the last phase.

---

## Track A — Public API + Webhooks (pending item 6)

### Scope & checklist
- [ ] A **separate, deliberately-versioned public API surface** (e.g. `/api/public/v1/`), not the internal admin API exposed directly
- [ ] Per-tenant API keys, scoped permissions (read-only vs. read-write, per resource)
- [ ] Webhook subscription + delivery system, retry/backoff via Phase 0's Hangfire
- [ ] Rate limiting on the public surface — reuse the existing rate-limiting infra from v1's hardening stage, don't rebuild it
- [ ] OpenAPI/Swagger documentation, generated from the actual public controllers — the technical foundation for the developer half of the docs site (pending item 13)

### Design decisions
- **This must not be the same surface the Angular admin app calls.** The internal API is shaped for that app's exact needs and changes freely as the frontend evolves — a third-party integrator building against it would break on every internal refactor. A public API needs its own stability contract from day one: versioned, deliberately scoped, changed only with real backward-compatibility discipline. This is the single most important decision in this track — get it wrong and every future phase's internal changes become breaking changes for outside developers.
- **Webhooks, not polling, for anything event-driven** (order placed, product updated, inventory changed) — matches how every serious platform API (Shopify included) actually works, and reuses infrastructure this roadmap already built rather than inventing a new delivery mechanism.

---

## Track B — App Marketplace (pending item 7, depends on Track A)

### Scope & checklist
- [ ] "App" concept: what installs, what API scope + webhook subscriptions it's granted, install/uninstall lifecycle
- [ ] App listing + install UI in Merchant Admin
- [ ] **1–2 first-party apps**, per the resolved decision — build these to genuinely exercise the API surface (real webhook consumption, real read/write calls), not trivial demos, since the whole point is validating Track A's design before any third party depends on it
- [ ] Third-party submission/review process — **deliberately not started in this pass**, per the resolved decision

### Design decisions
- Pick the first-party apps for what they force the API to prove, not just utility — e.g. a loyalty app needs read access to order history + write access to a points balance + a webhook on order-completed; an accounting sync needs read access to orders/invoices + a scheduled export. Between them they exercise read, write, and webhook-driven flows, which a single "simple" app wouldn't.

---

## Track C — Theme Store (pending item 9)

### Scope & checklist
- [ ] Browse/preview/install UI as its own designed surface — not just a "Themes" nav item, per the doc's own framing
- [ ] Free vs. paid theme distinction
- [ ] Preview/demo before install
- [ ] **The theme-customization-survives-update question** — flagged as unresolved in the source doc, still unresolved here, needs a real decision before this ships (see below)

### Design decisions
- **Apply the same "first-party first" pattern App Marketplace just resolved, for consistency** — WavCommerce's own theme team builds and curates the initial Theme Store catalog before any third-party theme-designer submission process exists. Not explicitly asked as a separate question earlier, but the same reasoning applies identically here, and having two inconsistent answers to the same shaped question would be a real design smell worth avoiding.
- **The customization-survives-update question needs an explicit decision, not a silent pick** — two real approaches exist: (a) *fork-on-customize* — the moment a merchant edits a theme, it becomes their own independent copy; future base-theme updates never touch it (simpler, safer, no merge-conflict risk, but the merchant misses future fixes/improvements to the base theme), or (b) *layered overrides* — customizations are stored as a diff on top of the base theme, base updates apply underneath while the merchant's specific changes persist (more powerful, meaningfully harder to build correctly, real conflict-resolution risk). Given the existing theme system already stores customization at the section-settings/blocks level, **fork-on-customize is the lower-risk starting point** — but flagging this as a recommendation to confirm, not a decision already made on your behalf, since the source doc explicitly left it open.

---

## Track D — Migration/Onboarding from Competitors (pending item 8)

### Confirmed by reading the actual code (2026-08-21) — this is largely built already

`AiImportController`/`AiImportService` (internally labeled AI-3/AI-4) is a real, working "bring your own file" product migration tool, not a stub:
- Upload any `.xlsx`/`.csv` → **deterministic, free, pre-vetted column-mapping presets already exist for Shopify, WooCommerce, and Wix** (`MigrationPresets.cs`), auto-detected from the header row (≥2 signature columns match) or manually selectable
- Anything a preset doesn't recognize falls through to AI column-mapping (metered only when actually invoked — the preset-covered columns cost nothing)
- Merchant reviews/corrects the proposed mapping before anything is written
- Apply auto-creates missing categories and writes real products through the existing `IProductImportService` — no separate/parallel import pipeline
- Already handles real-world export quirks: strips HTML from descriptions, picks the first URL when a cell packs multiple images (a known Shopify/Woo export pattern)

### What's actually missing, now that the real scope is known

- [x] **Zoho Commerce, Instamojo, and Dukaan presets** — done 2026-08-22. Zoho's mapping is confident (sourced from Zoho's own published export-column documentation). **Dukaan's and Instamojo's are best-effort, explicitly flagged as unverified** — neither platform publishes its bulk-template column names anywhere fetchable; the class doc comment says so directly and explains why this is safe to ship anyway: `Detect()` needs 2+ signature hits to activate (a wrong guess is inert, not harmful), and the existing import flow already requires merchant review of every proposed mapping before anything is written, so a wrong guess can't silently corrupt data — worst case is a wrong pre-selected preset the merchant corrects, not bad data. Confirm the real column names the first time either actually gets used in practice.
- [ ] **Customer & order-history import** — the existing tool is products-only; nothing imports customer records or past order history today
- [ ] Domain migration/DNS cutover guidance — the underlying infra already exists (`Tenant.CustomDomain`/verification), this track's remaining job is a **guided wizard**, not new domain infrastructure
- [ ] Theme/design recreation — stays a **guided, not automated** process: merchant picks the closest WavCommerce theme, AI content tools (Phase 4) help repopulate it — no realistic way to 1:1 auto-port a Shopify theme into a structurally different theme system

### Design decisions
- **Extend the existing importer, don't build a second one.** New platform presets are additive entries in `MigrationPresets.All` — the exact same mechanism Shopify/WooCommerce/Wix already use, not a new import pipeline.
- **This is the third time in this roadmap a design doc's "build from scratch" framing turned out to already be substantially built** (Growth, Helpdesk, now this). Treat that as a pattern for whatever comes after v4, not a coincidence specific to these three — audit before scoping, every time.

---

## Track E — Analytics + Reports (pending items 10–11, depends on Phase 3)

### Scope & checklist
- [ ] **Verify current scope in `documents/stages/stage-analytics-reporting.md` before assuming this is net-new** — v1/v3 already shipped an admin profit/margin dashboard and 6 reports (best-sellers, margin, return-rate, profit by category/supplier) with CSV export. This track extends that engine, it doesn't replace it.
- [ ] Analytics dashboards (funnel/conversion, traffic-source attribution) built **on Phase 3's event data** — same capture, second consumer, not a new pipeline
- [ ] Decide: native analytics vs. GA4 integration — recommendation below
- [ ] Reports: additional report types, scheduling (daily/weekly/monthly auto-email), additional export formats (PDF/Excel alongside existing CSV) — extending the existing reporting engine

### Design decisions
- **Shared engine across Super Admin and Merchant Admin, not two implementations** — resolves the source doc's own open question #5. Same underlying event/order data, scoped by query: Merchant Admin sees their own tenant, Super Admin sees cross-tenant aggregation. Building two separate analytics systems for the same underlying data would be pure duplication.
- **Native first, GA4 as an optional export/integration, not a hard dependency** — owning the data ties directly into Phase 3's own event capture (already built by this point), and not depending on a third-party product for a core platform capability. Offer GA4 export as a convenience for merchants who already use it elsewhere, not as the analytics engine itself.
- **Reports and Import/Export share real infrastructure** (both are "generate a structured file from platform data"), confirming the pending-tasks.md note that these should be designed together rather than as two unrelated features.

---

## Track F — Remaining Super Admin Gaps

### Scope & checklist
- [ ] Merchant onboarding & KYC/verification review workflow
- [ ] Fraud/security monitoring — see design decision below on scope realism
- [ ] Granular staff RBAC (platform staff currently flat, explicitly deferred per the audit)
- [ ] Global configuration & policy management (feature flags, platform-wide settings)

### Design decisions
- **KYC review, App Marketplace's future third-party review, and Theme Store's future third-party review are the same shape of problem** — someone submits something, staff reviews, approves or rejects, with a status/history trail. Worth building one generic "submission/review queue" primitive and using it for all three, rather than three bespoke review screens that drift apart over time. Only KYC needs this in Phase 6 itself; the App/Theme third-party review processes are deliberately deferred (Tracks B/C), but designing KYC's review queue with this reuse in mind now avoids rebuilding it twice later.
- **Fraud monitoring: be realistic about v1 scope, same posture the marketing doc itself takes toward its own "Tier 3 — be skeptical" items.** Real fraud-detection systems are their own specialized discipline; promising a sophisticated ML fraud model here would be over-committing. A grounded v1: rule-based flags (unusual signup velocity, blocklist matches — extending the existing `SignupBlocklist`, abnormal early order volume/value for a brand-new store) reviewed by a human, not an autonomous fraud-blocking system.
- **Granular staff RBAC reuses the pattern already proven for Merchant Admin staff** — per-permission `perm` JWT claims already exist and already work for tenant-level staff roles. Platform staff RBAC is the same mechanism applied to Super Admin's own staff, not a new permission model invented from scratch.

---

## Track G — Docs Site (pending item 13)

### Scope & checklist
- [ ] Merchant help docs — **no dependency on anything else in this phase**, can start anytime real stores exist (they already do)
- [ ] Developer/API docs — depends on Track A being real, generated from the actual OpenAPI spec rather than hand-written and drifting out of sync

### Design decisions
- Split timeline explicitly, per the earlier discussion when this was added to pending-tasks.md — don't let "we're building the docs site" become one combined effort blocked entirely on Track A when half of it has zero reason to wait.

---

## Sequencing within Phase 6

Track A first (everything else in this phase either depends on it or benefits from it existing). Tracks C, D, E, F, and merchant docs in Track G are all independently startable once A is underway — genuinely parallelizable if there's capacity, not a strict waterfall.

## Verification

- Public API: a real third-party-style client (not the Angular app) can authenticate with a scoped API key, read/write within its granted scope, and receive a webhook on a real event — end to end, not just unit-tested in isolation
- App Marketplace: both first-party apps function correctly through the public API alone, with no backdoor access to internal-only endpoints
- Theme Store: install a theme, customize a section, confirm the chosen customization-survival behavior (fork or overlay) behaves as decided when the base theme is later updated
- Migration: import a real (test) Shopify/Zoho export end-to-end, confirm products/customers/orders land correctly in WavCommerce's schema
- Analytics: a funnel/conversion number in the dashboard is independently verifiable against the raw event data it's computed from
- Reports: a scheduled report actually arrives by email on schedule, in the requested format
- KYC review: a submitted merchant document can be approved or rejected by staff, with the outcome correctly gating store activation
- RBAC: a platform staff member with a restricted permission set genuinely cannot perform an action outside their scope, server-side enforced

**Status:** not started.

---

## Closing note

This is the last phase in the v4 roadmap. Read together, `implementation-roadmap.md` plus these seven phase docs (`phase-0` through `phase-6`) are the complete plan — grounded in what the codebase actually has today, not what the source design docs assumed, with every genuinely open decision either resolved during discussion or explicitly flagged rather than silently picked. The recurring pattern worth carrying into whatever comes after v4: **audit before scoping.** Three separate times in this roadmap (Growth, Helpdesk, and now the import tooling), a design doc's "build this from scratch" framing turned out to already be substantially real under a different name — that's not luck, it's what happens when a fast-moving codebase outpaces its own planning docs. Worth checking again before v5.
