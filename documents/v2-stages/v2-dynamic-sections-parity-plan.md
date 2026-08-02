# Dynamic sections parity plan — Product / Collection / Cart / Search / Collections-list

**Status: Parts 2 and 5 done (2026-08-02) — Collection page rebuild + real Collections-list page.
Parts 1, 3, 4 (Product/Cart/Search) and the deferred faceted-search/predictive-search items remain
not started — held per explicit instruction to build Collection + Collections-list first, one by one.**

Triggered by a live review of the Collection (PLP) page against
Shopify, which surfaced three systemic defects that turned out not to be Collection-specific — they
affect every "dynamic" section across Product, Collection, Cart and Search, plus one entire template
(`list-collections`) that doesn't have a real page behind it at all. This plan fixes the architecture
once, then closes every page's gaps using it. No compromises: every declared setting gets wired, every
section type gets a real renderer, every selectable template actually renders somewhere, mobile gets
equal treatment to desktop.

## Root causes (found by reading code, not assumed)

**1. Declared settings are not connected to anything.** `SectionTypeRegistry.cs` declares `Settings` for
a dynamic section (e.g. `CollectionGrid`: columns/showFilters/showSort), the theme editor faithfully
renders edit controls for them (`admin-theme-editor.component.ts` doesn't special-case `Kind: "dynamic"` —
it renders `schema()?.settings` unconditionally), **but the Angular component that actually renders that
section on the storefront takes zero `@Input()`s and never reads them.** Confirmed dead across every
dynamic section checked: `CollectionGrid` (columns/showFilters/showSort), `ProductGalleryComponent`
(zoom), `ProductInfoComponent` (showSku/showShare), `ProductRelatedComponent`/
`ProductFrequentlyBoughtComponent` (heading/count). This isn't "not configurable yet" — it's actively
misleading: the editor shows a control that does nothing when changed. Only `EmptyState` is correctly
wired (confirmed from earlier work this session).

**2. Two declared section types have no renderer at all.** `SearchBar` and `SearchResults` are valid,
selectable section types (`Scope: ["search"]`) but grepping the whole storefront app for their string keys
returns nothing — `collection-page.component.ts`'s `@switch(slot.type)` only special-cases `Breadcrumbs`,
`CollectionHeader`, `CollectionGrid`; anything else (including these two) falls through to
`storefront-section.component.ts`'s generic `@default`, which renders a plain product rail. Same bug
class as the `ProductGallery`/`Breadcrumbs`(collection)/`EmptyState`(cart) fixes already shipped this
session — just two instances that weren't caught yet.

**3. One entire template has no page behind it — worse than #1 or #2.** `list-collections` (Shopify's
"All Collections" index — a grid of every collection/category the store has) is fully scaffolded in the
editor: it's a selectable template in the navigator, has its own `CollectionsList` section type with a
declared `columns` setting, an "Add section" control. But `app.routes.ts` has zero public routes for it
(the only `/collections*` paths are `admin/collections/*`, the merchant's curated-collections CRUD — a
different page entirely). The editor's own preview-path resolver admits it:
`case 'list-collections': return '/products';` in `admin-theme-editor.component.ts` — hardcoded, because
there's genuinely nowhere real to point the iframe. Confirmed via grep that zero of the 9 prebuilt themes
have ever authored a section for this template. A merchant who designs this page in the editor gets
nothing on their live storefront and no signal that anything's wrong — worse than #1/#2 because those at
least render *something*, even if wrong or inert.

**The fix for #1 and #2 is the same shape and should happen first, once, before any page-specific work**:
every dynamic section component needs to (a) actually exist and be switched-to on its page, and (b)
receive its own authored settings as a real `@Input()` it reads. Doing this as a first pass avoids
re-discovering root cause #1 five more times while going page by page.

## Part 0 — Architecture fix (do first, blocks everything else)

- Give every dynamic section component an `@Input() settings: Record<string, unknown> | null` (or a
  typed interface per section), parsed the same way static sections already parse their JSON settings
  blob in `storefront-section.component.ts`. Wire it from each page component's `@switch`, e.g.
  `<app-collection-grid [settings]="slot.data?.settingsJson" />`.
- Audit every dynamic section's declared `Settings` array against its component and wire each one for
  real (see per-page lists below — this is most of the work, not new design).
- Add real `SearchBarComponent`/`SearchResultsComponent` and switch to them on the `search` template
  (currently `CollectionGridComponent` is reused for search results via the existing search-template
  fallback logic — decide whether search gets fully dedicated components or continues reusing Collection's
  grid with a query-aware header; recommend dedicated components since search has different needs —
  query highlighting, "did you mean," no category sidebar).
- **Guardrail so this can't silently regress again**: add a small xUnit test (or a build-time check) that
  fails if a `SectionTypeSchema` entry's `Settings` keys aren't referenced anywhere in the corresponding
  Angular component file. Doesn't need to be a general-purpose static analyzer — even a hardcoded
  key-list-per-component-file test that a future contributor has to update is enough to make "I added a
  setting and forgot to wire it" a loud failure instead of a silent one.

## Part 1 — Product page (PDP)

| Section | Dead/missing today | Fix |
|---|---|---|
| `ProductGallery` | `zoom` setting declared, unused | Wire to gate lightbox/zoom-cursor behavior |
| `ProductInfo` | `showSku`/`showShare` declared, unused; trust-badge row (🚚/✅/🏷️) is **hardcoded copy**, not editable at all | Wire the two settings; add a real "trust badges" block type (icon + text, repeatable, merchant-editable) instead of hardcoded strings |
| `RelatedProducts` | `heading`/`count` declared, unused (always "You may also like" / 8) | Wire both |
| `FrequentlyBoughtTogether` | `heading`/`count` declared, unused | Wire both |
| `RecentlyViewed` | `heading`/`count` declared, unused | Wire both |
| Gallery layout | No layout options — thumbnails are always left-of-image on desktop, no mobile-carousel vs stacked choice | New setting: layout (thumbnails-left / thumbnails-bottom / carousel) |

## Part 2 — Collection page (PLP) — the main trigger for this plan
**Status: done (2026-08-02, commit `88949ba` + theme-content fix `22171e4`).**

**Split the category-tile grid out of `CollectionHeader`** into a new `CollectionCategories` dynamic
section (not a reuse of the generic static `Categories` type — that would have lost the existing
root-only-visible behavior, since `Categories` has no concept of "hide once a specific category is
active" and the `collection` template is shared by every category page). Preserves the original
conditional display while being addable/removable/reorderable/restylable via its own settings
(heading/style/columns) instead of unconditional non-editable markup.

**Real bug caught during deploy, not just design**: all 9 prebuilt themes already author their
`collection` template (from earlier R1-R3 work) with just `CollectionHeader` + `CollectionGrid` — so
moving the tiles into a new section type they'd never authored made the tiles silently disappear on
every already-installed store, including live bazaar. Fixed by inserting `CollectionCategories` into all
9 theme JSON bundles (future installs) and patching bazaar's already-live theme directly via the admin
theme API (`POST .../sections` + `PUT .../reorder` — installed themes don't re-read the bundle file).
Caught by an SSR content check against the real live page, not just a build passing.

**Wired `CollectionGrid`'s settings for real** — replaced the 3 previously-dead settings
(`columns`/`showFilters`/`showSort`) with a fuller set, all actually read by `CollectionGridComponent`
now: `columnsDesktop`/`columnsMobile` (was one fixed breakpoint ladder, not a setting at all),
`showFilters`, `showSort`, `showCategorySidebar`, `productsPerPage` (wired into `CollectionPageStore`,
read once before the first query to avoid a race against the settings load), `cardAspect`
(square/portrait — new `ProductCardComponent` input), `paginationStyle` (numbered / load-more, the
latter backed by a new `CollectionPageStore.loadMore()` that appends rather than replaces results).

**Built the mobile "Filter & Sort" drawer** — the sharpest gap from the review. The desktop category
sidebar was `hidden md:block` with zero mobile equivalent; mobile shoppers had no way to browse by
category or filter at all. New bottom-sheet drawer surfaces category list + brand + price + sort,
gated by the same `showFilters`/`showSort`/`showCategorySidebar` settings as the desktop bar.

**`CollectionHeader` gets real settings** — banner image override, overlay color, text alignment,
description override — previously zero settings, no way to author a promotional collection banner at all.

Verified live on `bazaar.wavcommerce.online`: root `/products` renders the category tiles (confirmed via
exact rendered-`<h2>`-tag match, not a naive text grep — an initial check gave a false positive matching
JSON embedded in the SSR TransferState script, not actual rendered HTML), `/category/mobile-phones`
correctly hides the tiles and renders real products (Xperia 5G etc.), the mobile "Filter & Sort" button
renders. `dotnet test` (276 tests) and the `PrebuiltThemeCatalogTests` bundle-validation suite (6 tests)
both pass after the theme JSON edits.

Deliberately not done in this pass (still open, matches the original design): full faceted filtering
with live per-option counts — flagged from the start as its own subsystem-sized follow-up, not bundled
into this build.

**Wire `CollectionGrid`'s existing settings for real** — `columns`, `showFilters`, `showSort` — then close
these gaps found by reading `CollectionGridComponent`'s actual template:

- **Columns is one fixed breakpoint ladder** (`2/3/4/5` by screen size), not a real setting. Split into
  desktop/tablet/mobile column counts, matching how Shopify's Dawn theme actually exposes this.
- **No products-per-page setting** — whatever the API default returns is what renders.
- **No card-style setting** — image aspect ratio, hover-swap-on/off, badge visibility are all global, not
  per-section.
- **Category sidebar is `hidden md:block`** — on mobile it doesn't degrade to anything, it just
  disappears. There is currently **no way to browse/filter by category on mobile at all** on this page.
  This is the single sharpest gap found in this review: real Shopify themes use a mobile "Filter & Sort"
  bottom sheet/drawer that surfaces everything (category, brand, price, sort) the desktop sidebar+bar
  shows. Needs its own component, not just a responsive tweak.
- **Filtering is 2 facets (brand, price)**, no counts per option (e.g. "Electronics (12)"), no
  multi-select, no size/color/availability facets. Shopify's real filter system is fully faceted with
  live counts. Flagging as the **largest single item in this plan** — a genuine backend + frontend
  feature (facet computation query, UI for multi-select filters), not a settings-wiring fix. Recommend
  scoping this as its own follow-up phase after the rest of this plan ships, not bundled in.
- **Pagination is numbered-only** — no "Load more" / infinite-scroll alternative, which most modern
  Shopify themes default to. Add as a per-section setting (`paginationStyle: numbered | loadMore`).
- **`CollectionHeader` has zero settings** — no way to override the banner image/description for a
  specific collection (every collection page just shows the Category entity's own name, with no
  merchant-authored promotional banner content, overlay color, or text alignment). Add real settings here
  too, not just to the grid.

## Part 3 — Cart page
**Status: done (2026-08-02, commits `b8c0cff` + `c2c3b97`).**

Confirmed `CartItems`/`CartSummary` had zero declared settings — genuinely under-featured, not dead
settings to wire. Built all four candidates: order notes (new `Cart.Notes` column — `PlaceOrderRequest.
Notes` already flowed into `Order.Notes` end-to-end, but nothing on the frontend ever captured a note at
all), a cart-page coupon field (carries the code to checkout via `?coupon=` rather than duplicating
apply logic — checkout's own field already existed and works, this just avoids retyping), a delivery
pincode estimator (reuses the existing `checkShipping` API, same pattern as the PDP's), and a new
`CartCrossSell` section ("you might also like", sourced via the existing product-listing query, no new
backend method). All three `CartSummary` additions are independently togglable via real settings;
`CartCrossSell` is opt-in, not added to the default section fallback, matching this session's "new
capability = opt-in" convention.

**Real bug caught during deploy**: migration 261 used `Carts` (plural) but `Cart.cs` maps to table `Cart`
(singular, confirmed via `EcommerceDbContext`'s `ToTable("Cart")`) — this migration skipped the local-
MySQL verification step that caught migration 260's FK bug, and paid for it (`ERROR 1146: table doesn't
exist`). Fixed the table name and added the same column-exists guard used in 260 for re-run safety,
verified against real local MySQL including a repeat run before redeploying.

**Verified live** via the real API on bazaar (not just a build passing): added an item to a guest cart,
set a note, reloaded the cart, confirmed the note persisted — proving the DB round-trip that the wrong-
table bug would otherwise have silently broken. The populated-cart UI itself (coupon field, shipping
estimator, cross-sell rail, notes textarea) is client-rendered only once a cart has items — unlike
Collection/mega-menu, cart state isn't part of the initial SSR HTML (`CartService.reload()` is browser-
only), so this piece was verified via code review + the successful build rather than a curl-fetched
render. Noting that honestly rather than overclaiming.

Built while a concurrent session was actively modifying `ecomm.api/Features/Catalog/**` and
`CollectionService.cs` for faceted search — scoped entirely away from those files/areas, confirmed via
`git diff` on every shared file (`SectionTypeRegistry.cs`) before staging.

## Part 4 — Search

- Ship the two missing components from Part 0 (`SearchBar`, `SearchResults`) as real, dedicated renderers.
- **No predictive/typeahead search exists at all** — confirmed via a full grep of the storefront app,
  zero matches for autocomplete/predictive/typeahead patterns. Today search is purely submit-and-load-
  results-page. Shopify's predictive search shows live product thumbnails+prices in a dropdown as the
  shopper types. This is a genuine, well-known Shopify differentiator and a real gap — sizing it as its
  own follow-up (needs a debounced lightweight search-suggest endpoint + a dropdown UI), not bundled into
  the Part 0/4 renderer fix.

## Part 5 — Collections list (build the missing page for real)
**Status: done (2026-08-02, commit `88949ba`).** Went with the recommended reading below (curated
Collections, not Category taxonomy) without further pushback needed.

**Open design decision first, not silently assumed**: the platform has two parallel browse concepts —
Category taxonomy (`/category/:slug`, header nav strip, the tile grid on Collection's "All products" view)
and curated merchandising Collections (`/collection/:slug`, a distinct `CollectionComponent`, already
public). Shopify unifies both under one "Collection" concept; we don't. Recommendation: `list-collections`
should enumerate curated **Collections** primarily — Categories already have two nav surfaces (header
strip + tile grid), so a dedicated index page adds the most new value surfacing Collections, which
currently have *no* discovery surface other than a direct link. Confirm this reading before building.

What's actually needed (confirmed by reading the code, not assumed complete):

- **New backend endpoint.** `CatalogService`/`catalog.service.ts` only has `getCollection(slug)` (single,
  with embedded `products[]`) and `getCollectionMembers(id, limit)` — there is no "list all public
  collections" endpoint at all. Needs a new lightweight public endpoint returning
  `{ collectionId, name, slug, imageUrl, productCount }[]` (no embedded product arrays — that would be
  wasteful for an index page listing every collection).
- **Real route + page component.** Add a public route (e.g. `/collections`) and a
  `CollectionsListPageComponent` mirroring `CollectionPageComponent`'s section-slot pattern
  (`DEFAULT_COLLECTION_SECTIONS`-style fallback layout when nothing's authored).
- **Wire `CollectionsList`'s `columns` setting for real** — same Part 0 architecture fix, applied here.
- **Fix the preview-path fallback** — `previewPath('list-collections')` should point at the new real
  route instead of the hardcoded `/products` stand-in.
- **Nav exposure.** Once real, nothing currently links to it — needs a footer/nav link option (e.g. "Shop
  all collections") or it stays reachable only by typing the URL, which defeats the SEO/discovery point of
  having it at all.
- **Content pass, scoped separately.** None of the 9 prebuilt themes author this template today; once the
  page is real, giving it authored content in the flagship themes is an R2/R3-style content pass, not part
  of the core build.

**Built**: `GET /api/catalog/collections` (new, lightweight, no embedded products — `CollectionService.
ListPublicAsync`), the `/collections` route + `CollectionsListPageComponent` + `CollectionsListGridComponent`
following the Collection page's exact section-slot pattern, `CollectionsList`'s `columns`/`heading`
settings wired for real, and the editor's `previewPath('list-collections')` fallback fixed to point at
the new real route instead of `/products`. Verified live: created a scratch curated Collection with 2
real products on bazaar, confirmed it round-trips through the public list endpoint (`productCount: 2`)
and — critically — through the actual server-rendered `/collections` HTML (not just the API response),
then deleted the scratch data.

**Not done in this pass, noted rather than silently skipped**: nav exposure (footer/menu link to
`/collections`). The page is fully real and reachable by direct link or via the Footer's existing
"Links (JSON)" editor (already supports arbitrary internal links, no code change needed to add one) —
but none of the 9 themes have been given that link by default. Content-pass work, same category as the
"give it authored content" item above — held per the instruction to keep this pass to the two pages
themselves.

Sizing: **small–medium**, not a new subsystem — the sibling single-collection page, the Category tile-grid
pattern, and the section-slot page-composition pattern all already exist. This is "finish half-built
plumbing," in the same weight class as Part 0/4, not the deferred faceted-search/predictive-search items.

## Suggested sequencing (no compromise on completeness, but sized honestly)

1. **Part 0** (architecture fix + guardrail) — small, blocks everything else, do first.
2. **Part 5** (Collections-list: new endpoint, real route/page, wire `columns`, fix preview fallback, nav
   link) — small–medium, same shape as Part 0/4's "give every section type a real home" work, natural to
   batch together.
3. **Part 1** (PDP dead-settings wiring + trust-badge block) — small.
4. **Part 2, wiring + column/products-per-page/card-style settings + CollectionHeader banner settings** —
   medium.
5. **Part 2, mobile filter drawer** — medium, high-impact (this is the sharpest real gap in the Collection
   review).
6. **Part 3** (cart audit + fixes) — size unknown until audited.
7. **Part 4, SearchBar/SearchResults renderers** — small (same shape as Part 0's guardrail fix).
8. **Deferred, sized separately, not part of this plan's initial scope**: full faceted filtering with live
   counts (Part 2's largest item), predictive/typeahead search (Part 4's largest item). Both are
   genuinely new subsystem-sized work, same category as the metafields/version-history items already
   sitting in the storefront roadmap's pending list — sequence them there, not squeezed into this pass.
