# Stage 5 — Checkout & Money

**Goal:** turn a cart into a paid, invoiced order. The money path.

## Checkout sequence
Cart → resolve **tax** (per HSN) → resolve **shipping** (serviceable pincode + charge) → create **Order** (reserve inventory) → **Payment** → on success: **Invoice (PDF)** + order enters tracking.

## Scope & checklist
- [x] **Tax-at-checkout** — `TaxService` resolves GST by **HSN** (fallback to a default rate), splits **CGST/SGST** (intra-state) vs **IGST** (inter-state) by comparing the destination state to the store state (`StoreState` setting).
- [x] **Shipping** — `ShippingService`: flat method (free above threshold) + `ShippingZones` for **pincode serviceability** (a non-serviceable range is seeded to demo rejection).
- [x] **Orders** — place (reserve inventory, `ORDyyyymmdd-#####`), confirm payment (commit inventory), cancel (release if unpaid / **restock** if paid), order-number generation, `OrderStatusHistory`.
- [x] **Payments** — `IPaymentGateway` with **MockPaymentGateway** (dev default — deterministic create→verify→capture→refund) and **RazorpayPaymentGateway** (real, activated by `Payments:Provider=Razorpay` + keys). Selected in DI.
- [x] **Cancellation & Refund** — gateway refund → `Refunds` + `Payments.Status=Refunded` + inventory restock.
- [x] **Invoice / Billing** — `InvoiceService` generates `Invoices`/`InvoiceItems` with GST split + **PDF download** (QuestPDF). `INV-yyyy-#####`.
- [x] **Admin order management** — list/filter, status transitions (Paid→Packed→Shipped→Delivered), cancel+refund, invoice PDF.

## Endpoints
- Customer: `GET /api/orders/quote`, `POST /api/orders`, `POST /api/orders/{id}/confirm`, `POST /api/orders/{id}/cancel`, `GET /api/orders`, `GET /api/orders/{id}`, `GET /api/orders/{id}/invoice` (PDF).
- Admin: `GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `POST …/{id}/status`, `POST …/{id}/cancel`, `GET …/{id}/invoice`.

## Frontend
- **Checkout** (`/checkout`, auth-guarded): address selection → live quote (subtotal, CGST/SGST or IGST, shipping, total, serviceability) → place & pay. Mock auto-confirms; Razorpay opens the widget when a public key is present.
- **Order detail / history** (`/account/orders`): status, items, totals, payment, **invoice download**, **cancel**.
- **Admin Orders**: table + filter + detail modal with status-advance / cancel-refund / invoice.
- Cart "Proceed to checkout" wired (login redirect for guests); header + account nav links.

## Config
`appsettings.json` → `Payments` (`Provider: Mock`, empty Razorpay keys). Seed migration `020_checkout_config.sql`: HSN `4910`+GST 12% on the calendar catalog, `StoreState`/`StoreGstin`/`StoreLegalName`, a non-serviceable shipping zone.

## Verification
Backend tested end-to-end on a throwaway port: quote (12% GST, intra-state split, free shipping), place→pay (mock)→**Paid**, inventory reserve→commit, invoice PDF (valid `%PDF`), cancel→**refund Processed**+restock, non-serviceable pincode rejected, admin list + Paid→Packed. Web builds clean; `/`, `/cart`, `/product/:slug`, `/checkout`→login, `/account/orders`→login all SSR-render 200. Test data cleaned up.

## Deferred
COD operational workflow (V1.1), coupons (Stage 7), shipments/courier tracking detail & confirmation email (Stage 6), credit notes (future).

## Tables (exist)
`Orders, OrderItems, OrderStatusHistory, Payments, PaymentTransactions, Refunds, TaxRates, ShippingMethods, ShippingZones, Invoices, InvoiceItems`

## Dependencies
Cart (Stage 4), Inventory (Stage 3). Razorpay keys → user-secrets/env when going live.

**Status:** ✅ Tax + shipping + orders + payments (mock/Razorpay) + refund + invoice PDF + admin order management. Verified backend + build + SSR.
