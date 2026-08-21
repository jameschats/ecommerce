# Commerce Platform — High-Level Feature Draft (v1)

**Model:** Multi-tenant SaaS, Shopify-style — each merchant runs a fully independent store with its own domain, subscribed to the platform via a plan.

---

## 1. Web App

### 1.1 Super Admin (Platform Owner)

**Stores**
- Store directory with search
- Per-store view: plan, status, health score, user count, order count
- Store lifecycle states: `Trial → Active → PastDue → Suspended`
- Store Health Score (computed risk/engagement indicator)
- Platform KPI dashboard: MRR, total stores, active stores, trial stores

**Analytics**
- Platform-wide analytics (cross-store performance, usage trends)

**Revenue**
- WavCommerce's own subscription revenue tracking

**Billing**
- Merchant billing & invoicing management
- Payment status per store (Active/PastDue/Suspended)

**Plans & Credits**
- Subscription plan management (Starter / Pro / etc.)
- Credit system administration (likely powers AI Marketing Engine usage — Generate, Product images, Campaigns)

**Payments**
- Platform-level payment gateway configuration

**Support**
- Helpdesk for merchant escalations (Merchant Admin → Super Admin)
- Livechat with merchants

**Announcements**
- Broadcast notices/updates to merchants

**Blocklist**
- Block/ban stores, users, or IPs

**Staff**
- Internal WavCommerce team management, roles & permissions

**Audit Log**
- System-wide activity/audit trail

**Also (from original draft, not yet screen-confirmed):**
- Merchant onboarding & KYC/verification
- Theme marketplace management (approve/publish themes)
- App/plugin marketplace management
- Global configuration & policy management
- Security & fraud monitoring

---

### 1.2 Merchant Admin (Store Owner Dashboard)

**Home**
- Dashboard (store performance snapshot)
- Getting Started checklist (theme, first product, payments, shipping, storefront, store details, domain) with progress tracker

**Orders**
- Orders
- Draft orders

**Products**
- Products
- Categories
- Collections
- Brands
- Attributes
- Colour swatches
- Inventory
- Suppliers
- Import / Export

**Customers**
- Customers
- Reviews
- Inbox
- Contact form

**Discounts**
- Discounts

**Marketing — AI Marketing Engine**
- Generate (AI product descriptions, ad copy, marketing content)
- Campaigns (email/social/ad campaign management)
- Product images (AI image generation/editing)
- Content library (repository of generated/approved content)
- Brand voice (AI tone/style training for consistent output)

*Feature prioritization (based on market research, Aug 2026):*

| Tier | Feature | Why |
|---|---|---|
| **1 — Build first** | AI product descriptions | Proven ~24% conversion lift when benefit-driven vs generic copy; ~68% cut in content production time |
| | AI product image editing (background/lifestyle) | 12–18% higher add-to-cart rate vs generic supplier photos |
| | SEO metadata automation | Consistently rated highest time-saver in independent reviews |
| | Brand Voice profile | Multiplier — every other Generate feature depends on this for consistent output |
| **2 — Human-in-the-loop** | Email/campaign copy & subject lines | Good first-draft generator, not autosend |
| | Content library | Reuse/version generated assets so features compound |
| | AI campaign scheduling suggestions | Suggest, don't auto-execute budget/timing |
| **3 — Be skeptical / not v1** | "AI conversion optimization" claims | No evidence AI features directly optimize conversions — they generate content, not revenue |
| | Fully autonomous ad spend | Advisor mode only, not auto-pilot on real ad dollars |
| | Bulk generation at scale | Quality degrades without per-SKU nuance; avoid over-promising |

**Guardrail:** Mandatory human review step before any AI-generated content (copy, images, specs) goes live — AI can occasionally invent details like certifications it wasn't given, which is a real liability risk in verticals like electronics/health & beauty.

**Credit model mapping:** Generate (text) → low cost, high frequency; Product images → metered (higher compute); Campaigns/Content library → bundled with plan tier. Ties into Super Admin's Plans & Credits module.

**Social Media Marketing** (posting/scheduling — distinct from Social Commerce below)
- Catalog-to-post automation: new/bestselling products auto-drafted into posts (image + AI caption in brand voice), merchant approves, then scheduled
- Multi-platform scheduling calendar: Instagram, Facebook, TikTok, Pinterest, X
- Auto-skip out-of-stock products in generated posts
- Phasing: V1 — Meta (Instagram/Facebook) + Pinterest scheduling; V2 — TikTok + X + video/reel generation; V3 — deeper native integrations

**Social Commerce** (shopping-in-app — treat as a Sales Channel, not just marketing)
- Instagram Shopping (product tags, native checkout for US)
- TikTok Shop (full in-app storefront)
- Facebook Shop (synced catalog + ads)
- Pinterest Product Pins
- **WhatsApp Commerce** (priority for India)
  - Catalog sync from Products module → Meta Commerce Manager/WABA
  - Conversational checkout with native UPI payments (India) — no leaving the chat thread
  - GST-compliant invoicing per order
  - Payment gateway options: Razorpay, PhonePe, Paytm
  - Abandoned cart recovery via WhatsApp reminders
  - Order confirmation & shipment tracking updates
  - AI auto-replies for FAQ-shaped product questions (extension of Brand Voice/Generate)
  - Bulk broadcast campaigns, re-engagement, Click-to-WhatsApp Ads
  - Shared team inbox for WhatsApp conversations (ties into Merchant Admin Inbox)
  - **Architecture note:** WhatsApp Business API requires routing through a BSP (Business Solution Provider — e.g., Gupshup, Interakt, Zoko) rather than direct Meta integration; build-vs-partner decision, not a self-serve API like Meta Graph/Pinterest
  - Rationale: WhatsApp shopper reply rates run 40–70%, roughly 10x email and 3x SMS — highest-leverage channel for India specifically

**Online Store**
- Themes
- Theme colours
- Pages
- Navigation
- Home sections
- Banners
- Files
- Preferences
- FAQs
- Custom domain management (default subdomain + option to connect own domain)

**Analytics**
- Analytics (store performance, Google Analytics–style)
- Notifications

**Store Settings**
- Store details (business name, contact email, address, GST/tax)
- Payments (gateway integration + Cash on Delivery fallback)
- Shipping rates
- Staff accounts & role permissions
- Test order / sandbox checkout (verify pricing, tax, shipping, invoice, confirmation email end-to-end)

**Support**
- Livechat with Super Admin

---

### 1.3 Customer (Storefront)

- Registration / login & profile management
- Product browsing & search
- Cart & checkout
- Order tracking & history
- Wishlist
- Reviews & ratings
- Payment methods & saved addresses
- Returns / refunds
- Support (raise tickets to Merchant Admin — via Inbox/Contact form)
- Notifications
- Loyalty / rewards (optional)

---

## Authentication & Notifications Strategy

**Login**
- **Customer:** Mobile OTP as primary login (matches dominant India pattern — Flipkart/Myntra/Meesho style, low friction, works for COD-heavy/lower-desktop users). Email/Google login offered as a secondary option, not forced mobile-only.
- **Merchant Admin / Super Admin / Staff:** Email + password as primary (recoverable, auditable identity for business accounts handling payouts & customer data), with OTP as a **2FA layer** rather than sole login.

**Notifications — channel by purpose, not one default**

| Notification type | Best channel | Why |
|---|---|---|
| OTP / login verification | SMS | Fastest, works without data/app, near-universal delivery |
| Order confirmation, shipping updates, cart recovery | **WhatsApp** | Highest engagement (40–70% reply rates, ~10x email) — already core to Social Commerce plan |
| Promotions, offers | WhatsApp broadcast + Push (mobile app) | Higher read-through than email |
| Invoices, GST receipts, reports, account/security alerts | **Email** | Business/legal record-keeping — permanent, searchable, attachable |
| Merchant Admin alerts (low stock, payout received, new order) | Email (primary) + in-app | Merchants check email for business ops more reliably than shopping-style channels |
| In-app real-time (order status, chat replies) | Push notification | Immediate, app already open |

**Principle:** Email = record-keeping/business channel, WhatsApp = transactional/engagement channel, SMS = verification channel, Push = real-time channel. Each channel does the job it's actually good at rather than one default channel handling everything.

---

## 2. Mobile Apps

### 2.1 Merchant App (Light Version)
- Order notifications & quick status updates
- Basic inventory/stock toggle
- Sales snapshot dashboard
- Lightweight support ticket replies
- Push notifications

### 2.2 Customer App
- Registration/login & profile
- Browse & search
- Cart & checkout
- Order tracking (real-time)
- Wishlist, reviews
- Payment methods & addresses
- Returns/refunds
- Support tickets
- Push notifications

---

## 3. Verticals (Draft — target minimum 10)

1. Fashion & Apparel
2. Electronics & Gadgets
3. Grocery & Food
4. Health & Beauty
5. Home & Furniture
6. Digital Products / Software
7. Jewelry & Accessories
8. Sports & Fitness
9. Books & Stationery
10. Automotive & Parts
11. Pet Supplies
12. Toys & Baby Products

*(Pending: narrow to priority 10, or confirm 12 is fine.)*

---

## Open Questions / Decisions Pending

1. **Support model** — ✅ Resolved: granular front-end surfaces (Inbox, Contact form, Reviews, Livechat) on one unified backend ticket/conversation model (`source` + `type` fields). Full design in `helpdesk-livechat-chatbot-design.md`.
2. **Credits system** — confirm AI Marketing Engine (Generate, Product images, Campaigns) runs on a credit/usage-based model tied to Plans & Credits.
3. **Vertical-specific features** — once verticals are finalized, identify specialized needs per vertical (e.g., expiry dates for grocery, digital delivery for software, size/variant charts for fashion).
4. **Super Admin gaps** — Merchant onboarding/KYC, theme & app marketplace, security/fraud monitoring are in the original draft but not yet confirmed against real screens — verify if in scope.
5. **Analytics** — confirm whether it's a shared engine powering both Super Admin and Merchant Admin, or two separate implementations.
6. **WavCommerce APIs (public/developer platform, like Shopify's API)** — do we design/build this now, alongside core modules, or defer to a later phase once core product surfaces (Super Admin, Merchant Admin, Customer, AI Marketing/Commerce Engines) are stable? Needs a decision on timing before we scope it. Would cover things like: public REST/GraphQL API for merchants' own integrations, webhook system, third-party app/plugin ecosystem (ties into the App marketplace idea under Super Admin gaps above), and API access tied to plan tier.
7. **App Marketplace / App Store (like Shopify App Store)** — need provision for at least one or two starter apps to build/validate the base architecture before opening it up to third-party developers. Depends on item 6 (APIs) being scoped first, since apps need something to plug into. Decide: build 1-2 first-party apps ourselves as proof of concept (e.g., a simple loyalty app or a basic accounting sync), then design the third-party submission/review process afterward?
8. **Migration / Onboarding from competitors** — how do we onboard merchants currently on Shopify, Zoho Commerce, Instamojo, Dukaan, or similar platforms? Needs a design covering: product catalog import (CSV/API-based), customer & order history migration, theme/design recreation or mapping, domain migration/DNS cutover, minimizing downtime during switch, and possibly a "migration assistant" (could be AI-assisted — parses exported data from competitor platforms and maps it into WavCommerce's schema).
9. **Theme Store** — a marketplace of storefront themes merchants can browse/install (referenced earlier under Super Admin's "Theme marketplace management" and Merchant Admin's "Themes" module, but not yet designed as its own surface). Needs: theme submission/review process (if allowing third-party theme designers), free vs. paid themes, theme preview/demo before install, versioning when a merchant customizes a theme (does customization survive a theme update?).
10. **Analytics (Google Analytics-style, deeper design needed)** — current draft has "Analytics" as a module name in both Super Admin and Merchant Admin, but the actual scope isn't designed yet: what metrics, what dashboards, real-time vs. historical, funnel/conversion tracking, traffic source attribution, integration with actual Google Analytics/GA4 (or a fully native replacement), and whether this is the same engine powering both admin layers (ties into existing open question #5).
11. **Reports** — distinct from Analytics (dashboards/visual) — likely scheduled/exportable reports (sales reports, tax/GST reports, inventory reports, payout statements) in PDF/CSV/Excel format. Needs its own scope: which reports are needed per vertical/role, scheduling (daily/weekly/monthly auto-email), and how this relates to the Export functionality already listed under Products (Import/Export).
12. **Infrastructure / Hosting Architecture** — start on a modest setup (e.g., Hostinger KVM4 or similar VPS) to keep early costs low while customer count is small, then migrate to a scalable cloud provider (AWS or Azure) once growth justifies it. Needs a design covering: what "trigger point" (customer count, traffic, storage, uptime/SLA needs) determines when to migrate; how to architect the app from day one so that migration is a lift-and-shift rather than a rebuild (e.g., avoid hard dependencies on VPS-specific quirks, containerize early, keep storage/database portable); migration plan itself (zero/minimal downtime cutover); and cost comparison at different scale points to validate the trigger point.
