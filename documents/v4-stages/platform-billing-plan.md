# Plan — Automated Subscription Billing (trial → auto-debit → dunning)

**Goal:** after a merchant's free trial ends, collect a payment method once and then charge the plan fee
**automatically every month** (auto-debit), with proper failure handling — instead of today's manual,
one-time, per-cycle checkout that a merchant must repeat by hand.

**Status:** planned. This is the missing half of platform monetization — the plan/credit *ceilings* are
enforced, but nothing collects money on a recurring basis.

---

## 1. Where we are today (verified in code)

| Piece | State |
|---|---|
| Plans & prices | `Plan.MonthlyPrice` + intro pricing (`IntroPriceInr`/`IntroMonths`). **Monthly only, INR only.** |
| Trial | `Tenant.TrialEndsAt` set at signup (`OnboardingService`). Initial `TenantSubscription` is `Status="Trial"` with **`CurrentPeriodEnd = null`**. |
| Lifecycle sweep | Hangfire recurring job (`SubscriptionService.RunLifecycleSweepAsync`). Moves `CurrentPeriodEnd < now` → `PastDue` (+grace) → `Suspended`. **Only downgrades; never charges.** |
| Paying the platform | One-time manual checkout (`StartCheckoutAsync` → browser pays → `RecordChargeAsync` activates for `now.AddMonths(1)`). Same flow for AI-credit top-ups. Razorpay or Mock. |
| Recurring / mandates | **None.** `IPaymentGateway` is one-time only (`CreateOrderAsync`/`VerifySignature`/`RefundAsync`). `TenantSubscription.RazorpaySubscriptionId` exists but is a **stub set only from an inbound webhook** — nothing creates a Razorpay subscription. |

### Two seams that must be fixed
1. **Trials never auto-expire.** The sweep filters on `CurrentPeriodEnd != null && CurrentPeriodEnd < now`,
   but a fresh trial has `CurrentPeriodEnd = null`. `Tenant.TrialEndsAt` is set but the sweep never reads
   it. → Trials sit forever unless a paid charge populates `CurrentPeriodEnd`.
2. **No recurring primitive.** No mandate/autopay/subscription creation; the sweep can't charge.

### Seams we can build on
- `PlatformPaymentGatewayFactory` cleanly isolates platform billing from merchant checkout.
- `RecordChargeAsync` is already **idempotent** (dedupes on payment id) and already clears `SuspendedAt` on
  a successful charge — i.e. "money landed → (re)activate" already works.
- Two webhook paths already exist (`BillingWebhookController` shared-secret; `RazorpayWebhookService` HMAC).
- Hangfire recurring sweep + config-driven cadence/grace already in place.
- `EffectivePriceAsync` centralizes server-side price derivation — the hook point for annual/proration later.

---

## 2. The right primitive: Razorpay Subscriptions (+ RBI e-mandate reality)

For India recurring, you do **not** re-charge a saved card yourself — RBI rules require a registered
**mandate** (e-mandate on cards, UPI Autopay, or net-banking e-mandate) with:
- a one-time **authentication** transaction (AFA) to register the mandate,
- a **pre-debit notification** to the customer ≥24h before each charge,
- an AFA-free auto-debit cap (currently **₹15,000** per transaction; above it needs step-up auth each time).

**Razorpay Subscriptions** handles all of this for us: it registers the mandate, sends the pre-debit
notice, runs the recurring charge on schedule, retries on failure, and emits webhooks
(`subscription.activated`, `subscription.charged`, `subscription.pending`, `subscription.halted`,
`subscription.cancelled`). **We should integrate Razorpay Subscriptions rather than build mandate
mechanics ourselves.** (Plan fees are well under ₹15k, so the AFA-cap edge case doesn't bite v1.)

---

## 3. Target architecture

### 3a. Data model (migration, additive)
Extend `TenantSubscription`:
- `RazorpaySubscriptionId` — now actually populated when we create the subscription.
- `RazorpayCustomerId`, `RazorpayPlanId` — Razorpay-side ids.
- `MandateStatus` — `none | pending | active | paused | cancelled`.
- `PaymentMethodSummary` — e.g. "Visa •••• 4242" / "UPI autopay" (display only; never store PAN).
- `NextChargeAt` — the next scheduled auto-debit (mirrors Razorpay's `charge_at`).
- `CancelAtPeriodEnd` (bool) — merchant asked to cancel; stop after the current cycle.

Map `Plan` → a Razorpay Plan (create once per plan+interval; cache the `RazorpayPlanId`, ideally on `Plan`).

### 3b. Gateway abstraction (new — parallel to the one-time `IPaymentGateway`)
```
IRecurringBillingGateway
  Task<MandateSetup> CreateSubscriptionAsync(tenant, plan, interval, ct)   // returns razorpay subscription id + hosted auth URL/short_url
  Task CancelSubscriptionAsync(subscriptionId, bool atCycleEnd, ct)
  Task<MandateStatus> GetStatusAsync(subscriptionId, ct)
  bool VerifyWebhookSignature(payload, signature)
```
Implementations: `RazorpaySubscriptionGateway` (real) + `MockRecurringGateway` (dev). Resolved through the
existing `PlatformPaymentGatewayFactory` (widen it), so Mock/Live stays a config toggle.

### 3c. The charge step in the sweep
The sweep stays the safety net (downgrade/suspend), but **charges are driven by Razorpay webhooks**, not by
us calling charge in the sweep. `subscription.charged` → `RecordChargeAsync` (already idempotent) advances
`CurrentPeriodEnd` and clears any PastDue/suspension. The sweep only acts when Razorpay *couldn't* charge.

---

## 4. End-to-end flows

### Flow A — Trial → convert (the core ask)
1. **Fix the trial seam:** at trial start, set `TenantSubscription.CurrentPeriodEnd = Tenant.TrialEndsAt`
   so the sweep treats trial-end uniformly (no special-casing). *(One-line change; unblocks everything.)*
2. **Reminder cadence** (dunning-before-the-fact): sweep emits "trial ends in 7/3/1 days — add a payment
   method" emails (reuse `INotificationService` templates). In-app banner on `/admin/billing`.
3. **Add payment method:** merchant clicks "Set up auto-pay" → we `CreateSubscriptionAsync` → redirect to
   Razorpay's hosted mandate authorization (card e-mandate / UPI Autopay) → they authorize (₹ auth txn).
4. **Activation:** `subscription.activated` + first `subscription.charged` webhook → `RecordChargeAsync`
   → `Status=Active`, `CurrentPeriodEnd = now + 1 month`, `NextChargeAt` from Razorpay.
5. **Every following month:** Razorpay auto-debits and fires `subscription.charged` → `RecordChargeAsync`.
   Fully hands-off for the merchant.

### Flow B — Failed charge → dunning → suspend
1. Razorpay retries per its schedule; fires `subscription.pending` → we set `PastDue`, `GraceEndsAt`, send
   "payment failed, update your method" emails.
2. If it recovers (`subscription.charged`) → back to `Active`.
3. If grace passes with no charge (or `subscription.halted`) → `Suspended` (storefront 404s, per today's
   `Tenant.SuspendedAt`). Merchant can re-authorize to reactivate.

### Flow C — Cancel / change plan
- **Cancel:** `CancelSubscriptionAsync(atCycleEnd:true)` → `CancelAtPeriodEnd=true`; access until period end,
  then Suspended. (Immediate cancel is the `atCycleEnd:false` path.)
- **Upgrade/downgrade:** simplest v1 = cancel current Razorpay subscription, create the new-plan one at next
  cycle (no mid-cycle proration). Proration is a v2 refinement hung off `EffectivePriceAsync`.

### Card-upfront vs card-after-trial — **decision needed**
- **Card-after-trial (recommended for Indian SMB):** lower signup friction; higher trial→paid drop-off,
  needs the reminder cadence above. Matches "let them taste it first."
- **Card-upfront (auto-convert):** higher conversion, but a card wall at signup deters exactly the
  small-merchant segment we target. Recommend **after-trial** for v1, revisit with real funnel data.

---

## 5. Admin/merchant UI (`/admin/billing`)
- Current plan, price, **next billing date**, payment-method summary, billing history (already have
  `TenantBillingHistory`).
- "Set up auto-pay" / "Update payment method" / "Cancel" actions.
- Trial banner with days-left + CTA when no mandate is active.
- Super-admin: see mandate status per tenant, retry/cancel, comp/extend (extends `SuperAdminService`).

## 6. Edge cases to cover
- Idempotent webhooks (already dedupe on payment id — keep it for `subscription.charged`).
- Mandate authorization abandoned (merchant starts, doesn't finish) → subscription stays `pending`; keep
  nudging; trial-end still suspends.
- Refunds / partial month → out of scope v1 (manual via Razorpay dashboard + `RefundAsync`).
- Currency: **INR only** (Razorpay India). Multi-currency billing is the [internationalization plan](internationalization-plan.md), not this one.
- Failed pre-debit notification / RBI edge states → surfaced via webhook status, shown in `/admin/billing`.
- AFA cap (₹15k): plan fees are below it; if a future enterprise plan exceeds it, that cycle needs step-up
  auth — flag then, not now.

## 7. Phasing
1. **P1 — Trial seam + reminders** (tiny, high value): populate `CurrentPeriodEnd` at trial start; sweep
   emits trial-ending emails + suspends on expiry. Makes trials actually end. *No gateway work.*
2. **P2 — Razorpay Subscriptions integration:** `IRecurringBillingGateway` + `RazorpaySubscriptionGateway`,
   Plan→RazorpayPlan mapping, `CreateSubscriptionAsync` + hosted mandate auth, `subscription.*` webhooks →
   `RecordChargeAsync`. Merchant "Set up auto-pay" flow.
3. **P3 — Dunning + lifecycle:** PastDue/grace/suspend driven by webhook states; dunning emails; cancel &
   plan-change flows; `/admin/billing` UI.
4. **P4 (later):** annual plans, mid-cycle proration, tax on platform invoices (ties into i18n).

## 8. Open questions
1. Card-upfront vs after-trial (recommend after-trial) — **your call.**
2. Which mandate methods to enable (cards e-mandate + UPI Autopay recommended; net-banking optional).
3. Grace period length (today defaults 3 days) and retry cadence — align with Razorpay's retry policy.
4. Do we want annual billing at launch (better cash flow + retention) or monthly-only v1?
