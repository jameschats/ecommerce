# Product Listing Page — faceted search & UX overhaul

**Goal:** turn the collection/products page from "a grid with a brand dropdown and a price box" into a real faceted storefront — filter by attributes, colour, size, price, rating, stock and sale; see counts; combine filters; find products fast on any device.

> **UX is the point.** Every filter is multi-select where it should be, every applied filter is a removable chip, the whole state lives in the URL (shareable, back-button-safe), and it collapses to a clean drawer on mobile. No dead ends, no "0 results" without a way back.

---

## Why now

The catalog already holds everything a good PLP needs — filterable attributes (`AttributeDefinition.IsFilterable`), variant colours/sizes (`VariantOption`), per-product stock (`Inventory`), approved reviews (`Review`) — but the storefront exposes **none of it as filters**. Today's PLP offers one brand, one price range, and a sort. A shopper on a 500-product store can't narrow by "Silk, blue, under ₹2000, in stock, 4★+". That's the gap.

Grounded in the code map: the filter UI lives in `CollectionGridComponent` (`collection-sections.component.ts`), data flows through `CollectionPageStore` → `CatalogService.getProducts` → `GET /api/catalog/products` → `ProductService.BrowseAsync`. The theme-editor agent works in different files (`storefront-section.component.ts`, `admin-theme-editor.component.ts`), so this overlaps almost nowhere.

---

## The two facet sources (critical design fact)

Filters come from **two different tables**, and the builder must merge them:

- **Product attributes** → `ProductAttributeValue` (a value is either a predefined `AttributeValueId` *or* free-text `ValueText`), shown only when `AttributeDefinition.IsFilterable && IsActive`. e.g. Fabric, Occasion, Material.
- **Colour & size** → `VariantOption` (`OptionName`/`OptionValue`, free text, US spelling "Color"). *Not* attributes. Colour additionally maps to a hex swatch via `GET /api/catalog/color-swatches`.

Everything else (brand, price, rating, stock, sale) is on the product/inventory/review tables directly.

---

## What a shopper gets (feature list — no compromise)

- **Facet rail** (desktop left, mobile drawer): Brand · Price · Colour (swatches) · Size · every filterable Attribute · Rating (4★+/3★+…) · In stock · On sale
- **Multi-select within a facet** (OR): "Silk **or** Cotton"; **AND across facets**
- **Live counts** next to every value ("Silk (12)"), computed with per-facet exclusion so multi-select stays usable
- **Active-filter chips** — each removable, plus "Clear all"
- **Price** as a range with sensible presets *and* min/max entry
- **Result count** always visible; a friendly empty state with "clear filters"
- **URL-synced** — every filter is a query param, so results are shareable, bookmarkable, and back-button-correct
- **Product card**: rating stars + count, compare-at price with **% off** badge, wishlist, stock state (low/out)
- **Search typeahead** — product names + categories + popular searches as you type
- **Category subtree** — filtering a parent category includes its children
- **Fix**: category tiles with no image render a coloured initial tile, not an empty grey box

---

## Backend

### B1 — Extend `ProductQuery` + `BrowseAsync`
Add params: `BrandIds[]` (multi), `Colors[]`, `Sizes[]`, `Attrs` (list of `code:value`), `InStock`, `OnSale`, `MinRating`, and sorts `rating`, `discount`. Keep the existing single `BrandId` working.
- Attrs: filter `ProductAttributeValue` where the attribute code matches and (`Value.Value` or `ValueText`) ∈ requested — multi-select = OR within a code, AND across codes.
- Colours/sizes: filter on `Variants.Options` (`OptionName == "Color"/"Size"`, value ∈ requested).
- InStock: `p.InventoryRecords.Sum(AvailableQty) > 0`.
- OnSale: `p.CompareAtPrice > p.Price`.
- MinRating: needs the rating aggregate (B2).
- **Category subtree**: expand `CategoryId` to itself + descendants (walk the category tree once).

### B2 — Rating on the card
Add `Rating` (avg) + `ReviewCount` to `ProductListItemDto`, computed as a batched group over approved `Review` rows joined into the list projection. Enables card display, the rating filter, and the rating sort — all from one aggregate.

### B3 — Facet endpoint
`GET /api/catalog/facets` taking the **same** query params as the product list. Returns, for the current filtered set:
- `brands[]` {id, name, count}, `colors[]` {value, hex, count}, `sizes[]` {value, count},
- `attributes[]` {code, name, values[{value, count}]} (filterable only),
- `priceMin`/`priceMax`, `ratingCounts` (4★+/3★+/…), `inStockCount`, `onSaleCount`, `total`.
- **Per-facet exclusion**: each multi-select facet's counts are computed with *that* facet's own selection removed, so choosing "Silk" doesn't zero "Cotton". Output-cached like the product list.

### B4 — Search suggestions
Upgrade `SearchService.SuggestAsync` (today: product-name LIKE, top 8) to also surface matching **category** and **brand** names and blend in **popular searches**, returning typed suggestions {type, label, url}. Endpoint stays `GET /api/catalog/suggest?q=`.

---

## Frontend (all in `collection-sections.component.ts` + `collection-page.store.ts` + card)

### F1 — Store: facet state, URL sync
`CollectionPageStore` reads/writes the new params (multi-value) to the URL, calls the facet endpoint alongside the product list, exposes `facets`, `activeFilters`, `resultCount`. All filter mutations navigate (merge query params, reset page) — same pattern as today, extended to arrays.

### F2 — Filter rail + mobile drawer
Rebuild the filter UI in `CollectionGridComponent`: a facet rail (desktop) and the existing mobile drawer, both rendering facet groups dynamically from `facets`. Colour swatches use the swatch lookup. Collapsible groups; "show more" for long value lists. Rendered when the existing `showFilters` setting is on — **no new theme-editor knob**, so `SectionTypeRegistry.cs` stays untouched.

### F3 — Active-filter chips + result count + empty state
A chip row above the grid: one chip per applied value, removable; "Clear all"; live "N products". A friendly empty state when a filter combo returns nothing, with a one-click clear.

### F4 — Product card
`product-card.component.ts`: rating stars + count, compare-at with **% off** badge, out-of-stock / low-stock treatment. (Shared component — check the other agent isn't mid-edit; small additive change.)

### F5 — Search typeahead
Dropdown under the search box driven by `/suggest`, keyboard-navigable, grouped by type (products/categories/popular).

### F6 — Empty category tile
`CollectionCategoriesComponent`: when `imageUrl` is falsy, render a coloured tile with the category's initial instead of an empty grey box.

---

## Sequencing & collision policy

1. **Backend first** (B1–B4) — no storefront overlap, fully verifiable via curl. Commit.
2. **Frontend** (F1–F6) — concentrated in `collection-sections.component.ts` + `collection-page.store.ts` + `catalog.model.ts`/`catalog.service.ts`. Commit.
3. **Avoid** `storefront-section.component.ts`, `admin-theme-editor.component.ts`, `theme-authoring.service.ts` (other agent). **Do not add a section type** (keeps `SectionTypeRegistry.cs` + the page switch out of scope). `product-card.component.ts` is shared — additive only, coordinate if touched.

---

## Verification

- **Backend**: seed a product with attributes + variant colours + reviews + stock; curl the product list with each new filter and combos; curl `/facets` and confirm counts and per-facet exclusion; confirm rating on the card DTO; confirm category-subtree filtering. Unit tests for the query builder + facet counts.
- **Frontend**: on a real store — filter by attribute + colour + price + rating together, confirm chips, counts, URL params, back button, mobile drawer, typeahead, and the empty-tile fix. Verify against the running app.

---

**Status:** ⬜ Plan written. Backend next.
