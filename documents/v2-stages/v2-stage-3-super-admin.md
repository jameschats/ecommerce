# V2-3 — Super Admin Panel

**Goal:** you, the platform owner, manage all tenants — their people, standing, plans, and platform revenue — and govern quality/trust across the platform. The **only** place cross-tenant queries are allowed.

---

## ADR-001 — Super admin lives in the same app + same API (not a separate app)  *(decided 2026-07-09)*

**Decision.** The platform/super-admin console ships as part of the **existing single API (`ecomm.api`) and single Angular app (`ecomm.web`)** — **not** as a separate `ecomm.superadmin` (or `ecomm.merchant-admin`) application. This **supersedes** the earlier draft in [design-v2.md §5](../design-v2.md) that listed `ecomm.merchant-admin/` and `ecomm.superadmin/` as separate Angular apps.

**Context.** There are three surfaces: **storefront** (per-tenant, public), **merchant admin** (per-tenant), and **platform/super admin** (cross-tenant, platform owner only). Only the last is cross-tenant, which is what tempts a separate app.

**Rationale.**
- **One backend is already correct** — the multi-tenant schema, EF Core global query filters, and the `Features/SuperAdmin` slice (with `IgnoreQueryFilters()` *confined* to that slice) all live cleanly in `ecomm.api`. Splitting the API duplicates entities/tables and invites drift.
- **A second SPA is over-engineering today** — the console is ~one dashboard; a separate app duplicates auth, the HTTP/API client, interceptors, models, theming, and the build+deploy pipeline.
- **Extraction stays cheap later** — keep the `SuperAdmin` backend slice cohesive and the `/superadmin` frontend routes isolated, so pulling it into its own app remains a lazy option if it ever grows into a product.

**The clean seam (planned refinement).** Give the console a **dedicated host** — `admin.wavcommerce.online` — that `TenantResolutionMiddleware` resolves to a **"platform context" (no tenant)** rather than a tenant:
- On the `admin.` host, the Angular app boots straight into the super-admin shell (no storefront chrome; no "Platform admin" link leaking into merchant stores).
- Super-admin endpoints require the **`SuperAdmin` role AND platform context**.
- This also sidesteps the **JWT tenant-claim/host-check conflict** — the platform host is explicitly "no tenant", so its token doesn't need to match a tenant.

**Current state.** Implemented in the same app at **`/superadmin`** (guarded by `superAdminGuard`); `admin@ecommerce.local` holds both `Admin` + `SuperAdmin` roles (migration 120). The **dedicated-host seam above is the planned refinement** — not yet built; today the console rides the apex/tenant-1 context.

## Scope & checklist

### 3a. Store directory + contacts
- [ ] **List/search/filter all stores** — by plan, status, **standing** (3c), tenure, region, health (V2-12).
- [ ] **Per-store detail with contacts** — owner + all staff users (name, email, phone, role, **last login**), subscription/billing, config snapshot.
- [ ] **Quick-contact actions** — email / WhatsApp / call, sent via the V2-11 dispatcher (great for support + engagement follow-up).
- [ ] **Export** the directory (CSV).

### 3b. Impersonation — two modes *(the "quick look")*
- [ ] **Read-only "View as store"** *(default)* — see exactly what the merchant sees, **cannot mutate**. This is the safe quick-look.
- [ ] **Full impersonation** *(escalated)* — act as the merchant to fix something; requires a reason / linked ticket.
- [ ] Both: a persistent **"Viewing as {Store}" banner**, a **short-lived time-boxed token**, and an entry in `PlatformAccessLog` (V2-10) + `AuditLogs` (who, which tenant, when, why).

> **Keep impersonation — yes.** It's the fastest way to reproduce and resolve a merchant's issue. The guardrail is: default to *read-only* view, escalate to full impersonation only with a reason, always audited and visibly banner-flagged.

### 3c. Merchant standing & governance *(track good stores · blackmark bad ones)*
- [ ] **Standing** — `Trusted / Good / Watch / Flagged / Suspended / Blacklisted`, plus free-form **tags** and append-only **internal notes**.
- [ ] **Watchlist** — borderline stores flagged for closer monitoring.
- [ ] **Blacklist / blackmark** — mark bad actors (fraud, policy violation, excessive chargebacks/refunds, non-payment); **block re-signup** via a `SignupBlocklist` (email / GSTIN / phone).
- [ ] **Governance signals (auto)** — chargeback rate, refund rate, customer complaints (tickets, V2-9), payment health, policy flags → a **suggested standing** the admin confirms.
- [ ] **Governance actions** — warn · restrict features · suspend · blacklist — every change reason-tagged and audited.

### 3d. Periodic review
- [ ] **Scheduled review cadence** (quarterly / annual) via the **V2-12 scheduler** → creates super-admin "review store" tasks.
- [ ] **Review checklist + history** — each cycle records the outcome, notes, and re-evaluated standing; **overdue-review flags** on the directory.

### 3e. Proactive assistance (platform → store)
- [ ] **Super-admin-initiated support** — open a support conversation *with* a store proactively (the reverse of V2-9's merchant-initiated tickets), e.g. after a diagnostics (V2-10) error signal or a low health score (V2-12).
- [ ] **Offer help to struggling / high-value stores** — reach out on stuck onboarding, low activity, repeated failed payments, or churn risk; sent via V2-11 (email/WhatsApp/in-app).
- [ ] **Assisted setup / concierge** — help a store configure things via read-only *View as* or (with the merchant's consent) full impersonation, guided; all audited.

### 3f. Tenant lifecycle + platform management
- [ ] **Suspend/activate**, change plan, extend trial.
- [ ] **Cross-tenant reads via explicit scope** — `IgnoreQueryFilters()` permitted **only** in `Features/SuperAdmin`, always with an explicit `WHERE TenantId = …` when targeting one tenant; every action audit-logged.
- [ ] **Plan management** — CRUD `Plans`, pricing, limits, feature flags.
- [ ] **Platform revenue dashboard** — MRR, active/trial/suspended/churned counts, churn rate, revenue by plan, new signups (unfiltered, super-admin only; reuses V1 analytics patterns).

## Security posture
- Highest-value target: separate origin, strong auth (consider **2FA**), optional IP allowlist; **every** mutating action, impersonation, standing change, and contact-view logged to `PlatformAccessLog` (V2-10) + `AuditLogs`.
- A bug leaking `IgnoreQueryFilters` into a tenant-facing path is critical — lint/guard for it.
- **Blacklisting affects real people/businesses** — require a reason + review; avoid wrongful blocks; make it reversible with an audit trail.

## Data model
Migrations `120–129`: `PlatformAdmins` (or `Users` + platform role); `MerchantStanding` (TenantId, Standing, UpdatedByAdminId, UpdatedAt) or standing fields on `Tenants`; `MerchantTags`; `MerchantNotes` (append-only, internal); `SignupBlocklist` (email/gstin/phone, hashed); `ReviewTasks`/`ReviewHistory` (TenantId, DueAt, Status, Outcome, Notes, ReviewedByAdminId); impersonation/access fields feeding `PlatformAccessLog` (V2-10). Governance signals **read** from `orders`/`payments`/`refunds`/tickets.

## Endpoints
`/api/superadmin/tenants` (GET list/search/filter, GET `/{id}` detail+contacts, `/{id}/contacts`),
`/{id}/impersonate?mode=view|full` (POST), `/{id}/standing` (PUT), `/{id}/tags`, `/{id}/notes`, `/{id}/blacklist`,
`/api/superadmin/blocklist` (CRUD), `/{id}/reviews` (GET/POST), `/api/superadmin/reviews/due`,
`/{id}` (PUT suspend|activate|plan), `/api/superadmin/plans` (CRUD), `/api/superadmin/revenue`.

## Gate
Super admin sees a searchable **store directory with per-store contacts**; can **"View as store" read-only** and escalate to **full impersonation** (both banner-flagged + audited); can set a store's **standing** to Trusted or Blacklisted, and a blacklist **blocks re-signup** with that email/GSTIN; a **quarterly review task** is created by the scheduler, completed, and recorded; governance signals surface a **suggested standing**; suspend makes a tenant's storefront + admin 404; no tenant user can reach any `/api/superadmin/*` route.

## Dependencies
V2-0 (tenancy + `IgnoreQueryFilters` policy), V2-1 (subscriptions/billing). Composes with V2-9 (complaints), V2-10 (signals + `PlatformAccessLog` + health), V2-11 (quick-contact), V2-12 (review scheduling + health score).

**Status:** ⬜ Not started.
