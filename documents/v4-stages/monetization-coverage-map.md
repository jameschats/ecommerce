# Monetization & Payments — Coverage Map (what's planned, what's still to plan)

A master checklist across the whole "merchant pays the platform + platform operates across countries"
domain, so nothing falls through. Cross-references the two deep-dive plans:
[platform-billing-plan.md](platform-billing-plan.md) and [internationalization-plan.md](internationalization-plan.md).

**Legend:** ✅ built · 🟡 partially exists · 📝 planned (in a doc) · ❗ **gap — needs a plan**

---

## A. Subscription & billing

| # | Item | Status | Notes / approach |
|---|---|---|---|
| A1 | Trial → auto-debit (recurring) | ✅ | **Shipped P2** (commit 8a949d4). Razorpay Subscriptions via `IRecurringBillingGateway`; card-after-trial. Pending: enable Razorpay Subscriptions on the account + subscribe the webhook to `subscription.*`. |
| A2 | Trial auto-expiry | ✅ | **Shipped P1** (commit 408c91d) — trials expire with 7/3/1-day reminder emails; signup path already set `CurrentPeriodEnd`. |
| A3 | Plan change (upgrade/downgrade) + **proration** | 📝 | Planned — [billing-v2-pending.md](billing-v2-pending.md). Lowest value; defer until A4/A5. |
| A4 | **Add-on SKUs** (AI Text/Image/Video marketing as separate paid modules) | 📝 | Planned — [billing-v2-pending.md](billing-v2-pending.md). Largest (multi-subscription refactor). |
| A5 | Annual vs monthly billing | 📝 | Planned — [billing-v2-pending.md](billing-v2-pending.md). |
| A6 | **Coupons/discounts for subscriptions** | 📝 | Planned — [billing-v2-pending.md](billing-v2-pending.md). |
| A7 | Metered/usage overage (AI credit top-ups beyond grant; video always-metered) | 🟡 | Credit top-ups exist (one-time). No auto-overage billing. **Plan if we allow overage vs hard-cap.** |
| A8 | Pause / reactivate subscription | 🟡 | Reactivation on charge works; explicit pause not modelled. |
| A9 | Grandfathering (price change for existing subscribers) | ❗ | No plan-version pinning. **Needs a policy.** |
| A10 | Free / freemium tier | ❗ | Decision: is there a permanent free plan, or trial-only? Affects gating + dunning. |
| A11 | Dunning / retry / involuntary-churn recovery | 🟡📝 | Grace/suspend exists; retry cadence + dunning emails in the billing plan. Align with Razorpay retry policy. |

## B. Payment method, verification & security

| # | Item | Status | Notes |
|---|---|---|---|
| B1 | **Card/mandate "verification"** | 📝 | Not a separate step — the Razorpay **mandate registration is the verification** (auth txn + AFA/3DS). No PAN stored (gateway-tokenized). |
| B2 | Payment-method management (update card, expiry notice, multiple methods) | ❗ | Card-expiry → notify + re-auth mandate. **Needs a plan.** |
| B3 | **Merchant business KYC** (to accept storefront payments) | 📝 | Today merchants **bring their own Razorpay keys** (`TenantPaymentAccount`, BYO) — with BYO, KYC is Razorpay's problem, not ours. Platform-managed path planned via **Razorpay Route** (rides Razorpay's PA license; coexists with BYO): [razorpay-route-plan.md](razorpay-route-plan.md). Recommended to build **after** subscription billing. |
| B4 | PCI-DSS scope | 📝 | Stays SAQ-A (gateway-hosted fields, no PAN touches our servers). Document, don't build. |
| B5 | RBI recurring-payment compliance (e-mandate, pre-debit notice, ₹15k AFA cap) | 📝 | Delegated to Razorpay Subscriptions (billing plan §2). |
| B6 | Webhook reliability (idempotency, signature verify, retries) | 🟡 | `RecordChargeAsync` idempotent; HMAC verify exists. Extend to `subscription.*` events. |

## C. Refunds & adjustments

| # | Item | Status | Notes |
|---|---|---|---|
| C1 | Merchant → shopper order refunds | ✅ | `Refund` entity + `RefundAsync` (storefront orders). |
| C2 | **Platform → merchant subscription refunds** | ✅ | **Shipped** (commit 7c2f188). Super-admin refunds a charge (best-effort gateway refund + negative billing row, double-refund guarded). |
| C3 | **Credit notes** (for refunds/adjustments on platform invoices) | ✅ | **Shipped** (migration 286). GST credit note against the original invoice, own per-FY series, downloadable by the merchant. |
| C4 | Chargebacks / disputes | ❗ | Handling + accounting for shopper disputes and merchant-vs-platform disputes. **Needs a plan.** |

## D. Invoicing & tax (platform → merchant)

| # | Item | Status | Notes |
|---|---|---|---|
| D1 | **GST tax invoice for the SaaS fee** | ✅ | **Shipped** (commit d5177ba, migration 285). `PlatformInvoiceService` issues a GST invoice per charge (GST-inclusive back-calc, CGST/SGST vs IGST from seller-vs-merchant state), QuestPDF, downloadable in `/admin/billing`. Super-admin sets seller GSTIN. |
| D2 | Merchant GSTIN capture + (optional) validation | 🟡 | Merchant GSTIN already captured (`StoreGstin`) and printed on the invoice as buyer. GSTIN format validation not added. |
| D3 | Invoice numbering & sequencing (compliant, gap-free per FY) | ✅ | Done in D1 — per-FY global sequence, unique constraint on number. |
| D4 | Non-India tax on SaaS fee (VAT/GST/sales-tax per country) | 📝 | Covered by the [i18n plan](internationalization-plan.md) tax-provider abstraction. |

## E. Countries / regions / currency / locale

| # | Item | Status | Notes |
|---|---|---|---|
| E1 | Per-tenant Country/Currency/Locale + CountryProfile | 📝 | [i18n plan](internationalization-plan.md) §2. |
| E2 | Pluggable tax per country (GST/VAT/sales-tax) | 📝 | i18n plan §3.2. |
| E3 | Second payment gateway (Stripe) for non-India | 📝 | i18n plan §3.3. Both merchant + platform lanes. |
| E4 | Addresses / shipping / phone / messaging generalization | 📝 | i18n plan §3.4–3.5. |
| E5 | i18n formatting then translation | 📝 | i18n plan §3.6 (formatting first; translation deferred). |
| E6 | Platform billing currency for non-India merchants | ❗ | Razorpay = INR only. Non-IN merchants bill via Stripe in their currency. Ties E3 + A1. **Needs a plan.** |
| E7 | Sanctions / restricted countries / data residency | ❗ | Legal gating of which countries we onboard; EU data residency. **Needs a plan.** |

## F. Merchant payouts / settlement

| # | Item | Status | Notes |
|---|---|---|---|
| F1 | Shopper→merchant settlement | ✅ (implicit) | BYO-keys → merchant is paid directly by their own gateway; **no platform payout today.** |
| F2 | Platform-managed payouts (only if B3 chooses connected-accounts) | 📝 | With Route, **Razorpay settles to the merchant's bank directly** — we don't run payouts (that's what keeps us out of PA scope). See [razorpay-route-plan.md](razorpay-route-plan.md) §6. |

## G. Compliance & legal

| # | Item | Status | Notes |
|---|---|---|---|
| G1 | DPDP (India) — behavioural capture + customer data | 🟡❗ | Flagged in AI Commerce; consent review open. |
| G2 | GDPR (EU) — if we expand there | ❗ | Consent, erasure, residency. Pairs with E7. |
| G3 | Terms/refund/cancellation policy surfaces | ❗ | Legal copy + in-product cancellation/offboarding flow. |
| G4 | Cancellation & offboarding (data export, deletion, retention window) | 🟡 | `OffboardedAt` exists; retention/deletion policy on non-payment not defined. **Needs a plan.** |

## H. Notifications & finance ops

| # | Item | Status | Notes |
|---|---|---|---|
| H1 | Billing emails (receipt, renewal reminder, payment failed, card expiring, trial ending) | 🟡 | Notification engine exists; templates + triggers to add (billing plan §4). |
| H2 | Revenue analytics (MRR, churn, LTV, trial-conversion) in super-admin | ✅ | **Shipped** (commit eff3789). Super-admin Revenue page: MRR, ARPU, 30-day churn, collected 30/90d, ARR, status breakdown, MRR by plan. |
| H3 | Reconciliation (gateway settlements vs our records) | ❗ | Finance-ops. **Needs a plan** before scale. |
| H4 | Fraud / trial abuse (repeat trials, disposable emails) | ❗ | **Needs a plan.** |

---

## The genuinely-new items this map surfaces (not in the two plan docs yet)
Ranked by importance:

1. **D1 — GST tax invoice for the SaaS fee** (compliance-critical; we bill but don't invoice). 🔴
2. **B3 — Merchant payment onboarding model** (BYO keys vs platform-managed connected accounts + KYC) — a fork that shapes payouts (F2), refunds, and the whole marketplace posture. 🔴
3. **C2/C3 — Platform refund + credit-note policy & flow.** 🟠
4. **A4 — Add-on subscription SKUs** (the AI modules are sold separately per the pricing model but not billed separately). 🟠
5. **A3 — Proration on plan change; A5 — annual billing; A6 — subscription coupons.** 🟠
6. **H2 — SaaS revenue analytics; H4 — trial-abuse; G4 — data-retention on non-payment.** 🟡
7. **E6/E7 — non-India platform billing currency; restricted-country/residency gating.** 🟡

## Recommended decisions to lock before building
1. **Merchant payments: BYO gateway keys (today) vs platform-managed connected accounts?** (Drives B3, C4, D2, F2.)
2. **Card-upfront vs after-trial; monthly-only vs annual; freemium vs trial-only.** (A1, A5, A10.)
3. **Are AI modules add-on SKUs at launch or bundled into plan tiers?** (A4 scope.)
4. **First non-India country + build-vs-buy tax** (E-series; recommend UAE + buy US tax).
