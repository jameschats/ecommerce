# Shopify-parity gaps — execution plan

Source: the round-2 audit in `v2-storefront-experience-roadmap.md` (six parallel code-grounded
research passes, 2026-08-02). This doc turns that audit into an ordered, sized, trackable backlog.
Executed one phase at a time — build, test, deploy via §10-WAV, verify live on
`bazaar.wavcommerce.online`, then mark done and move on, same discipline as every prior phase this
project.

No item here is dropped or waved away — anything not being built immediately is explicitly deferred
with a reason, not silently skipped.

## Phase 0 — Broken, not missing (highest priority: actively costs conversions/trust today)

- [x] **Dead URL-redirect wiring** — `POST /api/admin/navigation/redirects` (admin CRUD) was fully
  built but the storefront never called `GET /api/catalog/navigation/redirect`. Fixed: both the
  generic wildcard 404 (`NotFoundComponent`) and the PDP's own inline not-found path
  (`ProductPageStore.load`, which never falls through to the wildcard route since `/product/:slug` is
  a matched route) now check for a redirect before showing "not found," navigating there instead when
  one exists. **Status: done (2026-08-02).**
  - *Verified live, and better than expected*: assumed going in that a client-side `router.navigateByUrl`
    wouldn't produce a real HTTP redirect during SSR (no explicit response-object wiring exists in
    `ecomm.web/src/server.ts`) — that assumption was wrong. `@angular/ssr`'s Express server
    automatically converts a `navigateByUrl` triggered during initial render into a real HTTP redirect:
    `curl -sI https://bazaar.wavcommerce.online/old-clearance-sale` → `HTTP/1.1 302 Found`,
    `location: /products`. Correcting the record here rather than leaving the wrong caveat in place.
    **One real remaining gap**: it's a **302 (temporary)**, not a 301 — for a genuinely permanent
    redirect (discontinued product merged into its replacement, renamed collection) a 301 is the
    correct SEO signal so crawlers consolidate link equity onto the new URL instead of continuing to
    index the old one. Getting a 301 specifically needs explicit `server.ts` customization (a
    `RESPONSE`/request-response token, checked against the redirect table before Angular bootstraps,
    setting the status explicitly) since `Router.navigateByUrl` has no per-call status-code option.
    Small fast-follow, not done in this pass.
- [x] **Guest checkout is hard-gated behind login** — `/checkout` had `canActivate: [authGuard]`; an
  anonymous shopper could not place an order at all, despite the guest cart (X-Cart-Token) working fine
  through `/cart`. **Status: done (2026-08-02, commits `15efeae`, `67b6eb0`), option 1 (silent
  passwordless-account provisioning) as agreed.**
  - Backend: `AuthService.GuestCheckoutAsync` — new/reused passwordless `User` (a prior guest checkout,
    or any OTP/Google-only signup, is safe to reuse; a REAL password-protected account with the same
    email is a hard 409 asking them to sign in instead, not silently attached to). New
    `POST /api/auth/guest-checkout`. Also fixed `RequestPasswordResetAsync`, which previously silently
    skipped every passwordless account — the only way a guest-checkout account could ever gain a real
    password was through that exact flow, so excluding it was a dead end.
  - Frontend: `checkout.component.ts` branches on `auth.isAuthenticated()` — logged-in shoppers see the
    unchanged saved-address picker; guests see an inline address form. Submitting it calls
    `guestCheckout()` (logs them in via the same session path as `login()`/`register()`, so
    `CartService`'s existing login-triggered merge effect picks up the guest cart automatically — zero
    new code needed there) then reuses the ordinary `POST /account/addresses` endpoint — after that,
    quote/place/pay/order-confirmation is the exact same authenticated flow every shopper already uses.
    `authGuard` removed from `/checkout`.
  - **Live-verified on bazaar via a full API round-trip** (not just unit-level): guest cart → add item →
    `guest-checkout` (fresh email) → `cart/merge` confirmed the guest cart items landed in the new
    account's cart → `account/addresses` → `orders/quote` (serviceable, COD enabled) →
    `POST /api/orders` placed a real COD order (`ORD20260802-00011`) → order detail fetch confirmed
    "Confirmed" status. Re-running `guest-checkout` with the same email reused the same `userId` (no
    duplicate account). An email with a real password correctly 409'd instead of silently attaching an
    order to someone else's account. Confirmed `/checkout` no longer redirects anonymous visitors to
    `/login` (still returns the page, not a 302).
  - **A real bug was caught and fixed during this verification**: `Users.PhoneNumber` has a per-tenant
    unique constraint (`uq_users_tenant_phone`, backing OTP login) that the first version didn't account
    for — a phone number already tied to a different account threw a raw, unhandled `DbUpdateException`
    (500) instead of a clean response. Fixed by checking availability first and simply leaving the
    phone unset on collision rather than failing checkout over it (the real shipping contact number
    lives on the `CustomerAddress` created right after, which has no such constraint, so nothing is
    functionally lost).
  - **Deploy incident, noted rather than glossed over**: the phone-collision fix's redeploy skipped the
    `chown -R www-data:www-data` step (wrongly assumed a backend-only change didn't need the full
    sequence) — the freshly-published DLL was root-owned, `wavcomm-api` crash-looped
    (`FileLoadException: Access is denied`) for roughly one minute in a restart loop before the mistake
    was caught and fixed. Both `wavcomm-api`/`wavcomm-ssr` confirmed healthy afterward. Root cause: an
    unwarranted shortcut on a step §10-WAV's redeploy recipe always includes — no shortcuts on the
    ownership/restart sequence going forward, regardless of how small the change looks.
  - **Separate, pre-existing bug discovered as a side effect of this verification, explicitly NOT fixed
    here** (out of scope — not part of guest checkout, affects the whole registration system): every
    account created on bazaar's tenant — via normal `/api/auth/register` too, not just guest checkout —
    comes back with an empty `roles` array in its JWT. `AssignRoleAsync` silently no-ops when the
    tenant has no seeded "CUSTOMER" role row, and bazaar appears to be missing one. Confirmed by
    registering a normal test account directly (`roles: []` there too). Practical impact: these
    accounts don't show up in the admin **Customers** list (`CustomerAdminService` appears to filter by
    CUSTOMER-role membership), though the accounts themselves, their carts, addresses, and orders all
    function correctly regardless. **Flagging as its own separate bug, not folded into this item.**
  - Scratch test accounts/order left on bazaar (a disposable test/SIT tenant, not production) since the
    admin Customers endpoint can't see them to delete them (the bug above) — harmless.
- [ ] **Newsletter signup has no email capture** — `CtaNewsletter` section is a promo banner with a
  button, no email input, no subscriber table anywhere. Small-medium: add an email input + submit to a
  new lightweight `NewsletterSubscriber` table + admin list/export (reuse the existing
  `AcceptsEmailMarketing` flag pattern on `Customer` where the email matches an existing account).
- [ ] **Abandoned-cart recovery has the data, zero automation** — `Cart.Abandoned` status + an admin
  report already exist; no job ever sends a recovery email. Medium: a scheduled `IHostedService` (same
  shape as the existing `SubscriptionLifecycleService`) that finds carts abandoned >N hours, sends one
  recovery email via the existing `Email:Provider` templating system, respects `AcceptsEmailMarketing`.
- [ ] **"Logo upload" is a URL-paste text field** — not wired to the `MediaPickerComponent` already
  used elsewhere in the editor. Small: swap the theme-settings Logo/Favicon inputs for the picker.

## Guest checkout: the actual design fork
**Resolved: option 1, built and shipped — see the Phase 0 entry above.** Kept below for the record of
why, since it was a real architectural decision, not a coin flip.

Investigated `checkout.component.ts` in full: it's built entirely around an authenticated user's
*saved* `CustomerAddress` rows (`account.listAddresses()` → pick one by id → `orders.quote(addressId,
...)` → `orders.place(addressId, ...)`) — there's no inline "type your address" form anywhere, and
order confirmation navigates to `/account/orders/:id` (also behind `authGuard`). Making this work for
a real guest means picking one of two real approaches, not a small tweak:

1. **Silent account creation** — on first guest checkout, auto-create a real `Customer`/`User` row
   from the email they type (random unusable password, no login prompt), so the *existing*
   addressId-based `OrderService` surface works completely untouched. Smaller backend surface, but the
   shopper technically "has an account" they never chose to create — needs a clear "we've saved your
   order — set a password to track it anytime" moment rather than pretending nothing happened.
2. **True sessionless guest order** — `OrderService.QuoteAsync`/`PlaceOrderAsync` gain an overload
   taking a raw address (line1/city/state/pincode/etc., not an id), `Order` gets nullable `UserId` +
   `GuestEmail`/`GuestPhone`, and order lookup after payment works via a signed link/order-number +
   email rather than the account system. More surface area (new order-lookup page, checkout needs a
   real inline address form since there's no saved-address list to show), but matches how Shopify/most
   real stores actually behave and doesn't quietly enrol every guest as a "customer."

Recommendation: **option 1** first — it's the smaller, faster, lower-risk path to closing the actual
bug (can't check out at all), reuses 100% of the existing address/order/Razorpay plumbing, and doesn't
preclude layering option 2's nicer UX later. Flagging this rather than silently picking, since it's a
real architectural fork touching the Order/Customer data model — a hard-to-reverse call worth a second
opinion before the first line of code.

## Phase 1 — Quick wins (all small, batch together, no design decisions needed)

- [ ] **"Buy now" doesn't skip the cart** — `ProductPageStore.buyNow()` just adds to cart and redirects
  to `/cart`, identical to Add to cart. Make it carry a single-item intent straight to `/checkout`
  (reuse the checkout page's existing quote/place flow with a `?buyNow=productId&qty=N` param the store
  reads once, bypassing the multi-item cart for that pass).
- [ ] **Color-scheme `button`/`border` tokens are dead** — a scheme has 4 editable fields but
  `theme.service.ts`'s `resolveBg()`/`resolveText()` only ever read `background`/`text`. Wire the other
  two into the button/border-rendering call sites that already read `colorScheme`.
- [ ] **Collection pages emit no JSON-LD** — PDP/home/FAQ all call `SeoService.setJsonLd()`, collection
  pages never do. Add `ItemList` + `BreadcrumbList` to `CollectionPageStore`, same pattern as PDP.
- [ ] **No per-page `noindex` control** — add a `robots` field to `SeoData`/`SeoService.setMeta()`, used
  at minimum on the search-with-zero-results and heavily-filtered PLP states.
- [ ] **No Google Fonts preconnect hint** — add `<link rel="preconnect">` for
  `fonts.googleapis.com`/`fonts.gstatic.com` to `index.html`.
- [ ] **Hero/banner/promo image alt text has no fallback** — falls back to `''` when a merchant leaves
  a block's label/caption blank (PDP already falls back to product name). Fall back to the section's
  own heading or a generic "Store image" rather than empty.
- [ ] **Add-section picker is a flat uncategorized list** — group the ~33 section types the same way
  Shopify does (Product / Promotional / Content / Layout), matching the template picker's existing
  `TemplateGroup` pattern.
- [ ] **Device preview has no tablet breakpoint** — desktop/mobile only; add a third toggle state.

## Phase 2 — Cart & checkout UX

- [ ] **Cart drawer / mini-cart** — no flyout exists anywhere; every add-to-cart is a full nav to
  `/cart` or a static "✓ Added" message on the PDP. Medium: a slide-over component + a global
  open-state signal wired into `CartService`, triggered on add.
- [ ] **Checkout branding** — checkout reads zero theme state (only `storeName()` for the Razorpay
  modal title) despite the rest of the site being fully theme-driven. Medium: pull logo + primary
  color scheme into the checkout layout.
- [ ] **Checkout upsell/cross-sell** — cart page has a cross-sell rail, checkout doesn't. Medium: reuse
  the same `CartCrossSellComponent`-style pattern in the checkout summary column.

## Phase 3 — Customer retention & notifications (share underlying trigger infra — batch these)

- [ ] **Back-in-stock notifications** — no opt-in table, no trigger on inventory increase from 0.
  Small-medium.
- [ ] **Wishlist price-drop / restock alerts** — wishlist is pure CRUD today, no price-history or
  inventory-change hook. Small, builds directly on the back-in-stock trigger above.
- [ ] **True personalized "for you" rail** — `RecentlyViewed` is browser-local only, `RelatedProducts`
  is same-category-only; nothing surfaces a shopper's own purchase-history-driven picks. Medium.

## Phase 4 — Theme/store-builder depth

- [ ] **Footer "socials" don't exist** — despite the section's own description claiming them; only
  generic link columns exist. Small-medium: a dedicated social-icon-row block type.
- [ ] **No per-section/block mobile-only or desktop-only visibility** — `isVisible` is a flat bool, no
  breakpoint targeting. Medium.
- [ ] **No copy-section-to-another-page/theme** — only same-template duplicate and whole-theme
  duplicate exist. Medium.
- [ ] **No themeable icon system** — `icon` field type is free-text emoji; chrome icons are hardcoded
  inline SVG per component. Large, and genuinely low priority — deprioritized to last in this phase.

## Phase 5 — Large subsystems (each deserves its own scoped sub-plan; tackle last, one at a time)

- [ ] **Size charts** — a boutique theme's FAQ copy literally references a size chart that doesn't
  exist. Medium.
- [ ] **Customer-segment-targeted content/coupons** — segments exist only as an admin customer-list
  filter, never as an eligibility condition for a banner/section/discount. Medium-large.
- [ ] **Gift cards** — no product type, no stored-value ledger, no checkout redemption. Large.
- [ ] **Blog/articles engine** — zero entities/controllers/routes; a genuine content-marketing/SEO
  subsystem. Large.
- [ ] **Per-product/collection alternate templates** — one universal `product` template for every
  product, no per-item override. Large.
- [ ] **B2B/tiered customer pricing** — one price for every shopper, no customer groups. Large.
- [ ] **Local pickup at checkout** — delivery-only; needs a store/location model + pickup-point UI.
  Large.

## Explicitly out of scope for this plan (already decided elsewhere, not re-litigated here)
Metafields, localization/multi-currency, theme version history, persisted undo/redo, app-block
equivalent, A/B testing, Subscribe & Save, Loyalty/wallet, Product-finder quiz — all previously sized
and logged in `v2-storefront-experience-roadmap.md`'s "Pending / upcoming" list. Not duplicated here.

## Sequencing note
Phase 0 and Phase 1 are deliberately front-loaded because they're either active bugs or same-day
fixes with no design ambiguity. Guest checkout is called out on its own within Phase 0 because it's
simultaneously the highest-value item on this entire list *and* the one genuinely blocked on a design
decision — everything else in Phase 0/1 can proceed without waiting on it.
