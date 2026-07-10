# V2 — Storefront Theme Engine "Online Store 2.0" (S1–S7)

**What this is.** The detailed, phased plan for the **store / storefront itself** and the **theme editor that builds
it** — matching Shopify's Online-Store-2.0 structural depth (theme library + templates-by-page-type +
Header/Footer/Announcement section-groups + sections/blocks), in **our** design language. It is the **storefront
half** of the [V2-6 "Win-a-Merchant"](v2-stage-6-win-a-merchant.md) work and the structural successor to the
shipped **Storefront Builder P1–P5**. It is **separate** from the [merchant-admin plan](v2-merchant-admin-plan.md)
(M1–M9) and from the marketing engine.

It also **supersedes/extends the thin [V2-4 Per-Tenant Storefront](v2-stage-4-storefront.md)** doc (which covered
subdomain resolution + per-tenant theme/SEO — those foundations are ✅; this plan adds the theme *engine* on top).

---

## Status at a glance

| Phase | Title | Status |
|---|---|---|
| **P1–P5** | **Storefront Builder** (precursor foundation): sections-and-blocks backend, data-driven `/pages/:slug`, visual drag-drop builder, live preview, data-driven home, industry presets | ✅ `111a151` `bcf7b21` `baeed0a` `84787ed` |
| **S1** | Model & rendering skeleton (theme library data model, bundle read endpoints, group section types; migrate Home → theme `index`) | ✅ `273d55a` |
| **S2** | Store layout shell (data-driven Header/Announcement/Footer + theme-settings CSS vars) | ✅ `8e6c23e` (announcement zone + header/footer settings + CSS vars; full section-composed header/footer → S3/S4) |
| **S3** | Dynamic templates (product/collection/search/cart → section-composed) — **the big one** | ✅ product `d476dd5` · collection/search `fb8218a` · cart `2a89d36` |
| **S4** | Theme editor authoring (template picker + zones + per-template section CRUD + draft-preview) | ✅ `d04605e` (API) · `d5a6d16` (editor UI) |
| **S5** | Theme library + publish (multi-theme CRUD, duplicate, preview token, atomic publish/rollback) | ✅ `849f319` (API) · `0005928` (library UI) |
| **S6** | Prebuilt themes (author 5–10 free bundles + install flow + thumbnails) | ✅ `9b658a4` (6 themes) |
| **S7** | Checkout/account branding + polish (announcement bar, 404/password templates) | ✅ `76c6d88` |

**Legend:** ✅ done · 🟡 in progress · ⬜ not started

---

## Context
Today the store look = one per-tenant theme (CSS-var settings) + a section builder covering **Home + custom pages**.
Product/collection/cart/search/account are **hardcoded Angular components**; header/footer are **fixed app chrome**;
there's **one theme, no draft/publish**. To match Shopify's structural depth (the user's priority) we introduce a
proper **theme engine**: a **theme library** (Draft/Published) where each theme bundles **templates-by-page-type** +
**Header/Footer/Announcement section-groups** + **global settings**, and **every** storefront page (incl.
product/collection/cart/search) is **section-composed** and merchant-arrangeable.

## Confirmed decisions
- **Full OS-2.0 depth**: product/collection/cart/search become fully section-composed (dynamic sections reorderable).
- **Checkout**: stays app-rendered, inherits theme branding (Shopify does the same). Not section-editable.
- **Theme library**: multiple themes, exactly one **Published**, rest **Draft**; duplicate/preview/publish/rename/delete.
- **5–10 free prebuilt themes** (seeded bundles) — **no paid marketplace**.
- **Account pages**: app-rendered + theme-branded. **Blog/articles**: later stage.

## Target data model
New migration `141_theme_engine.sql` (V2 band ≥141; every new table `ITenantScoped` — `TenantId` + auto-stamp + query filter):
```
Theme  (extend existing `Themes`: + Status ENUM('Draft','Published'), + Settings JSON [global: colors, typography,
        buttons, layout, favicon, socials, checkout-branding], + Source [prebuilt key], + PreviewToken)
  └─ ThemeTemplate  (NEW)  { ThemeTemplateId, TenantId, ThemeId, TemplateKey, Name }
        TemplateKey ∈ index · product · collection · list-collections · cart · search · 404 · password · account
                     · header · footer · announcement   (header/footer/announcement = shared "section groups")
        unique (ThemeId, TemplateKey, Name)
       └─ ThemeSection  (NEW = today's PageSection generalised to a template instead of a page)
             { ThemeSectionId, TenantId, ThemeTemplateId, SectionType, Title?, Settings JSON, Blocks JSON,
               DisplayOrder, IsVisible, StartsAt?, EndsAt? }
```
- **Custom content pages stay as-is** (`Pages`/`PageSections`) — theme-independent tenant content; they render
  inside the published theme's chrome. Switching theme keeps your About page.
- **Home/index becomes theme-owned** — migrate the current `Page(slug=home)` sections into the Published theme's
  `index` template (different themes = different home layouts, the whole point).
- Exactly **one Published theme per tenant** (enforced in service; publish = txn swap Draft→Published / prev→Draft).
- Reuse existing JSON `Settings`/`Blocks` + `SectionTypeRegistry` + `HtmlSanitizer` machinery unchanged.

## Section-type registry — expand (`Features/Cms/SectionTypes/SectionTypeRegistry.cs`)
Keep the 7 **static** types (Hero, RichText, ImageWithText, Testimonials, Cta, FeaturedProducts, Categories). Add:
- **Group sections**: `AnnouncementBar`, `Header` (logo/nav/search/cart + menu blocks), `Footer` (columns/links/socials).
- **Dynamic template sections** (bind to route context; platform-owned data-binding, merchant configures via
  settings): `ProductGallery`, `ProductInfo` (title/price/variants/qty/add-to-cart), `ProductDescription`,
  `ProductReviews`, `RelatedProducts`, `Breadcrumbs`, `CollectionHeader`, `CollectionGrid` (columns/filters/sort),
  `CollectionsList`, `CartItems`, `CartSummary`, `SearchBar`, `SearchResults`, `EmptyState`.
- Each section-type gains `Scope` (`any` | valid template keys) + `Kind` (`static` | `dynamic` | `group`).

## Backend (`Features/Storefront/` new slice, or extend `Features/Cms/` + `Features/Theme/`)
- **Theme library service**: list / create-from-prebuilt(install) / duplicate / rename / delete; `GetPublishedBundle`
  (settings + header/footer/announcement groups); `GetTemplate(themeId,key)`; publish(txn); draft preview by token.
- **Public storefront endpoints** (anonymous, tenant-scoped, cacheable):
  - `GET /api/storefront/theme` → published theme settings + section-groups (loaded once at app init; replaces `/api/theme`).
  - `GET /api/storefront/template/{key}` → the published theme's section list for a page-type.
  - `?preview={token}` → serve a **Draft** theme instead of Published (admin-gated, tenant-scoped).
  - Custom pages keep `GET /api/cms/pages/{slug}`.
- **Admin authoring endpoints**: theme CRUD + per-(theme,template) section CRUD/reorder/duplicate; theme settings PUT;
  install-prebuilt; publish; duplicate. Sanitize richtext on write.

## Frontend (`ecomm.web`)
- **Store layout shell**: replace fixed `app.html` header/footer with data-driven Header/Announcement/Footer from the
  published theme's section-groups (fallback to current chrome if absent, so nothing breaks mid-migration). Theme
  settings → CSS vars (extend `ThemeService.apply`; add typography/layout/favicon).
- **Template rendering + dynamic sections**: a **StorefrontContext** service holds the current route entity
  (product/collection/cart/search). Each route loads (a) its template sections via `/storefront/template/{key}` and
  (b) entity data via existing catalog/cart endpoints (SSR resolvers), then renders through the section-renderer
  registry (extend `storefront-section.component.ts`). Static sections render from settings/blocks; dynamic sections read context.
- **Refactor hardcoded pages → dynamic section components** (the heavy lift): extract `product-detail`,
  `product-list`, `cart`, search logic into `ProductGallery/ProductInfo/AddToCart/…`, `CollectionGrid/Header`,
  `CartItems/CartSummary`, `SearchResults` — behaviour preserved (re-homed, not rewritten), behind the same services.
- **Theme editor**: evolve `features/admin/builder/admin-builder.component.ts` into a full editor — template picker
  dropdown, Header/Template/Footer zones, per-zone section add/reorder/configure, draft-preview iframe, theme-settings panel.
- **Theme library screen**: grid of themes (thumbnail, Published/Draft) with install/duplicate/preview/publish/rename/delete.
- **Checkout & account**: keep components; ensure theme branding applies; add a couple of checkout-branding settings.

## Prebuilt free themes (5–10)
Author each as a **seed bundle** (JSON): theme settings + template layouts (index/product/collection/cart/search/
404/password/account) + header/footer/announcement groups. Categories: Fashion, Electronics, Grocery, Beauty,
Home & Living, Jewelry, General/Minimal (+2 optional). "Install" copies a bundle into the tenant's library as a
Draft. Ship as `database/migrations/14x_prebuilt_themes.sql` seed data + a `PrebuiltThemeRegistry` (mirrors today's
`StorefrontPresets`, but full theme bundles). The current 4 presets fold in as `index`-template starters.

## Phasing (each phase builds + `dotnet test` green, committed; storefront never breaks — fallback chrome until cutover)
- **S1 — Model & rendering skeleton.** ✅ Done as migration **169** (not 141 — that number was long taken; V2 band
  is now at 169). `ThemeTemplates`/`ThemeSections` + `Themes.Status/Source/PreviewToken`; `SectionTypeRegistry`
  Kind/Scope + group + dynamic section types; `StorefrontThemeService` (bundle + per-template reads) at
  `GET /api/storefront/{theme,template/{key},section-types}`. Home→`index` is a **read-only fallback** plus an
  idempotent `BackfillIndexFromHome` (rather than a destructive SQL migration) so the storefront never breaks. No UI change.
  **Global theme settings stay in the existing `ThemeSettings` key/value store** (not duplicated into a `Themes.Settings` JSON column).
- **S2 — Store layout shell.** ✅ Storefront loads the theme bundle (`/api/storefront/theme`) and drives chrome from
  its zones with a fallback to the built-in chrome: new **announcement bar** (data-driven `announcement` section,
  SSR-safe rotation); header honours **Header-zone settings** (sticky / show-search / show-cart); footer copyright
  from the **Footer zone**; richer theme **CSS vars** (font, heading font, base size, container width, favicon) wired
  into `body`/`.page-container` with current-look fallbacks. *(Full section-composed header/footer markup replacement
  is deferred to S3/S4 when the editor can author those zones.)*
- **S3 — Dynamic templates (the big one).** ✅ Done. Every standard storefront page is section-composed via one
  pattern: `ThemeService.getTemplate(key)` + a page-scoped store (logic lifted 1:1 from the old component) + thin
  section components + a host that renders the theme's template or a built-in default section order (so behaviour is
  identical until a theme authors one). Old page components removed (git history = rollback; prod deploys are manual).
  - **Product ✅** (`d476dd5`): Breadcrumbs · **ProductInfo** (combined gallery+info two-column, chosen Shopify-style
    model → preserves the exact layout) · Description · Reviews. `ProductPageStore`.
  - **Collection/search ✅** (`fb8218a`): our `product-list` serves `/products`, `/category/:slug` and search
    (`?search=`), so they collapse into one page — CollectionHeader · CollectionGrid. `CollectionPageStore`.
  - **Cart ✅** (`2a89d36`): CartItems · CartSummary over `CartPageStore`; all mutations/totals stay in `CartService`.
    Heading, empty state and two-column layout host-owned to preserve the exact look.
- **S4 — Theme editor authoring.** ✅ `/admin/theme-editor` — grouped template picker (Header/Templates/Footer),
  drag-reorder section list per template with add/configure/duplicate/hide/delete, schema-driven settings+blocks
  form, live storefront preview iframe. Backend `ThemeAuthoringService` + `/api/admin/theme/*` enforces section
  `Scope` and sanitizes richtext. Edits the tenant's theme directly — **true draft-preview + publish is S5**. The
  existing theme-settings panel (colors/font/logo at `/admin/theme`) stays as-is for now.
- **S5 — Theme library + publish.** ✅ `/admin/themes` — many themes, exactly one **Published** (live), rest Draft:
  create / duplicate (deep copy of settings+templates+sections) / rename / publish (atomic swap, one txn) / delete
  (blocked on the live theme). Each theme has a **PreviewToken**; the editor targets a theme by id and its preview
  iframe renders that theme via `?preview={token}`, and the storefront (`ThemeService`) reads `?preview=` to serve a
  Draft before it's live. **Note:** theme *global settings* (colors/font/logo at `/admin/theme`) still target the
  active theme — per-draft settings editing folds in with the S7 theme-settings panel.
- **S6 — Prebuilt themes.** ✅ `PrebuiltThemeRegistry` — 6 free bundles (Minimal, Boutique, Circuit, Fresh, Bloom,
  Haven) across categories; each = palette/typography/button settings + a home layout + Header/Footer/Announcement
  zones from the central section catalog. Install (`POST /api/admin/themes/install`) copies a bundle in as a Draft;
  library shows a "Start from a free theme" grid with palette-gradient thumbnails. (Thumbnails are palette gradients,
  not screenshots — real preview is one click via the draft preview token.)
- **S7 — Checkout/account branding + polish.** ✅ Per-theme **settings panel** in the editor (palette/typography/
  buttons/logo/favicon/width, draft-safe, live preview) — closes the S5 gap. Themed **404** page (`NotFoundComponent`
  renders the `404` template's EmptyState + static sections; wildcard route serves a real 404 instead of redirecting
  home). Checkout/account/password inherit theme colours + fonts via the global CSS vars; the **announcement bar**
  went live in S2. **The theme engine (S1–S7) is complete.**

## Risks / decisions to watch
- **Dynamic-section refactor risk (S3)**: product/cart logic is business-critical — extract behind the same services
  (`CatalogService`, `CartService`, `OrderService`), keep component tests, verify add-to-cart/variant/coupon flows
  end-to-end after each extraction.
- **SSR + per-tenant caching**: template layouts are cacheable per tenant+theme; wire tenant-namespaced cache in S5
  (ties to the deferred Redis output cache — do NOT reuse the buggy global output cache fixed in `5b5cef7`).
- **Preview security**: the draft-preview token must be admin-gated and tenant-scoped.
- **Migration safety**: keep `Pages`/`PageSections` for custom pages; only Home moves into the theme.

## Verification
- **Unit** (`ecomm.tests`): theme publish swaps exactly one Published; install-prebuilt clones a full bundle; section
  CRUD on a template is tenant-isolated; richtext sanitized; dynamic-section settings validated.
- **E2E (tenant subdomains)**: install a prebuilt theme → edit its Product template (reorder gallery/info, add a
  RichText below) → preview draft → publish → storefront product/collection/cart/home render the theme; add-to-cart/
  variants/filters/coupons still work; a second tenant is isolated; switching theme changes layout but keeps custom
  pages + catalog.

## Scope boundary
This plan = **the store / storefront + the theme-editor that builds it**. The rest of the merchant admin is the
separate [merchant-admin plan](v2-merchant-admin-plan.md). The marketing engine is its own later plan.
