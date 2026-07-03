# V2-11 — Unified Multi-Channel Notifications

**Goal:** one dispatcher that fans any platform event out to the right channels — **in-app (platform bell), email, SMS, WhatsApp** (and push later) — per-tenant-branded, preference-aware, and compliance-safe. Upgrades V1's baseline (in-app + email + SMS) into a unified, per-tenant system.

> This is foundational: support (V2-9), diagnostics alerts (V2-10), billing (V2-1), orders, OTP, and marketing flows (V2-6) all send through it. The V1 baseline covers earlier stages; **consider pulling the dispatcher + channel abstraction forward** so every stage emits through one path.

## Architecture — channel abstraction + dispatcher
```
INotificationDispatcher.Send(event, recipient, tenant)
        │  resolves template + enabled channels + recipient prefs, fans out (async via Hangfire)
 ┌──────┴───────────────────────────────────────────────┐
 INotificationChannel:
   InAppChannel   → SignalR bell + `notifications` (V1)
   EmailChannel   → IEmailSender (Logging/Smtp, V1)
   SmsChannel     → ISmsSender (Logging/MSG91, V1)
   WhatsAppChannel→ IWhatsAppSender (NEW — WhatsApp Cloud API; shares the V2-6 integration)
   PushChannel    → (later)
```
Same config-selected-provider pattern as V1's payment/email/SMS abstractions.

## Scope & checklist
- [ ] **`INotificationDispatcher`** — event + recipient + tenant → resolve template + channels + preferences → fan out; per-channel delivery record; retries with backoff (Hangfire).
- [ ] **`IWhatsAppSender` + `WhatsAppChannel`** — WhatsApp Cloud API (shares the V2-6 WhatsApp integration); approved message templates + 24-hour session window rules.
- [ ] **Event catalogue** — order placed/paid/shipped/delivered/cancelled, refund, OTP, password reset, email verification, low-stock, review request, coupon; **support ticket** created/replied/resolved (V2-9); **billing** (trial ending, payment failed, suspended, V2-1); **platform announcements** (super-admin → merchants); **super-admin alerts** (new ticket, failed payment, webhook failures, SLA breach, churn).
- [ ] **Per-tenant configuration** — sender identity (email from-name/reply-to, SMS sender id, WhatsApp number), which channels per event, **branded templates** per tenant (extends V1 `notificationtemplates`), multi-language (ties to V3 regional languages) with preview/test-send.
- [ ] **Recipient preferences + compliance** — per-user opt-in/out per channel + event category; **transactional vs marketing** split (transactional always sent; marketing needs consent); email unsubscribe; **DPDP** consent.
- [ ] **Delivery tracking** — `NotificationDeliveries` per channel (status, provider message id, attempts, error); surfaced in diagnostics (V2-10) + merchant admin; retry failed sends.

## India compliance (must-handle)
- **SMS:** DLT registration — sender id + pre-approved template ids (MSG91); unregistered SMS is dropped (already noted in V1 deployment).
- **WhatsApp:** Meta Business verification, explicit opt-in, pre-approved templates, session-window rules.
- **Email:** SPF/DKIM/DMARC sender auth; transactional vs marketing; one-click unsubscribe for marketing.
- **DPDP:** consent capture + honour opt-outs; no PII in logs.

## Data model
Migrations `210–219`, tenant-scoped: extend `notificationtemplates` (per-tenant + per-channel + language); `NotificationPreferences` (user × event-category × channel, opt-in/out); `NotificationDeliveries` (per notification × channel: status, provider msg id, attempts, error); `PlatformAnnouncements` (+ read receipts). Reuse V1 `notifications`, `notificationhistory`. Per-tenant sender config in `TenantSettings`.

## Two directions of platform notifications
- **Platform → merchants:** announcements, maintenance windows, billing warnings, policy updates (super-admin composes; broadcast or targeted).
- **Platform → super-admin:** operational alerts (new ticket, failed payment, webhook failures, churn, SLA breach) — feeds V2-10 dashboards.

## Frontend
- **Merchant-admin:** notification settings (channels per event, sender identity, templates, test-send); delivery log.
- **Customer storefront:** channel preferences + unsubscribe.
- **Super-admin:** compose/broadcast announcements; view platform alerts.

## Gate
One order event fans out to in-app + email + (opted-in) WhatsApp under the tenant's brand; a customer opt-out suppresses the right channel while transactional still sends; a failed send is logged in `NotificationDeliveries` and retried; SMS uses a DLT template; a super-admin announcement reaches targeted merchants; deliveries show up in diagnostics (V2-10).

## Dependencies
V1 notification baseline (in-app/email/SMS), V2-0 (tenancy), V2-6 (WhatsApp integration — shared), Hangfire (async/retries). Consumed by V2-1, V2-9, V2-10.

**Status:** ⬜ Not started. Foundational — dispatcher + channel abstraction are candidates to pull forward.
