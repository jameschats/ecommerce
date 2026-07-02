# CLAUDE.md

Build guide for the **"Mini Flipkart"** e-commerce platform. Full design lives in [documents/design.md](documents/design.md); per-stage plans in [documents/stages/](documents/stages/); UI direction in [documents/ui-guidelines.md](documents/ui-guidelines.md).

## Working rules
1. **Ask, don't assume.** If something is unclear or hard to reverse, ask before building. For low-risk, obvious calls, decide and clearly flag the assumption — no *silent* assumptions.
2. **Simplest solution first.** Build the simplest thing that works. No abstractions nobody asked for.
3. **Don't touch unrelated code.** If it's not part of the task, don't modify it — even if it looks like it needs fixing (mention it instead).
4. **Flag uncertainty.** If not confident, say so before proceeding. Honest doubt beats fake confidence.

## What this is
A production-ready, single-seller (V1) e-commerce platform built as a master system-design project. The **database is designed for V3 (marketplace)** up front so the schema never needs a rewrite; V1 implements only single-seller functionality.

## Stack
| Layer | Tech |
|-------|------|
| Backend | ASP.NET Core **.NET 9** Web API — `ecomm.api` (modular monolith, vertical slices) |
| Frontend | **Angular 21** + **Tailwind CSS** — `ecomm.web` |
| Database | **MySQL 8** (`ecommerce`) |
| ORM | EF Core 9 + Pomelo, **database-first with hand-authored entities** |
| Auth | Custom **BCrypt + JWT**; admin-toggleable providers (Email/Password, Mobile OTP, Google) |
| Logging | **Serilog** (console + rolling file in `ecomm.api/logs/`) |

## Layout
```
ecomm.api/      # API: Common/ (ApiResponse, exceptions, middleware), Data/ (Context, Entities, Platform), Features/<slice>/
ecomm.web/      # Angular + Tailwind
database/migrations/   # 001_*.sql … forward-only, numbered; SQL is the source of truth
documents/      # design.md, stages/, ui-guidelines.md
```

## Commands
```bash
# API
dotnet build ecomm.api/ecomm.api.csproj
dotnet run --project ecomm.api --urls http://localhost:5080      # http://localhost:5080/api/health
dotnet test ecomm.tests/ecomm.tests.csproj                       # xUnit suite (tax/coupon/hasher/slug)

# Frontend
cd ecomm.web && npm start            # ng serve → http://localhost:4200

# Database — apply a migration (MySQL CLI is at "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe")
mysql -u root -padmin ecommerce < database/migrations/00X_name.sql
```

## Database
- Name **`ecommerce`**; dev conn (in `appsettings.json`): `Server=localhost;Database=ecommerce;Uid=root;Pwd=admin;CharSet=utf8mb4;`
- Migrations are **forward-only and numbered** (`001`–`022` so far). **Never edit an applied script** — add a new higher-numbered one. Each records itself in `__schema_migrations`.
- Tables/columns are PascalCase in SQL; MySQL-on-Windows stores **table** names lowercased (case-insensitive). That's why entities are **hand-authored + Fluent-mapped** (`ToTable("Users")`), not auto-scaffolded.

## Conventions
- **Vertical slices:** each module owns its Controller + Service + DTOs under `Features/<Module>/`.
- **Entities** live in `Data/Entities/`, mapped in `EcommerceDbContext`; add per feature as needed.
- All API responses use the **`ApiResponse<T>`** envelope; lists use `PagedResult<T>`.
- Throw **`AppException(message, statusCode)`** for handled errors — the exception middleware formats them.
- Match surrounding style; standalone Angular components; Tailwind for styling; theme values come from the Theme Engine (no hardcoded brand colors).

## Guardrails
- **Don't commit secrets.** JWT key, DB password, Razorpay/Google/SMS keys are dev placeholders → move to user-secrets/env. `logs/` and secrets are gitignored.
- **Admin login:** `admin@ecommerce.local` / `Admin@123` (seeded on startup) — change it.
- Run `dotnet build` (and tests, once they exist) before claiming a change works.
- COD and Theme/CMS are **deferred to V1.1** by design — don't build them in the P0 path unless asked.

## Status
Stages 0–8 ✅ — schema (61 tables) · auth (backend + UI) · catalog (SSR/SEO storefront + admin) · theming & CMS-lite · inventory & search · shopping (cart + profile + addresses) · **checkout & money** (GST/HSN tax, shipping + pincode serviceability, orders with inventory reserve/commit/restock, **payments via Mock or Razorpay** [`Payments:Provider`], cancellation + refund, **invoice PDF** via QuestPDF, admin order management) · **post-purchase & engagement** (transactional **email/SMS** notifications [`Email:Provider` Logging/Smtp] via templates + history, **reviews & ratings** with moderation, **coupons** [flat/%; caps; usage limits] at checkout + admin, **shipments** [courier/tracking, dispatch→deliver, customer tracking]). Also: admin-managed **home banners** + **product image uploads** (disk+Nginx via `IMediaStorage`), GST tax-mode toggle, **password-reset + email-verification** (email OTP), config-selectable **SMS** (`Sms:Provider` Logging/MSG91). · **differentiators** (Theme engine incl. **logo upload**, **home-section scheduling**, **wishlist**, **COD** with admin on/off flag) · **hardening** (rate limiting + security headers + HSTS, **login lockout**, forwarded headers, `/api/health/ready` DB probe, response compression, **output caching** for anonymous reads, **xUnit test suite** — `dotnet test ecomm.tests`) · **real-time notifications** (SignalR bell + notification pages for customer/admin; order/new-order/pending-review/low-stock events; review submission gated to verified purchasers) · **analytics & reporting** (admin profit/margin dashboard: cost snapshot at sale time [`OrderItems.UnitCost`, migration 028], 6 reports [best-sellers, high/low margin, return-rate, profit by category/supplier] with date range + CSV, business-activity widget, **Suppliers CRUD** + product cost/supplier assignment, **Umami** cookieless traffic analytics [SSR-safe, config-gated via `UMAMI_*` in `api.config.ts`]). **Deferred:** SEO infra (sitemap/robots); operational go-live (change prod admin pwd, real GSTIN, wire SMTP/MSG91, self-host Umami + set `UMAMI_*`). See [documents/stages/README.md](documents/stages/README.md).
