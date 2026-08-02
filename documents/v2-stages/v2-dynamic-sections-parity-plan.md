# Dynamic sections parity plan — Product / Collection / Cart / Search / Collections-list

**Status: design only, not started.** Triggered by a live review of the Collection (PLP) page against
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

**Split the category-tile grid out of `CollectionHeader`.** Today it's invisible, non-removable,
non-reorderable, baked into the header component and only shown when `!activeCategory()`. Reuse the
existing `Categories` section type (already built, used elsewhere) so a merchant can add/remove/reorder/
restyle it like any other section, with the collection template pre-seeding it on install rather than
hardcoding it into the page shell.

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

Not yet audited to the same depth — `CartItems`/`CartSummary` both currently declare zero settings, so
there's nothing "dead" to find, but that likely means the page is under-featured rather than clean. Needs
its own pass before implementation: candidates to evaluate are an order-notes field, a shipping estimator,
cross-sell/upsell block in the cart (Shopify has native cart upsell blocks), and whether the coupon field's
placement/visibility should be a section setting. Scope this pass alongside Part 4 rather than guessing at
settings here without reading the cart flow as carefully as Collection was read for this plan.

## Part 4 — Search

- Ship the two missing components from Part 0 (`SearchBar`, `SearchResults`) as real, dedicated renderers.
- **No predictive/typeahead search exists at all** — confirmed via a full grep of the storefront app,
  zero matches for autocomplete/predictive/typeahead patterns. Today search is purely submit-and-load-
  results-page. Shopify's predictive search shows live product thumbnails+prices in a dropdown as the
  shopper types. This is a genuine, well-known Shopify differentiator and a real gap — sizing it as its
  own follow-up (needs a debounced lightweight search-suggest endpoint + a dropdown UI), not bundled into
  the Part 0/4 renderer fix.

## Part 5 — Collections list (build the missing page for real)

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
