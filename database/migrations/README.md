# Database Migrations

Versioned, forward-only SQL migrations for the MySQL database.

## Convention
- File name: `NNN_short_description.sql` — zero-padded sequence (`001_`, `002_`, …).
- Applied in **strict numeric order**; never reorder or rewrite an applied script.
- Forward-only. To change something, add a **new** higher-numbered script.
- Idempotent where practical: `CREATE TABLE IF NOT EXISTS`, guarded `ALTER`s.
- Every applied script is recorded in the `__schema_migrations` table (created by `001`).

## Order so far
| Script | Purpose |
|--------|---------|
| `001_init_schema_history.sql` | Baseline — `__schema_migrations` tracking table |
| `002_core.sql`        | Tenants, Permissions, Roles, Users, UserRoles, RolePermissions |
| `003_catalog.sql`     | Categories, Brands, Products, ProductImages, Variants, Attributes |
| `004_inventory.sql`   | Inventory, InventoryTransactions |
| `005_sales.sql`       | Coupons, Cart, Orders, OrderItems, Payments, Refunds, … |
| `006_tax_shipping.sql`| TaxRates, ShippingMethods, ShippingZones, Shipments |
| `007_billing.sql`     | Invoices, InvoiceItems, CreditNotes (future) |
| `008_customer.sql`    | CustomerAddresses, Reviews |
| `009_cms.sql`         | Pages, PageSections, SectionConfigurations (V1.1) |
| `010_theme.sql`       | Themes, ThemeSettings (V1.1) |
| `011_platform.sql`    | Settings, Media, Notifications, AuditLogs, Search, ImportJobs |
| `012_marketplace.sql` | Sellers, SellerUsers, SellerCommissions, SellerSettlements (future) |
| `013_foreign_keys.sql`| All cross-domain foreign keys (added after every table exists) |
| `014_seed.sql`        | Default tenant, RBAC, admin user, GST rates, settings, theme, home page |
| `015_auth.sql`        | Multi-provider auth: Users ALTERs + AuthProviders, UserExternalLogins, OtpVerifications, RefreshTokens |
| `016_auth_provider_order.sql` | Make Mobile OTP the primary sign-in method (display order) |

**Result:** 61 tables, 72 foreign keys. Validated against MySQL 8.0.
Follows the data model in [`/documents/design.md`](../../documents/design.md) §10.

## After applying: scaffold EF entities (database-first)
```bash
cd ecomm.api
dotnet ef dbcontext scaffold "Server=localhost;Database=ecommerce;Uid=root;Pwd=ZHnYHh4IP0QDGraWFHQicZicjQ7M81eu;CharSet=utf8mb4;" \
  Pomelo.EntityFrameworkCore.MySql -o Data/Entities --context-dir Data/Context \
  -c EcommerceDbContext --use-database-names --no-onconfiguring
```
(Install the CLI once if needed: `dotnet tool install --global dotnet-ef`.)

## Applying (manual, V1)
```bash
# example: apply a single script
mysql -u <user> -p <database> < 001_init_schema_history.sql
```
A small migration runner (apply-in-order, skip already-recorded) can be added later; the `__schema_migrations` table already supports it.
