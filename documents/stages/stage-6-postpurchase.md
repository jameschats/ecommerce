# Stage 6 — Post-purchase & Engagement

**Goal:** everything after payment + engagement features.

## Scope & checklist
- [ ] Order status tracking lifecycle (`Pending→Paid→Packed→Shipped→Delivered`, +Cancelled/Returned) with history + customer emails
- [ ] Shipments (courier, tracking number) — `Shipments`
- [ ] **Transactional email** (`IEmailSender`): order confirmation, status updates, password reset, email verification (P0)
- [ ] Coupon engine (flat/percentage; usage limits) — `Coupons`/`CouponUsage`
- [ ] Reviews & ratings (verified-purchase flag, moderation)
- [ ] Notification engine (SMS templates; WhatsApp future) — `NotificationTemplates`/`NotificationHistory`

## Tables (exist)
`Shipments, Coupons, CouponUsage, Reviews, NotificationTemplates, NotificationHistory`

## Dependencies
Orders (Stage 5). Email/SMS providers via config (dev stubs first).

## Notes
- Transactional email also backfills Stage-1 email verification + password reset.

**Status:** ⬜ Not started.
