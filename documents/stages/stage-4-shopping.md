# Stage 4 — Shopping (Cart & Customer)

**Goal:** customer profile, addresses, and the shopping cart.

## Scope & checklist
- [x] Customer profile (view/update; built on `Users`) — `GET/PUT /api/account/profile`
- [x] Address book (billing & shipping, default flag, pincode) — `GET/POST/PUT/DELETE /api/account/addresses`, `POST …/{id}/default`; first address auto-default; setting a new default unsets the previous; soft-delete promotes another to default
- [x] Cart: add / remove / update quantity (user **+** guest/session carts) — `GET /api/cart`, `POST /api/cart/items`, `PUT/DELETE /api/cart/items/{id}`, `DELETE /api/cart`
- [x] Cart item **price snapshot** (`UnitPrice` captured at add time); **merge guest cart on login** — `POST /api/cart/merge`
- [x] **Stock validation** on add/update (against the product's total available inventory; overstock rejected with "Only N in stock")
- [~] Inventory reservation hooks — Reserve/Release/Commit exist (Stage 3); **wired into order placement in Stage 5**, not the cart (cart only validates availability)

## Design notes
- **Cart resolution:** logged-in users own a cart by `UserId`; guests by a client-generated **cart token** sent as the `X-Cart-Token` header (persisted in `localStorage` as `ecomm.cart`). For authenticated calls the JWT wins server-side. On login the guest cart is merged into the user cart and marked `Converted`.
- **Stock at product level:** availability is the sum of the product's inventory rows (matches the storefront in-stock logic). Per-variant reservation precision is deferred to checkout.
- Schema unchanged — `CustomerAddresses`, `Cart`, `CartItems` already existed.

## Frontend
- `CartService` (signals: `cart`, `itemCount`, `subtotal`, `items`) auto-loads on app start and **merges on login** via an effect watching `auth.currentUser`. SSR-safe (browser-gated).
- Header **cart badge**; PDP **Add to cart / Buy now** wired (variant resolved from selected options; stock errors surfaced); full **cart page** (qty steppers, remove, summary, checkout placeholder → Stage 5).
- **Account** area (`/account`, auth-guarded): layout + **Profile** (name/phone edit) + **Addresses** (CRUD, set default).

## Verification
Backend tested end-to-end on a throwaway port (guest cart add/update/overstock-block, profile get/update, address create/list/default-switch/update, guest→user merge). Web builds clean; all key pages (`/`, `/cart`, `/product/:slug`, `/account` → login redirect) SSR-render 200.

## Tables (exist)
`CustomerAddresses, Cart, CartItems`

## Dependencies
Catalog (Stage 2), Inventory (Stage 3), Identity (Stage 1).

**Status:** ✅ Backend + storefront cart + account pages done. Checkout/reservation commit lands in Stage 5.
