# Mega-menu / large-catalog navigation plan

**Status: design only, not started.** Triggered by hovering "Personal Care" on bazaar and finding only
a flat, undifferentiated list of children — not the multi-column, differently-grouped mega-menu real
large-catalog sites use (e.g. Minimalist's beminimalist.co: hovering "Skin & Body Care" opens five
labeled columns side by side — Shop by Concern, Shop by Ingredients, Skin Care, Body Care, Lip — each
drawing from a *different* grouping, not one category's children). No compromise on UI/UX: this plans
the real thing, not a smaller stand-in.

## How Shopify actually does this (two separate layers, not one)

It's easy to assume Shopify's nav is "just a menu with nested items," but that alone can't produce
Minimalist's menu — a raw link tree has no way to express "these three columns aren't children of a
common parent, they're three different taxonomies (concern / ingredient / product type) placed next to
each other for the shopper's convenience." Shopify actually has two distinct layers:

1. **The link tree** (Settings → Navigation): a plain nested `{title, url, children}` structure, 3
   levels deep. This alone renders as a basic dropdown/flyout in most themes — one column, one grouping.
2. **Per-item rich mega-menu content** (theme-level, in premium/OS 2.0 themes, or via an app): for any
   *specific* top-level nav item, a merchant can optionally attach a richer content block — multiple
   named columns, each independently populated (can point at collections, tags, pages, or raw links),
   plus an optional promo/image tile. This is a **separate authoring surface** from the plain link tree,
   layered on top of specific items, not a property of the whole menu.

The critical design implication: **most nav items stay simple** (plain link, or simple one-column
dropdown) and only the items that need it get the rich treatment. A large catalog doesn't mean every
nav item becomes a mega-menu — it means the *header itself* stays a small, curated set of items (never
auto-dumping all 50-100 categories into one row), and depth lives inside whichever items need it.

## Current state, precisely (from this session's investigation + the just-fixed bug)

- **Two disconnected systems today**: a `Menu` entity with a genuinely nestable model
  (`MenuItemDto{Label, Url, Children}`) that's completely unused by the storefront (`Header`'s
  `menuHandle` setting is declared, nothing reads it), and a separate hardcoded Category-only hover menu
  in `app.html` that just fixed a real CSS clipping bug (`overflow-x-auto` silently clipping the
  dropdown's vertical overflow) — it renders one flat grid of a category's direct children, no columns,
  no groupings, no non-category content.
- **The header nav bar auto-dumps every top-level Category** (`topLevelCategories()` = all categories
  with `parentCategoryId === null`, no curation, no limit, no manual ordering independent of raw
  category data). At 7 categories (bazaar today) this fits on one line. At 50-100 categories it would
  overflow the header entirely with no fallback — this alone blocks "large catalog" support regardless
  of mega-menu richness.
- **The admin menu editor exists but silently strips nesting on save** even though the backend model
  supports it — a merchant can't author a nested/grouped menu today even if they wanted to.
- **"Shop by Concern" / "Shop by Ingredients"-style cross-cutting groupings have no data-model path
  today** beyond tag-based `Collection`s (confirmed: `Category` is strict single-parent, a product can't
  live in two taxonomy branches at once; `Collection`'s automated rules support `tag`, not attribute
  values, so "Ingredient" groupings would need tagging ingredients too, not a new engineering dependency
  — just a content convention).

## Proposed data model

Two-layer, matching Shopify's actual split rather than trying to force one flat structure to do both jobs:

**Layer 1 — the curated top-level nav** (replaces "auto-dump every Category"): the existing `Menu`
entity (handle `main-menu`) becomes the real source of the header's top-level items — a merchant
explicitly picks what appears (Category link, Collection link, Page link, or a custom URL), in whatever
order and however many they want. This is a small, sharp fix on its own: **it's what actually enables
large-catalog support**, independent of mega-menu richness, since it decouples "what's in the header"
from "how many categories exist."

**Layer 2 — optional mega-menu content per top-level item**: a new structure attached to individual
`MenuItemDto` entries (not the whole menu) — something like:
```
MegaMenuColumn { Heading: string, Items: MegaMenuLink[] }
MegaMenuLink { Label: string, TargetType: "category" | "collection" | "page" | "url", TargetId/Url: string, ImageUrl?: string }
```
plus an optional `PromoTile { ImageUrl, Heading, Link }` slot (the promo/image column real mega-menus
often include — e.g. Minimalist's own top-of-page "Build Your Own Bundle" banner pattern, scaled down
to fit inside a menu panel). A top-level `MenuItemDto` with no mega-menu content attached just renders
as today's simple flyout (single column, its own `Children` if any) — **richness is opt-in per item, not
forced on every one**, matching Shopify's actual behavior and avoiding "every category now needs 5
columns filled in before the site looks finished."

## Admin authoring UX

Rebuild the (currently broken/flat-only) menu editor as a real tree/column editor:
- Top-level items: add/remove/reorder, each picks its link target the same way the Collection admin
  form already does (reused picker pattern — product/category/collection/page search, already built).
- Per top-level item, a toggle: **"Simple link"** (today's behavior, or a basic nested dropdown if it has
  `Children`) vs **"Mega menu"** — switching to mega-menu reveals a column editor: add/remove/reorder
  columns, each with a heading + add/remove/reorder link rows (same target-type picker), plus an optional
  promo tile (image picker + heading + link, reusing the media picker already built for theme sections).
- Live preview alongside the editor, matching this session's established theme-editor UX bar — not a
  blind JSON-editing experience.

## Storefront rendering

**Desktop**: hovering a top-level item with mega-menu content shows the existing dropdown panel design
(rounded card, shadow) but laid out as `grid-flow-col` columns, each with its own heading, plus the
promo tile as a visually distinct final column (image + heading + CTA). Items with no mega-menu content
keep today's simple single-list flyout — same component, conditional layout, not two separate
components.

**Mobile — this needs to be designed as its own pattern, not "the same panel, smaller."** Hover doesn't
exist on touch. Real large-catalog sites use a slide-in drill-down: tapping a top-level nav item (inside
the mobile hamburger menu) opens a full-screen panel listing that item's columns stacked vertically
(headings + links, no promo tile or a simplified one), with a back arrow to return to the top-level list.
This is meaningfully different UI, not a responsive CSS tweak on the desktop panel — needs its own
component built deliberately, the same way the Collection page's mobile filter drawer was built as its
own thing rather than a shrunk desktop sidebar.

## Large-catalog-specific considerations (the part of the original question beyond "mega menu")

- **Header stays curated, never auto-generated from all categories** (Layer 1 above) — this is the load-
  bearing fix for the 50-100-category case specifically; without it, richer mega-menu columns don't help
  because the header row itself breaks first.
- **A "Shop by X" cross-cutting facet is a content convention (tag-based Collections), not new
  engineering** — reuses what's already built (`Collection` automated rules on `tag`), just needs the
  merchant (or an AI-assist content pass, matching the pattern already used elsewhere in the admin) to
  tag products by concern/ingredient/etc., and the mega-menu column's "collection" target type links to
  those.
- **The just-built Collections-list page (`/collections`) becomes the large-catalog "overflow" surface**
  — a curated header can't show everything, so a real, crawlable, complete directory page matters more
  the bigger the catalog gets. This already exists and needs no further work for this plan specifically.
- **Category page itself** (already rebuilt this session — `CollectionCategories` section, mobile filter
  drawer) is the other half of this story: once a shopper lands inside a category via the mega-menu, the
  category page's own sub-category tiles + filters are what let them keep narrowing. Already done.

## Sizing and sequencing

This is subsystem-sized — same weight class as metafields/version-history already parked in the
storefront roadmap's pending list, not a quick features pass. Recommended order if/when picked up:

1. **Layer 1 alone** (curated header from `Menu` instead of auto-dumped categories) — smallest piece,
   highest leverage for the large-catalog case specifically, and unblocks everything else since Layer 2
   attaches to Layer 1's items. Ship-able independently.
2. **Admin menu editor rebuild** (real add/remove/reorder tree, target-type picker) — needed before Layer
   2 has anywhere to be authored.
3. **Layer 2 desktop rendering** (mega-menu columns + promo tile).
4. **Layer 2 mobile drill-down** (its own component, not deferred as an afterthought — matches the "no
   compromise on UI/UX" instruction this plan was requested under).
5. **Content pass**: tag-based "Shop by Concern/Ingredient" Collections + wiring them into a real menu on
   at least one flagship theme, to prove the whole thing end-to-end the way R2/R3 proved Rich Themes.

Not included in this plan, flagged rather than silently assumed: true faceted PLP filtering (already
parked separately in the dynamic-sections plan) and any app-extension/third-party mega-menu source —
both explicitly out of scope for the reasons already recorded elsewhere.
