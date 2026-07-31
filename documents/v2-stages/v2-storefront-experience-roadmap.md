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

**In progress**: Phase A (below).

## E3 — element-by-element editing (deferred, not detailed here)
The confirmed "next big thing" from the original `v2-theme-editor-parity-plan.md` sequence
(R1 + R2, then E3) — click any element on the storefront canvas and jump straight to editing that
field, plus the beginning of theme-blocks groundwork. Parked while this audit-driven work runs;
pick back up once the phases below are through, or sooner if priorities shift.

## R3 — roll Rich Themes to the remaining 6 (deferred until after E3)
Minimal, Ignition, Savor, Fresh, Bloom, Sprout — bring them to the same bar R2 set for
Boutique/Bazaar/Haven (distinct home structure, authored product/collection/cart, content pages,
curated imagery).

## Small follow-up from R2's research
`ProductGallery` (product), `Breadcrumbs` (collection), `EmptyState` (cart) are schema-valid section
types that silently render wrong if a theme author picks them (fall through to a generic product-rail
default). `RelatedProducts` was the fourth — fixed in Phase A. The other three are a small, contained
follow-up whenever convenient — same shape of fix as the RelatedProducts one.

---

## Phase A — Related Products fix + hover-swap image + New badge + Recently Viewed
**Status: building now.**

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
- Grid/List view toggle.
- Price-range filter (today: category/brand/search only).
- Popularity/bestseller sort exposed on the PLP sort dropdown (the sort string already exists for
  CMS ProductGrid sections, just not surfaced in the PLP's own `<select>`).

## Phase C — PDP upgrades
- Image zoom/lightbox on the product gallery (today: multi-image + thumbnails + arrows, no zoom).
- Sticky Add-to-Cart bar (stays visible on scroll).
- Delivery/pincode checker on the PDP itself (pincode logic exists today only in
  checkout/shipping-admin, not surfaced on the product page).

## Phase D — new commerce mechanics
- Compare products (nothing exists today — no service/entity/UI).
- Frequently Bought Together.

## Phase E — new theme-builder blocks
Theme-builder audit found 12 of 17 requested blocks already exist (mostly from R1). Four gaps:
- Dedicated Image Gallery block (lightbox-style; Collage/TileGrid cover mosaic-style but not this).
- Instagram Feed block.
- Custom HTML/embed block (RichText is sanitized, not raw embed — needs careful XSS scoping).
- AI Content Block (a section that self-generates/regenerates its own content — we have AI page and
  AI catalog generation already, not a section-level version of this).

## Phase F–K — from the Indian D2C site deep-dive (Snitch, Minimalist, boAt, Blue Tokai, Plum)
Most of what these sites do already maps to section types we have (trust-pillar blocks →
`Multicolumn`; shop-by-concern icon grids → `Categories`; countdown promos → `CountdownBar`; sticky
coupon banners → `Marquee`/`AnnouncementBar`). Six genuinely new patterns, roughly ordered by size:

- **F — Tabbed product grid** (boAt's "Big Deals": All/Earbuds/Smartwatches tabs switch the grid in
  place, no navigation). Smallest — a new section type + component, same shape as every block built
  this session.
- **G — Free-gift-at-spend-threshold promos** ("Free gift above ₹999"). Extends the existing coupon
  system (`CouponService`), not the theme builder.
- **H — Bundles/Combos as merchandised SKUs** (Blue Tokai, Plum, boAt: multi-product kits sold as one
  unit with combined pricing) — distinct from simple cross-sell; needs a product-bundle concept.
- **I — Subscribe & Save** (Blue Tokai: recurring delivery, customizable frequency/quantity/pack
  size) — needs new backend entities (subscriptions, recurring order generation).
- **J — Loyalty / cashback wallet** (Plum's "PlumCash", Minimalist's "MCash": points earned on
  purchase, redeemable later) — needs a wallet/ledger backend, plus checkout + account UI.
- **K — Product-finder quiz** (Plum's "Chemistry Match Maker", Blue Tokai's coffee quiz) — a
  multi-step interactive form → recommendation logic; the most novel/open-ended of the six.

Each of F–K gets its own research pass when reached — sizes vary hugely (F is a day's work, I/J are
much bigger, closer to a new subsystem each).
