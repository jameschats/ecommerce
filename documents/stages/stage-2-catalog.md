# Stage 2 — Catalog

**Goal:** the product catalog admins manage and customers browse.

## Scope & checklist
### Backend core (✅ done)
- [x] Categories (tree via ParentCategoryId, auto unique slugs) — CRUD
- [x] Brands — CRUD
- [x] Products — CRUD (price, HSN, status, featured, soft-delete) + images
- [x] Product images (primary + gallery; `MediaFileId` link ready)
- [x] Public read endpoints (browse with search/filter/sort + paging, detail by id/slug) via `PagedResult<T>`
- [x] Admin endpoints role-protected; verified end-to-end against MySQL

### Variants & attributes (✅ done)
- [x] Product variants + variant options (Size/Color/Capacity) — CRUD under `/api/admin/products/{id}/variants`
- [x] Attribute definitions + values — CRUD under `/api/admin/attributes`
- [x] Per-product attribute assignment — `GET/PUT /api/admin/products/{id}/attributes`
- [x] Variants + attributes included in product detail (public + admin)

### Bulk Import/Export (✅ core done)
- [x] **Excel import** (`.xlsx`, upsert by SKU) with `ImportJobs`/`ImportJobItems` tracking + per-row error messages
- [x] **Excel export** + downloadable **template**; export→edit→import roundtrip
- [x] **Dynamic attribute columns** — unknown columns auto-map to attributes (auto-created if new)
- [x] Auto-create brand by name; clear row errors ("Category not found", "Invalid price")
- [ ] Image **ZIP** upload (SKU→file) — *deferred: needs blob storage / Media Library*. Import supports image **URLs** now.

### Storefront UI + SEO (✅ done — Angular SSR)
- [x] **Angular SSR enabled** (on-demand server rendering; `security.allowedHosts` configured for SSRF guard)
- [x] App made **SSR-safe** (localStorage/document/GSI guarded with `isPlatformBrowser`)
- [x] **Home**: category strip + featured + new-arrivals product rails
- [x] **PLP** (`/products`, `/category/:slug`): search + brand + sort filters, category sidebar, pagination, product cards
- [x] **PDP** (`/product/:slug`): image gallery, price/compare-at, variants, specs (attributes), description
- [x] **SEO** (verified in server-rendered HTML): per-page title/meta + Open Graph/Twitter + canonical; **JSON-LD** — Product + BreadcrumbList (PDP), WebSite + SearchAction + Organization (home)
- [ ] SEO override fields per product/category (`017_*`: MetaTitle/MetaDescription/OgImageUrl) — *deferred; currently falls back to name / short description / primary image*

### Admin catalog management UI (✅ done)
- [x] Admin shell/layout (sidebar nav, role-guarded under `/admin`) + nested routes
- [x] **Products**: list (search + paging) + create/edit form with images, **variants** (options), and **specifications** (attribute values)
- [x] **Categories** manager (list + form, parent select)
- [x] **Brands** manager (list + form)
- [x] **Attributes** manager (definitions + values)
- [x] **Import/Export** screen (upload `.xlsx`, per-row error report, export + template download)

### Deferred (not blocking Stage 2)
- [ ] SEO override fields per product/category (`017_*`: MetaTitle/MetaDescription/OgImageUrl) — falls back to name/short-desc/image
- [ ] Image **ZIP** upload (needs blob storage / Media Library) — URLs supported today
- [ ] "Add to cart" wiring — belongs to **Cart (Stage 4)**

## Tables (exist)
`Categories, Brands, Products, ProductImages, ProductVariants, VariantOptions, Attributes, AttributeValues, ProductAttributeValues, ImportJobs, ImportJobItems`

## Dependencies
Media Library (images), Settings (catalog flags). Depends on Stage 0/1.

## Notes
- Generic, reusable **Import Engine** — V1 covers Products + Inventory; later Customers/Coupons/etc.
- Product search lives in Stage 3 (uses MySQL FULLTEXT index already on `Products`).

**Status:** ✅ Done — backend + storefront UI (SSR/SEO) + admin catalog management UI. SEO override fields, image-ZIP, and add-to-cart deferred (documented above).
