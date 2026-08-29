# Plan — Razorpay Route (platform-managed merchant payments)

**Goal:** offer merchants a **platform-managed** payment option — they onboard + KYC *inside our product*,
we take a transaction commission, and money still settles to *their* bank — **without us needing our own
Payment Aggregator license** (we ride Razorpay's, via Route). This coexists with today's BYO-keys model as a
second option, and is the same split-settlement foundation the **V3 marketplace** will need.

**Sequencing recommendation (read first):** *plan now, build after subscription billing.* Route is
transaction-revenue (a % of merchant GMV) — near-zero until merchants have real sales — and is gated on
Razorpay approving us for Route (a slow external step). So: **(1) start the Razorpay Route approval
application now** (business, no code); **(2) keep this plan ready; (3) build [subscription billing](platform-billing-plan.md)
first** (unblocked, sustains the platform); **(4) build Route when approval lands.**

---

## 1. Business prerequisites (not code — start these now)
- **Apply for Razorpay Route** on the platform's own Razorpay account; Razorpay vets the business before enabling it.
- **Decide the commission model:** flat % of order, or %+fixed, per plan tier or global. (This is our transaction revenue.)
- **Legal:** platform↔merchant agreement covering fees, refunds, disputes, settlement timing; update T&Cs.
- **Confirm current Route terms** with Razorpay (fund-holding limits, settlement timelines, KYC requirements) — RBI norms evolve.

## 2. Design principle — coexist with BYO, don't replace it
`TenantPaymentAccount.Provider` already switches per tenant (`Mock | Razorpay`). Add **`RazorpayRoute`** as a
third mode. A tenant chooses, in settings:
- **Bring your own** (today) — their Razorpay keys, money direct to them, no commission, they KYC with Razorpay.
- **Platform (Route)** — onboard/KYC in-product, we take commission, money settles to them via Razorpay.

The gateway resolver in `Program.cs:328` gains a Route branch; everything else (checkout, orders) stays.

## 3. Data model (additive)
Extend `TenantPaymentAccount` (already has `AccountId`, `IsVerified`, `IsEnabled`):
- `RouteLinkedAccountId` — Razorpay linked-account id (`acc_...`).
- `KycStatus` — `not_started | created | under_review | needs_clarification | activated | rejected/suspended`.
- `KycDetail` — last status note from Razorpay (for the merchant to act on).
- `SettlementConfig` — Razorpay handles bank settlement to the linked account; we store display status only.
- `CommissionPercent` (nullable) — per-tenant override; else the global/plan default.

New (platform-global) commission config: a setting or a column on `Plan` for the default platform fee %.

## 4. Onboarding flow (merchant, in-product)
1. Merchant picks **Platform (Route)** in payment settings → a KYC wizard (business name, PAN, GSTIN, bank
   account, contact) — the fields Razorpay's **Accounts / Linked Accounts API** requires.
2. We call Razorpay to **create the linked account** → store `RouteLinkedAccountId`, `KycStatus=created`.
3. Razorpay reviews; we track status via **webhooks** (`account.*`) + a poll fallback. Surface
   `needs_clarification` back to the merchant with the required fix.
4. On `activated` → `IsVerified=true`; merchant can flip `IsEnabled` and start accepting payments.
   > KYC is **mandatory and enforced by Razorpay** (RBI requires the PA to KYC every merchant) — Route makes
   > it in-product and faster, not skippable.

## 5. Checkout flow (Route mode) — the real difference
Unlike BYO (order on the merchant's own account), Route mode creates the order on the **platform's** Razorpay
account **with a transfer/split** to the merchant's linked account, minus commission:
- `CreateOrderAsync` (Route variant): create order + `transfers: [{ account: linkedAccountId, amount: net,
  ... }]` where `net = order total − platform commission`. The platform fee is retained on the platform
  account; the rest routes to the merchant.
- `VerifySignature` unchanged (platform keys).
- **This is a distinct `IPaymentGateway` implementation** (`RazorpayRouteGateway`) or a Route-aware branch —
  it must not leak into the BYO path.

## 6. Refunds, disputes, payouts
- **Refunds (shopper):** refund on the platform order; Razorpay reverses the transfer proportionally
  (commission handling per Razorpay's refund-on-transfer rules). Extend `RefundAsync` for the Route case.
- **Disputes/chargebacks:** land on the platform account; define who bears them (merchant, via reserve, or
  platform) — **decision needed**, tied to the legal agreement.
- **Payouts/settlement:** **we do not run payouts** — Razorpay settles to the linked account's bank on its
  schedule. This is the whole point: we stay out of the money-holding flow, so we stay out of PA scope.

## 7. RBI / compliance posture (why this is safe)
- We ride **Razorpay's PA license** via Route → **no PA authorization / ₹15–25cr net-worth** needed for us.
- We **never take settlement funds into our own account** → not an unauthorized PA. The platform fee is a
  legitimate commission Razorpay disburses to us; merchant funds settle through Razorpay's regulated nodal
  flow, not ours.
- Every sub-merchant is KYC'd (enforced). We must respect Razorpay's fund-holding/settlement-timing rules.
- **The one red line:** never architect a path where order money lands in a platform bank account and we pay
  merchants ourselves — that would make us an unauthorized PA. Route's transfer model avoids this by design.
- *Confirm the current specifics with Razorpay + a CA/lawyer before go-live.*

## 8. Admin / super-admin UX
- **Merchant:** payment-settings mode selector (BYO vs Platform); Route KYC wizard; status + "fix this"
  prompts; "payments live" toggle; commission shown transparently.
- **Super-admin:** linked-account list + KYC status; commission config (global/plan/per-tenant); Route
  revenue reporting; ability to suspend a linked account.

## 9. Phasing (when approval is in hand)
1. **R1 — Seam + data model.** `RazorpayRoute` provider mode, `TenantPaymentAccount` fields, resolver branch,
   commission config. BYO untouched.
2. **R2 — Linked-account onboarding.** KYC wizard → create linked account → status webhooks/polling → activate.
3. **R3 — Route checkout.** `RazorpayRouteGateway` with split transfers + commission; end-to-end test order.
4. **R4 — Refunds/disputes + reporting.** Route-aware refunds; dispute policy; super-admin revenue view.
5. **V3 reuse:** the split-settlement primitive extends to multi-seller carts (one order → transfers to N
   sellers) — build R1–R3 with that generalization in mind.

## 10. Open questions / decisions
1. **Commission model** — flat %? per-tier? (Our transaction revenue.)
2. **Who bears chargebacks/disputes** in Route mode — merchant reserve vs platform absorb?
3. **Default mode for new merchants** — BYO or Platform? (Platform reduces friction + earns commission, but
   needs Route live first.)
4. **Do we let a merchant switch modes** after going live (BYO ⇄ Route), and how to migrate in-flight orders?
5. **Timing** — confirm building this *after* subscription billing (recommended) vs alongside.
