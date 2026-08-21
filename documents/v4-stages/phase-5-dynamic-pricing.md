# v4 Phase 5 — Dynamic Pricing (Market-Based Only)

**Goal:** the highest legal/trust-risk feature in v4, built last on purpose, exactly as the design doc argues. Ships in **approval-mode only** (resolved decision), with the merchant controls treated as non-negotiable foundation, not add-ons.

**Depends on:** Phase 3 (demand-signal data — view/purchase velocity has to exist before the engine has anything real to react to).
**Blocks:** nothing downstream.

**Resolved scope, from earlier discussion:** ships in v4 (not deferred), approval-mode only, competitor-price signal explicitly **not** included — v1 runs on inventory + demand + seasonality only.

---

## Scope & checklist

- [ ] Per-product merchant controls: price floor, price ceiling, manual lock — before anything else, since the engine must never be able to operate without them already in place
- [ ] Bulk floor/ceiling setup (percentage-of-current-price, applied across a selected category) — the convenience layer on top of per-product bounds, so a merchant isn't hand-configuring hundreds of SKUs one at a time
- [ ] Deterministic, explainable pricing engine (inventory level, demand velocity, seasonality windows) — see design decision below on why this is rule-based, not ML, for v1
- [ ] Approval workflow — suggestion queue, same draft-then-approve UX pattern already used everywhere else in v4, not a new interaction model
- [ ] Change-frequency cap (e.g. one suggestion per product per day) — enforced at generation time, not just at apply time
- [ ] Full price-change audit log, per product and platform-wide
- [ ] Cold-start gating — reuses Phase 3's threshold mechanism, not a new one
- [ ] Real plan-tier enforcement — see design decision below

## Explicitly out of scope for this phase

- **Competitor price tracking.** Resolved as skipped for v1 — no build-vs-buy decision needed since the signal itself isn't in scope. Revisit only if the rest of the feature proves out and there's real demand for it.
- **Auto-apply mode.** Every price change requires merchant approval, no exceptions, no toggle to turn this off — this isn't a v1-vs-v2 phasing question, it's the permanent posture until there's real trust built up with real merchants over real time.
- **Any individual-based signal, ever.** Not a phasing decision — a hard architectural boundary. No shopper identity, location, browsing/purchase history of the specific visitor, or perceived ability to pay ever reaches the pricing engine. Worth a dedicated code-review checklist item for this module specifically, not just a design-doc sentence.

---

## Design decisions

### The engine is deterministic C#, not an LLM, and not ML — by design, not by limitation

Computing a numeric price suggestion is exactly the kind of task an LLM is unreliable at (precise arithmetic, reproducibility, auditability) — so the actual computation is plain, testable, deterministic logic over real signals: stock level vs. reorder threshold, view/purchase velocity trend from Phase 3's event data, and merchant-configured seasonal windows. `IAiService` has exactly one optional job here — phrasing the *explanation* shown to the merchant in plain language ("suggested +8% — stock is low and views are up 40% this week"), never computing the number itself. This also means every suggestion is naturally explainable, which the audit-log guardrail requires anyway — an opaque ML score would fight that requirement, not satisfy it. A learned/ML-refined model is a legitimate future direction once there's real outcome data to validate against (did approved suggestions actually help margin/velocity?) — building one now, before that ground truth exists, would be guessing at what "better" even means.

### Change-frequency limits are enforced at generation, not just at apply

Capping how often a price can move only where the *change* is capped still allows flickering if a merchant approves several suggestions in one day. Instead: the engine only generates one suggestion per product per cadence window in the first place (configurable, default daily) — this makes the frequency guarantee structural rather than relying on approval-time discipline.

### Floor/ceiling are always explicit per-product values, never computed on the fly

The bulk-setup convenience ("set floor to -15% of current price across this category") is a one-time action that *writes* explicit `MinPrice`/`MaxPrice` values onto each product — it never becomes a live percentage recalculated against a moving baseline. This matters because a merchant needs to be able to edit one product's bounds afterward without the bulk rule silently overriding it again later, and because the audit log needs a stable, unambiguous bound to check every suggestion against.

### Manual lock is the escape hatch, checked before the engine ever runs

A per-product lock flag means the engine skips that product entirely at generation time, not just "the merchant will reject the suggestion anyway" — a locked product never even produces a pending suggestion for the merchant to have to notice and dismiss.

### Cold-start reuses Phase 3's mechanism, not a parallel one

Same principle as Personalization: below a minimum demand-data threshold, the engine doesn't run for that store at all — static pricing continues exactly as today. Once Phase 3 already builds the "does this store have enough data" check for Personalization, Dynamic Pricing is a second consumer of the same check, not a second implementation of it.

### This is where plan-tier gating stops being deferrable

Phase 3 flagged that `Plan.MarketingEngineLevel`/etc. are currently decorative labels, not enforced, and scoped "real plan-tier gating" as a cross-cutting task without picking when it actually gets built. Dynamic Pricing is explicitly a Pro+-tier feature per the design doc, and it's now the **second** v4 feature (after Personalization) that genuinely needs this enforced, not just documented. Recommend this phase is where that cross-cutting task actually gets built — a small, generic "does this tenant's plan include capability X" check, used by both features, rather than a one-off gate bolted onto pricing alone.

---

## Implementation notes

- New entities: per-product `MinPrice`/`MaxPrice`/`PriceLocked` fields, `PriceChangeHistory` (ProductId, OldPrice, NewPrice, Signals JSON, SuggestedAt, ApprovedAt, ApprovedByUserId, Applied), `PricingSeasonRule` (tenant-scoped date range + bias + optional category scope)
- Suggestion generation runs as a scheduled job (Phase 0's Hangfire), not a request-triggered computation — it's a continuous background capability, consistent with how the design doc frames its cost model (plan-tier gated, not per-use credited, unlike Shopping Assistant)
- Approval UI: a review queue, deliberately styled the same as Growth's existing draft-review pattern rather than a new interaction paradigm a merchant has to learn separately

## Verification

- A product with no floor/ceiling set never receives a suggestion, regardless of signals
- A locked product never receives a suggestion
- A suggested price is mathematically verifiable against the documented signal logic — no black-box result
- No suggestion ever falls outside the product's own floor/ceiling
- Approving several suggestions for the same product in one day still only ever produced one suggestion for it that day — frequency cap holds even under enthusiastic merchant approval behavior
- Every price change (once approved and applied) appears in the per-product history with the exact signals that drove it
- A store below the demand-data threshold never sees Dynamic Pricing offered as active, same as Personalization's threshold gate in Phase 3
- A tenant on a plan tier without Dynamic Pricing access cannot enable it, enforced server-side not just hidden in the UI
- Confirm, by code review specifically for this module, that no individual-shopper signal (identity, location, personal browsing history) is reachable from the pricing computation path

**Status:** not started.
