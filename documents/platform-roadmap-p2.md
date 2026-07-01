# Platform Roadmap (P2) — from a store to a commerce platform

The V1 schema (60 tables) already covers the full commerce lifecycle at **Shopify/WooCommerce-core
level**, plus multi-tenant, RBAC, a marketplace layer, audit log, notification templates and bulk import
— things those platforms gate behind enterprise tiers or plugins.

This doc lists what's **missing to rival Shopify/Woo feature-for-feature**, as **future schema modules**.
Every item is an **additive, forward-only migration** — the V3-first design means none of these require
a re-architecture. Tiers are by leverage, not difficulty.

> **Highest-leverage two for "platform" positioning:** **Metafields** (extensibility) and
> **Multi-currency + i18n** (international). Do these first if the goal is to market as a Shopify alternative.

---

## Tier 1 — Store completeness (small, high customer value)

| Module | New tables | Notes |
|---|---|---|
| **Wishlists / saved items** | `Wishlists`, `WishlistItems` | Per-user saved products; guest→user merge like the cart. |
| **Gift cards / store credit** | `GiftCards`, `GiftCardTransactions`, `StoreCreditLedger` | Spendable balances (distinct from `CreditNotes`, which are refund accounting). Redeem at checkout. |
| **Returns / RMA** | `ReturnRequests`, `ReturnItems` (+ reasons) | You have order status `Returned` + `Refunds`; this adds the **workflow**: request → approve → restock → refund/exchange. |
| **Marketing / retention** | `NewsletterSubscribers`, `AbandonedCarts` (or reuse `Cart.Status='Abandoned'`), `Campaigns` | Newsletter capture, abandoned-cart recovery, basic campaigns (send via existing `NotificationTemplates`). |

## Tier 2 — Platform extensibility (the real Shopify-class differentiators)

| Module | New tables | Notes |
|---|---|---|
| **Metafields / custom fields** ⭐ | `Metafields` (`OwnerType`, `OwnerId`, `Namespace`, `Key`, `Value`, `Type`) | Generic key-value on **any** entity (product/order/customer/…). This is *how* Shopify apps extend everything — the single biggest "platform" unlock. |
| **Webhooks + API apps** | `ApiApplications`, `ApiTokens`, `Webhooks`, `WebhookDeliveries` | Lets external apps subscribe to events (order.created, etc.) and call a scoped API — the app-ecosystem foundation. |
| **Multi-currency + i18n** ⭐ | `Currencies`, `ExchangeRates`, `Translations` (`EntityType`, `EntityId`, `Field`, `Locale`, `Value`) | You're INR/English-only today. Enables Markets-style international selling. |
| **Multi-location inventory** | `Locations`; add `LocationId` to `Inventory` (+ `InventoryTransactions`) | Per-warehouse stock, transfers, "ship from nearest". Currently single-location. |
| **Customer groups + price lists** | `CustomerGroups`, `PriceLists`, `PriceListItems` | B2B / wholesale tiered pricing, group-based discounts. |

## Tier 3 — Advanced / niche

| Module | New tables | Notes |
|---|---|---|
| **Purchase orders / procurement** | `PurchaseOrders`, `PurchaseOrderItems`, `GoodsReceipts` | Builds on **Suppliers/ProductSuppliers** (already added). Closes the loop from low-stock → reorder → receive → cost. |
| **Subscriptions / recurring** | `SubscriptionPlans`, `Subscriptions`, `SubscriptionInvoices` | Recurring billing (calendar-of-the-month clubs, etc.). |
| **Sales channels / POS** | `Channels`, `ChannelListings` | Sell via POS, social, marketplaces from one catalog. |
| **Content: blog + navigation** | `Articles`, `ArticleComments`, `Menus`, `MenuItems` | Blog/SEO content and custom storefront menus (beyond the current `Pages`). |
| **Fraud / risk** | `OrderRiskAssessments` | Risk score per order (rules or 3rd-party). |
| **Full net P&L** | `OrderCosts` (gateway fee, shipping cost, handling) | Extends the P1 analytics from **gross margin** to true **net profit**. |

---

## Sequencing suggestion
1. **P1 first:** Analytics & Reporting (profit dashboard + Umami) — see [stages/stage-analytics-reporting.md](stages/stage-analytics-reporting.md).
2. **P2 kick-off:** **Metafields** + **Multi-currency/i18n** (platform positioning) → then Tier 1 store features by customer demand → Webhooks/API when a partner needs to integrate.
3. **Procurement (Tier 3)** whenever inventory replenishment becomes manual pain — the Suppliers foundation is already in.

_All additive. The schema-first foundation (multi-tenant, RBAC, media, notifications, audit, marketplace)
means each module is new tables + a feature slice, not a rewrite._
