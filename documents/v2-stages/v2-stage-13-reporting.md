# V2-13 — Reporting & Analytics (Merchant + Platform)

**Goal:** two report suites — a full **Reports section per merchant** (their own store) and **platform-wide reports for the super-admin** (across all tenants) — each with date ranges, CSV/PDF export, and scheduled email delivery.

> Builds on what already exists: V1's analytics (profit/margin dashboard, 6 reports, activity widget, Umami traffic) becomes **per-tenant** via V2-2; the V2-3 **revenue dashboard** is the seed of the platform suite. This stage extends both into full, exportable, schedulable report sets.

## 13a. Merchant Admin reports (per-tenant — their own store)
Tenant-scoped by the global query filters (V2-0), so each merchant sees only their data.
- [ ] **Sales & revenue** over time (day/week/month), AOV, gross vs net
- [ ] **Best-sellers · high/low margin · profit by category/supplier · return-rate** (from V1 analytics)
- [ ] **Orders** — status funnel, fulfilment times, cancellations
- [ ] **Customers** — new vs returning, top customers, repeat rate, LTV
- [ ] **Inventory** — stock on hand, low-stock, dead stock, stock valuation
- [ ] **Coupons / discounts** — usage + revenue impact
- [ ] **Tax / GST — GSTR-ready** — GST collected, HSN summary, CGST/SGST/IGST split (an India differentiator; merchants need this for filing)
- [ ] **Payments / settlements** — Razorpay Route settlements + fees
- [ ] **Traffic** — Umami (V1)

## 13b. Super Admin reports (platform-wide — across all tenants)
Cross-tenant aggregation via `IgnoreQueryFilters()` — **SuperAdmin only** (V2-0 policy).
- [ ] **Revenue** — MRR, ARR, ARPU, LTV, churn rate, revenue by plan; **subscription + commission (Route) revenue** combined
- [ ] **Merchant growth** — signups, activation rate, trial→paid conversion, active/trial/suspended/churned; **cohort retention**
- [ ] **Platform GMV** — total gross merchandise value across all stores, take-rate revenue, GMV by plan/region/category
- [ ] **Merchant leaderboard** — top by GMV/growth; **at-risk** (low health/standing, V2-3/V2-12)
- [ ] **Standing & health distribution** (V2-3)
- [ ] **Support metrics** — ticket volume, first-response/resolution time, SLA compliance, CSAT (V2-9)
- [ ] **Usage / adoption** — AI credits consumed (V3), feature adoption, channel usage

## Delivery mechanics (both levels)
- [ ] Date-range pickers; **CSV + PDF** export (reuse QuestPDF from V1 invoices)
- [ ] **Scheduled reports** — email a report on a cadence (Hangfire + the V2-11 dispatcher)
- [ ] **Tables + lightweight inline bars** (matches the V1 analytics choice — no heavy chart library)

## Data model
Mostly read/aggregate over existing tables. For platform scale (millions of orders across tenants), add **materialised rollups** refreshed by a nightly Hangfire job to keep dashboards fast: e.g. `DailyTenantSales`, `PlatformDailyMetrics`, `TenantMonthlyRevenue`. Migrations `230–239`. Reuses V1 analytics tables + V2 subscription/billing/commission + support/health.

## Endpoints
- **Merchant:** `/api/reports/{sales|orders|customers|inventory|tax|coupons|settlements}` (tenant-scoped, `?from=&to=`, `?format=csv|pdf`)
- **Super-admin:** `/api/superadmin/reports/{revenue|growth|gmv|cohorts|leaderboard|support|usage}`
- **Scheduled reports:** CRUD `/api/reports/schedules` (merchant) + `/api/superadmin/reports/schedules`

## Frontend
- **Merchant-admin:** a **Reports** section — report picker + date range + table/bars + export + "email me this on a schedule."
- **Super-admin:** **Platform Reports** — same shell at cross-tenant scope.

## Gate
A merchant sees only their store's reports with correct numbers, exports a GST report as CSV **and** PDF, and receives a scheduled monthly email; the super-admin sees accurate platform **MRR/churn/GMV/commission** across seeded tenants plus **cohort retention** and **support metrics**; cross-tenant aggregation uses `IgnoreQueryFilters` only in SuperAdmin; the nightly rollup keeps large dashboards fast.

## Dependencies
V1 analytics (per-tenant via V2-2), V2-3 (revenue dashboard baseline + standing/health), V2-5 (commission data), V2-9 (support metrics), V2-11 (scheduled delivery), V2-12 (health). Hangfire (rollups + schedules).

**Status:** ⬜ Not started.
