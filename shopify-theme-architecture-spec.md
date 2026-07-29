# Shopify Theme & Store Builder Architecture — Implementation Reference

**Purpose:** a complete, implementation-oriented description of how Shopify's theme system and visual store builder ("theme editor") work, written to be fed to a coding agent (Claude Code) as the reference spec for building an equivalent system inside your own commerce platform.

**Sources:** Shopify's public developer documentation (`shopify.dev/docs/storefronts/themes/*`, `shopify.dev/docs/api/ajax/section-rendering`), read in full and restated here in condensed, normalized form. Direct links are listed in Appendix D.

**Status of this document:** descriptive (how Shopify does it) + prescriptive (what you should build). Sections marked **[PORT]** contain recommendations for your platform rather than descriptions of Shopify.

---

## 0. How to use this document with Claude Code

Recommended setup:

```
your-repo/
├── CLAUDE.md                     # points at the files below
└── docs/
    ├── theme-architecture.md     # this file
    └── decisions.md              # your answers to §14
```

In `CLAUDE.md`, add something like:

> The storefront theming system is specified in `docs/theme-architecture.md`. Treat §5 (component model), §6 (settings), §7 (render pipeline) and Appendix A (JSON schemas) as normative. Before implementing any renderer or editor feature, read the relevant section. Terminology in §16 is binding — do not invent alternative names for these concepts.

Then drive the build with §15 (build order), one milestone per session. The appendix JSON schemas are deliberately machine-readable so an agent can generate validators, TypeScript types, and DB migrations directly from them.

---

## 1. The one-paragraph mental model

A **theme** is a bundle of *code* (templates written in a sandboxed template language) plus *data* (JSON files holding merchant configuration). A page render is a four-level composition: a **layout** wraps a **template**; the template is a list of **sections**; each section is a list of **blocks**; blocks may nest into other blocks. Every section and block declares a **schema** — a JSON manifest of merchant-editable **settings** — and the visual editor is a *generic* UI generated entirely from those schemas. The editor never edits markup; it edits the JSON data files. This separation is the whole trick: developers ship components with typed setting contracts, merchants recombine and configure them, and neither party touches the other's artifact.

```
Request  →  Route/resource resolution
              ↓
         Template  (JSON: ordered list of section instances + their setting values)
              ↓
         Section instances  → each renders its Liquid file with `section.settings`
              ↓
         Block instances    → each renders with `block.settings`, recursively nestable
              ↓
         Layout  (wraps the rendered template output; hosts header/footer section groups)
              ↓
         HTML
```

---

## 2. Code vs. data — the central architectural split

This distinction drives your database schema, your permissions model, your versioning strategy, and your diffing/merge behaviour. Get it wrong and everything downstream hurts.

| Artifact | Kind | Authored by | Mutated by editor | Ships with theme |
|---|---|---|---|---|
| `layout/*.liquid` | code | developer | no | yes |
| `sections/*.liquid` | code | developer | no | yes |
| `blocks/*.liquid` | code | developer (or AI generator) | no | yes |
| `snippets/*.liquid` | code | developer | no | yes |
| `assets/*` | code/static | developer | no | yes |
| `config/settings_schema.json` | code (manifest) | developer | no | yes |
| `locales/*.json` | code (manifest) | developer | yes, via language editor | yes |
| `templates/*.json` | **data** | developer seeds it | **yes** | yes (as defaults) |
| `sections/*-group.json` | **data** | developer seeds it | **yes** | yes (as defaults) |
| `config/settings_data.json` | **data** | developer seeds it | **yes** | yes (as defaults) |

Consequences you must design for:

- Editing a section's *code* is a developer action (deploy). Editing a section *instance* is a merchant action (save JSON).
- Theme updates must merge new code with existing merchant data. Shopify's answer is: settings have `default` values, unknown keys in data are ignored, missing keys fall back to defaults. Adopt the same forgiving-read policy.
- Data files that merchants edit do **not** preserve comments or trailing commas. Files developers edit (`settings_schema.json`, inline `{% schema %}` blocks) *do* allow JSONC. If you reuse a JSON parser, you need a JSONC-tolerant one on the developer side.

---

## 3. Directory structure (the theme package format)

```
theme/
├── assets/          # CSS, JS, images, fonts. Flat — no subdirectories.
├── blocks/          # theme blocks: reusable .liquid components with schemas
├── config/
│   ├── settings_schema.json   # global theme setting definitions
│   └── settings_data.json     # global theme setting values (+ theme presets)
├── layout/
│   └── theme.liquid           # REQUIRED. the only mandatory file in a theme
├── locales/         # en.default.json, en.default.schema.json, fr.json, ...
├── sections/        # section .liquid files AND section-group .json files
├── snippets/        # reusable .liquid partials, invisible to the editor
└── templates/
    ├── customers/   # legacy customer account templates (deprecated by Shopify)
    └── metaobject/  # templates for custom content-type pages
```

Rules:

- Subdirectories other than those listed are not supported. Flat namespaces per directory; the filename **is** the component's type identifier.
- `layout/theme.liquid` is the only required file.
- Section groups live in `sections/` despite being JSON, because they are the section-level data container.
- Assets can opt into template processing by appending `.liquid` (e.g. `theme.css.liquid`, `app.js.liquid`). Such files get access to the global settings object and filters only — not to section/block context.

**[PORT]** Keep the flat-directory + filename-as-type convention. It makes the type registry trivial (scan directory → map of type → compiled template) and makes references in data files human-auditable. If you store themes in a database rather than a filesystem, keep a `(theme_id, directory, filename)` unique index so the same invariants hold.

---

## 4. Request → page resolution

1. Resolve the URL to a **page type** and, where applicable, a **resource** (product, collection, article, page, blog, metaobject entry, search, cart, 404, index, password, gift card).
2. Determine the **template name**: the page type, plus an optional suffix if the resource has an alternate template assigned or the request carries `?view=<suffix>`.
3. Look for `templates/<name>.json` then `templates/<name>.liquid`. A template type may exist as JSON **or** Liquid, never both.
4. Apply **contextual overrides** if any (see §5.2.4).
5. Determine the **layout**: JSON templates declare it via the `layout` key (default `theme.liquid`, or `false` for no layout); Liquid templates declare it via a `layout` tag.
6. Render the template body, then render the layout with the body injected.

Page types Shopify supports, as a checklist for your router:

`index`, `product`, `collection`, `list-collections`, `page`, `blog`, `article`, `search`, `cart`, `404`, `password`, `metaobject/<type>`, plus Liquid-only special files: `gift_card`, `robots.txt`, `agents.md`, `llms.txt`, `llms-full.txt`.

Notes:
- No template type is mandatory, but a page type without a matching template cannot render.
- The special text-output templates (`robots.txt`, `agents.md`, `llms.txt`, `llms-full.txt`, `gift_card`) must be Liquid, since they emit non-HTML documents. `agents.md` / `llms.txt` are the machine-readable store descriptions used by AI shopping agents — worth building an equivalent if agentic commerce matters to you.
- Alternate template naming: `<type>.<suffix>.<json|liquid>`, e.g. `product.wholesale.json`. Assignment happens per-resource in admin, or ad hoc via `?view=wholesale`.
- Legacy customer-account templates are deprecated in favour of a platform-hosted account experience embedded via a web component. **[PORT]** Don't put auth flows in the theme; expose them as platform-rendered components the theme can place.

---

## 5. The component model

### 5.1 Layouts (`layout/*.liquid`)

The outermost HTML document. Holds `<head>`, global scripts, and site chrome. Two required output points:

| Object | Placement | Meaning |
|---|---|---|
| `content_for_header` | inside `<head>` | platform-injected scripts/meta. Opaque — themes must not parse or modify it. |
| `content_for_layout` | inside `<body>` | the rendered template output |

Both must be present in `theme.liquid` or the file is rejected on save. Multiple layouts are allowed (`layout/full-width.liquid` etc.). Layouts host **section groups** (§5.3) so merchants can edit headers/footers.

Common pattern: emit the template name onto `<body>` as a class (`template-product`) so CSS can target page types.

**[PORT]** `content_for_header` is your injection seam for analytics, consent, app scripts, and CSP nonces. Define it early and make it non-negotiable — treating it as opaque is what lets you change platform-injected markup later without breaking themes.

### 5.2 Templates

#### 5.2.1 JSON templates (the default, and what your builder should target)

A JSON template is pure data: an ordered list of section instances and their setting values. No markup lives here. Anything visual must live in a section that the template references.

```jsonc
{
  "layout": "theme",            // optional. string filename, or false for no layout
  "wrapper": "main#content.page-wrap[data-x=y]",  // optional, CSS-selector-ish
  "sections": {
    "hero": {
      "type": "image-banner",
      "settings": { "heading": "Summer sale" },
      "blocks": {
        "b1": { "type": "text", "settings": { "text": "<p>Up to 40% off</p>" } }
      },
      "block_order": ["b1"]
    },
    "grid": { "type": "featured-collection", "disabled": false, "settings": {} }
  },
  "order": ["hero", "grid"]
}
```

Field semantics:

| Field | Type | Req | Notes |
|---|---|---|---|
| `layout` | string \| `false` | no | filename without extension. `false` ⇒ no layout, and such pages can't be edited in the visual editor |
| `wrapper` | string | no | wraps all sections in one element. Only `div`, `main`, `section` allowed as the tag. Selector-ish syntax for id/class/attributes |
| `sections` | object | yes | keyed by instance ID (alphanumeric, unique within the file) |
| `order` | array | yes | instance IDs in render order; must all exist in `sections`; no duplicates |

Section instance shape (identical inside section groups and `settings_data.json`):

| Key | Type | Notes |
|---|---|---|
| `type` | string | section filename without extension |
| `disabled` | bool | if true, not rendered but still editable/visible in the editor sidebar |
| `settings` | object | setting id → value |
| `blocks` | object | block instance ID → block instance |
| `block_order` | array | block IDs in render order (excludes static blocks) |
| `custom_css` | string | **platform-owned**. Written by the editor's custom-CSS feature. Developers must not author or mutate it |

Sections are rendered strictly in `order`, with **no markup between them**. All spacing/structure must come from the sections themselves. That constraint is what makes arbitrary reordering safe.

A section referenced in a template must exist in the theme (or be an app section), else the render errors.

#### 5.2.2 Liquid templates

Plain template files with markup and logic. Use only for non-HTML outputs and for pages you deliberately don't want merchants restructuring. They cost you editor flexibility and push more data into the global settings file. **[PORT]** Default everything to JSON templates; treat Liquid templates as an escape hatch.

#### 5.2.3 Alternate templates

`<type>.<suffix>.json`. Lets one page type have many layouts (e.g. a product template for apparel vs. one for digital goods). Selected per-resource in admin, previewable via `?view=<suffix>`. You cannot override the base template with an alternate — the base always exists.

#### 5.2.4 Contextual templates and section groups

For market/B2B-specific variations, Shopify writes an **overlay file** rather than duplicating the template:

```jsonc
// templates/index.context.ca.json
{
  "context": { "market": "ca" },      // or { "b2b": true }
  "parent": "index.json",
  "sections": {
    "hero": {
      "settings": { "show_text_box": true },
      "blocks": { "heading": { "disabled": true } }
    }
  }
}
```

Only the overridden keys appear. The renderer deep-merges overlay onto parent. Same mechanism for section groups: `header-group.context.ca.json`.

**[PORT]** This overlay pattern generalizes well — you can reuse it for A/B tests, per-locale variation, per-customer-segment personalization, and scheduled campaigns. Design your merge function once, as a pure `merge(parent, overlay) → resolved` over the section-instance tree, and make context resolution a pluggable list of predicates evaluated at request time.

### 5.3 Section groups (`sections/*.json`)

A section group is the same data structure as a JSON template, but placed inside a **layout** rather than a template. It's what makes headers and footers merchant-editable.

```jsonc
{
  "type": "header",          // header | footer | aside | custom.<name>
  "name": "Header group",    // ≤ 50 chars
  "sections": { "hdr": { "type": "header", "settings": {} } },
  "order": ["hdr"]
}
```

Rendered from a layout with a dedicated tag: `{% sections 'header-group' %}`.

Guidance: most themes should have exactly two (header, footer). Extra groups should be named for their purpose. Avoid mixing static sections and section groups in the same layout; if you must, name the static ones distinctly so merchants aren't confused about what they can move.

**[PORT]** Model section groups and JSON templates with the *same* table and the *same* renderer. The only differences are (a) where the container is mounted and (b) the `type`/`name` metadata. Sharing the implementation halves the code and guarantees consistent editor behaviour.

### 5.4 Sections (`sections/*.liquid`)

A section is a self-contained, merchant-configurable page module: markup + optional scoped CSS/JS + a schema.

Three content parts:

1. **Markup** — template code with access to global objects plus two scoped objects: `section` (its own id, settings, blocks) and `block` (inside a block loop).
2. **Scoped assets** — `{% stylesheet %}` and `{% javascript %}` tags let a section bundle its own CSS/JS. The platform deduplicates and hoists these, so a section used five times emits its CSS once.
3. **Schema** — exactly one `{% schema %}` tag containing JSON. It is a manifest, not executable: nothing inside it is rendered, and it may not be nested inside another tag. More than one schema tag is a hard error.

Scoping rules that matter for your interpreter:

- Sections cannot read variables defined outside themselves (only globals).
- Variables defined inside a section don't leak out.
- Snippets rendered *inside* a section can see that section's `section`/`block` objects.

#### 5.4.1 Section schema reference

| Key | Type | Purpose |
|---|---|---|
| `name` | string | display name in the editor |
| `tag` | enum | wrapper element: `article`, `aside`, `div`, `footer`, `header`, `section`. Default `div` |
| `class` | string | extra class appended to the platform wrapper class |
| `limit` | 1 \| 2 | max instances of this section per template/group. Default unlimited |
| `settings` | array | input + sidebar settings (§6) |
| `blocks` | array | accepted block types (§5.5) |
| `max_blocks` | int ≤ 50 | cap on dynamic blocks. Static blocks don't count |
| `presets` | array | pre-configured variants shown in the "add section" picker |
| `default` | object | default config for *statically* rendered sections. Same shape as a preset |
| `locales` | object | section-local translations, keyed by language then key |
| `enabled_on` | object | allowlist: `{ "templates": [...], "groups": [...] }` |
| `disabled_on` | object | blocklist, same shape. Mutually exclusive with `enabled_on` |

`enabled_on`/`disabled_on` accept page-type names and `["*"]` for templates, and `header`/`footer`/`aside`/`custom.<name>`/`["*"]` for groups. These drive which sections appear in the merchant's "add section" picker in a given context.

**Presets** are the key discoverability mechanism:

```jsonc
"presets": [
  {
    "name": "Testimonial carousel",   // required; sorted alphabetically in the picker
    "category": "Social proof",       // optional; groups into collapsible categories
    "settings": { "layout": "carousel" },
    "blocks": [ { "type": "quote" }, { "type": "quote" } ]
  }
]
```

**A section without presets cannot be added by a merchant.** It can only be placed by a developer editing the JSON directly, and once placed, it can't be removed in the editor. This is a deliberate and very useful lever: it's how you ship a "main product" section that is mandatory and unremovable, while shipping a "rich text" section that is freely addable. Build the same rule.

Uncategorized presets sort first; the editor auto-generates a preview thumbnail by rendering the preset in an isolated preview mode (see §8.4).

#### 5.4.2 The section wrapper contract

Every rendered section is wrapped by the platform:

```html
<section id="shopify-section-{instance_id}" class="shopify-section {schema.class}">
  ...section output...
</section>
```

The `id` is the addressing scheme for the editor's DOM surgery **and** for partial re-rendering (§9). Instance IDs are dynamic when the section lives in a template or group (e.g. `template--5678__image_banner`, `sections--1234__header`) and equal to the filename when statically rendered. **[PORT]** Adopt an equivalent stable, parseable wrapper ID convention on day one; retrofitting it is painful.

#### 5.4.3 Static sections (legacy pattern, avoid)

`{% section 'featured-product' %}` renders a section inline in a Liquid file. Caveats: only one instance exists globally regardless of how many places render it, so all render sites share the same settings; merchants can't move or remove it; app blocks aren't supported inside it. Section groups exist precisely to replace this. Support it for compatibility, steer developers away from it.

### 5.5 Blocks

Three kinds, with different scoping and capabilities:

| | Theme blocks | Section blocks | App blocks |
|---|---|---|---|
| Defined in | `blocks/*.liquid` | inside a section's `{% schema %}` | third-party extension |
| Reusable across sections | yes | no | yes |
| Nestable | yes, up to 8 levels | no | within a supporting parent |
| Own file/schema | yes | no | yes (external) |
| Can coexist with the other kind in one section | ✗ (a section uses theme blocks *or* section blocks, not both) | ✗ | ✓ with either |

Limits: ≤ 50 blocks per section instance; ≤ 300 theme block files per theme (every file in `/blocks` counts, referenced or not).

#### 5.5.1 Theme blocks

Standalone component files. Schema keys:

| Key | Notes |
|---|---|
| `name` | editor display name |
| `settings` | same setting system as sections |
| `blocks` | accepted children: `@theme` (any), `@app`, or explicit type names. Theme blocks **cannot** define local blocks |
| `presets` | required for the block to appear in the "add block" picker; may compose children |
| `tag` | wrapper element. Any string ≤ 50 chars (custom elements allowed), or `null` for no wrapper |
| `class` | extra classes on the wrapper |

Rendering children: `{% content_for 'blocks' %}` outputs all child block instances in stored order. This one tag handles theme blocks and app blocks uniformly.

Default wrapper: `<div id="shopify-block-{id}" class="shopify-block {class}">`.

With `"tag": null`, no wrapper is emitted and the block file must have exactly one top-level element carrying `{{ block.shopify_attributes }}` — the editor needs a single element to move when reordering, or you get orphaned DOM. This unlocks blocks that render as `<h2>` vs `<h3>` based on a setting.

Scoping: a theme block can read its own `block` object, the enclosing `section` object, and globals. It **cannot** receive arbitrary variables from its parent (that's what snippets are for) — except for static blocks, below.

#### 5.5.2 Targeting and visibility

- `"blocks": [{"type": "@theme"}]` — accept every public theme block.
- `"blocks": [{"type": "slide"}]` — accept only `blocks/slide.liquid`.
- **Private blocks**: name the file with a leading underscore (`_slide.liquid`). Underscore-prefixed blocks are excluded from `@theme` wildcards and must be referenced explicitly. This is your "internal component" modifier — use it for slides, carousel controls, accordion rows, etc.
- **Recommended blocks**: list `@theme` *plus* specific types to pin those to the top of the picker while keeping everything else behind a "show all" affordance.

Dynamic block titles: the editor labels each block instance using the value of a setting whose `id` is `heading`, else `title`, else `text`; falling back to the block's `name`. Tiny feature, disproportionate UX payoff — merchants see "Free shipping over ₹999" in the sidebar instead of five identical "Text" entries. Implement it.

#### 5.5.3 Static blocks

A block rendered at a fixed position in the parent's markup:

```liquid
{% content_for "block", type: "_slideshow-controls", id: "controls" %}
```

`id` is author-supplied and must be unique within the immediate parent (block IDs need only be unique per parent, not per section). Behaviour vs. dynamic blocks:

| | Static | Dynamic |
|---|---|---|
| Configurable | ✓ | ✓ |
| Hideable | ✓ | ✓ |
| Reorderable / removable / duplicable | ✗ | ✓ |
| Renderable conditionally or in a loop | ✓ | ✗ |
| Counts toward `max_blocks` | ✗ | ✓ |
| Appears in `block_order` | ✗ | ✓ |
| Accepts arbitrary params from parent | ✓ | ✗ |

Arbitrary params: `{% content_for "block", id: "s1", type: "slide", accent: "#111" %}` — the child reads `{{ accent }}` directly. This is the only channel for passing parent data into a block.

In persisted data, static block instances carry `"static": true` and are omitted from `block_order`; the renderer derives their position from the parent's code. Conditionally-rendered static blocks get a visual "may be hidden" cue in the editor sidebar.

Use case that motivates the whole feature: a slideshow that always has controls at the bottom and an accordion row that always has a summary at the top — structure the developer guarantees, content the merchant controls.

#### 5.5.4 Section blocks (local)

Declared in the section's schema with `type`, `name`, optional `limit`, and `settings`. Rendered by iterating:

```liquid
{% for block in section.blocks %}
  {% case block.type %}
    {% when 'slide' %}
      <div class="slide" {{ block.shopify_attributes }}>...</div>
  {% endcase %}
{% endfor %}
```

`{{ block.shopify_attributes }}` emits the data attributes the editor uses to identify the element. Without it the block is invisible to selection, reordering, and live updates.

Never branch on a literal block ID — IDs are generated and unstable. Branch on `type`.

**[PORT]** Section blocks are the older, weaker model. If you're greenfield, consider shipping **only** theme blocks (file-based, nestable, reusable) and skipping local blocks entirely. It removes an entire category of "can't mix these" rules from your engine and your docs. The cost is that trivial one-off blocks need their own file.

### 5.6 Snippets (`snippets/*.liquid`)

Plain partials. Invoked with `{% render 'product-card', product: product, show_price: true %}`.

- Accept named parameters; **cannot** see caller variables otherwise.
- Local variables don't escape.
- Invisible to the editor; no schema, no settings.

Blocks vs. snippets — the decision rule:

| Need | Use |
|---|---|
| Merchant-configurable, appears in editor, instance-level settings | block |
| Pure code reuse, parameterized by the developer, no merchant UI | snippet |

They compose: blocks for structure and settings, snippets for the repeated markup inside them.

Shopify layers a doc-comment convention (`{% doc %}` with `@param`/`@example`) on snippets to drive IDE completion and param validation. **[PORT]** Cheap to add, and it's the difference between a theme codebase that's navigable and one that isn't. If your platform will host third-party theme developers, do this.

### 5.7 Assets (`assets/`)

Flat directory of CSS/JS/images/fonts, referenced through a URL filter that resolves to CDN paths. Appending `.liquid` to a non-binary asset enables template processing with access to global settings and filters — useful for injecting theme colors into CSS, though the modern approach is to emit CSS custom properties in the layout and keep stylesheets static (see §8.3 on live preview).

### 5.8 Config

**`config/settings_schema.json`** — an array of setting groups defining the "Theme settings" panel. The first entry is a metadata object (theme name, author, version, documentation/support URLs); subsequent entries are `{ name, settings: [...] }` groups. Values are read through a global settings object, and are reachable from `.liquid` assets.

**`config/settings_data.json`** — the values:

```jsonc
{
  "current": { "color_page_bg": "#FFFFFF", "heading_font": "helvetica_n4" },
  "presets": {
    "Minimal":  { "color_page_bg": "#FFFFFF" },
    "Midnight": { "color_page_bg": "#000000" }
  },
  "platform_customizations": { "custom_css": "..." }
}
```

- `current` — live values.
- `presets` — up to 5 named theme-wide design variants ("theme styles"). Selecting one overwrites `current`, **but only for presentational settings**: `checkbox`, `color`, `color_background`, `color_palette`, `color_scheme`, `color_scheme_group`, `font_picker`, `number`, `radio`, `range`, `select`. Content settings (text, images, resource pickers) are preserved. This is a genuinely clever detail — it lets a merchant reskin a store without losing their copy. Copy it exactly.
- `platform_customizations` — settings the platform owns, currently merchant-authored custom CSS. Developers must never write here.

Limits: file ≤ 1.5 MB; ≤ 5 theme presets.

### 5.9 Locales (`locales/`)

Two file families:

| Extension | Scope | Editable by merchant |
|---|---|---|
| `*.json` | storefront strings | yes, via a language editor |
| `*.schema.json` | editor UI strings (setting labels, info text, option labels) | no |

Naming follows IETF language tags (`en.json`, `en-GB.json`, `fr-CA.json`). Exactly one default per family: `en.default.json`, `en.default.schema.json`.

Structure is three levels — category → group → description — e.g. `blogs.article_comment.submit_button_text`. Referenced with a translation filter. Schema files let setting labels be written as `"label": "t:settings_schema.colors.settings.background.label"`.

Section-local `locales` in a schema are an alternative for portable sections that ship outside a theme; edits merchants make there are written back into the theme's locale files, leaving the schema untouched.

Limits: 3400 translations per file; 1000 characters per value.

**[PORT]** Two decisions to make early: (1) does merchant-authored *content* (setting values like headings) get translated separately from theme *strings*? Shopify says yes — text-bearing setting types are translatable through a separate translation tool, while `liquid`-type settings deliberately aren't, making them the right home for language-neutral values like class names. (2) Are translations part of the theme version or of the store? Shopify splits it: schema translations ship with the theme, storefront overrides live with the store.

---

## 6. The settings system

Settings are the contract between developer and merchant. Everything the visual editor renders is generated from these declarations — there is no bespoke UI per section.

### 6.1 Where settings live

| Level | Declared in | Read via | Dynamic sources |
|---|---|---|---|
| Theme (global) | `config/settings_schema.json` | global settings object | ✗ |
| Section | section's `{% schema %}` → `settings` | `section.settings.<id>` | ✓ |
| Block (local) | section's `{% schema %}` → `blocks[].settings` | `block.settings.<id>` | ✓ |
| Block (theme) | block file's `{% schema %}` → `settings` | `block.settings.<id>` | ✓ |

Setting IDs must be unique within their owning object. Duplicates are a hard error.

### 6.2 Two categories

- **Input settings** hold a value.
- **Sidebar settings** hold no value and aren't configurable — headers, paragraphs, and other informational elements used to structure the panel.

### 6.3 Standard attributes

| Attribute | Req | Notes |
|---|---|---|
| `type` | ✓ | one of the types in §6.4 |
| `id` | ✓ | key used to read the value |
| `label` | ✓ | shown in the editor; supports `t:` translation keys |
| `default` | — | initial value; required for some types |
| `info` | — | helper text; supports Markdown-style links `[text](url)` |
| `visible_if` | — | conditional visibility, see §6.5 |

### 6.4 Setting type catalog

Implement this as a registry: each type maps to (a) an editor control, (b) a validator, (c) a runtime value resolver. Adding a type should mean adding one registry entry, not touching the editor.

**Basic types**

| Type | Control | Extra attributes | Runtime value |
|---|---|---|---|
| `checkbox` | toggle | — | boolean; `false` if no default |
| `number` | numeric field | `placeholder` | number, or nil if empty. `default` must be a number, not a string |
| `radio` | radio group | `options[]` (`value`,`label`) — required | string; first option if no default |
| `range` | slider + numeric input | `min`✓, `max`✓, `step`, `unit` | number. `default` **required**; all four must be numeric. Out-of-range input clamps; off-step input rounds |
| `select` | dropdown or segmented control | `options[]`✓, optional `group` per option | string; first option if no default |
| `text` | single-line | `placeholder` (global settings only) | string, or empty object if unset |
| `textarea` | multi-line | `placeholder` (global settings only) | string, or empty object if unset |

Note the `select` rendering heuristic: render as a segmented control when there are 2–5 options, no `group` attribute, and the labels fit; otherwise render a dropdown. Small detail, meaningfully better panels.

**Specialized types**

| Type | Purpose | Notes |
|---|---|---|
| `article` | article picker | returns article object; no `default`; not overwritten by preset switching |
| `article_list` | multi-article picker | `limit` ≤ 50; returns array; paginatable; `.count` accessor |
| `blog` | blog picker | returns blog object; no `default` |
| `collection` | collection picker | returns collection object; no `default` |
| `collection_list` | multi-collection | `limit` ≤ 50 |
| `color` | color picker | `placeholder` for the empty-state hint. Returns color object or blank. If no `default`, merchants get a "clear" affordance; cleared ⇒ `""`. Unset is distinct from transparent |
| `color_background` | CSS background string | supports gradients; **not** image backgrounds. Returns string |
| `color_palette` | named theme palette | **one per theme, in `settings_schema.json` only**. `default` is a key→hex map, 2–20 entries, no alpha. Only supports the `id` attribute. Accessed as `settings.<id>.<key>`; iterable |
| `color_scheme` | scheme picker w/ preview | value resolves to a scheme object from the scheme group. Not supported in app blocks |
| `color_scheme_group` | defines the set of schemes | `settings_schema.json` only. `definition[]` of `header`/`color`/`color_background` settings + a `role` map that drives previews |
| `font_picker` | font selection | `default` **required**; values come from a platform font library |
| `html` | raw HTML | `<html>`, `<head>`, `<body>` stripped; unclosed tags auto-closed on save |
| `image_picker` | image from media library | returns image object or nil; no `default`; supports **focal points** (see below) |
| `inline_richtext` | bold/italic/link, no `<p>` wrapper | no line breaks supported |
| `link_list` | navigation menu picker | returns menu object; `default` accepts well-known menu handles |
| `liquid` | HTML + limited template code | ≤ 50 KB. Denied access to layout, header/layout content objects, section tag, scoped asset tags, schema, and global settings. Allowed: global objects, template-context objects, standard tags/filters. Unknown tags render empty |
| `metaobject` | custom-content-type entry picker | `metaobject_type`✓ |
| `metaobject_list` | multi-entry picker | `metaobject_type`✓, `limit` ≤ 50 |
| `page` | page picker | returns page object; no `default` |
| `product` | product picker | returns product object; no `default` |
| `product_list` | multi-product picker | `limit` ≤ 50; only active, published products |
| `richtext` | bold/italic/underline/link/paragraph/list | `default` must be wrapped in `<p>` or `<ul>` |
| `text_alignment` | icon segmented control | values `left`/`right`/`center`, default `left` |
| `url` | URL entry + resource picker | returns string or nil |
| `video` | platform-hosted video picker | returns video object; no `default`; accepts file-reference custom fields as a dynamic source |
| `video_url` | external video URL | `accept[]`✓ (`youtube`, `vimeo`); exposes parsed `id` and `type` |

Two behaviours worth calling out because they're easy to miss:

- **Preset-switch immunity.** Resource-ish and free-text settings (`text`, `article`, `blog`, `collection`, `page`, `product`, `image_picker`) are *not* overwritten when a merchant switches presets. See also the presentational-settings list in §5.8. Same principle, two places.
- **Image focal points.** Merchants can set a focal point on an image; the image-rendering filter emits a corresponding `object-position` style. Combined with `object-fit: cover`, crops stay on-subject at every aspect ratio. **[PORT]** Build this into your image pipeline from the start — it's the single highest-leverage image feature for merchant-authored content.

**Cross-setting references.** A `color` or `color_background` setting can declare its default as a reference to a palette entry (`"default": "{{ settings.colors.primary }}"`). When a merchant deletes a palette color, the editor asks for a replacement and rewrites the deleted key as a reference to it — so existing references stay intact without rewriting every template. Elegant indirection; worth stealing.

### 6.5 Conditional settings (`visible_if`)

```jsonc
{
  "type": "select", "id": "direction", "label": "Direction",
  "options": [ {"value":"row","label":"Horizontal"}, {"value":"column","label":"Vertical"} ],
  "default": "column",
  "visible_if": "{{ block.settings.layout_style == 'flex' }}"
}
```

Supported on all basic settings, all sidebar settings, and this subset of specialized ones: `color`, `color_background`, `color_scheme`, `font_picker`, `html`, `image_picker`, `inline_richtext`, `link_list`, `liquid`, `richtext`, `text_alignment`, `url`, `video`, `video_url`.

Hard constraint: the expression is evaluated against *stored setting values only*. It cannot see runtime context or the resolved value of a dynamic source. You may test whether a dynamically-sourced setting has a value; you may not branch on what it resolves to.

**[PORT]** Keep this constraint. It's what allows the editor panel to be evaluated client-side without a server round-trip per keystroke. Design the expression language as a tiny, total, side-effect-free comparison grammar (equality, inequality, boolean ops, `blank` checks) — do not let it grow into a general expression evaluator.

### 6.6 Dynamic sources (binding settings to structured data)

The mechanism that turns a static section into a data-driven one. A merchant can bind an input setting to a resource attribute or a custom field instead of typing a literal value.

Two source kinds:

1. **Resource attributes** — properties of the resource in context:

| Resource | Bindable attributes |
|---|---|
| product | `title`, `vendor`, `description`, `url`, `featured_image`, `collections` |
| collection | `title`, `image`, `description`, `url`, `products` |
| page | `title`, `url`, `content` |
| article | `title`, `url`, `author`, `content`, `excerpt`, `comments_count`, `image` |
| blog | `title`, `url` |

2. **Custom fields (metafields) and custom content types (metaobjects)** — merchant/app-defined typed data.

Availability rules — how the editor decides what's offerable for a given setting:

- Section is inside a product template ⇒ product fields/attributes available to the section and all its blocks.
- Section has a `collection` setting ⇒ collection fields available to the section and its blocks.
- A block has a `product` setting ⇒ that product's fields available to that block.
- Theme blocks resolve to the **closest ancestor** providing a resource of the required type.
- Storefront-visible custom content types are globally available.
- Not available for global theme settings.

Type compatibility matrix (setting type ← field types it can bind to):

| Setting | Compatible field types |
|---|---|
| `text` | single/multi-line text, list of single-line text, integer, decimal, date, datetime, weight, volume, dimension, rating, money |
| `richtext` | as `text`, plus rich text and link |
| `inline_richtext` | as `text`, plus link |
| `color` | color |
| `url` | url |
| `image_picker` | file reference |
| `video` | file reference (video types only) |
| `product` / `product_list` | product reference / list of product references |
| `collection` / `collection_list` | collection reference / list |
| `page` | page reference |
| `article` | article reference |
| `metaobject` / `metaobject_list` | entry reference / list, matching declared type |

List-typed sources iterate: binding a `list.metaobject_reference` to a text setting renders each entry's selected field as a list.

A setting's `default` may itself reference a dynamic source (`"default": "Featuring: {{ product.title }}"`), but only a bare reference — additional logic in the expression is an error. Only do this where the source is guaranteed present, otherwise you ship a section that errors on non-product pages.

Limits: 100 dynamic sources per JSON template, per section group, and per global settings file; 50 per individual setting; 50 per static section.

**[PORT]** This is the feature that separates a page builder from a *commerce* page builder, and it's the one most competitors get wrong. Two implementation notes: (1) resolve bindings at render time, not at save time, so content updates propagate without re-saving pages; (2) the compatibility matrix must be data, not code — merchants and apps will add field types you didn't anticipate.

---

## 7. The render pipeline

Normative pseudocode for the server-side renderer. This is the part to hand Claude Code first.

```
render(request):
  ctx        = resolve_context(request)          # shop, locale, market, customer, design_mode
  route      = resolve_route(request.path)       # → page_type + resource
  tpl_name   = template_name(route, request.query.view, resource.template_suffix)
  template   = load_template(theme, tpl_name)    # JSON or Liquid
  template   = apply_context_overlays(template, ctx)   # market / b2b / experiment

  if template.is_json:
      body = render_section_list(template.sections, template.order, ctx)
      body = apply_wrapper(body, template.wrapper)
  else:
      body = render_liquid(template.source, ctx)

  layout_name = template.layout ?? "theme"
  if layout_name is false:
      return body                                # unstyled, not editable in the builder

  layout = load_layout(theme, layout_name)
  return render_liquid(layout, ctx, {
      content_for_layout: body,
      content_for_header: platform_head_injection(ctx),
      sections_tag: name => render_section_group(name, ctx),
  })


render_section_list(sections, order, ctx):
  out = []
  for id in order:
      inst = sections[id]
      if inst.disabled: continue
      out.push(render_section(id, inst, ctx))
  return join(out, "")                           # NO markup between sections


render_section(instance_id, inst, ctx):
  def_   = registry.sections[inst.type]          # compiled template + schema
  if not def_: raise SectionNotFound(inst.type)

  settings = resolve_settings(def_.schema.settings, inst.settings, ctx)
  scope = {
    section: {
      id: instance_id,
      settings: settings,
      blocks: build_block_list(def_, inst, ctx),   # ordered, incl. static placement
      location: ctx.location,
    }
  }
  inner = render_liquid(def_.source, ctx, scope)
  tag   = def_.schema.tag ?? "div"
  cls   = trim("shopify-section " + (def_.schema.class ?? ""))
  return `<${tag} id="shopify-section-${instance_id}" class="${cls}">${inner}</${tag}>`


render_block(block_id, inst, ctx, parent_scope, depth):
  if depth > 8: raise BlockNestingTooDeep
  def_ = registry.blocks[inst.type]              # theme block file, or section-local def
  settings = resolve_settings(def_.schema.settings, inst.settings, ctx)
  scope = parent_scope + {
    block: {
      id: block_id,
      type: inst.type,
      settings: settings,
      shopify_attributes: editor_attrs(block_id, inst.type),
    }
  }
  inner = render_liquid(def_.source, ctx, scope)   # `content_for 'blocks'` recurses here
  if def_.schema.tag is null:
      return inner                                # block file must carry shopify_attributes itself
  tag = def_.schema.tag ?? "div"
  cls = trim("shopify-block " + (def_.schema.class ?? ""))
  return `<${tag} id="shopify-block-${block_id}" class="${cls}" ${editor_attrs(...)}>${inner}</${tag}>`


resolve_settings(schema_settings, stored, ctx):
  out = {}
  for s in schema_settings where s is an input setting:
      raw = stored[s.id]
      if raw is a dynamic-source binding:
          raw = resolve_dynamic_source(raw, ctx)
      if raw is missing or invalid:
          raw = s.default                        # forgiving read; never fail on unknown/missing
      out[s.id] = coerce(s.type, raw)            # per-type resolver → object or scalar
  # unknown keys in `stored` are ignored, not errors
  return out
```

Ordering of blocks inside a section:

- Dynamic blocks render in `block_order`.
- Static blocks render wherever their `content_for "block"` tag appears in the parent's code, and are **not** in `block_order`.
- When both exist, the editor sidebar shows static blocks positioned relative to dynamic ones according to their position in the source.

Error policy (match this — it's why Shopify themes rarely white-screen):

| Condition | Behaviour |
|---|---|
| Referenced section type missing from theme | error (hard) |
| Referenced block type missing | error |
| Setting present in data but absent from schema | ignore |
| Setting in schema but absent from data | use `default`, else type-appropriate empty |
| Dynamic source resolves to nothing | treat as blank; template guards with a blank check |
| Nesting deeper than 8 levels | error |
| Section render throws, during partial render | return null for that section, HTTP 200 overall |

### 7.1 Asset hoisting

Scoped `{% stylesheet %}` / `{% javascript %}` content from every rendered section and block must be collected during render, deduplicated by source component (not per instance), and emitted once — stylesheet content into the head or a single style element, script content into a single bundle. Design the render context with a collector for this; retrofitting it means walking the tree twice.

---

## 8. The visual editor contract

The editor is a host application with the storefront in an iframe. It never manipulates theme code. Its full contract with the renderer is:

### 8.1 What the editor reads

1. **Schemas** — to generate setting panels, the add-section picker (from presets, grouped by `category`), and the add-block picker (from block presets, filtered by parent `blocks` targeting and `enabled_on`/`disabled_on`).
2. **Data files** — the current template/group/settings state, which it mutates and saves.
3. **The rendered DOM** — addressed via the wrapper IDs and data attributes.

### 8.2 What the renderer must emit

| Contract | Requirement |
|---|---|
| Section identity | `id="shopify-section-{instance_id}"` on the wrapper |
| Block identity | `id="shopify-block-{block_id}"` on the wrapper, or, when `tag: null`, the `shopify_attributes` data attributes on the single top-level element |
| Design-mode flag (server) | a request flag readable in templates (`request.design_mode`) |
| Design-mode flag (client) | a global JS flag (`Shopify.designMode`) |
| Inspector flag | `Shopify.inspectMode` |
| Preview-mode flag | `request.visual_preview_mode` / `Shopify.visualPreviewMode` |

### 8.3 Events the editor emits into the iframe

DOM events that bubble and aren't cancellable. `event.target` is the section or block element.

| Event | Target | Detail | Meaning / expected theme response |
|---|---|---|---|
| `shopify:section:load` | section | `{sectionId}` | section added or re-rendered — re-run all its init JS as if the page just loaded |
| `shopify:section:unload` | section | `{sectionId}` | section removed or about to re-render — tear down listeners, timers, observers |
| `shopify:section:select` | section | `{sectionId, load}` | selected in the sidebar — scroll into view and stay visible |
| `shopify:section:deselect` | section | `{sectionId}` | — |
| `shopify:section:reorder` | section | `{sectionId}` | position changed |
| `shopify:block:select` | block | `{blockId, sectionId, load}` | e.g. a slideshow should slide to this block and pause |
| `shopify:block:deselect` | block | `{blockId, sectionId}` | resume normal behaviour |
| `shopify:inspector:activate` | — | — | preview inspector on |
| `shopify:inspector:deactivate` | — | — | preview inspector off |

`load: true` distinguishes "this fired because of a re-render" from "the merchant clicked it."

The reason this event surface exists: the editor swaps HTML into the live DOM without a page reload, so `DOMContentLoaded`-style initialization never re-runs. Every interactive component needs an idempotent init/teardown pair bound to these events. **[PORT]** If you use a component framework with lifecycle hooks (web components, Svelte, Vue), you get most of this for free — a custom element's `connectedCallback`/`disconnectedCallback` map almost exactly onto `section:load`/`section:unload`. Strongly consider mandating custom elements for interactive sections; it eliminates the most common category of theme bug.

### 8.4 Live preview (updating without a re-render)

For a subset of settings the editor updates the preview instantly rather than round-tripping to the server.

**Color settings.** The editor renders color settings inside `{% style %}` tags as CSS custom properties rather than literal values, then updates the property. Consequences:
- Applying a filter to a color setting inside a style tag breaks live preview, because the filter operates on the variable-reference *string*, not the color. Same for null-coalescing defaults — the value is never nil in the editor, so editor and storefront diverge.
- Correct pattern: apply filters outside the style tag, assign to a variable, reference the variable inside.
- Colors referenced from static stylesheets in `assets/` can't be live-previewed at all. The standard workaround is to declare CSS custom properties in the layout and consume them from static CSS. **[PORT]** Make this the documented default pattern rather than a workaround — it's also better for caching.

**Text settings** (`text`, `textarea`, `richtext`, `inline_richtext`) live-preview only when:
- the setting value is the sole child of its parent element,
- no filters are applied except escaping,
- the value isn't preceded/followed/wrapped by other template logic inside the parent element,
- the element isn't hidden at page load (so `{% unless x == blank %}<h1>{{ x }}</h1>{% endunless %}` breaks it).

These are real constraints on how theme authors write markup. Document them prominently or your builder will feel laggy for reasons your theme developers can't diagnose.

**Preset previews.** The picker's thumbnails are produced by rendering the preset in an isolated preview mode. Themes can detect that mode and adjust — e.g. expand a collapsed accordion so the thumbnail is legible.

### 8.5 Editor UX rules derived from schema

Encode these as engine rules, not per-section logic:

- No preset ⇒ not addable, not removable.
- `limit: 1|2` ⇒ picker disables the section once the count is reached.
- `max_blocks` ⇒ add-block disabled at the cap; static blocks excluded from the count.
- `enabled_on`/`disabled_on` ⇒ filters the picker per template type and group type.
- `_`-prefixed block ⇒ hidden from `@theme` wildcards.
- `@theme` + explicit types ⇒ explicit ones surface first, rest behind "show all".
- Block instance label ⇒ `heading` → `title` → `text` setting value, else block `name`.
- `disabled: true` ⇒ instance stays in the sidebar, doesn't render on the page.
- Conditionally-rendered static blocks ⇒ show a "may not appear" indicator.

---

## 9. Partial rendering API (section rendering)

Server-rendered themes need to update fragments without full reloads (cart drawers, filtered collection grids, paginated search, quick views). Shopify's solution is elegantly minimal: **any storefront URL accepts a query parameter that returns rendered section HTML instead of a full page.**

```
GET /collections/shoes?sections=facets,product-grid
→ 200 application/json
  { "facets": "<section id=\"shopify-section-...\">…</section>",
    "product-grid": "<section id=\"…\">…</section>" }

GET /collections/shoes?section_id=product-grid
→ 200 text/html   (raw markup, no JSON envelope)
```

Rules:

- `sections` accepts a comma-separated list or array syntax; **max 5** per request.
- Sections render in the full context of the requested URL — all normal query parameters (search terms, page numbers, filters) still apply. This is why it composes so well with pagination and faceting.
- You cannot pass setting values through the API. Settings come from the template/group data, or from defaults if the section isn't placed on that page.
- Sections that fail to render come back as `null` inside a 200 response — clients must handle nulls. Requesting a nonexistent single section via `section_id` returns 404.
- Section IDs: read from `section.id` in templates, or parse from the wrapper's `id` attribute. Statically rendered sections use the filename.
- Cart mutation endpoints accept a `sections` parameter in the request body, so an add-to-cart returns the updated cart drawer, header count, and free-shipping bar in one round trip ("bundled section rendering"). **[PORT]** Do this. It collapses the classic add-to-cart → refetch-cart → refetch-header waterfall into a single request, and it's the main reason Shopify storefronts feel fast without a SPA.
- Build locale/market-aware URLs from a platform-provided root path rather than hardcoding `/`, or international storefronts break.

---

## 10. Extensibility: third-party blocks

If your platform will have an app ecosystem, the theme system needs an extension seam. Shopify's design:

- A section or theme block declares `{"type": "@app"}` in its `blocks` array to accept app-provided blocks. `@app` entries don't accept `limit`.
- App blocks render through the same `{% content_for 'blocks' %}` tag. In legacy section-block loops, `{% render block %}` handles them.
- Sections supporting app blocks may declare at most one resource setting of each type (one product setting, one collection setting), so the platform can unambiguously autofill the app block's resource context.
- App blocks aren't supported in statically rendered sections.

**Top-level app blocks and wrapper resolution.** Merchants can add an app block as a full-width page element, not just inside an existing section. Since blocks aren't sections, the platform wraps them. Resolution order:

1. Theme provides `sections/apps.liquid` → used. Must accept `@app` and declare a preset, else the editor errors.
2. Else theme provides `sections/_blocks.liquid` → used. Must accept both `@theme` and `@app` and declare a preset.
3. Else platform-generated default wrapper.

Wrapper sections are special: they can't be rendered manually, don't appear in the add-section picker, and can't declare template restrictions (they must work everywhere). They typically expose one or two layout settings (padding, width) so app content matches theme rhythm.

**AI-generated blocks** follow the same path: generated code is written as an ordinary theme block file in `/blocks`, and `_blocks.liquid` wraps it when it's placed directly on a page. The lesson for your architecture: *if generated components are just ordinary components, generation is a UI feature rather than a subsystem.* Design the component format so a model can emit it — a single file with markup, scoped styles, and a JSON manifest is close to ideal for that.


---

## 11. Limits (and why each exists)

Copy the *shape* of these even if you pick different numbers. Every one of them is protecting a real failure mode.

| Limit | Value | Protects against |
|---|---|---|
| Sections per JSON template / section group | 25 | render time, editor sidebar usability |
| Blocks per section instance | 50 | same |
| `max_blocks` per section (developer-set) | ≤ 50 | design integrity |
| Theme block nesting depth | 8 (excluding section level) | infinite recursion, render blowup |
| Theme block files per theme | 300 | editor picker usability, bundle size |
| JSON templates per theme | 1000 | admin listing, build time |
| Theme presets | 5 | data file size |
| `settings_data.json` size | 1.5 MB | editor load time |
| `liquid`-type setting content | 50 KB | render time, abuse |
| Translations per locale file | 3400 | parse time |
| Translation value length | 1000 chars | — |
| Dynamic sources per template / group / global settings | 100 | N+1 data fetches |
| Dynamic sources per setting | 50 | — |
| Dynamic sources per static section | 50 | — |
| Sections per partial-render request | 5 | request amplification |
| Section `limit` attribute | 1 or 2 | design integrity |
| Section group `name` | 50 chars | UI |
| Block `tag` string | 50 chars | — |

---

## 12. Persistence and versioning **[PORT]**

Shopify ships themes as file bundles in a git-like store. Your platform probably wants a database. A workable model:

```sql
-- Immutable code, versioned together
themes            (id, store_id, name, version, parent_theme_id, role, created_at)
                  -- role: 'main' | 'unpublished' | 'development'
theme_files       (theme_id, path, content, content_hash, updated_at)
                  -- path e.g. 'sections/hero.liquid', 'blocks/text.liquid'

-- Compiled/derived, rebuilt on file change
component_defs    (theme_id, kind, type, schema_json, source_ref, compiled_ref)
                  -- kind: 'section' | 'block' | 'snippet' | 'layout'

-- Mutable merchant data
template_data     (theme_id, name, suffix, context_key, doc_json, updated_at)
                  -- context_key: null for base, else 'market:ca' | 'b2b' | 'exp:abc'
section_group_data(theme_id, name, context_key, doc_json, updated_at)
theme_settings    (theme_id, current_json, presets_json, platform_json)

-- Editor session state
theme_drafts      (theme_id, actor_id, patch_json, created_at)
```

Design decisions this implies:

- **Publish = pointer swap.** Publishing sets a store's active theme id. Editing the live theme mutates `*_data` rows in place with an autosave/undo stack; editing a draft theme is isolated.
- **Theme updates.** When a developer ships a new theme version, code rows are replaced and data rows are merged forward: keep values whose setting IDs still exist, drop the rest, backfill new defaults. The forgiving-read policy in §7 means an unmigrated data doc still renders.
- **Schema extraction is a build step.** Parse `{% schema %}` out of each component file on write, validate it, and store it separately. Never parse schemas at request time.
- **Cache keys.** Render cache should key on `(theme_version, template_name, context_key, resource_id, locale, market, customer_segment)`. Dynamic sources resolving at render time means you also need dependency-based invalidation on the underlying resources.
- **Context overlays as rows, not files.** One row per `(name, context_key)` beats file naming conventions once you have more than a couple of contexts.

---

## 13. What Shopify does *not* do (and where you can differentiate)

Honest assessment, since you're building a competitor rather than a clone:

- **Templates are per-page-type, not per-page.** Individual pages get variety through alternate templates assigned in admin, which is indirect. A per-page composition model (every page row owns its own section list) is simpler for merchants and is what most modern builders do. The cost is losing "change one template, update 500 products."
- **No component versioning at instance level.** Editing a section's code changes every instance everywhere, immediately. Consider pinning instances to component versions for large catalogs.
- **The editor is desktop-composition-only.** Responsive control is whatever settings the developer chose to expose. A first-class breakpoint model in the settings system (per-setting responsive values) is a genuine differentiator, though it complicates the schema and the panel UI considerably.
- **No layout primitives in the platform.** Grid/flex/spacing are per-theme conventions, so no two themes' sections compose predictably. A platform-level layout block set with standardized spacing tokens makes third-party sections interoperable.
- **Server-rendered only.** Great for SEO and TTFB; the partial-render API patches over the interactivity gap. If you go client-heavy instead, you must solve the editor's DOM-swap contract differently.
- **Setting types are closed.** Developers can't define custom setting types with custom editor controls. A plugin API for setting types would be genuinely useful — but note the tradeoff: it breaks the "editor UI is fully generic" property that makes the whole system maintainable.

---

## 14. Decisions to make before coding **[PORT]**

Record answers in `docs/decisions.md`; Claude Code should read them alongside this spec.

1. **Template language.** Options: (a) implement/adopt a Liquid-compatible engine — familiar to a large developer pool, sandboxed by design, and lets Shopify themes port with modest effort; (b) a JSX/component model — better DX, much harder to sandbox for third parties; (c) your own restricted DSL — most work. For a multi-tenant store builder, sandboxing is non-negotiable, which pushes hard toward (a). Whatever you pick: no arbitrary code execution, no filesystem/network access, hard render timeouts, and per-render memory caps.
2. **Section blocks: support or drop?** Recommendation: drop. Ship only file-based nestable blocks (§5.5).
3. **Composition granularity.** Per-page-type templates (Shopify) vs. per-page composition vs. both.
4. **Responsive model.** Per-setting breakpoint values, or developer-exposed responsive settings.
5. **Rendering strategy.** Server-rendered + partial-render API, islands, or SPA. Affects §8.3 heavily.
6. **Multi-tenancy of components.** Are sections/blocks per-theme (Shopify), or is there a shared platform library themes can pull from? A shared library needs namespacing and versioning from day one.
7. **Extension model.** Will third parties ship blocks? If yes, design `@app` equivalents and the wrapper-resolution rules (§10) now, not later.
8. **Data binding scope.** Which resources and which field types are bindable at launch (§6.6). Start with product/collection/page/article attributes + a custom-field system.
9. **Where custom CSS lives.** Shopify's platform-owned `custom_css` setting is a pragmatic escape hatch that keeps merchants out of theme code. Recommend replicating it.
10. **Theme distribution.** Single first-party theme, or a marketplace? A marketplace forces stricter schema validation, review tooling, and a linter (Shopify ships one) into the critical path.

---

## 15. Build order for Claude Code **[PORT]**

Each milestone is independently testable. Don't start the editor before M4 is solid.

**M1 — Template engine + component registry**
Sandboxed engine; parse `{% schema %}` out of section/block files; build a `type → {source, schema}` registry; validate schemas against Appendix A.
*Done when:* a section file with settings renders with hardcoded setting values.

**M2 — Data model + JSON template rendering**
Implement `render_section_list` and the section wrapper contract. Ordering, `disabled`, missing-type errors, forgiving setting reads.
*Done when:* a JSON template with three section instances renders in correct order with correct wrapper IDs.

**M3 — Blocks**
Theme blocks, `content_for 'blocks'`, nesting with depth cap, static blocks with params, targeting (`@theme`, explicit, `_`-private), `block_order`, `shopify_attributes` emission, `tag: null`.
*Done when:* a slideshow section with `_slide` children, one static controls block, and a nested group block renders correctly and round-trips through JSON.

**M4 — Layouts, section groups, settings, locales**
`content_for_layout`/`content_for_header`; section groups mounted from layouts; global settings schema/data; theme presets with presentational-only overwrite; locale files and the translation filter.
*Done when:* a complete two-page theme renders end to end, with editable header and footer.

**M5 — Editor: read-only preview + schema-driven panels**
Iframe host; generate setting panels from schemas (all basic types first); section/block tree sidebar; `visible_if`; dynamic block titles; selection highlighting via wrapper IDs.
*Done when:* every setting in a real theme is editable and saving produces valid JSON.

**M6 — Editor: composition**
Add/remove/reorder sections and blocks; presets and the pickers; `limit`, `max_blocks`, `enabled_on`/`disabled_on` enforcement; drag-and-drop; undo/redo; autosave and publish.

**M7 — Partial rendering + editor events**
`?sections=` and `?section_id=` endpoints with null-on-failure semantics; bundled section rendering on cart mutations; emit the `section:load/unload/select/...` event surface; design-mode flags.
*Done when:* changing one setting re-renders one section, not the page.

**M8 — Live preview**
CSS-custom-property strategy for colors; text-setting live patching with the §8.4 constraints; document the constraints for theme authors.

**M9 — Dynamic sources**
Resource attribute bindings, then custom fields; the compatibility matrix as data; binding UI in the settings panel; limits.

**M10 — Extension seam + hardening**
Third-party block support, wrapper resolution, asset hoisting, render timeouts, per-tenant caching, a schema linter, and the limits table from §11.

Testing strategy worth setting up at M2: a golden-file corpus of `(theme fixture, template data) → expected HTML`. Every milestone adds fixtures. It's the only way to refactor the renderer later without fear.

---

## 16. Glossary (binding terminology)

| Term | Definition |
|---|---|
| **Theme** | complete bundle of code + default data controlling a storefront's presentation |
| **Layout** | outermost HTML document wrapper; hosts repeated chrome and section groups |
| **Template** | per-page-type composition; JSON (ordered section instances) or code |
| **Alternate template** | named variant of a template type, assignable per resource |
| **Contextual template** | overlay file applying overrides for a market/segment |
| **Section group** | JSON container of sections mounted inside a layout (header/footer/aside/custom) |
| **Section** | reusable, configurable page module: markup + scoped assets + schema |
| **Section instance** | one placement of a section, with its own ID and setting values |
| **Block** | nestable sub-module of a section |
| **Theme block** | block defined as its own file, reusable across sections, nestable |
| **Section block** | block defined inline in a section's schema; local, flat |
| **App block** | block supplied by a third-party extension |
| **Static block** | block rendered at a fixed code position; configurable but not movable |
| **Private block** | `_`-prefixed theme block, excluded from wildcard acceptance |
| **Snippet** | parameterized partial with no schema and no editor presence |
| **Schema** | JSON manifest declaring a component's name, settings, accepted blocks, presets |
| **Setting** | typed, merchant-editable value declared in a schema |
| **Sidebar setting** | non-value informational element in a settings panel |
| **Preset** | pre-configured variant of a section/block shown in the add picker |
| **Theme preset** | a named set of global presentational setting values ("theme style") |
| **Dynamic source** | binding of a setting to a resource attribute or custom field |
| **Design mode** | the request state where the storefront renders inside the editor |
| **Partial render** | fetching rendered HTML for named sections instead of a full page |

---

## Appendix A — Machine-readable schemas

### A.1 JSON template

```jsonc
{
  "$id": "template.schema.json",
  "type": "object",
  "required": ["sections", "order"],
  "properties": {
    "layout":  { "oneOf": [{ "type": "string" }, { "const": false }] },
    "wrapper": { "type": "string", "description": "div|main|section with optional #id.class[attr=val]" },
    "sections": {
      "type": "object",
      "propertyNames": { "pattern": "^[A-Za-z0-9_-]+$" },
      "additionalProperties": { "$ref": "#/$defs/sectionInstance" },
      "minProperties": 1, "maxProperties": 25
    },
    "order": { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
    "context": { "type": "object", "properties": {
        "market": { "type": "string" }, "b2b": { "type": "boolean" } } },
    "parent":  { "type": "string" }
  },
  "$defs": {
    "sectionInstance": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": { "type": "string" },
        "disabled": { "type": "boolean", "default": false },
        "settings": { "type": "object" },
        "blocks": { "type": "object",
          "additionalProperties": { "$ref": "#/$defs/blockInstance" }, "maxProperties": 50 },
        "block_order": { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
        "custom_css": { "type": "string", "readOnly": true }
      }
    },
    "blockInstance": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": { "type": "string" },
        "static": { "type": "boolean" },
        "disabled": { "type": "boolean" },
        "settings": { "type": "object" },
        "blocks": { "type": "object",
          "additionalProperties": { "$ref": "#/$defs/blockInstance" } },
        "block_order": { "type": "array", "items": { "type": "string" } }
      }
    }
  }
}
```

### A.2 Section group

```jsonc
{
  "$id": "section-group.schema.json",
  "type": "object",
  "required": ["type", "name", "sections", "order"],
  "properties": {
    "type": { "type": "string",
      "pattern": "^(header|footer|aside|custom\\.[a-z0-9_-]+)$" },
    "name": { "type": "string", "maxLength": 50 },
    "sections": { "$ref": "template.schema.json#/properties/sections" },
    "order":    { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
    "context":  { "type": "object" },
    "parent":   { "type": "string" }
  }
}
```

### A.3 Section schema (`{% schema %}` in a section file)

```jsonc
{
  "$id": "section-schema.json",
  "type": "object",
  "required": ["name"],
  "properties": {
    "name":  { "type": "string" },
    "tag":   { "enum": ["article","aside","div","footer","header","section"] },
    "class": { "type": "string" },
    "limit": { "enum": [1, 2] },
    "settings": { "type": "array", "items": { "$ref": "setting.schema.json" } },
    "blocks": { "type": "array", "items": {
      "oneOf": [
        { "type": "object", "required": ["type"],
          "properties": { "type": { "enum": ["@theme", "@app"] } } },
        { "type": "object", "required": ["type"],
          "properties": { "type": { "type": "string" } } },
        { "type": "object", "required": ["type","name"],
          "description": "locally-defined section block",
          "properties": {
            "type": { "type": "string" }, "name": { "type": "string" },
            "limit": { "type": "integer" },
            "settings": { "type": "array", "items": { "$ref": "setting.schema.json" } } } }
      ] } },
    "max_blocks": { "type": "integer", "maximum": 50 },
    "presets": { "type": "array", "items": { "$ref": "#/$defs/preset" } },
    "default": { "$ref": "#/$defs/preset" },
    "locales": { "type": "object",
      "additionalProperties": { "type": "object", "additionalProperties": { "type": "string" } } },
    "enabled_on":  { "$ref": "#/$defs/availability" },
    "disabled_on": { "$ref": "#/$defs/availability" }
  },
  "not": { "required": ["enabled_on", "disabled_on"] },
  "$defs": {
    "preset": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "category": { "type": "string" },
        "settings": { "type": "object" },
        "blocks": { "type": "array", "items": { "$ref": "#/$defs/presetBlock" } }
      }
    },
    "presetBlock": {
      "type": "object", "required": ["type"],
      "properties": {
        "type": { "type": "string" }, "name": { "type": "string" },
        "id": { "type": "string", "description": "required when static" },
        "static": { "type": "boolean" },
        "settings": { "type": "object" },
        "blocks": { "type": "array", "items": { "$ref": "#/$defs/presetBlock" } }
      }
    },
    "availability": {
      "type": "object", "minProperties": 1,
      "properties": {
        "templates": { "type": "array", "items": { "type": "string" } },
        "groups":    { "type": "array", "items": { "type": "string" } }
      }
    }
  }
}
```

### A.4 Theme block schema (`{% schema %}` in a block file)

```jsonc
{
  "$id": "block-schema.json",
  "type": "object",
  "required": ["name"],
  "properties": {
    "name": { "type": "string" },
    "settings": { "type": "array", "items": { "$ref": "setting.schema.json" } },
    "blocks": { "type": "array", "items": {
      "type": "object", "required": ["type"],
      "properties": { "type": { "type": "string",
        "description": "@theme | @app | explicit block type. Local defs NOT allowed." } } } },
    "presets": { "type": "array",
      "items": { "$ref": "section-schema.json#/$defs/preset" } },
    "tag":   { "oneOf": [{ "type": "string", "maxLength": 50 }, { "type": "null" }] },
    "class": { "type": "string" }
  }
}
```

### A.5 Setting

```jsonc
{
  "$id": "setting.schema.json",
  "type": "object",
  "required": ["type"],
  "properties": {
    "type": { "enum": [
      "header","paragraph",
      "checkbox","number","radio","range","select","text","textarea",
      "article","article_list","blog","collection","collection_list",
      "color","color_background","color_palette","color_scheme","color_scheme_group",
      "font_picker","html","image_picker","inline_richtext","link_list","liquid",
      "metaobject","metaobject_list","page","product","product_list","richtext",
      "text_alignment","url","video","video_url" ] },
    "id":      { "type": "string" },
    "label":   { "type": "string" },
    "info":    { "type": "string" },
    "content": { "type": "string", "description": "sidebar settings only" },
    "default": {},
    "visible_if": { "type": "string" },
    "placeholder": { "type": "string" },
    "options": { "type": "array", "items": {
      "type": "object", "required": ["value","label"],
      "properties": { "value": {"type":"string"}, "label": {"type":"string"},
                      "group": {"type":"string"} } } },
    "min":  { "type": "number" }, "max": { "type": "number" },
    "step": { "type": "number" }, "unit": { "type": "string" },
    "limit": { "type": "integer", "maximum": 50 },
    "metaobject_type": { "type": "string" },
    "accept": { "type": "array", "items": { "enum": ["youtube","vimeo"] } },
    "definition": { "type": "array", "items": { "$ref": "#" },
                    "description": "color_scheme_group only" },
    "role": { "type": "object", "description": "color_scheme_group preview mapping" }
  },
  "allOf": [
    { "if": { "properties": { "type": { "const": "range" } }, "required": ["type"] },
      "then": { "required": ["id","label","min","max","default"] } },
    { "if": { "properties": { "type": { "const": "font_picker" } }, "required": ["type"] },
      "then": { "required": ["id","label","default"] } },
    { "if": { "properties": { "type": { "enum": ["radio","select"] } }, "required": ["type"] },
      "then": { "required": ["id","label","options"] } },
    { "if": { "properties": { "type": { "enum": ["metaobject","metaobject_list"] } }, "required": ["type"] },
      "then": { "required": ["id","label","metaobject_type"] } },
    { "if": { "properties": { "type": { "const": "video_url" } }, "required": ["type"] },
      "then": { "required": ["id","label","accept"] } },
    { "if": { "properties": { "type": { "const": "color_palette" } }, "required": ["type"] },
      "then": { "required": ["id","default"] } }
  ]
}
```

### A.6 Global settings data

```jsonc
{
  "$id": "settings-data.schema.json",
  "type": "object",
  "required": ["current", "presets"],
  "properties": {
    "current": { "type": "object" },
    "presets": { "type": "object", "maxProperties": 5,
                 "additionalProperties": { "type": "object" } },
    "platform_customizations": {
      "type": "object", "readOnly": true,
      "properties": { "custom_css": { "type": "string" } } }
  }
}
```

---

## Appendix B — Worked example: a nestable slideshow

Shows targeting, private blocks, nesting, static blocks, and presets in one artifact.

```liquid
{%- comment -%} sections/slideshow.liquid {%- endcomment -%}
<div class="slideshow" data-autoplay="{{ section.settings.autoplay }}">
  <div class="slideshow__track">
    {% content_for 'blocks' %}
  </div>

  {% if section.blocks.size > 1 %}
    {% content_for "block", type: "_slideshow-controls", id: "controls" %}
  {% endif %}
</div>

{% stylesheet %}
  .slideshow { position: relative; overflow: hidden; }
  .slideshow__track { display: flex; }
{% endstylesheet %}

{% schema %}
{
  "name": "Slideshow",
  "tag": "section",
  "class": "slideshow-section",
  "max_blocks": 8,
  "blocks": [{ "type": "_slide" }, { "type": "_slideshow-controls" }],
  "settings": [
    { "type": "header",   "content": "Behaviour" },
    { "type": "checkbox", "id": "autoplay", "label": "Auto-advance", "default": true },
    { "type": "range",    "id": "interval", "label": "Interval", "min": 2, "max": 10,
      "step": 1, "unit": "s", "default": 5,
      "visible_if": "{{ section.settings.autoplay }}" }
  ],
  "presets": [
    { "name": "Slideshow", "category": "Media",
      "blocks": [{ "type": "_slide" }, { "type": "_slide" }] }
  ],
  "enabled_on": { "templates": ["*"] }
}
{% endschema %}
```

```liquid
{%- comment -%} blocks/_slide.liquid — private: only reachable by explicit reference {%- endcomment -%}
<div class="slide" style="--slide-bg: {{ block.settings.background }};">
  {% if block.settings.image %}
    {{ block.settings.image | image_url: width: 2048 | image_tag: loading: 'lazy' }}
  {% endif %}
  <div class="slide__content">
    {% content_for 'blocks' %}
  </div>
</div>

{% schema %}
{
  "name": "Slide",
  "blocks": [{ "type": "@theme" }, { "type": "@app" }],
  "settings": [
    { "type": "image_picker",     "id": "image",      "label": "Image" },
    { "type": "color_background", "id": "background", "label": "Background",
      "default": "{{ settings.colors.primary }}" }
  ],
  "presets": [{ "name": "Slide" }]
}
{% endschema %}
```

```liquid
{%- comment -%} blocks/text.liquid — public: offered wherever @theme is accepted {%- endcomment -%}
<div class="text-block text-{{ block.settings.alignment }}">
  {{ block.settings.text }}
</div>

{% schema %}
{
  "name": "Text",
  "settings": [
    { "type": "richtext",       "id": "text",      "label": "Text" },
    { "type": "text_alignment", "id": "alignment", "label": "Alignment", "default": "center" }
  ],
  "presets": [
    { "name": "Text" },
    { "name": "Intro copy", "settings": { "text": "<p>Welcome to the store.</p>" } }
  ]
}
{% endschema %}
```

Resulting data after a merchant adds the preset and drops a text block into slide 1:

```jsonc
{
  "sections": {
    "hero": {
      "type": "slideshow",
      "settings": { "autoplay": true, "interval": 5 },
      "blocks": {
        "s1": { "type": "_slide", "settings": {},
                "blocks": { "t1": { "type": "text",
                                    "settings": { "text": "<p>New season</p>",
                                                  "alignment": "center" } } },
                "block_order": ["t1"] },
        "s2": { "type": "_slide", "settings": {}, "blocks": {}, "block_order": [] },
        "controls": { "type": "_slideshow-controls", "static": true, "settings": {} }
      },
      "block_order": ["s1", "s2"]
    }
  },
  "order": ["hero"]
}
```

Note: `controls` carries `static: true` and is absent from `block_order`; its position comes from the section's code.

---

## Appendix C — Implementation checklist

Renderer
- [ ] Sandboxed template engine, render timeout, memory cap
- [ ] Schema extraction and validation at write time
- [ ] Component registry keyed by filename
- [ ] Section wrapper IDs and classes
- [ ] Block wrapper IDs, `tag: null` handling, editor data attributes
- [ ] Recursive block rendering with depth cap
- [ ] Static block resolution with arbitrary params
- [ ] Forgiving setting resolution (unknown ignored, missing → default)
- [ ] No markup emitted between sections
- [ ] Scoped CSS/JS collection, dedupe, and hoisting
- [ ] Context overlay merge (market / segment / experiment)
- [ ] Layout resolution incl. `layout: false`
- [ ] Section groups mounted from layouts

Settings
- [ ] Type registry: control + validator + runtime resolver per type
- [ ] Sidebar (non-value) types
- [ ] `visible_if` expression evaluator (total, side-effect-free)
- [ ] Preset-switch immunity for content settings
- [ ] Presentational-settings list for theme presets
- [ ] Palette references and delete-with-replacement rewriting
- [ ] Image focal points → `object-position`

Editor
- [ ] Schema-driven panel generation
- [ ] Section/block tree with dynamic titles
- [ ] Add pickers driven by presets, categories, targeting, limits
- [ ] Drag-reorder, hide (`disabled`), remove, duplicate
- [ ] Static blocks: hideable, not movable, "may be hidden" cue
- [ ] Undo/redo, autosave, publish
- [ ] Event surface into the preview iframe
- [ ] Design-mode / inspect-mode / preview-mode flags
- [ ] Live preview for colors and text with documented constraints
- [ ] Preset thumbnail rendering in preview mode

Platform
- [ ] Partial-render endpoint (multi + single), null-on-failure
- [ ] Bundled section rendering on cart mutations
- [ ] Locale-aware URL construction
- [ ] Theme versioning, publish-as-pointer-swap, forward data merge
- [ ] Extension seam (`@app` equivalent) and wrapper resolution
- [ ] Limits enforced with clear errors
- [ ] Schema linter for theme developers
- [ ] Golden-file render test corpus

---

## Appendix D — Sources

All read from `shopify.dev` (July 2026):

- Theme architecture overview — `/docs/storefronts/themes/architecture`
- Layouts — `/architecture/layouts`
- Templates — `/architecture/templates`; JSON templates — `/architecture/templates/json-templates`; alternate templates — `/architecture/templates/alternate-templates`
- Sections — `/architecture/sections`; section schema — `/architecture/sections/section-schema`
- Section groups — `/architecture/section-groups`
- Blocks overview — `/architecture/blocks`; theme blocks quick start — `/architecture/blocks/theme-blocks/quick-start`; block schema — `/architecture/blocks/theme-blocks/schema`; targeting — `/architecture/blocks/theme-blocks/targeting`; static blocks — `/architecture/blocks/theme-blocks/static-blocks`; app blocks — `/architecture/blocks/app-blocks`; AI-generated blocks — `/architecture/blocks/ai-generated-theme-blocks`
- Snippets — `/architecture/snippets`
- Settings — `/architecture/settings`; input settings — `/architecture/settings/input-settings`; dynamic sources — `/architecture/settings/dynamic-sources`
- Config — `/architecture/config/settings-data-json`
- Locales — `/architecture/locales`
- Theme editor — `/tools/online-editor`; editor integration — `/best-practices/editor/integrate-sections-and-blocks`
- Section Rendering API — `/docs/api/ajax/section-rendering`

Shopify's docs change frequently; re-check the settings type catalog (§6.4) and limits (§11) before locking your schema, and note that Shopify publishes a `.md` variant of most doc pages (append `.md` to the URL) which is far easier to re-ingest programmatically.
