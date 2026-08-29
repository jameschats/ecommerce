# Plan — Multi-Country Expansion (internationalization)

**Goal:** today the platform is India-only (INR, GST, +91, pincodes, Shiprocket, English). This plan makes
it able to onboard stores in **other countries** — different currency, tax, payment methods, addresses,
phone/messaging, and locale — **without a rewrite**, by turning today's hardcoded India assumptions into a
per-tenant **country configuration** that drives pluggable providers.

**Status:** planned. Large, cross-cutting. Sequenced so each phase ships value even before a second country
goes live.

> **Design stance:** don't build "every country" up front. Build the **seams** (a `Country` on the tenant +
> provider abstractions) and prove them by launching **one** second country end-to-end. Adding the third is
> then config + one provider impl, not surgery.

---

## 1. The India-coupling seams (verified in code)

| Dimension | Today (India-hardcoded) | Where |
|---|---|---|
| **Anchor** | No `Country` / `Currency` / `Locale` on `Tenant` at all | `Data/Entities/Tenant.cs` |
| **Currency** | `"INR"` literal in ~15 checkout/billing sites; entity defaults `Order/Payment/Supplier.Currency="INR"`; `UsdToInr=88`; Angular `currency:'INR'` pipes; `₹`/`Rs.` in UI & invoice | `OrderService`, `SubscriptionService:120`, `AiCreditController`, `AiOptions:34`, storefront components |
| **Tax** | `TaxService` is a **GST engine** (HSN rate lookup, CGST/SGST vs IGST inter-state split); `TaxRate` has `Cgst/Sgst/IgstRate`; `ITaxService` shape is India | `Features/Checkout/TaxService.cs`, `Data/Entities/TaxRate.cs` |
| **Payments** | Razorpay only (India); factories recognize `"Mock"\|"Razorpay"` strings; `IPaymentGateway` is Razorpay-shaped (paise, HMAC, key_id) | `Features/Payments/` |
| **Addresses** | `CustomerAddress.Pincode` required, `Country="India"` default, free-string `State`; serviceability by pincode ranges; Shiprocket | `CustomerAddress.cs`, `ShippingZone.cs`, `Features/Shipping/Shiprocket/` |
| **Phone/OTP/msg** | `SmsOptions.CountryCode="91"`; MSG91 (India SMS + DLT); WhatsApp providers hardcode `+91`; bare-10-digit assumption | `SmsOptions.cs:20`, `Msg91SmsSender`, `*WhatsAppProvider.cs` |
| **Invoicing** | Fully India-GST: GSTIN, HSN column, CGST/SGST/IGST, "TAX INVOICE / BILL OF SUPPLY", "Rs." | `Features/Orders/InvoiceService.cs` |
| **Locale/i18n** | No Angular i18n (no `LOCALE_ID`/`registerLocaleData`/translations); all English; ₹ everywhere | `ecomm.web` |

---

## 2. The anchor: a per-tenant Country configuration

Add to `Tenant` (migration, additive):
- `CountryCode` (ISO-3166 alpha-2, e.g. `IN`, `AE`, `US`, `GB`) — the branch key everything reads.
- `CurrencyCode` (ISO-4217, e.g. `INR`, `AED`, `USD`) — replaces the implicit INR.
- `Locale` (e.g. `en-IN`, `en-AE`) — number/date/currency formatting + future translations.
- `Timezone` (IANA) — for schedules, invoices, reports.

Introduce a small **`CountryProfile`** concept (seeded config, not per-tenant): for each supported country,
its default currency, tax model, address schema, phone rules, allowed payment providers, and locale. The
tenant's `CountryCode` selects a profile; the profile drives the pluggable providers below. Adding a country
= add a `CountryProfile` row + whatever provider impls it references.

> Everything below reads the tenant's country/currency/profile. India remains the default profile, so
> existing stores behave identically (no behavior change until a tenant is explicitly non-IN).

---

## 3. Dimensions — current → target

### 3.1 Currency & money
- **Introduce a money seam.** Stop passing the `"INR"` literal; every write reads `tenant.CurrencyCode`.
  Entity `Currency` columns already exist on `Order`/`Payment` — populate them from the tenant instead of
  defaulting. Add `Currency` to `Product` price context (or derive from tenant — single-currency-per-store
  is the right v1: a store sells in one currency).
- **Formatting:** replace hardcoded `currency:'INR':'symbol'` Angular pipes and `₹`/`Rs.` literals with a
  currency-aware formatter driven by tenant currency+locale (a `MoneyPipe`/`money()` helper).
- **`UsdToInr=88`** (AI cost tracking) → generalize to `UsdToLocal` per currency, or keep provider cost in
  USD internally and convert only for display. (Internal cost accounting can stay USD.)
- **Decision:** single currency per store (recommended) vs multi-currency display for shoppers. V1: one
  store = one currency.

### 3.2 Tax — the biggest unwind
- Extract an **`ITaxProvider` per country** from today's GST-shaped `TaxService`:
  - `IndiaGstTaxProvider` — today's HSN + CGST/SGST/IGST logic, unchanged.
  - `SimpleVatTaxProvider` — single/again-few-rate VAT (UAE 5%, EU per-rate) with a VAT number instead of GSTIN.
  - `UsSalesTaxProvider` — destination-based, origin/nexus rules (or integrate Avalara/TaxJar for the US,
    which is genuinely hard to do by hand).
  - `NoOrFlatTaxProvider` — zero/flat fallback.
- The provider is selected by the tenant's `CountryProfile`. `ITaxService` stays the caller-facing seam; the
  India-specific method surface (`IsInterStateAsync`, HSN) moves *inside* the India provider.
- `TaxRate` gains a country/kind discriminator; India rows keep CGST/SGST/IGST, others use a generic
  `rate + label`.

### 3.3 Payment gateways
- Keep `IPaymentGateway` (one-time) + the planned `IRecurringBillingGateway` (see
  [billing plan](platform-billing-plan.md)); add a **`Stripe*` implementation** for non-India.
- The `CountryProfile` lists allowed providers; a tenant in `IN` gets Razorpay, in `AE/US/GB` gets Stripe.
- Widen the `"Mock"|"Razorpay"` provider enums/factory branches to include `Stripe`. Both the merchant lane
  (shopper→merchant) and platform lane (merchant→platform) need the second provider.
- **Note:** Stripe's model (PaymentIntents, webhooks, zero-decimal-currency handling) differs from
  Razorpay's paise/HMAC shape — the abstraction must not leak Razorpay assumptions (paise conversion,
  key_id-as-PublicKey). Normalize amounts to minor units per currency.

### 3.4 Addresses & shipping
- Replace the pincode-required, Indian-state model with a **generic postal address**: `PostalCode`
  (optional per country), `Region/State` (free or country-enumerated), `Country`, format hints from the
  profile. Keep `Pincode`→`PostalCode` as a rename/alias; India validation moves behind the profile.
- Shipping serviceability: today pincode-range zones + Shiprocket (India). Generalize `ShippingZone` to
  postal/region/country matching; add per-country carrier integrations (Shiprocket stays IN-only; add e.g.
  Stripe-agnostic flat-rate / country carriers elsewhere). Flat-rate + free-over-threshold works anywhere
  as a baseline.

### 3.5 Phone / OTP / messaging
- Make phone **country-code-aware** (libphonenumber-style validation by tenant country) instead of the
  bare-10-digit + `+91` assumption. Store E.164.
- Messaging providers per region behind the existing sender abstractions: **MSG91 (India SMS/DLT)** →
  Twilio (international SMS/WhatsApp) selected by profile. WhatsApp providers already abstract `IWhatsAppProvider`;
  drop the hardcoded `+91` normalization into the profile.
- India DLT template registration is India-specific regulation — keep it only for the IN profile.

### 3.6 Locale / i18n (language + formatting)
- **Formatting first, translation later.** Register Angular locales (`registerLocaleData`) and drive
  number/date/currency formatting from tenant `Locale` — this alone makes non-IN stores look correct
  without translating a word.
- **Translation (later phase):** introduce an i18n framework (Angular i18n or `@ngx-translate`) and extract
  UI strings. Big effort; defer until a non-English market is actually targeted. English-in-many-countries
  (UAE, SG, parts of EU) is a valid v1 that needs only formatting, not translation.

### 3.7 Invoicing
- Country-specific invoice templates behind an `IInvoiceRenderer` per profile: India-GST (today), VAT
  invoice (VAT number, VAT line), plain commercial invoice. Currency symbol/format from tenant.

### 3.8 Compliance / legal
- Per-country data-protection: DPDP (India, already flagged), GDPR (EU — consent, right-to-erasure, data
  residency), etc. Cookie/consent banners driven by profile. This is a legal workstream that pairs with the
  behavioural-capture DPDP review already open.

---

## 4. Architecture principles
1. **One tenant = one country/currency/locale.** Don't build per-shopper multi-currency; build per-store.
2. **`CountryProfile` selects providers.** Tax, payments, shipping, messaging, invoicing are all interfaces
   chosen by the tenant's country — India is just the first profile.
3. **India stays the default and is untouched** until a tenant is explicitly non-IN; zero regression risk
   for the live business.
4. **Prove with one second country end-to-end** before generalizing further. UAE (`AE`, AED, 5% VAT,
   English, Stripe) is a good first target — English-speaking (no translation), simple single-rate VAT,
   Stripe-supported.

---

## 5. Phasing
1. **P1 — Currency-neutral core.** Add `Country/Currency/Locale` to `Tenant` + `CountryProfile`; remove the
   `"INR"` literals (read tenant currency); currency-aware formatting in the API and Angular. India behaves
   identically. *No new country yet — this de-hardcodes.*
2. **P2 — Tax provider abstraction.** Extract `IndiaGstTaxProvider`; add `SimpleVatTaxProvider`; profile-
   selected. `IInvoiceRenderer` per profile.
3. **P3 — Second payment gateway.** Stripe behind `IPaymentGateway` (+ recurring), profile-gated.
4. **P4 — Addresses/shipping/phone/messaging generalization.** Generic postal address; Twilio; libphonenumber.
5. **P5 — Launch country #2 (UAE) end-to-end**; iterate from real gaps.
6. **P6 (later) — Translation/i18n** for a non-English market; deeper compliance (GDPR).

## 6. Open questions / decisions
1. **First target country?** (Recommend UAE: English + single-rate VAT + Stripe = smallest unwind.)
2. Single-currency-per-store (recommended) vs shopper-facing multi-currency display?
3. Build US sales tax by hand vs integrate Avalara/TaxJar? (Recommend integrate — US tax is a swamp.)
4. Translation timing — only when a non-English market is committed?
5. Data residency requirements per target market (esp. EU) — affects hosting, not just code.
