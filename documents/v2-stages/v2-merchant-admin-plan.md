# V2 — Merchant-Admin Plan (Shopify-parity gap analysis + M1–M9)

**What this is.** The detailed, phased build plan for the **merchant/store admin console** — the surface a store
owner uses to run their shop. It was produced by walking Shopify's merchant admin screen-by-screen (Areas 1–10
below), mapping each capability to our state (**HAVE / PARTIAL / MISSING**), and prioritising. Goal: simpler and
cheaper than Shopify, in **our** design language (Flipkart-like, elegant, theme-engine driven), with no critical gaps.

This plan sits **under the [V2-6 "Win-a-Merchant"](v2-stage-6-win-a-merchant.md)** umbrella and **supersedes the
thin, pre-gap-analysis checklist in [V2-2 Merchant Admin](v2-stage-2-merchant-admin.md)**. Per **ADR-001** the
merchant admin is **the same Angular app + same API** (`ecomm.web` `/admin`), not a separate app — the old V2-2
"new `ecomm.merchant-admin` app" line is obsolete.

The **storefront/theme half** (the store itself + the theme editor that builds it) is a **separate** plan:
[v2-storefront-theme-engine.md](v2-storefront-theme-engine.md) (S1–S7). The **marketing engine** (Campaigns /
Attribution / abandoned-cart / email-SMS marketing) is its own later plan.

---

## Status at a glance

| Phase | Title | Status |
|---|---|---|
| **M1** | Admin Home + Settings hub + store details | ✅ `2c0a7aa` |
| **M2** | Customers (list/profile/consent/notes/tags + prebuilt segments) | ✅ `107687b` |
| **M3** | Staff users + roles + activity log | ✅ `646d062` |
| **M4** | Payments & Shipping settings UI + Shiprocket | ✅ `b4ee312` `09ee991` `78c8107` |
| **M5** | Discounts upgrade + Draft/manual orders | ✅ `f827fcc` `3cce899` |
| **M6** | Merchandising & content (Collections, Menus/redirects, Files, product-editor adds) | ✅ `3c04f5c` `d978ab7` `b8bc403` `e2c51e4` `d8040ef` |
| **M7** | Legal/compliance + checkout/account settings + store preferences | ✅ `99151a4` `527fcba` `37cf7c5` |
| **M8** | Plan/Billing (merchant view) + Notifications sender/templates + Domains | ⬜ Not started |
| **M9** | Purchase orders *(deferred — build after M1–M8)* | ⬜ Deferred |

**Legend:** ✅ done · 🟡 in progress · ⬜ not started

---

## Shopify nav = the target surface map
Home · Orders · Products · Customers · Growth · Discounts · Content · Markets · Analytics ·
**Sales channels** (Online Store, Agentic) · **Apps** · **Settings**. Plus global chrome: ⌘K search,
notifications, store switcher, trial/subscribe banner, **Sidekick AI** side panel.

> **Design guidance (user):** take the *ideas*, not the pixels — implement in **our** design language, not a Shopify clone.

---

## AREA 1 — Store design / themes
*(Covered in depth by the separate [storefront theme-engine plan](v2-storefront-theme-engine.md). Summary here.)*
- Shopify = **theme library** (Draft/Published), **theme editor** (templates by page-type + Header/Footer/
  Announcement zones + sections/blocks), a paid **Theme Store**, and AI "generate a storefront".
- We HAVE: one per-tenant theme + a section builder for **Home + custom pages** (Storefront Builder P1–P5 ✅).
- **User decisions:** skip the paid marketplace; build **5–10 free prebuilt themes**; go deep on store structure
  (templates + section-groups + theme library). → the S1–S7 plan.

## AREA 2 — Orders
- **Orders (customer-placed)** — ✅ HAVE (list+detail, fulfillment, invoice, cancel/refund).
- **Draft / manual orders** — ✅ built in **M5b** (`3cce899`): create an order by hand (products + custom item,
  discount, shipping, pick/create customer), email invoice, collect payment, convert to order.

## AREA 3 — Products & inventory family
- **Product editor** — ✅ HAVE + **M6e** (`d8040ef`) added **Product Type, Tags, per-product SEO, Draft/Active status**.
- **Products list** — ✅ HAVE (status/inventory/category/vendor, search, Excel import/export).
- **Collections** — ✅ **M6a** (`3c04f5c`): manual **and** automated/rule-based (tag/price/category) collections.
- **Inventory** — ✅ HAVE (single-location). **Multi-location = DEFERRED.**
- **Purchase orders** — ⬜ **M9 (deferred)**. Suppliers + per-product cost exist; the PO workflow does not.
- **Transfers** — DEFERRED (needs multi-location). **Gift cards** — DEFERRED/optional.

## AREA 4 — Customers
- **Customers page** — ✅ **M2** (`107687b`): list (search/filter, spend, orders, last-order), profile (details,
  addresses, **marketing consent** email/SMS/WhatsApp, order history, notes, tags, lifetime value), Add/Import.
- **Segments** — ✅ M2 ships a **few prebuilt segments** (purchased ≥once/>once, no-purchase, email-subscribers,
  abandoned-30d) as saved filters — not a full query builder (intentional).
- **Companies / B2B** — ⬜ **OUT OF SCOPE for V2** (large feature).

## AREA 5 — Discounts
- HAVE: coupon codes (%/flat, caps, min-order, usage limits, window).
- ✅ **M5a** (`f827fcc`): upgraded to **Discounts** — **automatic** discounts + **free-shipping** + active windows.
- ✅ **M6b** (`d978ab7`): **applies-to product/collection** scoping (targeting).
- Still thin vs Shopify: **Buy X get Y**, **segment eligibility**, **discount combinations/stacking** —
  partially deferred (BXGY + stacking not built; flagged).

## AREA 6 — Content
- **Menus / Navigation** — ✅ **M6c** (`b8bc403`): editable Main/Footer/Account menus (nested) + **URL redirects**.
- **Files** (media library) — ✅ **M6d** (`e2c51e4`): central Files manager (list, upload, alt text, references).
- **Metaobjects** (custom content types) — ⬜ DEFERRED (advanced/CMS-power-user).
- **Blog posts** — ⬜ DEFERRED to a later storefront stage.

## AREA 7 — Analytics
- HAVE: solid analytics page (sales/profit/margin/best-seller/return/supplier reports + CSV) + **Umami** traffic.
- Shopify is deeper: **conversion funnel**, **cohorts/RFM**, **Live View**, self-serve **report builder**.
- Verdict: ⚠️ PARTIAL — our sales/profit analytics is competitive. **Gaps (funnel, cohorts, live view, report
  builder) = selectively later/optional.** Not a blocking M-phase.

## AREA 8 — Settings (the gear-icon hub)
A unified **Settings hub**. Sub-pages + state:
- **General / Store details** — ✅ **M1** (`2c0a7aa`): contact email/phone, address, currency display, units,
  **timezone**, **Order-ID prefix/suffix**, **order processing** (auto-fulfill/auto-archive).
- **Plan / Billing (merchant view)** — ⬜ **M8**. (Plans/subscriptions exist at the platform/super-admin layer;
  the merchant needs to see *their* plan + upcoming bill + invoices.)
- **Users / Roles / Security (staff)** — ✅ **M3** (`646d062`): multiple admins per tenant + roles + activity log.
- **Payments** — ✅ **M4a** (`b4ee312`): per-tenant provider/keys, COD, capture (`TenantPaymentAccount` wired).
- **Shipping and delivery** — ✅ **M4b/M4c** (`09ee991`/`78c8107`): zones/rates/ETA UI over existing engine +
  **Shiprocket** live rates (config-gated). *(Labels/tracking-webhooks: partial — flagged.)*
- **Taxes and duties** — ✅ HAVE (GST mode + state, single-region India). Multi-region = deferred.
- **Checkout** — ✅ **M7c** (`37cf7c5`): contact method, require-phone, **add-to-cart limit** (enforced),
  tipping toggle + presets *(exposed; not wired into the money path yet — flagged)*.
- **Customer accounts** — ✅ **M7c**: **self-serve cancellation** toggle (enforced) + "request return" affordance
  toggle *(returns workflow itself = later)*. Store credit = later.
- **Domains** — ⬜ **M8**: connect custom domain (`Tenant.CustomDomain` exists; needs merchant UI + resolution).
- **Notifications** — ⚠️ PARTIAL → **M8**: sender email + template management (email/SMS templates+history exist).
- **Policies** — ✅ **M7a** (`99151a4`): return/refund, privacy, ToS, shipping, contact, legal — storefront pages
  + footer links.
- **Customer privacy** — ⬜ cookie banner / consent — **partially in M7 scope; light-touch, flag if not done**.
- **Locations / Sales channels / POS / Metafields / Languages** — ⬜ DEFERRED / OUT.

## AREA 9 — Admin Home / setup checklist + global chrome
- ✅ **M1** (`2c0a7aa`): `/admin` is now a **dashboard** (KPIs via `AnalyticsService` + a **setup checklist**
  driven by real store state), replacing the old redirect-to-Products.
- Global chrome: notifications bell ✅ (exists). **⌘K search** = nice-to-have, later.

## AREA 10 — Growth / Markets / Catalogs / Agentic → **OUT OF SCOPE (confirmed)**
- **Growth / Campaigns / Attribution** — skip; **our own marketing engine comes later** (user decision).
- **Markets / Catalogs / Rollouts** (multi-region/currency, B2B price lists) — defer (single-region INR).
- **Agentic** (AI shopping-agent channel) — future.

### Online Store → Preferences *(merchant store settings under "Online Store")*
- **Password / pre-launch gate** — ✅ **M7b** (`527fcba`): restrict store to a password + visitor message.
- **Store-level SEO** (home title, meta description, **social-sharing image**) — ✅ **M7b**: overrides home meta via SSR.
- **Spam protection** (hCaptcha on contact/login/register) — ⬜ DEFERRED (needs a captcha account).
- **Automatic redirection / crawler signatures** — OUT / niche.

---

## What we reuse (don't rebuild)
- **Multi-tenancy**: `ITenantScoped` + auto-stamp + global query filters (`Common/Tenancy/*`, `EcommerceDbContext`).
- **Envelope/errors**: `ApiResponse<T>`, `PagedResult<T>`, `AppException`. Vertical-slice `Features/<Module>/`.
- **Existing engines surfaced via UI**: `Features/Checkout/ShippingService` + `ShippingMethod`/`ShippingZone`;
  `Features/Payments/*` (Mock/Razorpay, `TenantPaymentAccount`); `Features/Coupons/*`; `Features/Suppliers/*`;
  `Features/Notifications/*` (templates+history); tenant `Subscriptions`/`Plans` (platform layer).
- **Admin shell**: `features/admin/admin-layout.component.ts` (sidebar), `adminGuard`.

---

## The M1–M9 phases (detail)
Each phase: migration in the V2 band (≥ next free number), entities `ITenantScoped`, `dotnet build` + `dotnet test`
green, committed. Settings-only features reuse the generic `Settings` key/value table (no migration).

- **M1 — Admin Home + Settings hub + store details.** ✅ `/admin` dashboard (KPIs + setup checklist + needs-action);
  unified Settings with store details (contact, address, currency display, units, timezone, Order-ID prefix/suffix,
  order processing). `features/admin/home/*`, `features/admin/settings/*`; `Features/Settings/StoreSettingsService`.
- **M2 — Customers.** ✅ list, profile (details/addresses/consent/order-history/notes/tags/LTV), Add/Import, a few
  prebuilt segments. `Features/Customers/*`; `features/admin/customers/*`.
- **M3 — Staff users + roles.** ✅ multiple admins per tenant + roles/permissions + invite + activity log.
  Admin controllers gated; `features/admin/staff/*`.
- **M4 — Payments & Shipping + Shiprocket.** ✅ Payments UI (provider/keys/COD/capture → `TenantPaymentAccount`);
  Shipping UI (zones/rates/free-threshold/ETA) + Shiprocket live rates (config-gated). *(Labels/tracking = partial.)*
- **M5 — Discounts upgrade + Draft orders.** ✅ automatic + free-shipping discounts; draft/manual orders → invoice
  → paid → convert. *(BXGY, segment-eligibility, stacking = deferred/flagged.)*
- **M6 — Merchandising & content.** ✅ automated Collections; Menus/navigation + URL redirects; Files media library;
  product-editor adds (tags/type/SEO/draft). `Features/Collections`, `Features/Navigation`, extend `Features/Catalog`.
- **M7 — Legal/compliance + checkout/account + store preferences.** ✅ Policies + footer links (M7a); store SEO +
  pre-launch password gate (M7b); checkout settings (contact/required-fields/tipping/ATC-limit) + customer-account
  self-serve cancel/return toggles (M7c). `Features/Policies`, `Features/Storefront`, `Features/Settings`.
- **M8 — Plan/Billing + Notifications + Domains.** ⬜ Merchant-facing Plan (current plan, upgrade) + Billing
  (upcoming bill + invoices) from tenant subscription data; Notifications (sender email, template management);
  Domains (connect custom domain → `Tenant.CustomDomain` + resolution). Aligns with the V2-8 webhooks plan.
- **M9 — Purchase orders.** ⬜ DEFERRED. PO workflow (supplier→destination→receive→updates stock) over Suppliers +
  Inventory. Build only after M1–M8.

## Cross-cutting
- **Sidebar IA**: regroup admin nav into Shopify-like sections; **Home** is the `/admin` landing (done in M1).
  Optional **⌘K search** later.
- **Dependencies**: M5 segment-eligibility needs M2; M6 Menus + M7 password gate pair with the storefront theme
  plan (header/footer section-groups + password template).
- **Design**: our tokens/`btn-primary`/`.input`/theme vars; standalone Angular components; no Shopify look-copy.

## Deferred / flagged (honest scope cuts)
- Multi-location inventory + Transfers + Locations · B2B/Companies/Catalogs · Markets/multi-currency/-language ·
  Metafields/Metaobjects · Gift cards · Pixels/Customer-events · POS · Agentic · analytics report-builder/Live-View.
- **BXGY discounts, discount stacking, segment-eligibility on discounts** — not built (M5 shipped automatic +
  free-shipping + targeting only).
- **Tipping** — stored + exposed, **not** wired into order pricing/money path.
- **Self-serve returns** — a toggle + affordance only; **no returns workflow entity yet**.
- **Shiprocket** — live rates done; **label creation + tracking webhooks → order timeline = partial**.
- **Spam protection (hCaptcha)** — deferred (needs captcha account).

## Verification (per phase)
- **Unit** (`ecomm.tests`): settings/customer/discount/shipping services tenant-isolated; discount math correct;
  role checks deny cross-permission; Shiprocket rate mapping; checkout-settings validation + enforcement.
- **E2E (tenant subdomains)**: as a merchant — Home dashboard + checklist; add a customer & view order history;
  invite a staff user with a limited role; configure Payments + Shipping/Shiprocket and place a real order; create
  an automatic/free-shipping discount that applies at checkout; build a draft order → invoice → paid; create an
  automated collection + a menu that render on the storefront; publish policies + enable the password gate; a
  second tenant stays isolated throughout.
