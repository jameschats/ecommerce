# Tech Debt & Hardcoded-Value Audit

A project-wide audit of hardcoded values (backend, frontend, SQL/config) run 2026-07-01.
**Key finding: no committed secret is actually used in production** — `appsettings.json` holds
dev placeholders that prod overrides via `/etc/ecomm/api.env` (connection string, `Jwt__Key`,
Razorpay keys). So there is **no active security hole**; the items below are hardening,
operational, and maintainability improvements.

Legend: ✅ fixed · 🕒 deferred (owner will do) · ⚙️ operational (no code change) · 📝 optional/by-design.

---

## Open items (tracked — not yet done)
- [ ] ⚙️ **Change the live prod admin password** — still the seeded default `Admin@123`
  (`admin@ecommerce.local`). Highest real risk in the audit. Externalizing the seeder does NOT
  change the existing account; must be reset manually (admin profile change-password, or DB reset).
- [ ] ⚙️ **Enter real store GSTIN + legal name** in Admin → Store settings (fake `33AAAAA0000A1Z5`
  prints on tax invoices).
- [ ] 🕒 **Real contact details** on the contact page (currently fake `support@calendarshop.example`).

### Notification delivery (code seams built — need external onboarding + keys)
- [ ] ⚙️ **SMS delivery (MSG91)** — `ISmsSender` + `Msg91SmsSender` are built and config-selectable
  (`Sms:Provider=Msg91`). Blocked on **India DLT registration** (register business + 6-char sender id
  `CALSHP` + message templates on the DLT portal via MSG91 — a few-day KYC process), then set
  `Sms__AuthKey` / `Sms__SenderId` / `Sms__TemplateId` in `/etc/ecomm/api.env` and restart. Lights up
  **Mobile-OTP login + all order SMS** at once. Until then `Sms:Provider=Logging` (OTP in `journalctl`).
- [ ] ⚙️ **Email delivery (SMTP)** — `IEmailSender` + `SmtpEmailSender` built (`Email:Provider=Smtp`).
  Set `Email__Host/Port/Username/Password/FromAddress` in `api.env` (e.g. Zoho/SendGrid), then restart.
  Until then emails only log. Powers order emails + password-reset + email-verification.
- [ ] 💡 **(Optional) WhatsApp OTP** — a cheaper/higher-delivery alternative (or addition) to SMS in
  India. Implementable as **another `ISmsSender` provider** (`Sms:Provider=Whatsapp`) via MSG91's or
  Meta's WhatsApp Cloud API — no caller changes. Paid per-message (cheap in India), needs a WhatsApp
  Business Account + an **approved authentication template** (Meta approval, not DLT). Decide after SMS
  is live; the seam already supports it.

---

## Fixed in this pass
- ✅ **SSR allowedHosts** — added `calendarshop.online` / `www.calendarshop.online` to
  `angular.json` `build.security.allowedHosts` (was localhost-only; prod worked only because the
  box was patched locally). A clean rebuild now includes the prod domain.
- ✅ **Admin seed password/email now configurable** — `AdminUserSeeder` reads `Admin:DefaultPassword`
  and `Admin:Email` (falls back to the old constants). A fresh prod deploy can seed a strong
  password via env instead of the well-known default; startup logs a **warning** if the default is
  still in use.

## Must do before real customers transact (operational — no code change)
- ⚙️ **Change the live admin password.** Prod still has the seeded default `Admin@123`
  (`admin@ecommerce.local`). The seeder only sets a password on first creation, so externalizing it
  (above) does **not** change the existing prod account — log in and change it, or reset it.
- ⚙️ **Enter real store identity** in Admin → Store settings. Seeded placeholders print on **tax
  invoices**: GSTIN `33AAAAA0000A1Z5` (fake), legal name, and state
  (`database/migrations/020_checkout_config.sql`). Replace with the real GSTIN before invoicing.

## Deferred (owner)
- 🕒 **Real contact details.** `ecomm.web/.../pages/contact/contact.component.ts:63-65` shows
  placeholder address, phone `+91 62922 23322`, and a **fake** email `support@calendarshop.example`.
  To be replaced when the contact page is revised (ideally sourced from admin settings, not hardcoded).

---

## Optional hardening (works today, improve when convenient)
- 📝 **Frontend `api.config.ts` uses `sed`-replaced constants** (`API_BASE_URL`, `SITE_URL` =
  localhost, rewritten at deploy). Works, but a rebuild that skips the `sed` step silently points
  prod at localhost. Better: a runtime `config.json`/`window.__env` loaded before bootstrap, or
  Angular file-replacement build configs.
- 📝 **Duplicate 5 MB upload limit + allowed-types list** — `Features/Cms/BannerController.cs` defines
  its own `MaxImageBytes`/`AllowedTypes` instead of reusing `Features/Media/MediaOptions.MaxBytes`.
  If one changes the other won't. Consolidate into a shared image-upload validator.
- 📝 **Store name "CalendarShop" hardcoded** in ~10 frontend spots (header/footer `app.html`,
  SEO titles, Razorpay modal `checkout.component.ts`, schema.org JSON-LD). Centralize via a public
  settings endpoint (a `SiteName` setting already exists in the DB) if you rebrand or go multi-tenant.
- 📝 **Hardcoded page content** that could be CMS/admin-managed: FAQ Q&A (`faq.component.ts`),
  testimonials (`home.component.ts`), About stats/values (`about.component.ts`), homepage SEO copy.
  Fine for V1; move to CMS if content changes often.
- 📝 **Config classes for magic numbers** (currently reasonable hardcoded defaults):
  OTP expiry 5 min / resend 30 s (`OtpService`), pagination max 100 (`ProductService`,
  `InventoryService`, `OrderService`), search limits 8/20 (`SearchService`), bcrypt work factor 11
  (`PasswordHasher`), banner auto-rotate 5 s + product page size 12 (frontend).
- 📝 **Status string literals** (`"Paid"`, `"Active"`, `"Shipped"`…) scattered as raw strings.
  Some domains already have constant classes (`TaxMode`, `AuthProviderNames`, inventory txn types);
  extend the pattern to Order/Payment/Product status to prevent typos.

## Confirmed fine / by design (not debt)
- **Dev DB password + JWT key in `appsettings.json`** — overridden by `/etc/ecomm/api.env` in prod.
- **Razorpay keys** — empty in config; real keys in user-secrets (dev) / env (prod).
- **`TenantId = 1`** across ~20 services — intentional single-tenant V1 (schema is multi-tenant-ready).
  When going multi-tenant, resolve it from an `HttpContext` claim instead of a constant.
- **GST rates / HSN 4910 / shipping / `INR`** — correct India-market seed data, all admin-editable.
- **Razorpay / Google / schema.org URLs** — stable public endpoints; correct to hardcode.
- **Placeholder images** (picsum.photos banners, placehold.co cart/order fallbacks) — demo data;
  banners are now admin-uploadable, product images too.
