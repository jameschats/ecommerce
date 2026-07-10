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
