# V2-5 — Per-Merchant Payments

**Goal:** each merchant collects money into **their own** Razorpay account; the platform's commission is deducted at source. Distinct from V2-1 (you billing merchants) — this is merchants billing *their* customers.

## Approach — Razorpay Route (recommended)
Single checkout; Razorpay splits funds to the merchant's linked account and deducts your platform fee at source. Avoids the "platform collects & remits" model (PA-DSS / regulatory complexity). See [design-v2.md §6.5](../design-v2.md).

## Scope & checklist
- [ ] **Merchant connect flow** — merchant links/creates their Razorpay account; store linked-account info in `TenantPaymentAccounts` (`Provider`, `AccountId`, `IsVerified`, `ConnectedAt`).
- [ ] **Route at checkout** — the V1 payment slice picks the tenant's linked account + adds the platform commission as a Route transfer; on success, funds settle to the merchant minus commission.
- [ ] **Commission config** — platform fee per plan (flat %/order or tiered); recorded per transaction for reconciliation.
- [ ] **Verification/KYC gating** — a merchant can't go live on payments until their linked account is `IsVerified`; until then, fall back to Mock (dev) or block checkout with a clear prompt.
- [ ] **Refunds** — reverse the split correctly (merchant + platform portions) via the existing refund path.
- [ ] **Reconciliation view** — per-tenant settlement + commission report (merchant admin sees theirs; super admin sees platform totals).

## Data model
`TenantPaymentAccounts` (created in V2-1 migrations, used here). Migrations `130–139` for commission config + per-transaction commission records. Reuses V1 `Payments`/`Refunds`.

## Endpoints
`POST /api/tenant/payments/connect`, `GET /api/tenant/payments/status`, checkout uses the tenant's Route account transparently; `GET /api/tenant/payments/settlements`.

## Gate
In Razorpay test mode: a customer order on tenant A splits to A's linked account with the platform commission deducted at source; refund reverses both portions; an unverified account is blocked from live checkout with a clear message.

## Dependencies
V2-0 (tenancy), V2-1 (`TenantPaymentAccounts`, plans for commission), V1 checkout/refund. Razorpay Route enabled on the platform account.

**Status:** ⬜ Not started.
