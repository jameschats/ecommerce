# Study & Plan — Real category themes (a Shopify-style theme store)

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

### Net effect on sequencing
T1–T6 (Phase A–E) is unchanged as the priority — it's what makes any theme look real. T7's `CatalogFit`/
`Features` tags are cheap enough to fold into **Phase D** (author-the-remaining-themes) as you touch each
theme's metadata anyway. The fullscreen/mobile preview toggle (part of T7) is cheap enough to do any time.
T8 is a non-issue. T9–T11 are backlog, not blocking.
