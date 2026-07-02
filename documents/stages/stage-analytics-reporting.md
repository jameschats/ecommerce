# Stage — Analytics & Reporting (P1)

**Goal:** give the admin real decision-making insight — **product profitability, margins, and business
pulse** — plus lightweight **traffic analytics**. Most small stores never surface this; it's a genuine
differentiator.

Two independent tracks:
- **A. Admin analytics** — *build* (profit/margin dashboard + reports + activity widget).
- **B. Traffic analytics** — *integrate* (Umami/Plausible; don't build web analytics from scratch).

Can run right after Stage 6 (it's admin-facing and independent of post-purchase).

---

## Track A — Admin analytics (build)

### Data foundation (already in place, + one snapshot to add)
| Need | Source |
|---|---|
| Revenue per item | `OrderItems.UnitPrice × Quantity` |
| **Cost per item (COGS)** | `Products.CostPrice` (list) / `ProductSuppliers.CostPrice` (per supplier) |
| Supplier of a product | `ProductSuppliers` (`IsPrimary`) → `Suppliers` *(migration 021 ✓)* |
| Returns / refunds | Order status `Returned/Cancelled` + `Refunds` |
| Category | `Products.CategoryId → Categories` |

> ⚠️ **Add `OrderItems.UnitCost` (cost snapshot at sale time).** Cost prices change over time, so
> historical profit must use the cost **as it was when the order was placed** — exactly like we already
> snapshot `UnitPrice`. Scope includes a small migration (`022_orderitem_cost.sql`) + wiring
> `OrderService.PlaceOrderAsync` to copy the product's current cost onto each `OrderItem`.

### Scope: **gross margin / product profitability** (not full net P&L)
Define **margin = revenue − COGS − refunds**. True *net* profit also needs payment-gateway fees +
real shipping cost, which aren't modelled yet — so those stay out of scope here (see roadmap). Tax is
pass-through and excluded from profit.

### The 6 reports
| Report | Definition | Query shape |
|---|---|---|
| **Best-selling products** | Units + revenue, ranked | `OrderItems` group by product, `SUM(Quantity)`, `SUM(LineTotal)` |
| **Highest-margin products** | `(price − cost) / price`, ranked desc | join OrderItems→cost snapshot |
| **Low-margin products** | same, ranked asc (+ "below X%" flag) | " |
| **Return rate by product** | returned-or-refunded units ÷ sold units | OrderItems + order status/Refunds |
| **Profit by category** | `Σ(revenue − cost)` grouped by category | OrderItems→Products→Categories |
| **Profit by supplier** | `Σ(revenue − cost)` grouped by primary supplier | OrderItems→ProductSuppliers→Suppliers |

All reports take a **date range** and honour `TenantId`.

### Business-activity widget (dashboard home)
Cheap, high-value, from data you already own:
- Orders today / this week · revenue today · AOV
- New signups
- Low-stock count (from `Inventory`)
- Top searches (`PopularSearches` / `SearchLogs`)
- Pending orders needing action (Paid → not yet Packed/Shipped)

### Supplier management (uses migration 021)
- Entities: `Supplier`, `ProductSupplier` (+ DbContext maps).
- Admin: **Suppliers CRUD**, assign suppliers to products (SKU, cost, lead time, mark primary).
- A **"cost price / supplier missing"** nudge in the product admin so margin data stays trustworthy.

### API (admin, `[Authorize(Roles="Admin")]`)
`GET /api/admin/analytics/summary` (activity widget) ·
`GET /api/admin/analytics/best-sellers` · `/margins?order=high|low` · `/return-rate` ·
`/profit-by-category` · `/profit-by-supplier` — all accept `?from=&to=`.
Suppliers: `GET/POST/PUT/DELETE /api/admin/suppliers`, `…/products/{id}/suppliers`.

### Admin UI
An **Analytics** section: activity widget on top, then the report tables (sortable) with a date-range
picker and CSV export. Simple bar/line charts optional (keep dependencies light).

---

## Track B — Traffic analytics (integrate, not build)

- **Umami** (recommended): self-host free on the same VPS (Node + its own Postgres/MySQL), **cookieless
  → no consent banner** (good under India DPDP). Or **Plausible** (cloud ~$9/mo, or self-host).
- Add the tracking `<script>` to the Angular app (browser-only, SSR-safe — guard with `isPlatformBrowser`).
- Link the Umami/Plausible dashboard from the admin nav (SSO/embed optional).
- Gives sessions, unique visitors, geo, acquisition, realtime — without reinventing sessionization/bot-filtering.

### Implementation (done)
- `WebAnalyticsService` (`core/services/web-analytics.service.ts`) injects the Umami `<script>` **once, browser-only**
  (`isPlatformBrowser`), and is a **no-op until configured**. Called from `App.ngOnInit`.
- Config in `core/api.config.ts` (deploy-replaced, empty by default = disabled):
  `UMAMI_SRC`, `UMAMI_WEBSITE_ID`, `UMAMI_DASHBOARD_URL`.
- Admin sidebar shows a **"Web traffic ↗"** link only when `UMAMI_DASHBOARD_URL` is set.
- Umami auto-tracks SPA route changes, so no per-navigation wiring is needed.

### VPS self-host guide (run once on the Hostinger box)
Umami needs Node 18+ and its own database. Simplest path with Docker Compose (own MySQL container):

```bash
# 1. App + DB via Docker Compose
mkdir -p /opt/umami && cd /opt/umami
cat > docker-compose.yml <<'YML'
services:
  umami:
    image: ghcr.io/umami-software/umami:mysql-latest
    ports: ["3000:3000"]
    environment:
      DATABASE_URL: mysql://umami:CHANGE_ME@db:3306/umami
      DATABASE_TYPE: mysql
      APP_SECRET: CHANGE_ME_RANDOM_LONG_STRING
    depends_on: [db]
    restart: always
  db:
    image: mysql:8
    environment:
      MYSQL_DATABASE: umami
      MYSQL_USER: umami
      MYSQL_PASSWORD: CHANGE_ME
      MYSQL_ROOT_PASSWORD: CHANGE_ME_ROOT
    volumes: ["umami-db:/var/lib/mysql"]
    restart: always
volumes: { umami-db: {} }
YML
docker compose up -d      # Umami now on 127.0.0.1:3000

# 2. Nginx subdomain → reverse-proxy to :3000 (analytics.calendarshop.online)
#    server_name analytics.calendarshop.online;  location / { proxy_pass http://127.0.0.1:3000; ... }
#    then: certbot --nginx -d analytics.calendarshop.online

# 3. In the Umami UI (default login admin/umami — CHANGE IT):
#    Settings → Websites → Add → domain calendarshop.online → copy the Website ID + script URL.

# 4. Point the frontend at it (deploy-replaced api.config.ts before ng build):
#    UMAMI_SRC           = 'https://analytics.calendarshop.online/script.js'
#    UMAMI_WEBSITE_ID    = '<website-id>'
#    UMAMI_DASHBOARD_URL = 'https://analytics.calendarshop.online'
```

---

## Tables
New: `Suppliers`, `ProductSuppliers` *(021 ✓)*; add `OrderItems.UnitCost` *(028 ✓)*.
Reads: `Orders, OrderItems, Products, Refunds, Categories, Inventory, PopularSearches, Users`.

## Dependencies
Stage 5 (orders/refunds/OrderItems), Catalog (`CostPrice`), Suppliers migration (021).

## Deferred (→ P2 / future)
Full **net P&L** (gateway fees + shipping cost + inventory valuation), **Purchase Orders** (procurement
on top of Suppliers), cohort/RFM/LTV, custom report builder, scheduled email reports.

**Status:** ✅ Done. Track A — cost snapshot (migration 028) + cost wiring in `OrderService`; the 6 reports +
activity widget (`AnalyticsService`/`AnalyticsController`); admin Analytics UI (widget, report tabs, date range,
CSV export, inline bars); Suppliers CRUD + product supplier assignment + `CostPrice` field & missing-cost nudge on
the product form. Track B — SSR-safe Umami tracking (`WebAnalyticsService`, config-gated) + admin "Web traffic" link
+ VPS self-host guide above. **Remaining ops step:** run the Umami self-host + fill `UMAMI_*` config on the VPS.
