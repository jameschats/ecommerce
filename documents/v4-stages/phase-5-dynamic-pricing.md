# v4 Phase 5 — Dynamic Pricing (Market-Based Only)

**Goal:** the highest legal/trust-risk feature in v4, built last on purpose, exactly as the design doc argues. Ships in **approval-mode only** (resolved decision), with the merchant controls treated as non-negotiable foundation, not add-ons.

**Depends on:** Phase 3 (demand-signal data — view/purchase velocity has to exist before the engine has anything real to react to).
**Blocks:** nothing downstream.

**Resolved scope, from earlier discussion:** ships in v4 (not deferred), approval-mode only, competitor-price signal explicitly **not** included — v1 runs on inventory + demand + seasonality only.

---

## Scope & checklist

- [x] Per-product merchant controls: price floor, price ceiling, manual lock — done 2026-08-22
- [x] Bulk floor/ceiling setup (percentage-of-current-price, applied across a selected category) — done
- [x] Deterministic, explainable pricing engine — done, **with a real, honest limit**: only 2 of the 3 signals are live (inventory + seasonality); demand velocity is a hardcoded 0 pending Phase 3 Track B, not faked. See below.
- [x] Approval workflow — done, same draft-then-approve pattern, approving *is* applying (one step, matching Growth's own "Kept" pattern rather than a separate apply step)
- [x] Change-frequency cap — done, enforced at generation (a product already suggested today is skipped, not just "the new suggestion would be capped")
- [x] Full price-change audit log — done (every suggestion persists regardless of outcome; the queue and the audit log are the same read, filtered by status)
- [ ] ~~Cold-start gating~~ — **doesn't apply yet, not skipped.** Cold-start only matters for the demand signal, which isn't live (see above) — inventory and seasonality have no cold-start problem (inventory exists from product #1, season rules are merchant-authored). Revisit once Phase 3 Track B ships and the demand signal goes live.
- [x] Real plan-tier enforcement — done, and **turned out to already exist**: see correction below.

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

### Correction (2026-08-22): the generic mechanism already existed — checked before building

`MarketingEngineLevel`/`LiveChatLevel`/`HelpdeskLevel` genuinely are decorative-only (`Plan.cs` doc comment: "free-text pricing-page label... not gated") — that part of the roadmap's claim was right. But a **separate, already-real, already-generic mechanism** exists alongside them: `Plan.Features` (a JSON string list) + `[RequiresFeatureAttribute("key")]` + `IEntitlementService.HasFeatureAsync`, server-side enforced (402 if missing), and **already in production use** — `GrowthController` has been gated with `[RequiresFeature("growth")]` since Growth shipped. This phase didn't need to build plan-tier gating; it needed to be the *second real consumer* of a mechanism that was already there, same "audit before scoping" pattern that's recurred all through this v4 build. `PricingController` is gated with `[RequiresFeature("dynamic-pricing")]`, and migration 270 turns the feature on for the `pro`/`enterprise` plan tiers (same data-migration pattern migration 242 used for `growth`).

---

## Implementation notes (what actually shipped, 2026-08-22)

- New: `Product.MinPrice`/`MaxPrice`/`PriceLocked`, `PricingSeasonRule`, `PriceSuggestion` (migration 269) — `Features/Pricing/` (`PricingControlsService`, `PricingEngineService`, `PricingSuggestionService`, `PricingController`).
- **The demand signal is hardcoded to 0%, not faked or simulated.** `PricingEngineService`'s inventory signal (stock vs. reorder level, three fixed thresholds: <0.5x → +8%, <1.0x → +4%, >3.0x → −5%) and seasonality signal (active `PricingSeasonRule` window, largest-absolute-bias-wins when overlapping) are both real and tested. Demand velocity has no data source until Phase 3 Track B's event capture ships — the code comment on `DemandSignalPercent` and the class doc comment both say so explicitly, so this isn't a silent gap.
- Approving a suggestion re-checks the product's current bounds/lock state at approval time, not just at generation time — a merchant may have tightened a floor/ceiling or locked the product in the time between a suggestion being generated and reviewed; approval is rejected (not silently clamped) if the suggestion no longer fits.
- Scheduled generation (`RunScheduledGenerationAsync`, Hangfire, once daily by default via `Pricing:SweepIntervalHours`) loops every active tenant with `dynamic-pricing` entitlement using `tenant.BeginScope(id)` — the same cross-tenant background pattern already used by `SupportService`/`SuperAdminService`/`OnboardingService`, not a new one.
- Admin UI: `/admin/pricing` (linked from the sidebar's Discounts section) — three tabs: suggestion queue (approve/dismiss, "check now" button), per-product bounds + bulk setup, season rules CRUD. Built now rather than left backend-only, since an approval queue nobody can see or act on isn't a real feature — unlike some other backend-only slices this session where the gap was more defensible.
- 16 new backend tests covering the exact verification checklist below: no-bounds/locked/normal-stock all correctly produce zero suggestions, each inventory threshold matches its documented percentage exactly, clamping never exceeds bounds, an expired season rule never applies, the frequency cap holds across repeated same-day generation, approve/reject/re-approve-rejected/approve-after-lock all behave correctly, bulk bounds skip locked products, and the entitlement gate genuinely blocks generation for a non-entitled tenant. Full suite: 383/383 passing. `ng build` clean.
- **Code-review pass completed** (the plan's own verification item, high-stakes enough to check explicitly rather than assume): grepped `Features/Pricing/` for any reference to `UserId`/`CustomerId`/`ShopperId`/session/`db.Users`/`db.Orders` — the only `UserId` present is `ApprovedByUserId` (which merchant admin approved a suggestion, an audit field, not a signal input). `GenerateSuggestionsForCurrentTenantAsync` reads only `Products`, `Inventory`, and `PricingSeasonRules` — no individual-shopper signal is reachable from the pricing computation path, confirmed by inspection, not just by design intent.

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

**Status:** Done, 2026-08-22 — with the demand signal honestly stubbed at neutral pending Phase 3 Track B.
