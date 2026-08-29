# Billing v2 — pending cluster (A3 / A4 / A5 / A6)

**Status:** planned, not built. These four all mutate the plan/subscription/checkout model and interact with
the Razorpay-gated auto-pay (P2), so they're best built as one focused cycle rather than piecemeal.
Everything already shipped — trial expiry+reminders (P1), recurring auto-debit (P2), GST invoice (D1),
refunds+credit notes (C2/C3), SaaS metrics (H2) — is the foundation these extend.

Build order recommendation: **A5 → A6 → A4 → A3** (annual and coupons are self-contained; add-ons are the
big one; proration is lowest value and depends on the others).

---

## A5 — Annual billing (monthly vs annual)
**Why:** better retention + cash flow; standard SaaS offering.
**Plan:**
- `Plan.AnnualPrice` (decimal?, nullable) + treat a subscription's cadence via a new
  `TenantSubscription.BillingInterval` ("monthly" | "annual").
- `EffectivePriceAsync` returns the annual price when interval = annual (intro logic applies per-cycle as today).
- Period math: the charge caller sets `PeriodEnd` = `AddYears(1)` for annual (today it's `AddMonths(1)`).
- Auto-pay: `RazorpaySubscriptionGateway.EnsurePlanAsync` creates a `period="yearly"` Razorpay plan for
  annual; cache a second `Plan.RazorpayPlanIdAnnual`.
- UI: plan chooser gets a monthly/annual toggle; show annual price + "2 months free"-style savings.
- Migration: `Plan.AnnualPrice`, `Plan.RazorpayPlanIdAnnual`, `TenantSubscription.BillingInterval`.

## A6 — Subscription coupons (merchant-facing promo codes)
**Why:** acquisition/promo lever for merchants signing up.
**Plan:**
- `PlatformCoupon` (global): Code, DiscountType (percent|flat), Value, PlanId? (null=all), DurationMonths?
  (null=forever / N cycles / 1=once), MaxRedemptions?, TimesRedeemed, ExpiresAt?, IsActive.
- `PlatformCouponRedemption` (global): CouponId, TenantId, RedeemedAt — one active coupon per tenant.
- Service: `ValidateAsync(code, planId)` → discount; `RedeemAsync(code, tenantId)`.
- Apply: checkout/auto-pay accept an optional code → reduce the charge; record redemption. Recurring
  discounts on Razorpay use its Offers/addons — v1 can discount the first N cycles via `EffectivePrice`
  (track cycles used).
- Super-admin CRUD for coupons; merchant enters a code on `/admin/billing`.
- Migration: `PlatformCoupons`, `PlatformCouponRedemptions`.

## A4 — Add-on SKUs (AI Text / Image / Video marketing as separate paid modules)
**Why:** the pricing model sells AI marketing as add-ons, but `Plan.MarketingEngineLevel` etc. are
decorative labels today — no separate billing. **This is the largest of the four** (multiple concurrent
subscriptions per tenant).
**Plan:**
- Model add-ons as their own `Plan` rows with a `PlanKind` ("base" | "addon") + `AddonKey`
  ("ai-text" | "ai-image" | ...), or a dedicated `AddonPlan` table.
- Allow **multiple active `TenantSubscription` rows per tenant** (one base + N add-ons) — today the code
  assumes one (`OrderByDescending(...).FirstOrDefault()`). This is the core refactor: everywhere that
  reads "the" subscription must become base-vs-addon aware.
- Each add-on is its own Razorpay subscription (its own mandate/charge) OR a line on the base subscription
  (Razorpay addons). Recommend separate subscriptions for clean cancel/entitlement.
- Entitlement: `IEntitlementService.HasFeatureAsync` already gates features; wire add-on ownership → feature
  flags (e.g. owning "ai-image" unlocks the image generator).
- GST invoice + credit note already generalize (per charge), so D1/C2 cover add-on billing for free.
- UI: an "Add-ons" section on `/admin/billing` to subscribe/cancel each module.
- Migration: `Plan.PlanKind` + `AddonKey`; relax the one-subscription assumption.
> Do A4 after A5/A6 — it's the refactor that touches the most surface (the "one subscription per tenant"
> assumption is baked into `SubscriptionService`, the trial banner, entitlements, and the sweep).

## A3 — Proration on plan change
**Why:** fairness on mid-cycle upgrade/downgrade. **Lowest value now** — current behavior (switch at next
cycle, no proration) is acceptable and already shipped.
**Plan:**
- On upgrade: charge the prorated difference immediately (days remaining × daily-rate delta) and shift the
  plan now; on downgrade: credit the difference (a credit note via C3) or apply at next cycle.
- Hang the math off `EffectivePriceAsync` + a `ProrationService`; issue a credit note (C3) for downgrades.
- Only worth building once A4/A5 make plan changes common. **Defer until then.**

---

## Cross-cutting note
All four should preserve the invariants already established: charges flow through the idempotent
`RecordChargeAsync`; every charge yields a GST invoice (D1); refunds yield credit notes (C3); the sweep
(P1) handles expiry/dunning. Keep those seams — don't add parallel charge paths.
