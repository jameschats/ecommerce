# V2 — Theme Editor Parity Plan (E1–E6 + T17): "Edit store" like Shopify

**Status: fully shipped (2026-07-31).** E1–E6 and T17 are all done and live-verified on
`bazaar.wavcommerce.online`. T17 (`CustomSection`, commit `cac7091`) closes the program: a
purely-additive free-form block canvas (Heading/Text/Image/Button/Spacer/Divider, freely mixed,
reordered, added, removed) rather than retrofitting the 20+ existing section types, which are
legitimately uniform-repeating-unit patterns by design and didn't need this. Zero migration risk —
nothing about the 9 already-authored themes changed shape.

**What this is.** A thorough gap analysis + build plan to bring our theme editor's *editing experience* to
Shopify's level. Sources: three live Shopify editor screenshots analysed 2026-07-29 (page navigator open;
Hero section selected with its settings panel; a Button *block* selected in-canvas with its own outline and
focused panel), the normative reference `shopify-theme-architecture-spec.md` (§8 visual-editor contract,
§15 build order M5–M8), and first-hand verification of our current code (`admin-theme-editor.component.ts`,
`storefront-section.component.ts`, `section-field.component.ts`, `MediaController.cs`).

## Honest assessment: the gap is the editor, not the engine
The *engine* is already Shopify-shaped: theme library with draft/publish, templates-by-page-type,
section groups, 28 section types, resource-picker fields (T13), colour schemes (T14), basic
click-to-select (T15). What's behind is the **experience layer**: Shopify's editor is a
direct-manipulation canvas (hover anything → see what it is; click anything → edit exactly that thing;
type → watch it change live). Ours is a form beside a preview window that fully reloads on every save.
Same data model underneath — the work is UI/interaction, almost entirely frontend.

## Capability decomposition (screenshots + spec §8, vs. our code today)

**Status (2026-07-31): every row below is shipped and live.** E1–E6 plus T17 (`CustomSection`) are
all done — this table is now a historical record of the gap analysis, not an open task list.

| # | Capability | Shopify | Ours today | Status |
|---|---|---|---|---|
| 1 | Hover affordances | Outline + name badge ("Hero", "Menu") on hover over any section/block; "+" insertion points between sections | Hover outline + name badge, in-canvas block outlining, floating toolbar (hide/duplicate/delete), "+" insert points, desktop/mobile device toggle — all shipped (E1) | ✅ |
| 2 | Section click-to-select | Canvas click → sidebar + panel jump | Shipped (T15), incl. reverse highlight | ✅ |
| 3 | Block/element click-to-select w/ own canvas outline | "Shop all" button gets its own outline; URL carries `&block=` | Every block-bearing section type has `data-block-index`; every individual field (`data-field`) across all 20 section types is independently clickable and outlines in-canvas — a click resolves both which block AND which field, exactly the "Shop all button gets its own outline" bar (E3) | ✅ |
| 4 | Sidebar block tree | Section rows expand to block rows with **dynamic titles** ("Heading — *Browse our latest pro…*"); Add block inline; per-row actions | Shipped (E3): the selected section's blocks list directly beneath it in the tree, dynamic titles (heading/title/text/question/label/value fallback chain), "+ Add {type}" inline, drag-reorder | ✅ |
| 5 | Focused per-block panel | Selecting a block shows *only its* fields (Label, Link chip, Style, Button colors) with back/X | Shipped (E3): a section row shows only its settings; a block row shows only that block's fields with a "← Back" breadcrumb — never bundled together | ✅ |
| 6 | Rich field controls | Image picker w/ media library + stock explore; link shows a resource **chip** ("All Products") + open-in-new-tab; segmented controls; palette-linked colours | Shipped (E4): media-picker modal (Library/Upload/URL) on every image field, resolved link chips, segmented controls for ≤4-option selects, AI-assist on text fields | ✅ |
| 7 | Page navigator | Searchable dropdown of every page type, drill-in to pick the *specific* product/collection the preview uses | Searchable navigator (Pages/Products/Categories/Collections) shipped; preview-context picker for `product`/`collection` templates shipped (E5 remainder) — choose the real product/collection a dynamic template previews with | ✅ |
| 8 | Live preview, no reloads | Settings patch the canvas live before save (spec §8.4); saves re-render one section (§9) | Shipped (E2): draft settings/blocks stream into the canvas live as the merchant types (250ms debounce); saving a section's content no longer reloads the iframe; dirty-state Save bar with a discard-confirm guard | ✅ |
| 9 | Editor chrome | Dirty-state Save bar, undo/redo, device toggle, fullscreen, inspector toggle, deep-link URL (`?section=&block=`) | All shipped (E1 + E3 + E6): session-scoped undo/redo, fullscreen preview, an inspector toggle that suspends canvas click-interception so real links/Add-to-Cart work for a sanity check, Esc/Ctrl+Z/Ctrl+Y/arrow-key support, `?template=&section=&block=` deep links | ✅ |
| 10 | No accidental nav/text-selection in canvas | ✓ | ✓ (T15 + the mousedown fix) | ✅ |

## Why this is very buildable on our stack (and in one place cheaper than Shopify's)
- **The bridge already exists.** T15's same-origin postMessage channel between editor and canvas is
  exactly the seam spec §8.2/8.3 requires — every milestone below *extends its message vocabulary*
  rather than inventing infrastructure.
- **Live preview is cheaper for us than for Shopify.** Shopify needs a server round-trip to re-render a
  Liquid section (§9's partial-render API exists to make that tolerable). Our `storefront-section`
  renders **client-side from a settings signal** — the editor can stream draft settings JSON into the
  iframe and the section re-renders instantly with zero server involvement. §9 explicitly doesn't need
  porting (already noted in the theme-store-plan addendum).
- **Media library backend already exists** — `api/admin/media`: upload (10MB limit), paginated list,
  delete (`MediaController.cs`, verified). The image picker is pure frontend.
- **`SectionFieldComponent` (T13) is the single choke point** for every field type, used by both the
  settings and blocks forms — each control upgrade lands once and applies everywhere.
- **Colour schemes (T14)** supply the palette that palette-linked colour swatches display.
- **AI text-gen (G1, shipped)** can back Shopify's ✨ text-assist affordance nearly free (a shared
  `ai-assist-button` component already exists in `shared/`).

## Milestones

**Status: E1 ✅ · E2 ✅ · E3 ✅ · E4 ✅ · E5 ✅ · E6 ✅ · T17 ✅ — all done (2026-07-31).**
E3's commit folded in E5's remainder and all of E6 as well — they shared the same file/state model and
were small enough to land together rather than as separate passes. E4 shipped as its own pass: a media
picker (Library/Upload/URL) on every image field, resolved link chips, segmented controls for ≤4-option
selects, and AI-assist on text fields. T17 (`CustomSection`, commit `cac7091`) shipped last, its own
dedicated research + design pass as planned: a purely-additive free-form block canvas rather than
retrofitting every existing section type. Every capability row in the table above is ✅ — this program
is complete. R3 (rolling Rich Themes to the remaining 6 themes) is tracked separately and also done —
see `v2-storefront-experience-roadmap.md`.

**E1 — Canvas feel** *(medium)*
Hover outline + name badge on sections and blocks (CSS `content: attr(data-section-label)` — a host
attribute the component already has the data for); extend `data-block-index` to the remaining
block-bearing markup (Hero's positional slides, AnnouncementBar messages, Footer columns); per-block
outline **in the canvas** on selection (today only the right-panel card highlights); floating selection
toolbar in-canvas (hide/duplicate/delete → postMessage → the editor methods that already exist); "+"
insertion points between sections (message carries insert position → add-section picker pre-seeded);
device-width toggle (desktop/mobile) on the iframe container.

**E2 — Live editing, zero reloads** *(medium; highest perceived value — do immediately after E1)*
(a) Stop reloading the iframe on save: postMessage the updated section JSON; the section component swaps
its data in place (signal update → instant re-render). (b) Stream *draft* edits live before save:
debounce `settingsObj`/`blocksArr` changes → postMessage → canvas updates as the merchant types, exactly
like Shopify. (c) Dirty-state Save bar with an unsaved-changes guard, replacing the per-section button.

**E3 — Sidebar tree + focused panels** *(large — the structural rework)*
Expandable section→block tree with **dynamic block titles** (first non-empty of `heading`→`title`→`text`,
else the block type — spec §8.5's exact rule); selecting a block shows a *focused* panel with only its
fields + breadcrumb back to the section; "Add block" inline in the tree; drag-reorder blocks (same CDK
machinery as sections); deep-linkable editor state (`?section=&block=` query params, restored on load);
three-way selection sync (canvas ⇄ tree ⇄ panel).

**E4 — Rich field controls** *(medium; near-zero backend)*
`image` fields → media-library picker modal (existing list+upload endpoints) with a URL tab as fallback;
`link` fields render the chosen resource as a labelled chip; `select` with ≤4 options renders as a
segmented control (spec §6.4's exact heuristic); colour fields show the theme's scheme/palette swatches
alongside the free picker; ✨ AI-assist on text/richtext fields via the existing G1 pipeline.

**E5 — Page navigator + preview context** *(small–medium)*
Searchable page/template dropdown replacing the plain `<select>`; product/collection search inside it to
choose *which* sample resource dynamic templates preview with (replaces the hardcoded first-product).

**E6 — Chrome polish** *(medium, incremental)*
Session-scoped undo/redo (in-memory snapshots; defer server-side history); fullscreen preview; inspector
on/off toggle (temporarily suspend editor-mode click interception to test real storefront behaviour
in-canvas); keyboard support (Esc deselect, arrow reorder).

## Explicit non-goals
- **Theme code editor** (Liquid equivalent) — our platform has no per-theme code by design (tokens +
  variants instead); nothing to edit.
- **App embeds panel** — T16 (App Blocks) already decided out of scope.
- **Checkout editor** — Shopify locks checkout out of the theme editor too; we match.
- **"Explore free images" stock integration** — optional later (e.g. an Unsplash API proxy; licensing
  review needed); the media library + URL tab covers the need meanwhile.

## Stated product requirements this plan serves (user, 2026-07-29)
"Choose a theme → edit it **page by page, section by section, element by element**; themes for **small
catalogs and large catalogs**." Mapping:
- **Page by page** — templates-per-page-type already exist (S4); the E5 page navigator makes moving
  between them Shopify-fluid, incl. choosing which sample product/collection a page previews with.
- **Section by section** — shipped (T15 click-to-select); E1/E2 make it feel right (hover badges, live
  edits, no reloads).
- **Element by element** — both layers now shipped: *(E3)* clicking an element (a heading, a button)
  inside the canvas focuses **that field** in the panel via `data-field` anchors alongside
  `data-block-index`, mapping a click to section → block → field on every existing section type;
  *(T17)* `CustomSection` gives merchants a genuinely composable canvas — Heading/Text/Image/Button/
  Spacer/Divider blocks, freely mixed, added, removed, reordered — the true first-class-block
  experience, for the one place a merchant actually wants that freedom (custom layouts), without
  retrofitting the 20+ purpose-built section types that don't need it.
- **Small-catalog vs large-catalog themes** — the T7 `CatalogFit` decision in the theme-store plan
  (tag every prebuilt theme Small/Medium/Large, show it in the picker, filter later) plus T4 (author
  more themes so both fits are genuinely covered: spacious hero-led themes for few products, dense
  rail/department themes for many). Promoted from "fold in later" to a near-term milestone at the
  user's request — it's catalog work on `PrebuiltThemeRegistry`, independent of E1–E6, can run in
  parallel any time.

## Order & sizing (as executed)
E1 → E2 → E3 (+E5 remainder +E6, folded in) → E4 → T17. E1+E2 produced the visceral "this feels like
Shopify" difference; E3 was the deepest structural rework of the *editor* (and the home of
element-level selection); E4 was steady, focused polish; T17 was the deepest remaining piece
overall — the actual data model, done last and deliberately scoped down to one additive section
type rather than a platform-wide retrofit. The catalog-fit theme work (T4/T7/R3) ran as a parallel
content track. Each milestone got its own plan → build → test → deploy → live-verify cycle.
