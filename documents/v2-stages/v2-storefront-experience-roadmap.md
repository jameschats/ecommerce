# Storefront experience roadmap — PLP/PDP/theme-builder parity + beyond

Consolidated plan covering everything identified from two rounds of research this session: a
feature audit against real e-commerce sites (Snitch, Nestasia, plus a deeper pass on Minimalist,
boAt, Blue Tokai, Plum), and the standing theme-editor parity work (`v2-theme-editor-parity-plan.md`).
Executed one phase at a time — each phase is built, tested, deployed via §10-WAV, and verified live
on `bazaar.wavcommerce.online` before moving to the next.

## Status
**Done** (this session, before this roadmap doc existed): theme catalog → JSON bundles; Rich Themes
R1 (6 new section types, `pages[]` bundle field, sample-catalog nudge) + R2 (Boutique/Bazaar/Haven
re-authored with distinct structures + product/collection/cart templates + content pages); theme
editor selection-persistence bug fix; page navigator; live preview thumbnails + prominent
Published card; Hero carousel layout; category-bar real images + broadened icon fallback; a
pre-existing EF Core bug fixed (Category/Brand/Variant edits were 500ing); bazaar tenant category
data corrected.

**Done**: Phase A (RelatedProducts fix, hover-swap image, New badge, Recently Viewed — commit
`46d4de6`). Phase B (PLP grid/list toggle, price-range filter, real Popularity sort — commit
`13adef3`; also fixed a pre-existing bug where `source: bestsellers` on FeaturedProducts CMS
sections silently never sorted by sales). Phase C (PDP image lightbox, sticky Add-to-Cart bar,
pincode delivery checker via a new anonymous `GET /api/catalog/shipping/check` endpoint reusing
`IShippingService` — commit `b02e77c`). Phase D (client-side Compare list + `/compare` page;
Frequently Bought Together — a new `FrequentlyBoughtTogether` theme section ranked by real
order co-purchase frequency via a new `IProductService.GetFrequentlyBoughtTogetherAsync` — commit
`7abf3a4`; FBT ranking logic verified end-to-end against real multi-item order data on a local
scratch order, since neither the bazaar tenant nor local dev DB had any pre-existing multi-item
paid orders to exercise it against live). All deployed and live-verified on
`bazaar.wavcommerce.online`. Phase E, partial (2 of 4 theme-builder gaps — commit `5f7eadf`):
`ImageGallery` (click-to-open lightbox photo grid) and `InstagramFeed` (curated square grid +
"Follow us" link, photos uploaded not pulled live) — both deployed and live-verified via a
scratch section added to a draft theme, then removed. Custom HTML/embed and AI Content Block
deliberately deferred (see Phase E section below) rather than rushed into the same pass.

**In progress**: Phase F (below) — Phase E's two deferred items stay parked until their own
scoped passes; not blocking the rest of the roadmap.

## E3 — element-by-element editing
**Status: done.** See `v2-theme-editor-parity-plan.md` for the full E1–E6 program (all six milestones
now shipped) — E3 delivered field-level canvas selection, the sidebar tree, and focused block panels.
T17 (theme blocks — true first-class composable elements) is the remaining structural piece, tracked
as its own dedicated pass in that doc.

## R3 — roll Rich Themes to the remaining 6
**Status: done (2026-07-31, commits `df6a94b` + `9ed942b`).**
Minimal, Ignition, Savor, Fresh, Bloom, Sprout brought to the same bar R2 set for
Boutique/Bazaar/Haven — distinct home structure per theme's identity, authored product/collection/
cart templates, 2 content pages each, curated imagery (reuse-first; 4 new Savor Collage images
`curl -I` verified). Minimal was the weakest bundle going in (broken empty hero image, only 4
sections, no inner templates, no pages) and got the fullest rebuild. Live scratch-installed and
verified on `bazaar.wavcommerce.online`, including a real bug caught during that verification:
the new pages used plain slugs while R2's already used theme-prefixed ones
(`boutique-our-story`) for exactly this reason — fixed to `minimal-our-story` etc. so installing
multiple prebuilt themes on one tenant can't silently drop a page to a slug collision. All 9
prebuilt themes now have distinct home structures, authored inner templates, and real content
pages.

## Small follow-up from R2's research
**Status: done (2026-07-31, commit `17801ed`).**
`ProductGallery` (product), `Breadcrumbs` (collection), `EmptyState` (cart) were schema-valid
section types that silently rendered wrong if a theme author picked them (fell through to a generic
product-rail default, or — for cart's EmptyState — were bypassed entirely by a hardcoded message
that never even checked for an authored section). `RelatedProducts` was the fourth — fixed in Phase
A. All four now fixed, live-verified via scratch sections on the draft Ignition theme, then removed.

**Found and also fixed (2026-07-31, commit `2c03dd6`):** the `search` template key existed in the
section-type schema, but `CollectionPageComponent` always requested the `collection` template even
when serving search results (`?search=`), so a `search`-scoped section (like `EmptyState` for "no
results") could never render regardless of authoring. Now reacts to the `search` query param and
requests the `search` template, falling back to `collection` when none is authored. Live-verified: a
zero-result search rendered a scratch `EmptyState` section's custom heading, then removed.

---

## Phase A — Related Products fix + hover-swap image + New badge + Recently Viewed
**Status: done.**

- **Bug**: `RelatedProducts` is selectable in the theme editor but no code populates it — permanent
  empty placeholder for real shoppers. Fix: a new `ProductRelatedComponent` (mirrors the existing
  `ProductBreadcrumbsComponent` pattern) fetches same-category products via `ProductPageStore` +
  `CatalogService`, rendered with the existing `product-card` component.
- **Hover-swap image**: product cards show a second photo on hover, if one exists. Needs
  `SecondaryImageUrl` added to `ProductListItemDto`.
- **"New" badge**: products created in the last 30 days get a badge. Needs `CreatedAt` added to
  `ProductListItemDto`.
- **Recently Viewed**: new `RecentlyViewed` section type, placeable anywhere. Tracked client-side
  (localStorage, ~12 products, most-recent-first) via a new `RecentlyViewedService`; rendering reuses
  the existing generic product-rail mechanism in `storefront-section.component.ts`. Needs a new `Ids`
  filter on `ProductQuery`/`ProductService.BrowseAsync` (fetch specific products by id list).
- No DB migration — additive DTO fields + one new query filter only.

## Phase B — PLP upgrades
**Status: done.**
- Grid/List view toggle.
- Price-range filter (today: category/brand/search only).
- Popularity/bestseller sort exposed on the PLP sort dropdown (the sort string already exists for
  CMS ProductGrid sections, just not surfaced in the PLP's own `<select>`).

## Phase C — PDP upgrades
**Status: done.**
- Image zoom/lightbox on the product gallery (today: multi-image + thumbnails + arrows, no zoom).
- Sticky Add-to-Cart bar (stays visible on scroll).
- Delivery/pincode checker on the PDP itself (pincode logic exists today only in
  checkout/shipping-admin, not surfaced on the product page).

## Phase D — new commerce mechanics
**Status: done.**
- Compare products (nothing exists today — no service/entity/UI).
- Frequently Bought Together.

## Phase E — new theme-builder blocks
Theme-builder audit found 12 of 17 requested blocks already exist (mostly from R1). Four gaps:
- **Done**: Dedicated Image Gallery block (lightbox-style; Collage/TileGrid cover mosaic-style but
  not this) — new `ImageGallery` section type, click-to-open lightbox (prev/next, Escape/backdrop
  close), reusing the same block/settings architecture as every other section type.
- **Done**: Instagram Feed block — new `InstagramFeed` section type, a curated square photo grid
  styled like Instagram with a "Follow us" link to the merchant's real profile URL. Scoped as
  merchant-uploaded photos, not a live Instagram Graph API pull (that needs OAuth/business-account
  setup — a materially bigger, separate integration; flagged, not silently substituted).
- **Deferred**: Custom HTML/embed block (RichText is sanitized, not raw embed — a safe version needs
  a deliberate XSS-scoping pass: likely a sandboxed `<iframe srcdoc>` rather than raw `[innerHTML]`,
  since this section type would be the one place a tenant admin's input renders unsanitized on their
  own storefront — worth its own focused review rather than bundling into a features pass).
- **Deferred**: AI Content Block (a section that self-generates/regenerates its own content on
  demand — we have AI page and AI catalog generation already as precedent, but a section-level
  "regenerate via AI" control is a distinctly-sized feature: credit metering, a generation service
  call from inside the section renderer, its own settings UI — not a same-shape block like the two
  shipped above).

## Phase F–K — from the Indian D2C site deep-dive (Snitch, Minimalist, boAt, Blue Tokai, Plum)
Most of what these sites do already maps to section types we have (trust-pillar blocks →
`Multicolumn`; shop-by-concern icon grids → `Categories`; countdown promos → `CountdownBar`; sticky
coupon banners → `Marquee`/`AnnouncementBar`). Six genuinely new patterns, roughly ordered by size:

- **F — Tabbed product grid** — **done (2026-07-31, commit `40389e1`).** New `TabbedProductGrid`
  section type (up to 6 tabs, each its own source/category/count); clicking a tab fetches and swaps
  its products in place via the existing `CatalogService`, no navigation. Live-verified: added a
  2-tab scratch section to the draft Ignition theme, confirmed the heading, both tab labels, and the
  first tab's real product ("Xperia 5G") rendered server-side, then removed it.
- **G — Free-gift-at-spend-threshold promos** — **done (2026-08-01, commit `8b8a520`).** `Coupon`
  gets an optional `GiftProductId`/`GiftVariantId` reward, independent of discount/free-shipping (an
  Automatic coupon with `MinOrderAmount` + a gift and no discount value gives "spend ₹999, get a free
  gift"). The gift becomes a real zero-price `OrderItem` (flagged `IsFreeGift`) at order placement —
  both customer checkout (`OrderService.PlaceOrderAsync`) and admin phone/manual orders
  (`DraftOrderService`) — reserved/committed through the same inventory pipeline as any other line, so
  payment confirmation, cancellation, invoicing and admin order views all handle it for free by
  iterating `OrderItems` generically; nothing needed a special case. An out-of-stock gift never blocks
  the sale — it's silently dropped. Admin coupon form gets a gift-product picker (reusing the existing
  product-search pattern); checkout page shows a "you're getting a free X" banner; order detail/admin
  orders/invoices label the line FREE GIFT. Verified two ways: 3 scratch xUnit tests against the
  in-memory DB (gift attaches, stock reserves+commits generically through `DraftOrderService.ConvertAsync`,
  out-of-stock gift is dropped without failing the order) — deleted after passing, not part of the
  permanent suite — plus a live scratch coupon created/verified/deleted via the real admin API on
  `bazaar.wavcommerce.online` confirming the gift product name resolves end-to-end through production.
- **H — Bundles/Combos as merchandised SKUs** — **done (2026-08-01, commits `a31644a` + `fa32aee`).**
  A bundle is just a `Product` (`IsBundle=true`) — reuses the whole catalog/PDP/cart/order pipeline for
  free (own price, images, slug, description). A new `BundleItems` table (fixed composition, decided via
  AskUserQuestion over the configurable-per-slot alternative — simpler entity, matches the cited
  examples, extensible later without a rewrite) holds the real products+quantities it's made of; the
  bundle holds no inventory of its own. New `IBundleService.ExpandForInventoryAsync` resolves what an
  order line actually needs — its own product, or (for a bundle) each component scaled by quantity sold
  — and every checkout call site (`PlaceOrderAsync`'s reserve/COD-commit, `ConfirmPaymentAsync`'s commit,
  `CancelOrderAsync`'s release/restock, `DraftOrderService.ConvertAsync`) now expands through it. The
  order/cart line itself stays one merchandised entry ("Starter Kit × 1") — components never become
  separate `OrderItems`, so invoicing/admin views needed no special-casing. Availability is derived, not
  stored: tightest `floor(component.available / component.qtyNeeded)` across all components; `CartService`
  and the checkout quote both use it so a shopper can't add more kits than the scarcest component
  supports. Admin: existing product form gets a "This is a bundle/kit" checkbox + component picker
  (reused product-search pattern). Storefront: PDP shows a "This kit includes" list. No bundle-of-bundles
  (components can't themselves be bundles) — keeps inventory expansion a single flat pass. Verified via
  3 scratch xUnit tests (availability math, generic reserve+commit fan-out, clean failure when a
  component can't cover the requested quantity — deleted after passing) plus a live scratch bundle
  product created/populated/verified/deleted on `bazaar.wavcommerce.online`, confirming the real PDP
  (`/product/:slug`) server-renders the components list with real product data. **Bug caught and fixed
  during deploy**: the original migration used a signed `BIGINT` for the `BundleItems` FK columns, but
  `Products.ProductId` is `BIGINT UNSIGNED` — MySQL rejected the FK (error 3780) after the column-add
  half of the script had already applied on the VPS. Fixed to `BIGINT UNSIGNED` and made the column-add
  safely re-runnable; only ever caught because deploys apply against real MySQL, not the EF Core
  in-memory provider the test suite and scratch tests use.
- **I — Subscribe & Save** (Blue Tokai: recurring delivery, customizable frequency/quantity/pack
  size) — needs new backend entities (subscriptions, recurring order generation).
- **J — Loyalty / cashback wallet** (Plum's "PlumCash", Minimalist's "MCash": points earned on
  purchase, redeemable later) — needs a wallet/ledger backend, plus checkout + account UI.
- **K — Product-finder quiz** (Plum's "Chemistry Match Maker", Blue Tokai's coffee quiz) — a
  multi-step interactive form → recommendation logic; the most novel/open-ended of the six.

Each of F–K gets its own research pass when reached — sizes vary hugely (F is a day's work, I/J are
much bigger, closer to a new subsystem each).
