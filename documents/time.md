# Development Time Log

> Hours are **scope-based estimates** (effort implied by what was built), not literal wall-clock tracking. Update as work continues.

| Date | Est. hours | What was developed |
|------|-----------:|--------------------|
| 2026-06-25 | 5.0 | **Kickoff & foundations.** Distilled the design doc (`design.md`) from the requirements; scaffolded the **.NET 9 Web API** + **Angular 21 + Tailwind**; set up MySQL connection + numbered SQL-migration convention; built **Stage 0** — the full V3 schema (61 tables, 72 FKs) across migrations `001`–`015` with seed data, validated against MySQL. |
| 2026-06-26 | 3.0 | **Auth → Catalog → Theming → CalendarShop.** Stage 1 auth (BCrypt + JWT, admin-toggleable Email/Mobile-OTP/Google providers) backend + Angular auth UI; Serilog + docs/stage reorg + `CLAUDE.md`. Stage 2 catalog: backend (categories/brands/products/variants/attributes + Excel import/export), **SSR + SEO** storefront (home/PLP/PDP), and admin catalog UI. Stage 2.5: **Theme Engine** (CSS-variable theming) + **CMS-lite** home-section manager. Rebranded to **CalendarShop** with a calendar catalog, hero slider, category icons, testimonials marquee, footer, and About/Contact/FAQ pages. |
| 2026-06-26 | 6.0 | **Inventory/Search → Shopping.** Stage 3: inventory (available/reserved, transaction ledger, low-stock, Excel import/export, admin UI) + **MySQL FULLTEXT** search + autocomplete + popular searches; storefront stock display. Follow-ups: attribute/category/brand search, **variant-level stock** + **transaction history** admin UI, fixed a latent inventory-list query. Stage 4 **shopping**: cart backend (user + guest carts via `X-Cart-Token`, **merge-on-login**, stock validation, price snapshot), customer **profile** + **address book** (default handling); frontend **CartService** + header badge + PDP add-to-cart/buy-now + cart page + auth-guarded **account** area (profile, addresses). |
| 2026-06-26 | 7.0 | **Stage 5 — Checkout & Money.** GST tax engine (HSN resolve + CGST/SGST vs IGST split), shipping (flat + free-threshold + **pincode serviceability** via zones), **orders** (reserve→commit inventory, order numbers, status history), **payment gateway abstraction** (deterministic **Mock** default + real **Razorpay** when keys set), **cancellation + refund** (gateway refund + restock), **invoice** generation + **PDF** (QuestPDF) with GST split, and **admin order management** (list/filter, status transitions, cancel-refund). Frontend: checkout page (address → live quote → pay), order history/detail with cancel + invoice download, admin Orders screen. Verified end-to-end (place→pay→invoice→cancel→refund) + SSR. |
| | **21.0** | **Total so far** (through end of Stage 5). |

## Milestones reached
- ✅ Stage 0 — Schema & seed
- ✅ Stage 1 — Identity (auth backend + Angular UI)
- ✅ Stage 2 — Catalog (backend + storefront SSR/SEO + admin UI)
- ✅ Stage 2.5 — Theming & CMS-lite
- ✅ Demo content — CalendarShop storefront + content pages
- ✅ Stage 3 — Inventory & Search
- ✅ Stage 4 — Shopping (cart, profile, addresses)
- ✅ Stage 5 — Checkout & Money (tax, shipping, orders, payments, refund, invoice PDF)
- ⬜ Stage 6 — Post-purchase & engagement (next)

> See [stages/README.md](stages/README.md) for the full stage breakdown and status.
