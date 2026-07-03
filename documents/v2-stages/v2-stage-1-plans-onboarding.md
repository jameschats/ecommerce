# V2-1 — Plans & Merchant Onboarding

**Goal:** a merchant can self-serve sign up, get an auto-provisioned subdomain + 14-day trial, and convert to a paid Razorpay subscription — the domain where **you collect money from merchants** (distinct from merchants collecting from their customers).

## Scope & checklist
- [ ] **Plans** — seed Starter / Growth / Pro / Enterprise (`MonthlyPrice`, `MaxProducts`, `MaxOrders`, `AiCredits`, `Features` JSON). See [design-v2.md §11](../design-v2.md).
- [ ] **Merchant signup** — creates a `Tenant` (Slug validated + unique), a merchant-admin `User` scoped to that tenant (per-tenant seeder, not the global V1 one), starts a 14-day trial (`TrialEndsAt`).
- [ ] **Auto-subdomain provisioning** — `slug.calendarshop.online` live immediately (wildcard DNS + Nginx; no per-tenant Nginx block).
- [ ] **Razorpay Subscriptions** — plan selection → create subscription; `TenantSubscriptions` state machine `Trial → Active → Suspended → Cancelled`.
- [ ] **Billing webhook handler** — idempotent (dedupe on `RazorpayPaymentId`/subscription id); writes `TenantBillingHistory`; advances subscription state.
- [ ] **Grace + suspension** — payment fail → 3-day grace → suspend (`SuspendedAt`, store 404s) → data retained 30 days. Timers via **Hangfire**.
- [ ] **Cancellation** — deactivate at period end; data export available 30 days (export itself is V2-7).

## Onboarding UX (the <15-min flow — [design-v2.md §15](../design-v2.md))
Signup → **pick industry template** (pre-loaded demo store) → setup checklist (first product · Razorpay connect · shipping/pincode · logo/colours · subdomain) → **one-click Launch** → shareable link + QR. India defaults pre-filled (GST on, COD on, default courier, legal pages, AI SEO meta). "Coming from Shopify/Woo? Import" entry point (import lands in V2-6).

## Data model
Migrations `110–119`: `Plans`, `TenantSubscriptions`, `TenantBillingHistory`, `TenantSettings` (per-tenant `Key`/`Value`), `TenantPaymentAccounts` (used in V2-5). Keep hand-authored entities + Fluent mapping.

## Endpoints
`POST /api/onboarding/signup`, `GET /api/plans`, `POST /api/subscriptions` (select plan), `POST /api/webhooks/razorpay-subscription` (idempotent), `GET /api/tenant/setup-status`.

## Gate
A test tenant can sign up → land on its subdomain → complete the checklist → be charged end-to-end in Razorpay test mode → `TenantBillingHistory` row written. Trial→paid and a failed-payment→suspend cycle both verified.

## Dependencies
V2-0 (tenancy). Hangfire (new). Razorpay Subscriptions keys.

**Status:** ⬜ Not started.
