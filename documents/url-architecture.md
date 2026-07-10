# URL Architecture — WavCommerce (multi-tenant)

How storefronts, merchant admin, super-admin, signup and custom domains map to URLs. Design decision
per **ADR-001**: one Angular app + one API, **tenant resolved by host** (subdomain), admin served under
`/admin` on the tenant's own host — *not* a separate admin app/domain like Shopify's `admin.shopify.com`.

`{baseDomain}` = the platform root (prod: **`wavcommerce.online`**; dev: `localhost`). Configured via
`Tenancy:BaseDomain`. `{slug}` = the tenant's store address (auto-generated Shopify-style, e.g. `cafe24-a3k9`).

## The four worlds

| World | Who | URL |
|---|---|---|
| **Platform landing / signup** | prospective merchant | `{baseDomain}` · `{baseDomain}/signup` |
| **Super-admin** (platform operator) | us | **`{baseDomain}/superadmin`** (the apex host) |
| **Merchant admin** (store console) | store owner/staff | **`{slug}.{baseDomain}/admin`** |
| **Storefront** (the shop) | customers | **`{slug}.{baseDomain}`** (+ custom domain) |

Concrete (prod):
- Storefront: `https://cafe24-a3k9.wavcommerce.online`
- Merchant admin: `https://cafe24-a3k9.wavcommerce.online/admin`
- Super-admin: `https://wavcommerce.online/superadmin`
- Password gate: `https://cafe24-a3k9.wavcommerce.online/password`

### vs Shopify
| | Shopify | WavCommerce |
|---|---|---|
| Storefront | `{handle}.myshopify.com` | `{slug}.wavcommerce.online` |
| Merchant admin | `admin.shopify.com/store/{handle}` (central) | `{slug}.wavcommerce.online/admin` (per-tenant) |
| Super-admin | internal | `wavcommerce.online/superadmin` |

Storefront matches Shopify. The admin differs by design (ADR-001) — it lives on the store's own host, so a
merchant bookmarks one stable address (`{slug}.wavcommerce.online/admin`) and never juggles two domains.

## How the tenant is resolved (backend)
`TenantResolutionMiddleware` picks the tenant from the request host (X-Forwarded-Host behind Nginx/SSR):
- `{slug}.{baseDomain}` → the tenant with that **Slug**.
- A connected **custom domain** → the tenant with that `CustomDomain` (cached).
- apex / `www` / localhost / IP / unknown → the **default tenant** (keeps V1 working; hosts the platform landing + super-admin).
- unknown/suspended subdomain → **404** (fail-closed).
- Defence in depth: a JWT carrying a `tenant` claim can't be used against a different host (403).

## Custom domains — the clean rule
**A merchant's custom domain serves the storefront only; the admin consoles never appear on it.**

When a merchant connects `www.brand.com` (Admin → Custom domain, verified):

| URL | Before | After mapping `www.brand.com` |
|---|---|---|
| Storefront home | `{slug}.wavcommerce.online` | **`www.brand.com`** |
| Product / cart / search | `{slug}.wavcommerce.online/product/…` | **`www.brand.com/product/…`** |
| Password gate | `{slug}.wavcommerce.online/password` | **`www.brand.com/password`** |
| **Merchant admin** | `{slug}.wavcommerce.online/admin` | **`{slug}.wavcommerce.online/admin`** ← *unchanged* |
| **Super-admin** | `wavcommerce.online/superadmin` | `wavcommerce.online/superadmin` ← *unchanged* |

So the admin URL **never changes** when a domain is connected — customers see `www.brand.com`, the merchant
keeps logging in at `{slug}.wavcommerce.online/admin`.

### Enforcement (implemented)
Because the app would otherwise serve `/admin` on any host that resolves to the tenant, `www.brand.com/admin`
would have opened the console. Guard: **`platformHostGuard`** (`core/guards/auth.guard.ts`) runs before the role
guards on `/admin`, `/admin/**`, and `/superadmin`. It calls **`GET /api/tenant/host-info`**
(`{ slug, platformHost, isCustomDomain, adminUrl, superAdminUrl }`) and, on a custom-domain host, redirects:
- `/admin*` → `https://{slug}.{baseDomain}/admin`
- `/superadmin` → `https://{baseDomain}/superadmin`

On the platform host / apex / dev it's a no-op. (The admin API stays JWT-protected regardless; this is about
separation of concerns + not exposing the console on the brand domain, not a security boundary.)

### SEO (recommended, not yet done)
Once a custom domain is verified, set the storefront **canonical** to the custom domain and 301 the
`{slug}.wavcommerce.online` storefront → the primary custom domain (as Shopify does with `.myshopify.com`), so
search engines index the brand domain. *(Tracked; the guard above ships first.)*

## Reserved subdomains
`admin`, `store`, `app`, `api`, `www`, `mail`, `login`, `signup`, `account`, `dashboard`, … can't be a tenant
slug (see `OnboardingService.ReservedSlugs`), so `admin.{baseDomain}` etc. stay free for platform use.

## Store address (slug) generation
Auto-generated, Shopify-style: `{name-base}-{random}` (e.g. `cafe24-a3k9`), guaranteed unique. Min 3 chars
(no single-letter `c.wavcommerce.online`). A merchant may pick their own valid address, or leave it blank to
get one. `GET /api/onboarding/suggest-slug?name=…`.

## Optional future polish
- A memorable central **`admin.wavcommerce.online`** login that redirects into the store's `/admin` (Shopify-style
  front door) — a redirect only, not the full central-admin refactor.
- Full central admin host (`admin.wavcommerce.online/store/{slug}`) — **rejected** for now: days of work, reverses
  ADR-001, and the `platformHostGuard` already keeps the admin off brand domains.
