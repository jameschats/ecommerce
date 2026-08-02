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
  - *Known limitation, not silently glossed over*: this is a client-side `router.navigateByUrl`, not a
    true HTTP 301 from SSR (the SSR server has no response-object/redirect wiring anywhere in the
    codebase — confirmed via `ecomm.web/src/server.ts`). Search engines that crawl the *old* URL
    directly via SSR will still get a 200-with-redirect-then-render rather than a 301 status. Real fix
    needs `@angular/ssr`'s response injection token wired into `server.ts`, checked against the same
    redirect table before Angular even renders — noted as a small fast-follow, not done in this pass.
- [ ] **Guest checkout is hard-gated behind login** — `/checkout` has `canActivate: [authGuard]`; an
  anonymous shopper cannot place an order at all, despite the guest cart (X-Cart-Token) working fine
  through `/cart`. This is the single highest-value item in the whole plan. **Needs a design decision
  before starting — see "Guest checkout: the actual design fork" below.** Large.
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
