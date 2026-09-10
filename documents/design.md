# E-Commerce Platform — Design Document

> **Project codename:** "Mini Flipkart" — a mid-level e-commerce platform built as a master system-design project.
> **Author/Architect:** James (.NET full-stack, Technical Architect)
> **Status:** V1 design — approved baseline (revised after scope review)
> **Source:** Distilled from the design conversation in `design-chat.pdf`, plus an architecture review of V1 launch-readiness.

---

## 1. Overview

A production-ready, mid-sized e-commerce platform — more complex than a Shopify store, simpler than Flipkart/Amazon. It is built as a serious learning-grade architecture project that can evolve from a single-seller store into a full marketplace without painful rewrites.

### Reference products
| Tier | Examples | What we learn from them |
|------|----------|--------------------------|
| Primary | Nykaa, FirstCry, Lenskart | Catalog, search, cart, orders, payments, inventory, recommendations at million-user scale |
| Secondary | Myntra, Meesho | Product listing, offers, seller management, order tracking |
| Advanced (later only) | Flipkart, Amazon | Hundreds of microservices, event streaming, ML recommendations, multi-region — **deliberately out of scope** |

### Goals
- Ship a **production-ready Version 1** (single seller, single store).
- Lay a **database foundation designed for Version 3** (marketplace) so no schema rewrites are needed later.
- Cover the full breadth of modern backend architecture concepts (auth, catalog, search, cart, checkout, payments, billing, shipping, CMS, audit).

### Non-goals (V1)
- Microservices, Kafka/event streaming, Elasticsearch, multi-region — all deferred to later phases.
- ML recommendations, fraud detection, advanced logistics, multi-tenant SaaS, marketplace seller flows — DB designed for them, **not implemented**.

---

## 2. Versioning Strategy (the core principle)

> **Design the database for Version 3. Implement functionality for Version 1.**

| Version | Product shape | Build status |
|---------|---------------|--------------|
| **V1** | Single Seller, Single Store | **Implement now** — production ready |
| **V2** | Multi-Tenant SaaS | DB ready; not implemented |
| **V3** | Full Marketplace (many sellers) | DB ready; not implemented |

This means: marketplace/tenant/seller tables are **created from Day 1 but left unused** in V1, avoiding a costly redesign when the platform evolves.

---

## 3. Scale Targets

| Metric | V1 (build for this) | V2 | V3 (design DB for this) |
|--------|--------------------:|---:|------------------------:|
| Registered users | 100,000 | 1,000,000 | 1,000,000+ |
| Daily active users | 10,000 | — | — |
| Concurrent users | 500 | — | — |
| Products | 100,000 | 1,000,000 | 1,000,000+ |
| Sellers | 1,000 (data only) | 10,000 | 10,000+ |
| Orders / day | 2,000 | 20,000 | 20,000+ |

V2+ introduces Redis (heavily), a message queue, a search engine, and a CDN as scale demands.

---

## 4. Technology Stack

| Layer | V1 Choice | Later phases |
|-------|-----------|--------------|
| **Frontend** | Angular 21 + **Tailwind CSS** (`ecomm.web`) | — |
| **Backend** | ASP.NET Core **.NET 9** Web API (`ecomm.api`) | — |
| **Database** | **MySQL** | Read replicas |
| **DB migrations** | Versioned raw SQL — `database/migrations/001_*.sql` (applied in sequence) | — |
| **ORM** | EF Core 9 + Pomelo MySQL, **database-first** — hand-authored entities mapped to existing tables (not auto-scaffold; see note) | — |
| **Search** | **MySQL Full-Text** | Elasticsearch |
| **Cache** | Redis | Redis (expanded) |
| **Messaging** | — (in-process) | Apache Kafka |
| **Storage** | Blob / object storage | CDN in front |
| **Logging / Monitoring** | **Serilog** (console + rolling file) | + Grafana + Prometheus |
| **Deployment** | Single host / container | Kubernetes |
| **Cloud** | Microsoft Azure | Azure (multi-region) |

**Payments (V1):** **Razorpay** (online — UPI, cards, netbanking, wallets) at launch. **COD is schema-ready but its operational workflow is deferred to V1.1** (see §8.4).

### Repository layout
```
ecommerce/
├── ecomm.api/              # ASP.NET Core (.NET 9) Web API — Modular Monolith
│   ├── Common/             # cross-cutting: Models (ApiResponse, PagedResult), Exceptions, Extensions, Constants
│   ├── Data/
│   │   ├── Context/        # EcommerceDbContext (DB-first, hand-mapped entities)
│   │   ├── Entities/       # hand-authored entity classes mapped to existing tables
│   │   └── Platform/       # data infra: seeders, connection helpers
│   ├── Features/           # vertical slices — one folder per module (Controller + Service + DTOs)
│   │   ├── Auth/  Catalog/  Inventory/  Search/  Cart/  Customers/  Orders/
│   │   ├── Payments/  Shipping/  Billing/  Coupons/  Reviews/  Notifications/
│   │   ├── ImportExport/  Media/  Settings/  Audit/  Health/
│   │   └── Cms/  Theming/         # V1.1
│   ├── logs/               # Serilog rolling files (gitignored)
│   └── Program.cs
├── ecomm.web/              # Angular 21 + Tailwind CSS frontend
├── database/
│   └── migrations/         # Versioned SQL: 001_*.sql, 002_*.sql, … applied in sequence
├── documents/
│   ├── design.md           # this document
│   └── stages/             # per-stage build docs (stage-0 … stage-8)
└── ecommerce.sln
```

### API project structure (modular monolith)
- **`Common/`** — cross-cutting building blocks: response envelope (`ApiResponse<T>`), paging (`PagedResult<T>`), exceptions, extensions, constants.
- **`Data/`** — persistence only. `Context/` holds the scaffolded `EcommerceDbContext`; `Entities/` holds the generated entity classes (kept flat — that is how `dotnet ef dbcontext scaffold` emits them); `Platform/` holds data infrastructure (design-time factory, seeders).
- **`Features/`** — **vertical slices**: each module (Auth, Catalog, Orders, …) owns its Controller, Service, and DTOs in one folder, so a module can later be lifted out into its own microservice (Phase 2).

### Database migration convention
- Plain, versioned SQL files in `database/migrations/`, named `NNN_short_description.sql` (`001_`, `002_`, …), **applied strictly in numeric order**.
- Each script is **forward-only and idempotent where practical** (`CREATE TABLE IF NOT EXISTS`, guarded `ALTER`s).
- A `__schema_migrations` table records which scripts have run, so applying is repeatable.
- **SQL is the source of truth** (no EF migrations). Entities are **hand-authored** in `Data/Entities` and mapped to the existing tables via Fluent config in `EcommerceDbContext` (`ToTable("Users")`, etc.), added per feature slice. *Why not auto-`scaffold`:* MySQL on Windows stores table names lowercased (`lower_case_table_names=1`), so `OrderItems` → `orderitems` loses word boundaries and scaffolding emits mis-cased classes (`Orderitem`). Column names are preserved, so hand-mapping keeps clean PascalCase entities while honoring the DB as source of truth.

### Database & connection
- **Database name: `ecommerce`** (lowercase — safe across case-sensitive MySQL hosts).
- Dev connection string (in `appsettings.json` → `ConnectionStrings:Default`):
  `Server=localhost;Database=ecommerce;Uid=root;Pwd=ZHnYHh4IP0QDGraWFHQicZicjQ7M81eu;CharSet=utf8mb4;`
- ⚠️ For anything beyond local dev, move the password out of `appsettings.json` into **user-secrets** or environment variables.

---

## 5. Architecture & Phased Roadmap

**V1 is a Modular Monolith** — a single ASP.NET Core application with clean, well-bounded modules sharing one MySQL database. Module boundaries are drawn so each can later be extracted into its own service.

| Phase | Architecture | Focus / what we learn |
|-------|--------------|------------------------|
| **Phase 1 (V1)** | **Modular Monolith** — Users, Products, Cart, Orders modules | REST API design, SQL modelling, authentication |
| Phase 2 | Microservices — User / Product / Cart / Order services | Service-to-service comms, API Gateway |
| Phase 3 | Event-Driven Architecture | Kafka, eventual consistency. Events: `OrderPlaced`, `PaymentCompleted`, `InventoryReserved`, `ShipmentCreated` |
| Phase 4 | High Scale | Redis, Elasticsearch, CDN, read replicas, performance tuning |

### Domains designed for independent evolution
Product Catalog · Search Engine · Shopping Cart · Checkout · Inventory Management · Payment Gateway · Order Management · Shipping/Logistics · Seller Management · Recommendation Engine · Notification Service · Review System · Coupon Engine · Fraud Detection · Analytics Platform.

---

## 6. Priorities (revised after launch-readiness review)

> **What changed from the original draft:**
> - **Promoted to P0:** Shipping/Delivery, Tax-at-checkout, Cancellation + Refund, Transactional Email — a real checkout cannot ship money/goods without these.
> - **Pulled into P1 (revised):** Theme Engine + a **lite CMS home-section manager** — built right after the storefront to avoid retrofitting hardcoded styling/sections. The **full** freeform CMS builder and the **COD operational workflow** stay deferred to V1.1.

### P0 — Mandatory (V1 launch-critical)
| # | Module | Notes |
|---|--------|-------|
| 1 | **Authentication & Authorization** | Login, Registration, Forgot/Reset Password, email verification |
| 2 | **Roles & Permissions** | RBAC for admin vs customer |
| 3 | **Product Catalog** | Categories, Sub-categories, Brands, Products |
| 4 | **Product Variants** | Size / Color / Capacity |
| 5 | **Product Attributes** | Dynamic per-category attributes (EAV) |
| 6 | **Inventory** | Available + reserved stock, low-stock alerts, reservation on checkout |
| 7 | **Search** | Product / Category / Brand / Attribute (MySQL Full-Text) |
| 8 | **Cart** | Add / remove / update quantity |
| 9 | **Customer & Addresses** | Profile + address book (billing & shipping) |
| 10 | **Orders** | Place / cancel / track |
| 11 | **Tax-at-checkout** | GST computed per HSN/tax rate on cart & invoice *(new)* |
| 12 | **Shipping / Delivery** | Shipping charges, pincode serviceability, courier + tracking number *(new — promoted)* |
| 13 | **Payments** | Razorpay (online) at launch |
| 14 | **Cancellation & Refund** | Cancel order + Razorpay refund path *(new — promoted)* |
| 15 | **Invoice / Billing** | Invoice generation incl. GST/HSN, PDF download |
| 16 | **Order Tracking** | Status lifecycle (see §8.3) |
| 17 | **Transactional Email** | Order confirmation, password reset, status updates *(new — promoted from Notifications)* |

### P1 — Must-Have (V1, built after P0)
| # | Module | Notes |
|---|--------|-------|
| 18 | **Audit Logs** | Who / what / when / old→new value / IP. *Built from Day 1 as a foundation, even though prioritised here.* |
| 19 | **Settings Engine** | Nothing hardcoded (`SiteName`, `EnableReviews`, …). *Also foundational; wired early.* |
| 20 | **Media Library** | Central image management (files + folders) |
| 21 | **Product/Inventory Import-Export** | Excel import/export + image ZIP upload (see §7) |
| 22 | **Coupon Engine** | Flat & percentage discounts |
| 23 | **Reviews & Ratings** | Customer ratings + reviews |
| 24 | **Notification Engine (full)** | SMS now, WhatsApp future, templating (email already in P0) |
| 25 | **Theme Engine** | *Pulled into P1* — admin-set colors/font/logo via CSS variables. Built right after the storefront to avoid retrofitting hardcoded styling. |
| 26 | **CMS home-section manager (lite)** | *Pulled into P1* — admin reorders / shows-hides the existing seeded home sections (`PageSections`). The **full** freeform/custom-HTML page builder stays deferred (see below). |

### V1.1 — Deferred (schema ready, build after launch)
| Module | Why deferred |
|--------|--------------|
| **CMS full page builder** | Freeform sections + custom HTML + scheduling + dynamic renderer. Expensive; the lite section-manager (P1) covers the single-seller need. |
| **COD operational workflow** | Schema-ready; needs fulfillment + remittance reconciliation + RTO handling |
| **Wishlist / Save-for-Later** | Nice-to-have; parked from the original module list |

### Future — V2 / V3 (DB-ready, not built)
Multi-tenant SaaS · Marketplace sellers (Sellers, SellerUsers, SellerCommissions, SellerSettlements) · Credit Notes / Returns · Recommendation Engine · Fraud Detection · Analytics · Logistics integrations · Elasticsearch · Kafka events.

---

## 7. Functional Module Details

Module mechanics worth pinning down (priority tier in brackets):

- **Authentication [P0]** — Login, Registration, Forgot/Reset Password, email verification, JWT sessions.
- **Product Variants [P0]** — e.g. T-Shirt → XL/L/M; Battery → 64Wh/96Wh.
- **Product Attributes [P0]** — dynamic, per-category (Electronics → Warranty, CompatibleModel, Voltage; Calendar → Pages, Year, Size).
- **Inventory [P0]** — available vs reserved stock; cart→order reserves stock and releases on cancel/timeout; low-stock alerts.
- **Search [P0]** — "Dell Battery", "Black XL Shirt"; logs popular searches; MySQL Full-Text now → Elasticsearch later.
- **Tax-at-checkout [P0]** — tax rate resolved by HSN code; CGST/SGST/IGST split on invoice.
- **Shipping [P0]** — shipping-charge rules (flat/zone-based), pincode serviceability, courier assignment + tracking number on the order.
- **Cancellation & Refund [P0]** — cancel before shipment; trigger Razorpay refund; record refund against the payment. (Full credit notes are Future.)
- **Theme Engine [P1]** — admin controls primary/secondary color, logo, font, button style; applied via CSS variables so the whole storefront re-themes live.
- **CMS home-section manager [P1, lite]** — admin reorders / shows-hides the seeded home sections (Banner, Featured, Categories, New Arrivals, Best Sellers); storefront home renders from `PageSections`. *Full freeform builder (custom HTML, scheduling) deferred to V1.1.*
- **Import Engine [P1]** — generic; V1 covers Products + Inventory, reusable later for Customers, Coupons, Categories, Orders, Sellers (see §9).

---

## 8. Key Flows & Rules

### 8.1 Checkout sequence
Cart → resolve **tax** (per HSN) → resolve **shipping** (serviceable pincode + charge) → create **Order** (reserve inventory) → **Payment** (Razorpay) → on success: generate **Invoice (PDF)** + send **confirmation email** → order enters tracking lifecycle.

### 8.2 Billing ≠ Payment
| Payment | Invoice |
|---------|---------|
| Customer paid ₹1200 · Gateway = Razorpay · TransactionId = XYZ123 · Status = Success | Invoice No = INV-20260001 · Customer name · Billing address · GST details · Products · Tax · Total |

**India / GST fields** (design now, even if calc is basic at first): `GST Number`, `HSN Code`, `CGST`, `SGST`, `IGST`. Both Admin and Customer can **download invoice PDFs** from order history.

**Tax display mode (admin-toggleable `TaxMode`)** *(new)* — an admin setting controls how tax is presented and charged:
- **`Exclusive`** (default) — GST added on top of the price; CGST/SGST/IGST shown as separate lines in cart, checkout and invoice. `Total = Subtotal + Tax + Shipping`.
- **`Inclusive`** — prices already include GST; storefront hides the breakdown and shows **"inclusive of all taxes"**. Tax is **reverse-calculated** (`tax = price − price/(1+rate)`) so the **tax invoice remains legally valid** (GST amount still recorded/shown), but nothing is added on top: `Total = Subtotal(gross) + Shipping`.
- **`None`** — no GST charged (unregistered / composition seller); invoice renders as a **Bill of Supply** with no tax lines.

Default is `Exclusive` so existing behaviour is unchanged until an admin switches it. ⚠️ Switching to `Inclusive` makes the listed price the final price (tax no longer added on top) — a pricing decision, since effective revenue changes unless base prices are set tax-inclusive.

### 8.3 Order status lifecycle
`Pending → Paid → Packed → Shipped → Delivered`, plus `Cancelled` and `Returned`. Every transition is recorded in `OrderStatusHistory` and (for customer-facing transitions) triggers a transactional email.

### 8.4 COD (deferred to V1.1)
COD is modelled as a payment-method type in the schema, but the **operational workflow is not built in V1**: it requires order confirmation without prepayment, fake/abandoned-order handling, RTO (return-to-origin), and COD remittance reconciliation. **Launch Razorpay-only; enable COD when a fulfillment + reconciliation process exists.** (If the business genuinely requires COD on day one, this becomes a business override — re-prioritise then.)

---

## 9. Bulk Import / Export Engine (P1, near-essential)

Manual entry does not scale to 10k–100k products. Support **both** manual entry (single updates, corrections, small catalogs) and **file upload** (initial import, mass updates, vendor catalogs).

### Excel format (start with Excel — business users understand it)
| SKU | Product Name | Category | Brand | Price | Stock |
|-----|--------------|----------|-------|-------|-------|
| BAT001 | Dell Battery 65Wh | Batteries | Dell | 1200 | 50 |

**Dynamic attribute columns** map automatically into `ProductAttributeValues`:
| SKU | Product Name | Warranty | Compatible Model |
|-----|--------------|----------|------------------|
| BAT001 | Dell Battery | 1 Year | Dell Latitude 5420 |

### Images
- **Option A — URLs in Excel:** system downloads images from the given URL.
- **Option B — ZIP upload:** `products.xlsx` + `images.zip` with files named by SKU (`BAT001.jpg`); system matches SKU → image.

### Job tracking
- `ImportJobs` (JobId, Status) + `ImportJobItems` (per-row result). Failed rows surfaced with reasons ("Row 45 — Category not found", "Row 67 — Invalid price") and can be fixed and re-uploaded.

### Operations & maintenance
- Via file: **Add** new · **Update** existing · **Deactivate** (Status = Inactive).
- **Export → Edit → Import** is the easiest maintenance workflow (Export Products → edit `products.xlsx` → re-upload).
- **Generic engine:** V1 covers Products + Inventory; reuse the same framework later for Customers, Coupons, Categories, Orders, Sellers.

---

## 10. Data Model

> All tables below are **created from Day 1**, even those unused in V1 (marketplace tables), to honour the design-for-V3 principle. Tables added in this revision are marked **(new)**.

### Core
`Tenants` *(future)* · `Users` · `Roles` · `Permissions` · `UserRoles` · `RolePermissions`

### Catalog
`Categories` · `Brands` · `Products` · `ProductImages` · `ProductVariants` · `VariantOptions` · `Attributes` · `AttributeValues` · `ProductAttributeValues`

### Inventory
`Inventory` · `InventoryTransactions`

### Sales
`Cart` · `CartItems` · `Orders` · `OrderItems` · `OrderStatusHistory` · `Payments` · `PaymentTransactions` · `Refunds` **(new)** · `Coupons` · `CouponUsage`

### Tax & Shipping **(new)**
`TaxRates` **(new)** — HSN → rate (CGST/SGST/IGST) · `ShippingMethods` **(new)** — flat/zone rules · `ShippingZones` **(new)** — pincode serviceability · `Shipments` **(new)** — courier + tracking number per order

### Billing
`Invoices` · `InvoiceItems` · `CreditNotes` *(future)* · `CreditNoteItems` *(future)*

**`Invoices`** — `InvoiceId`, `InvoiceNumber`, `OrderId`, `InvoiceDate`, `Subtotal`, `TaxAmount`, `TotalAmount`
**`InvoiceItems`** — `InvoiceItemId`, `InvoiceId`, `ProductId`, `Quantity`, `UnitPrice`, `TaxAmount`

### Customer
`CustomerAddresses` · `Reviews`

### CMS *(V1.1)*
`Pages` · `PageSections` · `SectionConfigurations`

### Theme *(V1.1)*
`Themes` · `ThemeSettings`

### Platform
`Settings` · `MediaFiles` · `MediaFolders` · `NotificationTemplates` · `NotificationHistory` · `AuditLogs` · `SearchLogs` · `PopularSearches` · `ImportJobs` · `ImportJobItems`

### Future Marketplace (create now, leave unused)
`Sellers` · `SellerUsers` · `SellerCommissions` · `SellerSettlements`

---

## 11. Build Plan — Sequential (dependency order)

> Build the **full V3 schema first**, then implement V1 functionality bottom-up: foundations → catalog → checkout → fulfillment → engagement → P1 → V1.1. Each stage depends on the ones above it.

### Stage 0 — Foundations
1. **Full database schema** (all tables incl. tax/shipping/refunds + future/marketplace) as sequential SQL scripts (`database/migrations/001_*.sql` …) + seed data, then scaffold EF entities database-first.
2. **Settings Engine** + **Audit Logs** wired from Day 1 (every later module logs through them).
3. Cross-cutting: logging, error handling, API conventions, **Media Library** (catalog depends on it).

### Stage 1 — Identity
4. **Authentication & Authorization** (users, JWT, email verification, forgot/reset password).
5. **Roles & Permissions** (RBAC: admin vs customer).

### Stage 2 — Catalog
6. **Categories, Brands, Products, Product Images**.
7. **Product Variants** + **Dynamic Attributes**.
8. **Bulk Import/Export** (Excel + image ZIP, job tracking) — depends on catalog + media.
8b. **Storefront UI** (Angular SSR) + **SEO** + admin catalog management.

### Stage 2.5 — Theming & CMS-lite *(pulled forward from V1.1)*
8c. **Theme Engine** — admin colors/font/logo → CSS variables; storefront re-themes live (done now to avoid retrofitting the fresh storefront).
8d. **CMS home-section manager (lite)** — admin reorders / shows-hides seeded `PageSections`; home renders from them. *Full freeform builder stays in Stage 7.*

### Stage 3 — Inventory & Search
9. **Inventory** (available/reserved stock, low-stock alerts).
10. **Search** (MySQL Full-Text) + search logging.

### Stage 4 — Shopping
11. **Customer profile & address book** (billing + shipping addresses).
12. **Cart** (add/remove/update; inventory reservation hooks).

### Stage 5 — Checkout & Money
13. **Tax-at-checkout** (HSN → TaxRates; CGST/SGST/IGST).
14. **Shipping** (ShippingMethods/Zones, pincode serviceability, charges).
15. **Orders** (place/cancel; inventory reserve/release).
16. **Payments — Razorpay** (online).
17. **Cancellation & Refund** (Razorpay refund path; `Refunds`).
18. **Invoice / Billing** (PDF, GST fields).

### Stage 6 — Post-purchase & engagement
19. **Order Status Tracking** + **Shipments** (courier, tracking number).
20. **Transactional Email** (order confirmation, status updates, password reset).
21. **Coupon Engine** (flat/percentage).
22. **Reviews & Ratings**.
23. **Notification Engine (full)** — SMS, templates (WhatsApp future).

### Stage 7 — Differentiators (V1.1, post-launch)
24. **CMS full page builder** (freeform sections, custom HTML, scheduling, dynamic renderer) — Theme Engine + lite section-manager already shipped in Stage 2.5.
25. **COD operational workflow** (when fulfillment + reconciliation is ready).
26. **Wishlist / Save-for-Later**.

### Stage 8 — Hardening
28. Caching (Redis), performance passes, monitoring, automated tests, deployment to Azure.

---

## 12. Authentication & Identity (P0)

Multi-method sign-in where every method is an **admin-controlled provider**. The storefront only shows what's enabled, so toggling a method needs no redeploy.

### Decisions
- **Custom auth** — BCrypt password hashing + our own **JWT** (access + refresh). No ASP.NET Core Identity framework (it fights our DB-first custom schema).
- **SMS** — provider-agnostic `ISmsSender`; V1 ships a **dev/console sender**, a real gateway (MSG91/Twilio/…) plugs in later via config.
- **Account linking** — **auto-link by verified email**: a verified email matching an existing account attaches the new login to it (one user, many login methods).
- **Google** — **frontend ID token → backend verify** (Google Identity Services in Angular; API validates signature + audience). No server-side OAuth redirect in V1.

### Providers (admin-controlled)
| Provider | Flow | Admin toggles |
|----------|------|---------------|
| **EmailPassword** | register / login / forgot-password / verify-email | `IsEnabled`, **`AllowRegistration`** (login-only if off) |
| **MobileOtp** | phone → SMS OTP → verify (passwordless) | `IsEnabled` |
| **Google** | Google Sign-In, ID token verified server-side | `IsEnabled` (+ client-id in config) |

- Public `GET /api/auth/config` returns only enabled providers → Angular renders the login page dynamically.
- Admin `GET/PUT /api/admin/auth-providers` (perm `settings.manage`) manages them.
- All three flows **converge on the same JWT**, so the rest of the API is auth-method-agnostic.

### Schema impact (`015_auth.sql` — forward-only ALTERs + new tables)
- **`Users`:** `Email`, `NormalizedEmail`, `PasswordHash` become **nullable** (phone-only users have no email; Google/phone users have no local password); add `IsPhoneVerified`, `PhoneVerifiedAt`; unique `(TenantId, PhoneNumber)`.
- **`AuthProviders`** — per-tenant provider config (`IsEnabled`, `AllowRegistration`, `DisplayOrder`, `ConfigJson`). **Secrets stay in user-secrets/env, not the DB** — `ConfigJson` holds only public values (e.g. Google client-id).
- **`UserExternalLogins`** — `Provider` + `ProviderUserId` (Google `sub` / phone), unique; one user may have several linked.
- **`OtpVerifications`** — `Identifier`, `Channel`, `Purpose`, `CodeHash`, `ExpiresAt`, `AttemptCount` — OTP for mobile login, email verification, password reset.
- **`RefreshTokens`** — hashed refresh tokens with rotation/revocation.

### Security
OTP = 6-digit, 5-min TTL, **hashed at rest**, max-attempts + resend cooldown + rate limiting; passwords BCrypt (work factor ≥ 11); Google ID-token signature + audience validated; refresh-token rotation; provider secrets never in the DB.

> **Stage 0 admin user:** its placeholder `PasswordHash` is replaced with a real BCrypt hash by the Auth module's startup seeder (in `Data/Platform`).

### Enabling Google SSO (setup)
The Google flow is built (backend `auth/google` + frontend GSI). To activate it:
1. **Google Cloud Console** → APIs & Services → Credentials → create **OAuth 2.0 Client ID** (type: Web application).
2. **Authorized JavaScript origins:** `http://localhost:4200` (dev) + the production domain.
3. Copy the **Client ID** → Admin **Sign-in methods** screen → paste into *Google → Client ID*, tick **Enabled**, **Save** (stored in `AuthProviders.ConfigJson`, a public value).
4. The storefront login then shows **Continue with Google**; the returned ID token is verified server-side (signature + audience). No client secret is needed for ID-token sign-in.

---

## 13. SEO & Discoverability

The storefront must be crawlable and rich-result friendly — organic search drives e-commerce traffic. A client-rendered SPA is weak here, so SEO is planned as a first-class concern of the storefront build.

### Rendering strategy (key decision)
| Option | SEO | Cost |
|--------|-----|------|
| **Angular SSR** (`@angular/ssr`) — *recommended* | Best — server-renders product/category pages to full HTML; correct social/link previews | Adds a Node SSR server beside the .NET API; project currently `--ssr=false`, so SSR must be enabled |
| **Prerender (SSG) + SSR hybrid** | Good — static pages prerendered, dynamic pages SSR per route | Moderate |
| **CSR + Meta service only** | Weak — JS-rendered; unreliable indexing, broken social previews | Cheapest; not advised for a store |

> **Decision (confirmed):** enable **Angular SSR** for the storefront — to be scaffolded (`ng add @angular/ssr`) as the first step of the storefront UI build.

### Per-page metadata
- Dynamic `<title>` + `<meta name="description">` per page (home, category, product, search).
- **Open Graph** + **Twitter Card** tags (og:title/description/image/type/url) — product primary image as `og:image`.
- **Canonical** `<link rel="canonical">`; handle filter/pagination URLs to avoid duplicate content.

### Structured data (JSON-LD)
- **Product** (name, image, sku, brand, `offers`{price, priceCurrency, availability}, aggregateRating/review when present) → Google rich results / Shopping.
- **BreadcrumbList**, **Organization**, **WebSite** (+ SearchAction sitelinks search box).

### Crawl infrastructure (API-supported)
- **`/sitemap.xml`** generated from active products + categories (cached API endpoint, with `lastmod`).
- **`robots.txt`** — allow crawl, link the sitemap, disallow `admin`/`cart`/`checkout`.
- Clean **slug URLs** (already implemented): `/category/{slug}`, `/product/{slug}`, with `GET /catalog/products/by-slug/{slug}`.

### Data model
- Optional **SEO override fields** per product/category — `MetaTitle`, `MetaDescription`, `OgImageUrl` (fall back to name / short description / primary image). Add via a small migration (`016_*`) when building the storefront; `Pages` already carries `MetaTitle`/`MetaDescription`.
- Image **alt text** already on `ProductImages.AltText`.

### Performance / Core Web Vitals
- LCP: responsive/lazy images, preconnect, CDN (later); CLS: reserve image dimensions; INP: keep JS light. SSR improves first paint across all three.

### Build sequencing
- **Stage 2 (storefront UI):** confirm SSR, then build meta + Open Graph + JSON-LD + canonical alongside each screen; add SEO override fields + migration.
- **Stage 8 (hardening):** `sitemap.xml` + `robots.txt`, Core Web Vitals pass, CDN.

---

*End of revised V1 design baseline. Microservices, event-driven architecture, Elasticsearch, and marketplace flows are explicitly deferred to later phases; the schema is built to accommodate them — plus tax, shipping, refunds, COD, multi-provider auth, and SEO — without rework.*


