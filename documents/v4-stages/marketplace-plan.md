# Marketplace (V3) — Thorough Plan + How Shopify Does It

**Goal:** evolve the platform from single-seller stores (V1) into a **multi-seller marketplace** —
many sellers listing products under one marketplace storefront, one shopper checkout, money split to
sellers minus commission — the Amazon / Flipkart / Meesho / Myntra model. The DB was designed for this
from day 1 (`012_marketplace.sql`: `Sellers`, `SellerUsers`, `SellerCommissions`, `SellerSettlements`;
`Product.SellerId`), so this is building the *flows*, not a schema rewrite.

> **Positioning (from design.md):** *"more complex than a Shopify store, simpler than Flipkart/Amazon."*
> So the marketplace target is the Flipkart/Amazon model — **not** Shopify's model. That distinction is the
> first thing to get right, because "how Shopify built it" has a surprising answer.

---

## 1. How Shopify actually does it (and why it's different)

**Shopify core is NOT a marketplace — it's a store builder.** Each merchant runs an *independent* store:
own catalog, own checkout, own payments, own customers. There is no shared cart across merchants, no split
settlement, no commission on sales. This is the opposite of a marketplace. So "how Shopify built a
marketplace" is mostly "Shopify deliberately did *not* — it built single-tenant stores." What Shopify has
around the edges:

| Shopify product | What it is | Marketplace? |
|---|---|---|
| **Shop app** | A consumer app that *aggregates/discovers* products across Shopify stores | A discovery front-end — but each purchase still goes through *that merchant's own* checkout (Shop Pay). Not one-cart-many-sellers. |
| **Shopify Collective** | One Shopify store sells another store's products (dropship), with automated inter-merchant payment | Supplier↔retailer, not a public multi-seller marketplace. |
| **Shopify Markets** | International selling (currencies, duties, domains) | Not multi-seller at all. |
| **Commerce Components / Marketplace Kit** | Enterprise APIs to build *your own* marketplace on Shopify's infra | The building blocks — used by large marketplaces, closest analogue. |

**The real multi-seller marketplaces** (Amazon, Flipkart, Etsy, Meesho) are built very differently, and
that's the model here. Their hard parts — the actual scope of a marketplace — are:
1. **Seller onboarding + KYC** (identity, GSTIN, bank).
2. **Multi-seller catalog** (products owned by a seller; approval/quality control).
3. **Unified storefront + one cart/checkout across sellers.**
4. **Split payments** — buyer pays once; platform splits to each seller minus commission (a *payment
   facilitator* problem).
5. **Per-seller fulfillment, shipping, returns.**
6. **Commissions + settlements/payouts** to sellers.
7. **Seller performance** (ratings, SLAs), **dispute resolution**, **trust & safety / fraud**.
8. **Tax** — in India, **TCS under GST** (the marketplace must collect 1% Tax-Collected-at-Source on seller
   supplies), plus each seller is the *supplier of record* (their GSTIN on the invoice; the marketplace is a
   facilitator).

**The key architectural insight:** the split-payments primitive that a marketplace lives or dies on is the
*same* one already planned for platform-managed merchant payments — **Razorpay Route** (see
[razorpay-route-plan.md](razorpay-route-plan.md)). Route lets one payment split to N sub-merchant linked
accounts under Razorpay's PA license — exactly a marketplace's settlement engine. Building Route for the
SaaS side and building it for the marketplace are the *same work*; do it once.

---

## 2. What already exists (day-1 foundation)
- **Schema:** `Sellers` (Status Pending/Active/Suspended, CommissionRate), `SellerUsers` (seller logins),
  `SellerCommissions` (per order-item commission), `SellerSettlements` (payout batches) — `012_marketplace.sql`.
  `Product.SellerId` (nullable) already on products.
- **Multi-tenant core** (`ITenantScoped`, tenant resolution) — a marketplace is one tenant in
  "marketplace mode"; sellers are sub-entities under it.
- **Orders/checkout/GST-invoice/refund/notifications/reviews/shipping** pipelines — all reusable per seller.
- **Razorpay Route plan** — the split-settlement primitive.
- **Entitlements/plans** — a marketplace is a plan capability.

**Gap:** none of the marketplace *flows* are implemented (the tables are unused, per the design-for-V3
principle). Everything below is new behaviour on the existing schema.

---

## 3. Architecture decisions
1. **Seller = sub-entity of a marketplace tenant** (matches the schema: `Sellers` belong to a tenant), not
   "each tenant is a seller." A tenant flips to *marketplace mode*; its catalog becomes seller-owned.
2. **One order → per-seller sub-orders.** A cart spanning 3 sellers becomes one shopper order split into 3
   seller order-groups (fulfilled, shipped, returned independently) — model via `OrderItem.SellerId` +
   `SellerCommissions` per item; a "sub-order" is the set of items for one seller in one order.
3. **Payment: one charge, split via Route.** Buyer pays the order total once; Razorpay Route transfers each
   seller's share (minus commission) to their linked account. The marketplace never holds funds → stays out
   of PA scope (rides Razorpay's license). Commission is retained on the platform account.
4. **Seller is supplier of record.** Each seller's GSTIN goes on the shopper invoice for their items; the
   marketplace issues a *commission* invoice to the seller and handles **TCS**. Reuse the D1 invoice engine
   per seller.
5. **Reuse, don't fork.** Orders, GST invoicing, refunds→credit notes, notifications, reviews, shipping —
   all already exist; extend them to be seller-aware rather than building parallel systems.

---

## 4. Build blocks (phased)

### MP1 — Seller onboarding + KYC + seller portal
- Seller registration → KYC (PAN, GSTIN, bank) → **Razorpay Route linked account** (reuse the Route plan) →
  `Sellers.Status` Pending→Active on approval. `SellerUsers` for seller logins (a role: `SELLER`).
- Seller portal shell (login, profile, KYC status) — a new `/seller` area, or extend admin with a seller role.

### MP2 — Multi-seller catalog
- Products owned by `SellerId`; a seller manages only their own products. Marketplace-operator **approval
  workflow** (new products reviewed before listing) + category/attribute governance.
- Search/listing already exists; make it seller-aware (seller name on PDP, "sold by", seller rating).

### MP3 — Unified storefront + cross-seller cart
- One marketplace storefront aggregating all active sellers' products; cart can hold items from multiple
  sellers (cart already supports arbitrary products — add per-seller grouping in cart/checkout UI).

### MP4 — Split checkout + per-seller orders + commission
- Checkout: one payment for the whole cart → Route transfers per seller (net of `Sellers.CommissionRate` /
  category-level overrides) → write `SellerCommissions` per order-item.
- Split the order into per-seller sub-orders; each seller sees/fulfils only theirs.

### MP5 — Per-seller fulfillment, shipping, returns
- Seller dashboard: their orders, dispatch, tracking (reuse `Shipment`), returns/refunds → credit notes
  (reuse C3), with commission reversal on refund (`SellerCommissions` adjusted).

### MP6 — Settlements / payouts + reporting
- With Route, Razorpay settles to seller bank accounts directly (we don't run payouts) — record
  `SellerSettlements` for reporting/reconciliation. Seller earnings dashboard; commission statements.

### MP7 — Marketplace operator console + trust/safety
- Operator: seller approval queue, commission config (global/category/seller), dispute resolution, seller
  performance (ratings, SLA, cancellation rate), fraud/quality controls, payout reconciliation.

---

## 5. Tax & regulatory (India — do not skip)
- **TCS under GST:** an e-commerce operator must collect **1% TCS** on the net taxable supplies of each
  seller and deposit/report it (GSTR-8). This is a real build: per-seller TCS calc at order time + monthly
  reporting. **Sellers must have a GSTIN** (mandatory to sell on a marketplace).
- **Supplier of record:** the *seller's* GSTIN + name on the shopper's tax invoice for their items (the D1
  engine, made seller-aware); the marketplace issues a **commission invoice** (18% GST on commission) to the
  seller.
- **Payments/RBI:** ride **Razorpay Route** (Razorpay is the PA) — never take seller funds into a platform
  account (that would require a PA license). Same red line as the Route plan.
- **Consumer-protection (e-commerce rules):** seller identity disclosure, grievance officer, return/refund
  policy display — India's Consumer Protection (E-Commerce) Rules.

---

## 6. What's reusable vs genuinely new
| Reuse | New |
|---|---|
| Multi-tenant core, roles, entitlements | Seller entity flows, `SELLER` role, seller portal |
| Orders / cart / checkout pipeline | Cross-seller cart grouping + per-seller sub-orders |
| **Razorpay Route** (split settlement) | Route linked-account per *seller* + commission split |
| D1 GST invoice + C3 credit notes | Seller-as-supplier invoices, **TCS**, commission invoices |
| Shipping / `Shipment` / returns | Per-seller fulfillment + commission reversal on refund |
| Reviews, notifications, search | Seller ratings, "sold by", seller-aware ranking / buy-box |

---

## 7. Recommended sequencing
1. **Do Razorpay Route first** (it's the shared prerequisite for both platform-managed payments *and* the
   marketplace split settlement — build once).
2. **MP1 → MP2 → MP4** (onboarding → catalog → split checkout) is the minimum viable marketplace.
3. **MP3, MP5, MP6, MP7** layer on. TCS (§5) must land before real GMV flows through sellers.
4. Gate the whole thing behind a **marketplace plan/entitlement** — a tenant opts into marketplace mode.

## 8. Open decisions
1. **Listing model:** seller-specific listings (Etsy/Meesho — each seller lists their own product) vs a
   shared catalog with a **buy-box** (Amazon — many sellers on one product page)? Recommend seller-specific
   listings for v1 (far simpler; the schema's `Product.SellerId` fits it).
2. **Fulfillment:** seller-fulfilled only (v1) vs platform-fulfilled (FBA-style, later).
3. **Commission model:** flat %, per-category %, or per-seller negotiated (schema supports per-seller +
   per-commission-row rates).
4. **Who owns the customer relationship / returns SLA** — marketplace-mediated (recommended) vs seller-direct.

**Status:** planned. Prerequisite: Razorpay Route. Then MP1→MP2→MP4 for a minimum viable marketplace, with
TCS compliance before go-live.
