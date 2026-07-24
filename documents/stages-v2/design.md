# DailyCalendarShop — Design Document (Phase 1)

**Status:** Draft 3 — written from Anna's WhatsApp notes + venuscrackers.in screenshots (24 Jul 2026).
*Draft 2: per-channel Mock/Live configuration from admin (§9), WhatsApp moved into Phase 1 (§9.4), bulk image handling decided (§10.2), Phase 2 re-scoped as separate functionality (§16).*
*Draft 3: dropped the browser-console echo and Mock Outbox; email integrates with Brevo early (§9.3); WhatsApp Click-to-send confirmed as the launch mode (§9.4).*
**Scope of this document:** **Phase 1 only.** Phase 2 will be appended after Phase 1 is agreed.
**Base platform:** this is the `DailyCalendarShop` branch of the `ecommerce` repo (Mini Flipkart). It reuses that codebase and schema; it does **not** merge back to `main`.
**Database:** its own local MySQL schema `dailycalendarshop` (migrations 001–029 applied).

---

## 1. What we are building

A **single-seller, wholesale-first calendar shop** that sells *daily calendars* (and related calendar products) to dealers and bulk buyers.

The UX model is deliberately **not** a browse-and-click storefront. It is a **single-page quick-order price list**: the entire catalogue is one long table, the buyer types quantities straight into the rows, running totals update live, and one submit turns the whole thing into an order. This is the pattern used by the Sivakasi crackers sites (reference: `venuscrackers.in`) and it is what the calendar wholesale trade already understands.

> **Reference vs. us.** We copy the crackers sites' *interaction model* (one table, type quantities, live totals, estimate drawer, one form at the bottom). We do **not** copy their look — those sites are visually dated (loud red/orange, cramped tables, 2010-era chrome). Our look follows our own CalendarShop direction: modern, bright, clean. See §11.

### Who uses it
| Actor | What they do |
|---|---|
| **Dealer / bulk buyer** (primary) | Opens the price list, tabs down the table entering quantities, reviews the estimate, logs in with OTP, places the order, pays by UPI/bank transfer, tracks it. |
| **Retail / individual buyer** | Same flow, smaller quantities. |
| **Shop admin** (Anna + staff) | Uploads the catalogue from one spreadsheet, confirms payments received, enters courier + tracking number, marks delivered, reads reports. |

### Guiding constraints (Anna's words)
- *"Simple not complex."*
- *"Everything on one table."*
- *"One excel sheet with data to upload as a whole… the attributes."*
- *"There will be 400 plus products."*
- *"Payment manual display of account details / QR code."*
- *"Login using mobile… mostly mobile to push WhatsApp message."*
- *"Login with my account facility to check orders."*

---

## 2. Phase 1 scope

### In scope
1. **Public pages:** Home, About Us, Order Now, Contact Us. *(No "Safety Tips" — that is a fireworks thing, not applicable to calendars.)*
2. **Quick-order table** — the whole catalogue in one table, grouped by category band, keyboard/tab-driven quantity entry, live totals.
3. **Estimate drawer** — right-side slide-in cart, editable quantities, remove line, totals, "Confirm Estimate".
4. **Order form** — State, City, Name, Mobile, Email, Address + order summary (Net Total, Discount, Sub Total, Min. Order Amount, Packing Charges, Round Off, Overall Amount).
5. **OTP login gate at order placement** — guest builds the order freely; login (email or mobile OTP) is required only at Submit.
6. **Manual payment** — after the order is placed we show a **UPI QR code + bank account details**. Customer pays via GPay/PhonePe/transfer. Admin confirms receipt manually.
7. **Notifications on three channels** — **Email** (Brevo), **SMS OTP**, and **WhatsApp** — each independently switchable between **Mock and Live** from an admin screen, with no redeploy. See §9.
8. **Manual fulfilment** — admin enters courier name + tracking number, marks dispatched/delivered. *(Already exists in the base repo.)*
9. **Admin catalogue upload** — one spreadsheet (CSV **or** Excel) uploads/updates all 400+ products in one go.
10. **My Account** — order history + status + tracking for the logged-in buyer.
11. **Admin order management** — list, filter, view, confirm payment, dispatch, deliver, cancel.
12. **Admin integration settings** — provider mode and credentials for each channel, plus a *Send test* button per channel.

### Explicitly out of Phase 1
- Razorpay / any online payment gateway (**dropped by decision** — manual QR only).
- Product detail pages, wishlist, reviews & ratings, coupons, product grid/PLP browsing. *(The code exists in the base repo — we simply do not route to it.)*
- Reports. **Anna will specify these separately** — Phase 1 ships the data needed to build them; the report screens come after.
- COD, multi-seller, subscriptions, SEO sitemap infra.

---

## 3. Stack

Unchanged from the base platform — this is a customization, not a rewrite.

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core .NET 9 — `ecomm.api`, vertical slices |
| Frontend | Angular 21 + Tailwind (SSR enabled) — `ecomm.web` |
| Database | MySQL 8 — schema **`dailycalendarshop`** |
| ORM | EF Core 9 + Pomelo, hand-authored entities |
| Auth | BCrypt + JWT, OTP providers |
| Email | **Brevo (Sendinblue)** — ~300 emails/day free tier |
| Logging | Serilog |

New migrations for this project continue the same forward-only numbering from **`030_`** onward.

---

## 4. Information architecture

```
/                 Home        — announcement bar, hero, intro, THE TABLE, terms, footer
/order            Order Now   — THE TABLE, bare (no hero/marketing), straight to business
/about            About Us    — static content page
/contact          Contact Us  — address, phone, WhatsApp, email, map, enquiry form
/order/{no}/pay   Payment     — UPI QR + bank details (shown right after order placement)
/account/orders   My Orders   — order history, status, courier + tracking
/admin/**         Admin       — catalogue upload, orders, payments, settings, reports (later)
```

**Home vs. Order Now.** Anna said these are "almost the same". The difference:
- **Home** = announcement strip + hero/banner + a short intro block + **the same table** + Terms & Conditions + footer. It is the marketing face.
- **Order Now** = the table alone, sticky toolbar, minimal chrome. It is the working screen a dealer keeps open.

Both render **the identical table component**, so there is one implementation and one source of truth.

---

## 5. The Quick-Order Table (the core screen)

This is the product. Everything else is supporting cast.

### 5.1 Columns

Derived from Anna's *"Item design no, price, discount %, discounted price, total, image of Calendar design parameters"*:

| # | Column | Source | Notes |
|---|---|---|---|
| 1 | **Image** | `ProductImages.Url` (primary) | Small thumbnail; click → lightbox with the full calendar design. Lazy-loaded. |
| 2 | **Design No** | `Products.Sku` | The trade identifier. Also the upsert key for the spreadsheet import. |
| 3 | **Product Name** | `Products.Name` | e.g. *"Daily Calendar — 2027 Deluxe (Tamil)"* |
| 4 | **Content** | attribute `Content` | Pack unit — `Pkt` / `Box` / `Pcs` / `Bundle`, e.g. *"1 Box (50 Pcs)"* |
| 5 | **MRP** | `Products.CompareAtPrice` | Struck through, muted |
| 6 | **Disc %** | *computed* | `(MRP − Price) / MRP × 100`, rounded. Not stored — always derived, so it can never disagree with the prices. |
| 7 | **Our Price** | `Products.Price` | The price actually charged |
| 8 | **Quantity** | user input | Numeric input, empty by default |
| 9 | **Total** | *computed* | `Qty × Our Price`, read-only |

> The reference site stores the discount as a category-level label ("80% DISCOUNT" in the band). We compute it **per row** instead — more honest, and it survives per-product price edits.

### 5.2 Grouping

Anna: *"Category will be size, type."*

We use a **two-level category tree** and render **one coloured band row** per leaf category, spanning the full table width — exactly like the reference:

```
Type (level 1)          →  Size (level 2)          →  band label
Wall Calendars             12" × 18"                  WALL CALENDARS — 12" × 18"
Wall Calendars             15" × 20"                  WALL CALENDARS — 15" × 20"
Daily / Date Calendars     Small                      DAILY CALENDARS — SMALL
Table / Desk Calendars     Standard                   TABLE CALENDARS — STANDARD
```

The `Category` dropdown in the sticky toolbar jumps/filters to a band. Bands are collapsible.

### 5.3 Keyboard-first entry — *required*

This is the single most important interaction. A dealer entering 60 quantities must never touch the mouse.

| Key | Behaviour |
|---|---|
| `Tab` / `Shift+Tab` | Move to next / previous **quantity** input, skipping every other focusable element in the row. Sequential DOM tab order, no `tabindex` hacks. |
| `Enter` / `↓` | Next quantity input |
| `↑` | Previous quantity input |
| Type digits | Row total, band subtotal and the sticky header totals update on every keystroke (debounced ~50 ms) |
| `Esc` | Clear the current input |
| `/` | Jump focus to the search box |

Additional rules:
- Non-numeric input rejected; negative and non-integer rejected; blank = 0 = not ordered.
- The focused row gets a clear highlight and stays scrolled clear of the sticky toolbar.
- Quantities **persist to `localStorage` on every change** — a dealer half way through 400 rows must not lose the work to an accidental refresh. Restored on return with a dismissible "we restored your last entry" notice.
- Full keyboard operation must work on the **filtered** table too (search narrows rows; tab order follows what is visible).

### 5.4 Sticky toolbar

Pinned to the top while scrolling: **Category** dropdown · **Search** box (filters rows live on Design No + Name) · **Net Total** · **You Save** · **Overall Total** · **Cart icon with item count**.

### 5.5 400+ rows — performance

The catalogue is one table with 400–600 rows and 400–600 thumbnails. Non-negotiables:
- **Render all rows.** No virtual scrolling — it breaks tab order, `Ctrl+F` and print, all of which dealers use. 500 plain `<tr>`s is fine for a browser.
- **Images:** `loading="lazy"`, fixed width/height to prevent layout shift, served as ~64 px WebP thumbnails (not full-size images scaled down in CSS).
- **SSR:** the table HTML is server-rendered (good for SEO and first paint) and served through the existing **output cache** for anonymous visitors, since the price list is the same for everyone.
- **Totals maths runs client-side** — no API round-trip per keystroke. The server independently recalculates everything at order placement and is the authority.
- Budget: interactive in **< 2.5 s on a mid-range Android over 4G**. If we miss it, the first lever is thumbnail size, then band-level lazy rendering (collapsed bands render on expand) — *not* virtualization.

### 5.6 Mobile

The 9-column table cannot survive a phone screen. Below `md`, each product becomes a **card**: thumbnail left; name, design no and content stacked; MRP struck + price + discount badge; quantity stepper with a numeric keypad input; line total. Same data, same state, same totals bar pinned to the bottom.

---

## 6. Estimate drawer (cart)

Clicking the cart icon slides a **right-side panel** in (as in the reference screenshots):

- One block per ordered line: thumbnail, name, `qty × price` (qty editable in place), line total, remove (`×`).
- Totals: **Net Total** (at MRP) · **Discount Total** (what they save) · **Sub Total** (payable).
- **Confirm Estimate** button → scrolls to and focuses the order form.
- Below the button, the **Min. Order Amount by state** table (Tamil Nadu ₹x, Kerala ₹y, …) — a straight lift from the reference, and genuinely useful for a wholesale buyer.

Edits in the drawer and edits in the table are the same state — they stay in sync both ways.

---

## 7. Order placement flow

```
 Build order in table
        │
        ▼
 Confirm Estimate ──► Order form: State*, City, Name*, Mobile*, Email, Address*
        │             (right side: Net Total, Discount Total, Sub Total,
        │              Min. Order Amount, Packing Charges %, Round Off, Overall Amount)
        ▼
   [ Submit ]
        │
        ▼
 ┌──────────────────────────────┐
 │  LOGIN GATE — OTP            │   Not logged in? Modal: enter email or mobile,
 │  email OTP  |  mobile OTP    │   receive 6-digit OTP, verify. Account is created
 └──────────────────────────────┘   on first use. The typed order is never lost.
        │
        ▼
 Server revalidates prices, min-order, stock ──► Order created
        │                                        Status: PendingPayment
        ▼
 PAYMENT PAGE  — UPI QR code + bank account details + order no + amount
        │       "Pay and enter your UPI reference / upload the screenshot"
        ▼
 Admin verifies money received ──► marks Payment Confirmed
        │
        ▼
 Packed ─► Dispatched (courier + tracking no, entered by hand) ─► Delivered
```

### 7.1 Validation at Submit (server-side, authoritative)
- Every line's price re-read from the DB — the browser's numbers are never trusted.
- Minimum order amount for the selected **state** enforced.
- Stock checked and reserved (base platform already does reserve → commit → restock).
- Mobile: 10 digits, no `+91`, no spaces (the reference's rule; keep it, it prevents a lot of bad data).
- Email optional on the form **unless** email OTP was the login method.

### 7.2 Why login only at the end
Forcing a login before a dealer can see prices kills the conversion. Building the order is free; identity is required only when it becomes an actual commitment. Anna's *"Login with my account facility to check orders"* is satisfied because by the time an order exists, an account exists.

---

## 8. Payment — manual UPI / bank transfer

**No payment gateway.** `Payments:Provider` gains a third implementation, `Manual`, alongside the existing `Mock` and `Razorpay` (which stay in the code, unused).

**Payment page** (also emailed, and reachable later from My Orders):
- Order number and exact payable amount.
- **UPI QR code** — generated server-side from a configured UPI ID, with amount and order number pre-filled in the UPI intent string so the buyer cannot mistype the amount. Rendered as a PNG.
- **Bank account details** — account name, number, IFSC, branch — from admin settings.
- **"I have paid"** form: UPI/UTR reference number + optional payment screenshot upload. *(Recommended — it removes almost all of the "did this person pay?" back-and-forth. Flagged as open question Q4.)*

**Admin side:** a *Payments to verify* queue — order, amount, buyer's claimed reference, screenshot. One click confirms → order moves to Confirmed and the confirmation email goes out.

Order payment states: `PendingPayment → PaymentReported → PaymentConfirmed` (or `PaymentFailed` / `Cancelled`).

---

## 9. Notification channels — Email, SMS OTP, WhatsApp

Three channels, **one rule for all of them**: every channel has a **Mock** mode and a **Live** mode, chosen by an admin from a screen, with no code change and no redeploy. This is what lets us build, demo and test the entire order flow before a single paid account exists — and it is how we keep testing cheap forever.

### 9.1 The Mock / Live switch — the core principle

> **Mock and Live must run the *same* code path.** Only the last step — the actual delivery — differs. A mock that short-circuits the verification logic proves nothing; the day we flip to Live we would be running that logic for the first time in production.

Concretely, for OTP: Mock still creates the `OtpVerifications` row, still hashes the code, still honours the 5-minute expiry, the 30-second resend cooldown and the max-attempt lockout. The **only** differences are that the code is the fixed test code instead of a random one, and it is not handed to a paid provider.

| Channel | Mock mode | Live mode |
|---|---|---|
| **Email** | Not sent — written to the **server log** only, via the `LoggingEmailSender` that already exists. Mostly a stopgap: **email goes Live on Brevo early** (§9.3). | Brevo SMTP relay |
| **SMS / Mobile OTP** | OTP is the fixed test code — **`000000`**. Nothing is sent to a provider. Nothing needs to be displayed anywhere, because the code is a known constant. | MSG91 (already in the repo) |
| **WhatsApp** | Message rendered and written to the server log; not delivered. | **Click-to-send** (launch mode), Cloud API later (§9.4) |

> **No browser-console echo, no Mock Outbox.** An earlier draft proposed piping mocked messages back to the browser console and storing them in an admin-visible outbox. Both are dropped: the mock OTP is a fixed, known `000000` so there is nothing to look up, and email switches to real Brevo delivery early rather than living in mock. The existing server log covers what is left. This also removes the risk that came with the console route — a `devMessages` field on the API envelope is one config mistake away from broadcasting real OTPs to every browser.

**Fixed test OTP.** In Mock, `OtpService` generates the configured `Otp:MockCode` (default `000000`) instead of `RandomNumberGenerator.GetInt32(...)`. It is still BCrypt-hashed into the row and still verified through the identical `VerifyAsync` path. So `000000` "just works" in testing, and on the day we switch to Live nothing about verification changes — only the generator.

**Safety rails** (these matter — a mock OTP left on in production is an open door to every account):
- The admin screen shows a **loud persistent banner** whenever any channel is in Mock.
- The API logs a **warning on every startup** listing which channels are mocked.
- The mock code is **never** the literal string in code — it is a setting, so it can be changed without a deploy.
- Mock **must be off for SMS/Email before go-live**. This goes on the launch checklist as a blocking item, not a nice-to-have.

### 9.2 Where the configuration lives

Today these live in `appsettings.json` (`Email:Provider`, `Sms:Provider`, `Payments:Provider`). Anna cannot edit a JSON file on a server, so Phase 1 moves them to the **`Settings` table** (which already exists and already backs `StoreSettingsService`), read through a small `IChannelSettings` service with the config file as fallback.

Admin screen — **Settings → Integrations**, one card per channel:

```
EMAIL          ( ) Mock   (•) Live                     [ Send test email ]
               Provider: Brevo SMTP
               Host  smtp-relay.brevo.com   Port  587
               Username  ********           Password  ********  [change]
               From  orders@dailycalendarshop.in   "DailyCalendarShop"

SMS / OTP      (•) Mock   ( ) Live                     [ Send test SMS ]
               Mock OTP code:  000000
               Provider: MSG91    Auth key ********   Sender ID ******

WHATSAPP       (•) Mock   ( ) Click-to-send  ( ) Live  [ Send test message ]
               Business number  +91 …
               Provider: Meta Cloud API   Token ********   Phone number ID ******
```

**Secrets in the database.** SMTP passwords, MSG91 keys and WhatsApp tokens stored in `Settings` must be **encrypted at rest** (ASP.NET Data Protection) and **write-only in the API** — reads return a `********` mask, never the value. Getting this wrong turns a database backup into a credential leak.

### 9.3 Email — Brevo

The base platform already has `IEmailSender` behind a provider switch, plus a template + history system.

**Approach: use Brevo's SMTP relay.** It works with the existing `SmtpEmailSender` — host, port, username, password — so Live email is **configuration, not code**. Brevo's HTTP API would add bounce/open tracking; that is an upgrade we can make later, not a Phase 1 need.

**Email goes Live early.** Because Brevo costs nothing at our volume and needs no approval process, there is no reason to develop against a mocked email channel for long. We wire Brevo up as soon as the account exists and test against real delivery. Mock stays available as a fallback (and for offline work) but it is not where this channel is expected to live.

| Trigger | To | Contains |
|---|---|---|
| Login OTP | Customer | 6-digit code, short expiry |
| Order placed | Customer | Order no, full line items, totals, **payment instructions + QR** |
| Order placed | Admin | New order alert with buyer details |
| Payment confirmed | Customer | Receipt, what happens next |
| Dispatched | Customer | Courier name + tracking number |
| Delivered | Customer | Thank you |

**Free-tier budget:** 300 emails/day. A day with 40 orders costs roughly 40 × 4 ≈ 160 emails plus OTPs — comfortable. Log send counts from day one so a peak season does not surprise us.

### 9.4 WhatsApp — in Phase 1

Anna's stated preference is WhatsApp over email (*"mostly mobile to push WhatsApp message"*), so it belongs in Phase 1. But there is a real-world obstacle worth stating plainly:

> **Meta's WhatsApp Cloud API needs business verification and per-template approval, which takes days to weeks and is outside our control.** If we make launch depend on it, an approval queue at Meta becomes our launch blocker.

So the channel ships as an abstraction, `IWhatsAppSender`, with **three** modes rather than two:

| Mode | What happens | Cost / setup |
|---|---|---|
| **Mock** | Message rendered and written to the server log. Nothing sent. | Free, instant |
| **Click-to-send** ✅ | Admin sees a **"Send WhatsApp"** button on the order; it opens `wa.me/<number>?text=<pre-filled message>` so they send it from their own WhatsApp in one tap. | **Free, zero approval, works day one** |
| **Live** | Meta Cloud API (or MSG91 WhatsApp) sends the approved template automatically. | Paid, needs approval |

**Decided: we launch on Click-to-send.** ✅ It is good enough for a shop doing tens of orders a day — the message is pre-written and correct, a human just taps send — and it means WhatsApp genuinely works on day one instead of being blocked behind Meta's approval queue. Same message templates, same `IWhatsAppSender` interface, so flipping to Cloud API later is a settings change, not a rewrite.

Practical notes for Click-to-send:
- The button is per-order and per-event, so the admin sends the *right* message (dispatched with tracking number, not a generic one).
- Every send is recorded in `NotificationHistory` the moment the button is used, so the order still shows "WhatsApp sent, 14:32" — otherwise we lose the audit trail the automated channels give us.
- It works from a phone and from WhatsApp Web, so it does not tie Anna to one device.

WhatsApp messages to support: order placed (+ payment details), payment confirmed, dispatched (+ tracking), delivered.

### 9.5 SMS and the login channel

SMS costs money per message; **email OTP is free** on Brevo. So: ship **email OTP as the default login channel**, with mobile OTP available and testable from day one via Mock (`000000`), and flip SMS to Live when Anna decides to fund it. The mobile number is **mandatory on the order form regardless** — it is how the shop actually reaches buyers and it is what the WhatsApp channel uses.

---

## 10. Admin

### 10.1 Catalogue upload — the important one

Anna's model of the world is a spreadsheet with 400+ rows, and that is exactly right. The base repo **already has** `ProductImportService` (ClosedXML, `.xlsx`, upserts by SKU, isolates bad rows, and treats any unknown column as a dynamic product attribute). Phase 1 work on top of it:

1. **Accept CSV as well as `.xlsx`** — Anna said CSV; the current service is Excel-only. Same column contract, sniff by file extension.
2. **Extend the column set** for calendars: `DesignNo` (→ SKU), `Name`, `Type`, `Size`, `Content`, `MRP` (→ CompareAtPrice), `Price`, `Status`, `ImageUrl`, `SortOrder`. Any extra column still becomes an attribute.
3. **Auto-create categories** from `Type` + `Size` if they do not exist — otherwise a 400-row upload fails on row 1 of a new size.
4. **Dry-run preview before commit** — show "112 new, 289 updated, 3 errors (rows 45, 78, 301 — reason)" and let Anna cancel. With 400 rows in one file, an un-previewed import is a genuinely dangerous button.
5. **Download current catalogue** as the same spreadsheet — edit prices in Excel, re-upload. This becomes the routine way to do a price revision.
6. **Template download** with the correct headers and one example row.

Import runs are already recorded in `ImportJobs` / `ImportJobItems`, so every upload has an audit trail.

### 10.2 Images — decided: ZIP keyed by Design No, with a URL fallback

Nobody is going to paste 400 URLs into a spreadsheet, and nobody is going to upload 400 images one at a time. So there are **three** ways in, in order of what Anna will actually use:

1. **ZIP upload, filename = Design No** — *the primary path.* `CAL-1042.jpg` attaches to product `CAL-1042`. Anna exports her design images from wherever they already live, names them by design number (which she already thinks in), zips the folder, drops it in. Matching is case-insensitive and extension-agnostic (`.jpg` / `.jpeg` / `.png` / `.webp`).
   - The upload reports back: *"388 matched, 12 unmatched (listed), 5 products still without an image (listed)."* Unmatched files are never silently discarded — that is how you end up with a catalogue quietly missing images.
   - On ingest each image is resized into a **~64 px thumbnail** (for the table) and a **display-size** version (for the lightbox), stored via the existing `IMediaStorage` (disk + Nginx). We do not serve 2 MB originals into a 400-row table.
   - Re-uploading a ZIP replaces matched images — that is how a design gets updated.
2. **`ImageUrl` column in the spreadsheet** — kept for the handful of cases where an image is already hosted. Fetched and ingested the same way.
3. **Single-product upload in the admin product editor** — for one-off fixes.

Products without an image render a neutral placeholder rather than a broken image, and the admin catalogue list has a **"missing image"** filter so Anna can see the gap and close it.

### 10.3 Other admin screens
- **Orders** — list/filter/search, detail view, confirm payment, dispatch (courier + tracking), deliver, cancel, **Send WhatsApp**. *Largely exists in the base repo.*
- **Payments to verify** — the queue described in §8. **New.**
- **Settings → Shop** — UPI ID, bank details, min. order amount per state, packing charge %, announcement bar text, contact details, price-validity date. **New (small).**
- **Settings → Integrations** — the Mock/Live channel configuration in §9.2, with *Send test* per channel. **New.**
- **Reports** — placeholder. Anna to specify.

---

## 11. Look and feel

The reference sites are visually of their era — saturated red/orange, hard borders, no whitespace, no type hierarchy. We keep their *ergonomics* and drop their *aesthetics*.

**Direction:** modern, bright, clean — carrying over our CalendarShop UI language and the existing [ui-guidelines.md](../ui-guidelines.md): generous whitespace, one strong accent, restrained shadows, rounded corners, crisp type hierarchy.

**Theme:** the base platform's **Theme Engine** already drives all colours from the database, so the palette is an admin setting, not a code change. This matters because the user's own note is *"we shall improve theme gradually… may need to make it bright or maybe a 3-coloured theme."* Phase 1 therefore:
- Ships a sensible bright default (one primary + one accent + neutrals).
- Hardcodes **no** brand colour anywhere — every colour resolves through the theme.
- Leaves a 3-colour scheme as a theme swap, not a rebuild.

**Things worth keeping from the reference** (they earn their place): the sticky totals bar, the full-width coloured category band, struck-through MRP next to the live price, the right-side estimate drawer, the min-order-by-state table.

**Things to drop:** the shouty all-caps red bands, the double phone number in the header, cramped row height, the 2010 gradient buttons.

---

## 12. Data model

**No schema rewrite.** The base schema (66 tables) already covers this. Mapping:

| Concept | Existing table | Change |
|---|---|---|
| Calendar product | `Products` | Reuse. `Sku` = Design No, `CompareAtPrice` = MRP, `Price` = our price |
| Type / Size | `Categories` (self-referencing parent/child) | Reuse as a 2-level tree |
| Content, and any other spreadsheet column | `Attributes` + `ProductAttributeValues` | Reuse — already dynamic |
| Design image | `ProductImages` | Reuse |
| Stock | `Inventory` | Reuse (reserve → commit → restock) |
| Order + lines | `Orders`, `OrderItems` | Reuse |
| Payment | `Payments`, `PaymentTransactions` | Reuse; add the `Manual` provider + UTR reference and screenshot fields |
| Courier + tracking | `Shipments` | Reuse as-is |
| Email templates + log | `NotificationTemplates`, `NotificationHistory` | Reuse |
| Users, OTP | `Users`, `OtpVerifications` | Reuse |
| Spreadsheet upload runs | `ImportJobs`, `ImportJobItems` | Reuse |
| WhatsApp message log | `NotificationHistory` | Reuse — add `WhatsApp` as a channel value |
| Channel config + secrets | `Settings` | Reuse — add the Mock/Live keys, encrypt secret values |

**New in migration `030_dailycalendarshop.sql`:**
- `Settings` rows for: UPI ID, bank details, packing charge %, announcement text, price-validity date.
- `Settings` rows for the channel configuration in §9.2 — `Email:Mode`, `Sms:Mode`, `WhatsApp:Mode`, `Otp:MockCode` (default `000000`), plus each provider's credentials (**encrypted at rest, read back masked**).
- `StateMinOrderAmounts` — state → minimum order value.
- `Payments`: `ReferenceNumber` (UTR), `ProofImageUrl`, `ReportedAt`, `ConfirmedBy`, `ConfirmedAt`.
- `Products`: `SortOrder` — so Anna controls row order within a band from the spreadsheet.

---

## 13. Build plan

Sequential; each slice is independently demoable.

| Slice | What ships | Depends on |
|---|---|---|
| **V2-0** Foundations | Branch + `dailycalendarshop` DB *(done)*, migration `030`, shop settings screen, bright default theme | — |
| **V2-1** Channel plumbing | `Settings`-backed **Mock/Live** switch for email + SMS + WhatsApp, encrypted secrets, `Otp:MockCode` = `000000`, *Send test* buttons, startup warning banner | V2-0 |
| **V2-2** Catalogue import | CSV + Excel upload, extended columns, category auto-create, dry-run preview, export, template, **ZIP image upload keyed by Design No** | V2-0 |
| **V2-3** The table | Quick-order table, category bands, search, sticky totals, keyboard/tab entry, localStorage persistence, mobile cards | V2-2 |
| **V2-4** Estimate + order form | Right drawer, order form, min-order rules, packing/round-off, server revalidation, order creation | V2-3 |
| **V2-5** OTP login gate | Email OTP login, mobile OTP working in Mock, account auto-create, order survives the login | V2-4, V2-1 |
| **V2-6** Manual payment | `Manual` provider, UPI QR generation, bank details page, "I have paid" + UTR/screenshot, admin verify queue | V2-4 |
| **V2-7** Email notifications | Brevo SMTP, the six templates, send + history | V2-6, V2-1 |
| **V2-8** WhatsApp channel | `IWhatsAppSender`, message templates, **Click-to-send** from admin orders, Cloud API adapter behind the Live flag | V2-6, V2-1 |
| **V2-9** Fulfilment + My Orders | Admin dispatch/deliver (mostly existing), customer order history + tracking | V2-6 |
| **V2-10** Content pages | Home hero/announcement, About Us, Contact Us + enquiry, Terms, footer with map | V2-3 |
| **V2-11** Polish + go-live | Performance budget, accessibility pass, print price list, theme refinement, **all channels flipped off Mock** | all |
| **V2-12** Reports | *Awaiting Anna's specification* | V2-9 |

**Go-live blocking checklist** (from §9.1): Email **Live**, SMS **Live or deliberately disabled**, WhatsApp at least **Click-to-send**, admin password changed, real UPI/bank details entered, mock OTP code no longer accepted anywhere.

---

## 14. Assumptions made (please correct any of these)

1. **Prices are tax-inclusive** and no GST breakup is shown on the storefront. The base platform's GST/HSN engine stays switched off for Phase 1. *(If invoices need GST, this changes §7 and the invoice template.)*
2. **Single currency, INR**, single seller, single warehouse.
3. **Delivery is by transport/courier to the buyer's city**, as in the reference — not doorstep with pincode serviceability. So the form takes State/City/Address as free text and we do **not** use the base platform's pincode serviceability engine.
4. **Shipping is not charged at checkout** — only a packing charge %. Freight is settled with the transporter.
5. **Invoice PDF** (QuestPDF, already built) is still generated after payment confirmation.
6. **Existing storefront features stay in the code but are un-routed** in Phase 1: PDP, wishlist, reviews, coupons, product grid.

---

## 15. Open questions for Anna

| # | Question | My recommendation |
|---|---|---|
| **Q1** | Is the **minimum order amount per state** real for calendars, or a crackers-only rule? | Build the table (it is cheap), default it to a **single global minimum**, let Anna add per-state overrides only if she wants them. |
| **Q2** | Category = "size, type" — is a **2-level Type → Size** tree right, or is one flat list of bands enough? | Two levels. It costs nothing now and a flat list cannot be split later without re-importing. |
| **Q3** | Login OTP channel — **email** (free) or **mobile SMS** (paid) as the default at launch? | Email as default. Mobile OTP is built and testable from day one via Mock (`000000`); flipping it Live is a settings change once SMS is funded. |
| **Q4** | Should the buyer submit a **UTR reference / payment screenshot**? | Yes. It removes most payment-reconciliation phone calls. |
| **Q5** | Is there a **price validity date** ("prices valid up to 31 July 2026") and what happens after it? | Make it an admin setting shown in the announcement bar. Do not auto-disable ordering. |
| **Q6** | **Packing charge %** — fixed, per state, or per order value? | Single global %, admin-editable, default 0. |
| **Q7** | Do dealers need a **printable / PDF price list**? | Yes, and it is nearly free — the table is already one clean HTML table. |

### Decided (no longer open)
- **Bulk images** → **ZIP upload keyed by Design No**, with the `ImageUrl` column and single-product upload as fallbacks. Full spec in §10.2.
- **Mock/Live per channel** → admin-configurable from the database, `000000` as the fixed mock OTP. Full spec in §9.
- **WhatsApp launches on Click-to-send** → pre-filled `wa.me` link from the admin order screen; Cloud API is a later settings flip, not a rewrite. §9.4.
- **No browser-console echo and no Mock Outbox** → dropped as unnecessary. The mock OTP is a known constant and email goes Live on Brevo early, so there is nothing to look up; the server log covers the rest. §9.1.
- **Email integrates with Brevo early** rather than developing against a mocked channel. §9.3.

---

## 16. Phase 2 — placeholder

**Phase 2 is a different set of functionality living in the same website** — not a continuation of the ordering flow described here. It will be specified separately and appended to this document once Phase 1 is agreed.

What that means for Phase 1 decisions: nothing here should assume the site only ever does wholesale quick-ordering. Keep the routing, navigation, theming and auth general enough that a second body of functionality can be added alongside rather than bolted on.

Known Phase-1 leftovers that are *not* Phase 2, and still owed:
- **Reports** — Anna to specify; the data is being captured from day one.
- WhatsApp **Cloud API** upgrade from Click-to-send (settings flip, no rewrite).
- Brevo **HTTP API** for bounce/open tracking, if email volume justifies it.
