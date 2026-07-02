# Stage 7 — Differentiators (V1.1)

**Goal:** admin-configurable storefront + deferred conveniences. Built after the launch-critical path.

## Scope & checklist
- [x] **Theme Engine** — admin sets primary/secondary color, font, button style, **logo** (now uploadable via the media feature) (`Themes`/`ThemeSettings`)
- [x] **CMS Home-Page Builder** — compose/reorder/rename/hide sections + **schedule** (StartsAt/EndsAt visibility window); Banners admin (`Pages`/`PageSections`)
- [x] **COD** — admin **on/off flag**; place-without-prepay (order → Confirmed, inventory committed), fulfil, **cash collected on delivery**; cancel restocks with no refund
- [x] **Wishlist / Save-for-Later** — `WishlistItems`; heart toggle on cards/detail, wishlist page, move-to-cart

## Implementation
- **Logo upload:** Theme admin reuses `/api/admin/media` (upload or paste URL).
- **Section scheduling:** `PageSections.StartsAt/EndsAt` (already in schema) now filtered in the public
  home list (`CmsService`); admin has From/To date pickers. Out-of-window sections hide on the storefront.
- **Wishlist:** `Features/Wishlist` — authed CRUD returning `ProductListItemDto`; frontend keeps a reactive
  set of wishlisted ids (synced to auth); reusable `WishlistButtonComponent`.
- **COD:** `CodEnabled` setting (Store settings). `OrderService.PlaceOrderAsync` branches on
  `PaymentMethod=COD` → status **Confirmed**, inventory committed, COD `Payment` (Pending), no gateway.
  Admin flow is COD-aware (transition map); `CollectCodOnDeliveryAsync` marks the payment Success on delivery.

## Tables
`Themes, ThemeSettings, Pages, PageSections` (existing) + `WishlistItems` (migration 025). COD = a payment method + `CodEnabled` setting.

## Verification
Backend tested end-to-end on a throwaway port: wishlist (idempotent add/list/remove, 401 for guests);
section scheduling (expired/future sections hidden from `/cms/home`, present in admin); COD (enable→
place→Confirmed+inventory committed+confirmation→Packed→ship→deliver+cash collected; disabled→rejected).
Web builds clean.

**Status:** ✅ Theme (incl. logo upload) · CMS scheduling · wishlist · COD — verified backend + build.
Deferred (future): COD RTO/remittance reconciliation, more section types, per-variant wishlist.
