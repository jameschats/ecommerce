# Plan — Platform surfaces: apex landing/pricing + super-admin shell

## Problem
- **Apex `wavcommerce.online`** resolves to the default (seed) tenant and renders a **storefront** — wrong for a
  SaaS platform. It should be a **marketing landing + pricing** page that funnels into signup.
- **`/superadmin`** renders *inside* the storefront app shell, so it inherits the tenant's storefront chrome
  (header with search/cart/categories, footer). The super-admin already has its own "WavCommerce · Platform Admin"
  bar — it just shouldn't be wrapped in storefront chrome.
- **Root cause:** the app shell (`app.html`) shows storefront header/footer for *every* non-`/admin` route, and the
  apex host isn't distinguished from a tenant storefront.

## Goals
1. Apex = a proper **SaaS landing + pricing** page (not a storefront), with the **free trial** front and centre.
2. Super-admin = its **own platform-admin chrome**, no storefront header/footer.
3. *(Related, larger — its own track, not this plan)* strengthen super-admin **functionality**.

## Design

### Foundation — host awareness
- Extend `GET /api/tenant/host-info` → add **`hostType: 'apex' | 'store' | 'custom'`** (computed from the request
  host vs `BaseDomain`: apex = `{baseDomain}`/`www`, store = `{slug}.{baseDomain}`, custom = the tenant's domain).
- `PlatformInfoService` already caches host-info; expose `hostType`.

### App-shell chrome control
- Replace the `isAdminRoute` gate in `app.ts`/`app.html` with **`hideStorefrontChrome`** = route starts with
  `/admin` **or** `/superadmin`, **or** `hostType === 'apex'`. When true, the storefront header/announcement/footer
  are not rendered — the surface supplies its own chrome. (The storefront gate/SEO/theme load stay unaffected.)

### Part A — Apex landing + pricing (P2)
- New standalone **`LandingComponent`** (full-page, its own marketing header + footer):
  - **Header:** WavCommerce logo · nav (Features · Pricing · Log in) · **"Start free trial"** button → `/signup`.
  - **Hero:** "Launch your online store in minutes" + **"Start free — 14-day trial, no card required"** → `/signup`.
  - **How it works / features** (3–4 tiles: themes, payments, shipping, your domain).
  - **Pricing** from `GET /api/plans` — plan cards (price, limits) each with **"Start free trial"**; trial messaging
    prominent (14 days free, no card). *(This is the "we must have a trial version" requirement.)*
  - Social proof + **footer** (platform links + legal).
- **Host-aware root:** on `hostType === 'apex'` render `<app-landing>`; on store/custom render the storefront
  `<app-home>`. Implement via a small root wrapper (or a `CanMatch` on `/` that swaps) — URL stays `wavcommerce.online/`.
- Optional routes/anchors: `/pricing`, `/features`.

### Part B — Super-admin shell (P1, quick)
- Storefront chrome suppressed on `/superadmin` (via `hideStorefrontChrome`) → the existing "WavCommerce · Platform
  Admin" bar stands alone, clean.
- Give the super-admin its **own platform shell**: keep the top bar; add a light left-nav for sections
  (**Stores · Blocklist · Revenue** — the current tabs) so it reads as a console, not a page. Neutral platform
  styling (independent of the tenant theme colours).
- *(Deeper functionality — tenant detail/manage, impersonation polish, platform analytics/MRR, plan/billing
  oversight, support queue — is Part C, a separate plan.)*

## The trial (requirement)
Signup already creates a **14-day trial with no card** (`OnboardingService`, `Subscriptions`). The landing/pricing
must surface this as the primary CTA ("Start free trial"). **Gap (separate item):** converting trial → paid isn't
wired end-to-end — `SubscriptionService.SelectPlanAsync` marks the plan but doesn't create a **Razorpay
subscription + collect payment** (like Shopify's subscribe screen). Track as **Part D — billing checkout**.

## Phasing
- **P1 — Foundation + super-admin shell** ✅ `9ad83a8`. host-info returns `hostType`; app shell
  `hideStorefrontChrome` strips storefront chrome from `/admin`, `/superadmin` and the apex landing — the
  super-admin's own "Platform Admin" bar now stands alone. *(Platform left-nav for super-admin deferred into P3.)*
- **P2 — Apex landing + pricing** ✅ `9ad83a8`. `LandingComponent` at `/welcome` (self-contained marketing chrome:
  hero, features, pricing from `/api/plans`, trial-first CTAs). `apexLandingGuard` redirects apex `/` → `/welcome`;
  `welcomeGuard` keeps it apex-only. Signup honours `?plan=`.
- **P3 — Super-admin functionality** ⬜ *(large, separate):* the real platform-admin depth (+ platform left-nav).
- **P4 — Billing checkout** ⬜ *(medium, separate):* Razorpay subscription so a trial converts to paid.

## Open decisions (confirm before building)
1. Apex `/` renders the landing directly (lean: **yes**, keep the clean URL) vs redirect to `/welcome`.
2. Landing scope: one strong marketing page + pricing (lean) vs multi-page (features/pricing/about/contact).
3. Where do **merchants** log in? Today: their store subdomain (`{slug}.wavcommerce.online/login`); apex login is
   for super-admin + a "find my store" helper. Confirm we keep that split.
4. How much super-admin nav to build in P1 (just the existing Stores/Blocklist/Revenue) vs defer to P3.

## Effort
P1 ≈ half a day · P2 ≈ 1–1.5 days · P3/P4 = separate roadmap items.

## Non-goals (per earlier decisions)
- No central `admin.wavcommerce.online/store/{slug}` host (reverses ADR-001; the `platformHostGuard` already keeps
  admin off brand domains). See [url-architecture.md](../url-architecture.md).
