# V2 — Theme Editor Parity Plan (E1–E6): "Edit store" like Shopify

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

| # | Capability | Shopify | Ours today | Status |
|---|---|---|---|---|
| 1 | Hover affordances | Outline + name badge ("Hero", "Menu") on hover over any section/block; "+" insertion points between sections | Nothing on hover; outline only after click | ⬜ Missing |
| 2 | Section click-to-select | Canvas click → sidebar + panel jump | Shipped this session (T15), incl. reverse highlight | ✅ |
| 3 | Block click-to-select w/ own canvas outline | "Shop all" button gets its own outline; URL carries `&block=` | Partial: 4 `@for` section types report a blockIndex; highlight lands on the right-panel card, not the canvas element; Hero's positional blocks not addressable | 🟡 Partial |
| 4 | Sidebar block tree | Section rows expand to block rows with **dynamic titles** ("Heading — *Browse our latest pro…*"); Add block inline; per-row actions | Flat section list; blocks exist only as always-expanded cards in the right panel | ⬜ Missing |
| 5 | Focused per-block panel | Selecting a block shows *only its* fields (Label, Link chip, Style, Button colors) with back/X | Whole-section form including every block at once | ⬜ Missing |
| 6 | Rich field controls | Image picker w/ media library + stock explore; link shows a resource **chip** ("All Products") + open-in-new-tab; segmented controls; palette-linked colours | Image = URL text field; link = T13 type-dropdown+select (functional, chipless); plain selects/colour inputs | 🟡 Partial |
| 7 | Page navigator | Searchable dropdown of every page type, drill-in to pick the *specific* product/collection the preview uses | Plain `<select>` of templates; preview product hardcoded to first product | 🟡 Partial |
| 8 | Live preview, no reloads | Settings patch the canvas live before save (spec §8.4); saves re-render one section (§9) | Full iframe reload (cache-busted) after every save; nothing live before save | ⬜ Missing — **the biggest "feel" gap** |
| 9 | Editor chrome | Dirty-state Save bar, undo/redo, device toggle, fullscreen, inspector toggle, deep-link URL (`?section=&block=`) | Per-section Save button only | ⬜ Missing |
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

## Order & sizing
E1 → E2 → E3 → E4 → E5 → E6. E1+E2 together produce the visceral "this feels like Shopify" difference;
E3 is the deepest structural rework; E4–E6 are steady increments. Each milestone = its own plan → build →
test → deploy cycle, like T13–T15 were.
