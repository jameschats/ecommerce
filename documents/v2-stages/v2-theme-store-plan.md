# Study & Plan — Real category themes (a Shopify-style theme store)

> **STATUS CORRECTION (2026-07-29, first-hand code re-check):** The "why themes look simple" diagnosis
> below is **stale**. Phase A (design tokens + real Google-Fonts loading + shared-component retrofit,
> `4f0a862`), Phase B (section variants + flagship Ignition theme, `17ca883`), and TD4 (real mini-preview
> thumbnails, `16a69cf`) were **already shipped** after this doc was written — the T1–T6 gap it describes
> no longer exists in that form. Verified directly against code, not memory: `theme.service.ts` sets 20+
> CSS vars (color ×4, typography ×6 incl. real `loadFonts()` Google-Fonts injection, layout ×2, shape/
> radius ×4, card style ×3, favicon); `styles.css`'s `.sf-card`/`.btn-primary` genuinely consume them;
> `SectionTypeRegistry.cs` has **27** registered section types (not "~7 static + a handful"); `Hero` has
> 3 layout variants (boxed/split/banner), `FeaturedProducts` has 2 (grid/carousel), `Categories` has 3
> (grid/cards/strip). **Two gaps from this doc remain real and confirmed** (see the addendum below): the
> theme editor is still flat/section-level only (no click-to-select-in-canvas — T10), and the theme
> browse/install UI is still a plain grid with no filters (T7). Treat the body below as **historical
> context for why the token system exists**, not a current punch list — the addendum + this note are the
> current source of truth.

**Goal (user):** 5–10 genuinely distinct, category-tailored storefront themes (electronics, apparel, watches,
restaurant/burger, grocery, beauty, home…) that look real like Shopify's (Ignite/Focal/Shapes) and carry the look
across home · product · collection · cart. Later: grow the catalog.

## Why our current themes look "simple" (root cause)
Today a theme only controls **~8 CSS variables** (primary/secondary color, one font name, base size, container
width, button radius) and:
- **Section HTML/layout is identical across every theme** — a theme only *recolours* the same markup. No layout
  variety. (`storefront-section.component` renders each section type exactly one way.)
- **Fonts aren't actually loaded** — we set `--app-font` but never add a Google-Fonts link, so custom fonts
  silently fall back to system fonts. Typography — the biggest driver of a theme's personality — is effectively off.
- **Shared components use fixed styles, not tokens** — `product-card`, buttons, headings hardcode
  `rounded-xl`/`border-slate-200`/etc., so they look the same in every theme regardless of its radius/card style.
- **Very few design tokens** — no spacing/density, radius scale, card style, shadow style, typography scale,
  image treatment, or per-section colour schemes.
- **Few section types + no section variants** — ~7 static + a handful of dynamic; each renders one fixed way.

So the theme engine's *structure* (S1–S7: templates + sections + zones + library + publish) is solid — the gap is
**presentation depth**, not architecture.

## How Shopify actually does it (the key insight)
Shopify OS-2.0 themes share a lot of underlying section code. Their distinctiveness comes from:
1. **Deep theme settings** — full typography (font pairings, scale, weight, case, spacing), **multiple colour
   schemes** applied per section, layout density/spacing, corner radius, shadows, button + card styles.
2. **Section *variants*** — each section exposes "layout"/"style" options (Hero: full-bleed/split/minimal;
   Featured collection: grid/carousel/slider; etc.).
3. **A rich section catalog** — slideshow, collage, multicolumn, image-with-text, rich-text-columns, logo list,
   countdown, blog posts, FAQ/accordion, newsletter, "shop the look", video, map…
4. **Curated content + demo imagery** so a theme looks complete the moment you install it.

**Takeaway:** we don't need bespoke HTML per theme (unmaintainable). We need **rich design tokens + section
variants + more section types + curated content + real font loading**. That's the scalable path — the same
sections, heavily parameterised, produce dramatically different looks.

## Strategy — 6 workstreams

### T1 — Design-token system *(foundation, highest leverage)*
Expand what a theme controls, applied as CSS variables on `:root`, consumed everywhere:
- **Colour:** primary · secondary · accent · background · surface · text · muted · border · sale/success. Later:
  named **colour schemes** (scheme-1..n) a section can opt into (light/dark/accent bands, like Shopify).
- **Typography:** heading font · body font (**Google-Fonts loaded** — see T6) · type scale · heading weight ·
  heading letter-spacing · heading case (e.g. UPPERCASE) · body line-height. Font pairing = 60% of personality.
- **Layout & density:** container width · section vertical padding (compact/cozy/spacious) · grid gap.
- **Shape & surface:** radius scale (buttons/cards/inputs/images) · button style (solid/outline/pill) · card style
  (bordered/shadow/flat/elevated) · shadow style · image treatment.
Then **retrofit the shared components** (`product-card`, buttons/`.btn-primary`, headings, inputs, section
wrappers, page container) to read these tokens instead of hardcoded Tailwind. *This one workstream alone makes
every theme look genuinely different — including on product/collection, since those reuse the shared components.*

### T2 — Section *variants*
Add a `style`/`layout` setting to section schemas and switch on it in the renderer:
- Hero: full-bleed · split · boxed · minimal · slideshow.
- Featured products: grid · carousel · list · "editorial".
- Categories: tiles · strip · cards · collage.
- ImageWithText: image-left/right · overlap · full-width.
Each variant = a branch in the section component (shared, not per-theme).

### T3 — Expand the section catalog
Build the section types real themes need: **Slideshow**, **Collage/Gallery**, **Multicolumn** (features/USP row),
**Rich-text-columns**, **Image banner**, **Logo list**, **Countdown/Promo bar**, **Blog posts** (needs a blog —
later), **FAQ/Accordion**, **Newsletter** variants, **"Shop the look"**, **Video**, **Map/Contact**. Prioritise the
ones the target categories need most (see themes below).

### T4 — Author 5–10 rich prebuilt themes
Each theme = a distinct **token set** (T1) + a category-tailored **home layout** (T2/T3 sections) + a header/footer
variant (T5) + curated demo content (T6). Replaces the thin S6 bundles. (Extend `PrebuiltThemeRegistry` to carry
the full token set, or move bundles to seed JSON if they get large.)

### T5 — Header/footer variants
Header layouts (logo left/center · mega-menu · transparent-over-hero · sticky styles) and footer layouts — as
variants of the Header/Footer group sections (S2 already renders these zones' settings).

### T6 — Fonts + demo content
- **Load theme fonts** (Google Fonts): inject `<link>` for the theme's heading+body fonts (SSR-safe, preconnect).
  *(Without this, T1 typography doesn't show.)*
- **Demo imagery:** curated stock/placeholder images per theme category (hero, category tiles, banners) so a theme
  looks complete on install. Host under `/uploads` or a CDN; reference in the bundle.

## The 5–10 themes (categories + design direction)
| Theme | Category | Direction |
|---|---|---|
| **Base** | General/Minimal | Clean, neutral, flexible — the safe default. |
| **Ignition** | Electronics | Dark accents, deals-led, mega-menu, bestseller rails, promo badges, dense grid, bold sans. |
| **Atelier** | Apparel/Fashion | Editorial: big elegant hero, lookbook, generous whitespace, muted palette, large imagery. |
| **Chronos** | Watches/Jewelry | Luxe: dark + gold, serif headings, centered header, large hero, minimal chrome. |
| **Craving** | Restaurant/Burger | Bold, appetizing: warm palette, big display font, menu-led, prominent "Order now". |
| **Harvest** | Grocery | Fresh: green, aisle tiles, delivery promo, friendly rounded, dense. |
| **Lumen** | Beauty | Soft pastel, rounded, airy spacing, serif+sans pairing, gentle. |
| **Loft** | Home & Living | Warm/earthy, large imagery, "shop the room". |
*(Start with 5–6; grow to 10+.)*

## Phasing & sequencing (recommended)
1. **Phase A — Token foundation + fonts (T1 + T6 fonts).** Expand tokens, load fonts, retrofit shared components.
   *Highest leverage — instantly makes themes look distinct across all pages.*
2. **Phase B — Flagship theme end-to-end (one, e.g. Ignition/Electronics).** Prove tokens + a few variants + 1–2
   new sections + demo content produce a real, polished theme. Sets the pattern + quality bar.
3. **Phase C — Section variants + priority new sections (T2 + subset of T3).** The variety the other themes need.
4. **Phase D — Author the remaining themes (T4)**, iterating 1–2 at a time, each verified on home/product/collection.
5. **Phase E — Header/footer variants + polish (T5)**, plus growing the catalog over time.

**Do NOT** try to author 8 themes before the token/variant foundation exists — they'd all look samey (today's
problem). Foundation first, then a flagship, then replicate.

## Effort (honest)
- Phase A: **large** (the retrofit touches shared UI) — but it's the multiplier for everything.
- Phase B: medium (one theme, deeply).
- Phase C: medium-large (variants + a few sections).
- Phase D: ~0.5–1 day per theme once the pipeline exists.
- Phase E: medium.
This is a multi-week track. It's the right investment — themes are the #1 "chosen over Shopify" differentiator.

## Open decisions
1. **Design language:** "inspired-by, our own execution" (recommended) vs closely mirroring specific Shopify themes.
2. **Colour schemes:** ship per-section colour schemes now (more Shopify-like, more work) or a single palette first.
3. **Demo images:** curated stock set we host (licensing!) vs neutral placeholders vs AI-generated. *(Confirm the
   licensing path.)*
4. **Fonts:** Google Fonts (easy, needs network) vs self-hosted subset (faster, privacy). Lean: Google Fonts first.
5. **Blog:** several themes want blog sections — build a lightweight blog, or defer those sections.

## Non-goals
- A *paid* theme marketplace (decided earlier — free prebuilt themes only).
- Per-theme bespoke HTML/Liquid-style custom code (unmaintainable; tokens + variants instead).

---

## Addendum (2026-07-28) — Shopify Theme Store UX review

Reviewed live Shopify admin screenshots (Online Store → theme list, Theme Store browse-all with filters,
theme detail/preview, install flow, theme editor canvas) against our S1–S7 engine + `PrebuiltThemeRegistry`
(9 themes) + `admin-theme-library.component.ts` (flat 3-col install grid). The T1–T6 "presentation depth"
diagnosis above still stands as the #1 gap. Beyond that, five smaller gaps surfaced:

### T7 — Theme Store browse/discovery page
Shopify's theme store (1221 themes, screenshot-verified) is a filterable marketplace: left-rail facets for
**Price** (Free 24 / Paid 1197), **Industry** (~20 checkboxes: Art, Auto, Bags, Beauty, Clothing, Electronics,
Food and drink, Home, Jewelry, Kids, Pets, Sports, Toys, Wellness…), **Catalog size** — exactly four tiers,
confirmed from the sidebar counts: **One Product** (26) · **Few (2-10)** (88) · **Some (11-100+)** (811) ·
**Lots (500+)** (296) — plus a quick "Large catalogs"/"Small catalogs" shortcut in the top nav dropdown as a
coarser alias of the same facet. **Features** (~20 tags with adoption counts, see T12 below). Each theme's
detail page has a desktop/mobile preview toggle, a fullscreen expand, and a live scrollable iframe demo; price/
like% badges sit above "View demo" / "Try theme" — the latter installs as a draft with a progress bar ("Adding
X to your online store themes…").

Our picker is a flat grid, category label only — no filters, no detail page, no fullscreen preview.

**This directly answers "categorize themes large/small catalog"**: don't build the filter rail yet — at 9
themes it'd return near-empty facets, worse UX than today's grid. Do it in two parts:
1. **Now (cheap, independent of catalog size):** add a `CatalogFit` tag (`Small` | `Medium` | `Large`) to each
   `PrebuiltTheme` — maps to which layout suits a curated few-SKU store (Boutique, Bloom — spacious, hero-led)
   vs. a dense many-SKU store (Ignition, Bazaar — compact grids, department tiles). This is a data-model addition,
   ~1 line per theme, and doubles as guidance text ("best for stores with 200+ products") even before any filter
   UI exists. `Features` tags can accrue the same way as T2/T3 land (e.g. "carousel", "mega-menu").
2. **Later (Phase D+, once the catalog is ≥15–20 themes):** build the filter rail + a real browse-all page +
   fullscreen/mobile preview toggle on the existing preview-token iframe (that last part is cheap and can move
   up independently — it doesn't need more themes).

### T8 — Theme versioning/updates: not a gap
Shopify shows "Version X.X.X available" because paid/free themes are vendor-maintained code the merchant didn't
fork. Ours are **copy-on-install** (S6 deep-copies a bundle into the tenant's library) — editing never conflicts
with an upstream push. This model is simpler and correct for a single-vendor library; skip building a version/
update channel.

### T9 — Import/export a theme (minor, low priority)
Shopify supports uploading a theme .zip or importing from another store. No equivalent here. Only worth building
if a real need shows up (merchant backup, or migrating a theme between two of our stores) — not before Phase D.

### T10 — Block-level selection in the editor (confirmed gap)
Screenshot-confirmed, not just inferred: clicking the **Heading block** *inside* an Image-banner section (not
the section itself) opens a **block-scoped** right panel — rich-text toolbar (AI-assist icon · Bold · Italic ·
Link), the field pre-selected, a "Heading size" dropdown, "Remove block" — while the left sidebar tree expands
to show that section's individual blocks (Text / Heading / Buttons) as separately clickable rows. Separately,
selecting a **section** (not a block) shows a small floating **in-canvas toolbar** right next to it on the
canvas: an "Ask for changes" AI pill + duplicate/hide/delete icon buttons — distinct from the block-level right
panel. So Shopify has two granularities: section-level (floating canvas toolbar + full settings panel) and
block-level (inline canvas click + focused mini-panel).
**Action:** check `admin-theme-editor.component.ts` — does it support selecting an individual block within a
section's `Blocks` JSON array from the canvas, or only a flat per-section settings+blocks form? If it's the
latter, this is a real authoring-UX gap. Rank below T1 (affects merchant editing comfort, not what shoppers see).

### T11 — AI "describe your business" storefront generator (idea, not a gap)
Shopify's Theme Store front page has a prompt box that generates tailored design options from a business
description. This is separate from the in-canvas AI edit pill. Since AI Growth (G1 text-gen, G4 image-gen) is
already shipped, the cheap version of this isn't a new AI system — it's "describe your business → recommend the
closest prebuilt theme (using `Category`/`CatalogFit`) and pre-fill its hero copy/image via the existing G1/G4
pipeline." Worth a line in a future V3/AI stage doc; explicitly out of scope for this theme-depth track.

### T12 — Feature-adoption counts as a T3 priority signal (new, code-verified)
The Theme Store's Features facet lists adoption counts out of 1221 themes — that's a ranked signal for which
storefront capabilities are table-stakes vs. niche, worth reading directly into T2/T3 prioritization:

**Near-universal (>80% of themes) — treat as baseline, not differentiators:**
Sticky header (1202) · Color swatches (1181) · Mega menu (1171) · Quick view (1130) · Stock counter (1086) ·
In-menu promos (1078) · Breadcrumbs (1075) · EU translations (1104) · Swatch filters (1001).

**Cross-checked against our code (grep, not guesswork):**
- ✅ **Sticky header** — exists (`Header` section has a `sticky` setting, on by default in every prebuilt theme).
- ✅ **Breadcrumbs** — exists (`SectionTypeRegistry.cs:178`, dynamic section, live on product pages per S3).
- ⬜ **Color swatches** (variant colour shown as a swatch dot on the product *card*, not just the PDP) — no
  match anywhere in `ecomm.web/src/app`. Missing.
- ⬜ **Mega menu** (multi-column dropdown nav with images/links) — `Header` only has `layout: standard|centered|
  minimal`; no mega-menu block type. Missing — this is T5's territory (header variants).
- ⬜ **Quick view** (product modal from a collection grid, no page nav) — no match. Missing.
- ⬜ **Stock counter** ("Only 3 left") — no match. Missing.
- ⬜ **Countdown/promo bar** — already flagged as not-yet-built in T3 above; the adoption count (965/1221, ~79%)
  confirms it's worth prioritizing, not a nice-to-have.
- ⬜ **Swatch filters** (filter a collection grid by colour swatch) — `CollectionGrid` is described only as
  generic "columns/filters/sort" in the engine doc; no swatch-specific filter UI found.

**Recommendation:** when sequencing T3 (expand section catalog) and T5 (header/footer variants), prioritize
**color swatches on product cards, mega menu, quick view, and a countdown/promo bar** over lower-adoption
items — they're present in 4 out of 5 real Shopify themes, so their absence reads as "unfinished," not
"different design language." Lower-adoption ones (Sign in with Shop — Shopify-account-specific, skip entirely;
Quantity pricing 347/1221 · Quick order list 240/1221 · Right-to-left 605/1221) are genuinely optional/niche —
don't let them compete for the same sprint.

### T13 — Setting-type vocabulary is far narrower than Shopify's (new, doc-verified)
Fetched `shopify.dev`'s actual input-settings reference (not inferred from screenshots). Shopify section
schemas support ~30 field types; ours (`FieldSchema.Type` in `SectionTypeRegistry.cs` + the `@switch` in
`admin-theme-editor.component.ts:142-150`) supports 10: `text|textarea|richtext|number|boolean|color|
image|url|select|category`. Confirmed missing, in priority order (highest-impact first):
- **Resource pickers**: `product`, `collection`, `page`, `blog`, `article`, `link_list` (menu picker), and
  a proper combined `url` picker (product/collection/page/article, not a free-text URL field). Highest
  priority — right now e.g. `FeaturedProducts.source` is a free-text field where a merchant types
  `"bestsellers"` by convention; a real `select`/`collection` picker would be far less error-prone and is
  what every Shopify theme does.
- **`range`** (slider with min/max/step) — we approximate this with plain `number` inputs (e.g.
  `FeaturedProducts.columns`); Shopify uses a slider UI. Cosmetic but very visible in the editor.
- **`font_picker`** — we already have a fixed 15-font list rendered as a plain `<select>`
  (`admin-theme-editor.component.ts:205-208`); a real font-picker UX (search/preview) is polish, not a
  functional gap.
- **`image_picker`** (with focal point + alt text) vs our plain `url`-as-text `image` field — no focal
  point support today.
- **`video`/`video_url`**, **`inline_richtext`**, **`checkbox`** (we only have `boolean`, fine as-is),
  **`metaobject`** (no metaobject concept in our schema at all — skip, not applicable without that
  primitive existing elsewhere in the platform).

### T14 — Named colour schemes (theme-level primitive, not just a per-section colour field)
Confirmed via docs: Shopify's `color_scheme`/`color_scheme_group`/`color_palette` types let a merchant
define a handful of named palettes once (e.g. "Scheme 1: light", "Scheme 2: dark accent band") and then
each *section* picks which scheme to render in — this is how a Shopify theme alternates light/dark bands
down the homepage without per-section colour re-entry. This was already flagged as **open decision #2** in
the original plan body above ("ship per-section colour schemes now… or a single palette first") — now
confirmed as a real, named Shopify primitive worth adopting directly rather than inventing our own shape.
Depends on T13 (needs a `color_scheme` field type) — sequence after it.

### T15 — Editor click-to-select-in-canvas (T10, promoted — now committed, not just flagged)
Confirmed first-hand (not from a subagent summary) by reading `admin-theme-editor.component.ts` directly:
the editor is a flat whole-section form (`lines 139-153` settings loop, `155-173` always-expanded blocks
loop) and the live preview is a bare `<iframe [src]="previewUrl()">` with **no interactivity at all** — no
postMessage bridge, nothing clickable inside it. Shopify's editor lets a merchant click an element on the
rendered page itself and jump straight to its settings.
**Approach:** the storefront renderer (`storefront-section.component.ts`) already wraps every section in a
predictable DOM structure — add a thin "editor mode" (activated via a preview-only query param/flag) that:
(a) wraps each rendered section/block in a hoverable/clickable overlay with a `data-section-id`/
`data-block-index` attribute, (b) on click, `postMessage`s the id back to the parent editor window,
(c) the editor (`admin-theme-editor.component.ts`) listens for that message and calls `select()`/sets
`selectedBlockIndex` — the flat form already exists as a target, this closes the "how do I get there"
gap without redesigning the settings panel itself. This is the largest single UX investment in the backlog
next to T13's picker fields — budget it as its own focused build, not a quick add.

### T16 — App Blocks / Theme App Extensions: **flagged, not assumed in scope**
Shopify's biggest structural feature we have *no* equivalent of: third-party **apps** can inject "app
blocks" directly into any section via the theme editor (a reviews widget, a size-chart, an upsell block)
with zero code changes to the theme, through a separate **Theme App Extensions** framework (its own CLI,
sandboxed rendering, app-review/publish pipeline, a 300-block-per-theme cap). This is not "one more section
type" — it is an entirely separate product capability: a third-party developer SDK + extension marketplace
+ sandboxing + review process, roughly comparable in scope to building a second product alongside the
storefront/theme engine, not a feature to fold into the T1–T15 backlog.
**This needs an explicit decision, not a default.** Our platform today is single-vendor-per-tenant SaaS —
there's no third-party developer ecosystem building for *our* platform the way there is for Shopify's. Two
honest paths: (a) **out of scope** — Shopify has this because it's a multi-million-merchant app
marketplace; we don't need our own plugin ecosystem to be "sophisticated like Shopify" in the ways that
matter to a merchant actually using the storefront/editor (T1–T15 cover all of those); (b) **in scope as a
future, separate initiative** — if the long-term vision includes third-party developers extending tenant
storefronts, that's its own multi-month platform project with its own plan doc, not a line item here.

### T17 — Theme blocks vs. section blocks: we only have the model Shopify itself recommends dropping (new, spec-verified)
Cross-referenced against `shopify-theme-architecture-spec.md` §5.5 (a full normative spec sourced from
`shopify.dev`, more rigorous than the screenshot-based research above — treat it as the current source of
truth for theme architecture questions going forward). Shopify has **three** kinds of blocks; the spec's own
build guidance (§5.5.4, §14 decision #2) is blunt: *"Section blocks are the older, weaker model. If you're
greenfield, consider shipping only theme blocks and skipping local blocks entirely."*

| | Theme blocks (recommended) | Section blocks (**what we have**) | App blocks (out of scope — T16) |
|---|---|---|---|
| Defined | own file/schema, reusable across sections | inline in one section's schema, that section only | third-party extension |
| Nestable | yes, up to 8 levels | no | within a supporting parent |

Our `SectionTypeRegistry.cs` only implements the section-blocks model: each section's `BlockTypes` are
locally scoped (e.g. `Hero`'s `Slide` block can't be reused inside `Multicolumn`), and there's no nesting.
This is a real architectural gap, not a missing field type — closing it means introducing a reusable,
independently-schema'd block library (a block has its own type/settings, is placeable inside *any* section
that accepts `@theme`-equivalent blocks, and can nest inside itself). It's comparable in size to the T1/T2
work already done for sections, but for blocks.
**This needs a scope decision like App Blocks did — but note the difference:** App Blocks (T16) was scoped
out because it's a third-party-developer-ecosystem feature we have no counterpart need for. Theme blocks are
different — Shopify recommends them even for a **greenfield, single-vendor** build, because reusability and
nesting are just better architecture, independent of any third-party angle. **Decided (2026-07-29): in
scope, sequenced as its own dedicated milestone (own plan/EnterPlanMode session) after T13 and T15** — not
squeezed into the current field-type work.

### T18 — Dynamic sources: unbuilt, real, but not urgent
Spec §6.6: binding a setting to a resource attribute (e.g. a heading auto-fills from `product.title`) or a
custom field, instead of typing a literal value. We have no equivalent. Valuable (it's what turns section
config from "static text" into "templated across a catalog"), but it's a bigger lift than T13's pickers and
nothing in the current plan blocks on it. Backlog, revisit after T15/T17.

### T19 — `visible_if` (conditional settings): small, worth folding into T13/T14 while there
Spec §6.5: a setting can declare `visible_if` against *other stored setting values only* (no runtime
context, no dynamic-source results) — deliberately a tiny, side-effect-free comparison grammar, not a general
expression language. Directly relevant to the just-added `FeaturedProducts.categoryId`/`collectionId` fields
in T13 — right now both show unconditionally even though only one applies depending on `source`. Small,
contained addition; fold into the tail of T13 or do as an immediate follow-up, not a separate milestone.

### T20 — Editor event contract for T15: adopt the spec's vocabulary, don't invent one
T15 (editor click-to-select-in-canvas, still queued) sketched a generic postMessage bridge. Spec §8.3 gives
an exact, battle-tested event surface instead: `shopify:section:{load,unload,select,deselect,reorder}` and
`block:{select,deselect}`, plus a `load: true` flag distinguishing "re-rendered" from "merchant clicked."
**When T15 is implemented, use this vocabulary** rather than a bespoke one — it's free rigor, already solves
the "component re-init after DOM swap" problem the spec calls out (§8.3's point about idempotent init/
teardown pairs) that a naive implementation would likely miss.

### Noted, not actionable now
- **§9 Partial-render API** (`?sections=`, bundled section rendering on cart mutations) solves a problem
  specific to *server-rendered* Liquid themes avoiding full page reloads. We're an Angular SPA/SSR hybrid —
  the client already does partial updates via component data-binding + API calls; this doesn't port 1:1.
  The one idea worth stealing later: bundling "updated cart + header count + promo bar" into one API
  response on add-to-cart, instead of three round trips — a response-shape optimization, not a new subsystem.
- **§7 error policy** ("a section that throws renders as null, not a 500") is worth a quick first-hand check
  against `storefront-section.component.ts` next time that file is touched — not verified either way yet.

### Net effect on sequencing
T1/T2/T6 (tokens, variants, fonts) are **already shipped** (see the correction note at the top of this doc)
— T3 (more section types), T4 (author more themes), T5 (header/footer variants) remain open. T7's
`CatalogFit`/`Features` tags fold into T4 as you touch each theme's metadata anyway; the fullscreen/mobile
preview toggle (part of T7) is cheap enough to do any time. T8 is a non-issue. T9, T11 are backlog. **T10/
T15 (editor click-to-select, now spec-informed — see T20) and T12 (swatches ✅ done, stock counter ✅ done,
mega menu/quick view/countdown bar still open) and T13/T14 (setting-type vocabulary + colour schemes) are
the confirmed, in-scope remaining work** — none of it blocked on anything else, sequence by whichever the
user wants live first. T16 (App Blocks) is explicitly not queued until a scope decision is made. **T17
(theme blocks) is a new, real, sizeable architectural gap** — recommend its own milestone, not bundled into
T13/T14. T18 (dynamic sources) is backlog. T19 (`visible_if`) is small — fold into T13's tail.
