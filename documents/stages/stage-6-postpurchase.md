# Stage 6 — Post-purchase & Engagement

**Goal:** everything after payment + engagement features.

## Scope & checklist
- [x] Order status tracking lifecycle (`Pending→Paid→Packed→Shipped→Delivered`, +Cancelled/Returned) with history + customer emails
- [x] Shipments (courier, tracking number) — `Shipments`; admin create/deliver, customer tracking view
- [x] **Transactional email** (`IEmailSender`): order confirmation + status updates + **password reset +
  email verification** (email OTP). `LoggingEmailSender` (dev) / `SmtpEmailSender` (real) via `Email:Provider`.
- [x] Coupon engine (flat/percentage; caps; total + per-user usage limits; window) — `Coupons`/`CouponUsage`; checkout + admin
- [x] Reviews & ratings (verified-purchase flag, moderation) — product page + admin moderation
- [x] Notification engine (Email + SMS templates; WhatsApp future) — `NotificationTemplates`/`NotificationHistory`
- [x] **Password-reset + email-verification** — `OtpService` delivers email codes via templates;
  `/auth/password/forgot`+`/reset` (anti-enumeration) + `/auth/email/verify/request`+`/confirm`;
  forgot-password page + account verify prompt.

## Implementation
- **Notifications:** `Features/Notifications` — `IEmailSender` (Logging/Smtp), `INotificationService`
  renders admin-editable `NotificationTemplates` (`{{token}}`) + records every send in `NotificationHistory`.
  Wired into order confirm/status/cancel + shipment dispatch. Migration `024` seeds templates.
- **Reviews:** `Features/Reviews` — public list (approved + rating summary/distribution), customer submit
  (verified-purchase auto-detected, re-moderated on edit), admin approve/delete.
- **Coupons:** `Features/Coupons` — server-side re-validation on quote + place; order-level discount off
  subtotal (tax pre-discount for V1); usage recorded in the order transaction.
- **Shipments:** folded into `OrderService` (reuses the notification helper) — create shipment advances to
  Shipped + sends `OrderShipped` (email+SMS w/ tracking); deliver advances to Delivered.

## Tables (exist)
`Shipments, Coupons, CouponUsage, Reviews, NotificationTemplates, NotificationHistory`

## Config
`Email` section (`Provider: Logging` dev; `Smtp` + host/port/creds for real delivery). Migration `024_notification_templates.sql`.

## Dependencies
Orders (Stage 5). Email/SMS providers via config (dev stubs by default).

## Verification
Backend tested end-to-end on a throwaway port: order confirmation email+SMS on pay; status-update email
on Packed; coupon (percentage under cap, invalid-code message, usage recorded, per-user limit); review
submit→pending→approve→public with rating summary; shipment dispatch→Shipped (email+SMS w/ tracking),
re-ship rejected, deliver→Delivered, customer tracking visible. Web builds clean.

**Status:** ✅ Email/SMS notifications · reviews · coupons · shipments · password-reset + email-verification
— verified backend + build. Deferred (future): Returns/RMA, WhatsApp.
