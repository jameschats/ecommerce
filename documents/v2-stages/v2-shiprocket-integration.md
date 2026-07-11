# Plan — Shiprocket fulfillment integration (per-tenant, embedded)

## Decisions (confirmed with user)
- **Two fulfillment methods per store:** **Self** (manual — merchant enters courier + tracking, uses
  manual shipping rates) — the default — and **Shiprocket** (live rates + auto AWB/pickup/label/tracking).
  The switch is `TenantShippingAccount.IsEnabled` (off = Self, on = Shiprocket) and gates **both** rates and
  fulfillment; Self is always the fallback.
- **Per-tenant accounts:** each store connects its **own** Shiprocket account (mirrors per-tenant Razorpay).
  This is how Shopify + Shiprocket work (per-merchant, API-based).
- **Embedded in our admin:** the merchant fulfills from **our** order page (not Shiprocket's panel). We call
  Shiprocket's API directly (we are the custom channel).
- **Build now, verify live later** once the user supplies Shiprocket credentials.

## How Shopify+Shiprocket works (research)
Per-merchant, API-based: orders sync into the Shiprocket panel where the merchant assigns AWB/courier,
schedules pickup, prints labels; Shiprocket syncs fulfillment + tracking back. Shopify is a closed platform so
it uses the "channel app" model. **We are the platform**, so Shiprocket's own recommendation for us is the
direct **API integration** — create order → assign AWB → pickup → label → tracking webhooks.

## Data model
- **`TenantShippingAccount`** (migration 170, `ITenantScoped`): Provider, Email, `PasswordCipher`
  (Data-Protection ciphertext), PickupPincode, PickupLocation, IsVerified, IsEnabled, ConnectedAt.
- Bearer token (~10-day) is **cached in-memory per tenant**, never stored.
- Reuse the existing **`Shipment`** entity (OrderId, Courier, TrackingNumber, Status, ShippedAt, DeliveredAt)
  — Shiprocket just populates it automatically instead of the merchant typing it.

## Phases
- **SR1 — Connect + method toggle** ✅: `TenantShippingAccount` + migration + DbContext; `ShiprocketSettingsService`
  (Self/Shiprocket, encrypt password, **verify creds by auth**) + `/api/admin/shipping/shiprocket/settings`.
- **SR2 — Per-tenant rates:** a scoped per-tenant `IShiprocketService` (auth/token cache + `GetCheapestRate`),
  refactor `ShippingService.QuoteAsync` to use it when the tenant has Shiprocket enabled (else manual rates).
  Keep `ShiprocketClient.ParseCheapest` (unit-tested) as the parser.
- **SR3 — Create shipment + AWB:** admin "Ship with Shiprocket" on a Paid/Confirmed order → create Shiprocket
  adhoc order (items + address + weight + pickup location) → assign cheapest-courier **AWB** → store
  Courier + TrackingNumber on the `Shipment`, status Shipped (replaces manual `CreateShipmentAsync` entry).
- **SR4 — Pickup + label:** schedule pickup request; generate label/manifest PDF (download from admin).
- **SR5 — Tracking webhooks:** `/api/webhooks/shiprocket` (shared-secret/HMAC) → map status → `Shipment.Status`
  + order timeline + customer notification; poll as fallback.
- **SR6 — Frontend:** merchant Settings UI (radio: Self vs Shiprocket + connect form + pickup) and the order-page
  "Ship with Shiprocket / pickup / label / track" actions. Storefront checkout already consumes live rates.

## Verification
- **Unit:** `ParseCheapest`, status mapping, weight/pincode payload building — tenant-isolated.
- **Live (once creds added):** connect a store → live rate at checkout → place order → Ship with Shiprocket →
  AWB assigned → pickup scheduled → label downloads → tracking webhook flips status → customer notified. A
  Self-mode store is unaffected (manual flow).

## Notes
- The old app-level `Shiprocket:Provider` config path is superseded by per-tenant; keep the `ShiprocketOptions`
  (BaseUrl/DefaultWeightKg) as shared defaults.
- DataProtection keys must persist on the VPS (same caveat as the Razorpay secret) so ciphertext survives restarts.
