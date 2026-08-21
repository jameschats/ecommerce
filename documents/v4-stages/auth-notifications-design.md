# Authentication & Notifications — Complete Design

**Scope:** How every user type (Customer, Merchant Admin, Super Admin, Staff) proves who they are, and how the platform reaches them afterward — across SMS, WhatsApp, Email, and Push.

---

## 1. Design Principles

1. **Right method for the right identity.** Consumers (Customers) and business accounts (Merchant/Super Admin/Staff) have different risk profiles and different login expectations — don't force one pattern on both.
2. **Right channel for the right message.** Every channel (SMS, WhatsApp, Email, Push) has a job it's genuinely good at. Route by message purpose, not by convenience or a single default.
3. **Fallback, always.** No channel is 100% reliable. Every critical message (OTP, order confirmation, payout alert) needs a defined fallback if the primary channel fails.
4. **Consent and compliance are not optional.** SMS/WhatsApp marketing in India is tightly regulated (DND, opt-in). This needs to be designed in from day one, not retrofitted.
5. **One notification engine, many channels.** Don't build four separate notification systems. Build one routing engine that decides the channel based on message type, with pluggable providers per channel.

---

## 2. Authentication Design

### 2.1 Customer Login

**Primary: Mobile OTP**
- Flow: enter mobile number → receive 6-digit OTP via SMS → enter to verify → session created
- Rationale: matches the dominant India consumer pattern (Flipkart/Myntra/Meesho), removes password friction, works for shoppers who may not check email regularly, fits COD-heavy user base

**Secondary: Email / Google login**
- Offered as an alternate option on the same login screen, not hidden — some returning customers or shared-device households prefer it
- Useful for account recovery if the customer changes phone numbers

**Session & security:**
- OTP expires in a short window (e.g., 5–10 minutes), limited retry attempts, rate-limited resend
- Session tokens with reasonable expiry; "stay logged in" option on trusted devices
- Account linked to mobile number as primary identifier; email optional but recommended for order receipts/recovery

**Edge cases to design for:**
- Customer changes phone number → needs a verified recovery path (email or support-assisted)
- OTP delivery failure/delay → fallback to WhatsApp OTP delivery, or voice call OTP as a last resort
- Shared/family phone numbers → account switching support on the same device

---

### 2.2 Merchant Admin / Super Admin / Staff Login

**Primary: Email + Password**
- Rationale: these are business accounts tied to payouts, customer data, and store settings — need a recoverable, auditable identity. A phone number can be lost/swapped; a business email is a more stable anchor for account recovery and audit trails.

**Required: OTP as 2FA layer**
- After email+password, a second factor via SMS or authenticator-app OTP before access is granted
- Mandatory for Super Admin and any Staff role with payout/financial access; strongly recommended (default-on, toggle-off with warning) for standard Merchant Admin accounts

**Session & security:**
- Role-based session policies — Super Admin sessions expire sooner / require re-auth for sensitive actions (e.g., changing payout bank details)
- Full login audit trail (ties into Super Admin's existing Audit Log module) — every login, failed attempt, and 2FA event logged
- Staff accounts support role-scoped permissions (already planned in Merchant Admin — Staff accounts & role permissions)

**Edge cases to design for:**
- Password reset flow — email-based reset link, time-limited, invalidates existing sessions
- Lost 2FA device — recovery path via backup codes or Super Admin-assisted verification (for merchants) / manual internal process (for platform staff)

---

## 3. Notification System Design

### 3.1 Routing Principle

One notification engine receives events from the platform (order placed, OTP requested, low stock, payout sent, etc.) and routes each to the right channel(s) based on **message type**, not a single default channel.

```
Platform Event → Notification Engine → Channel Router → [SMS | WhatsApp | Email | Push]
                                              ↓
                                    (fallback chain if primary fails)
```

### 3.2 Channel Assignment

| Notification type | Primary channel | Fallback | Why |
|---|---|---|---|
| OTP / login verification | SMS | WhatsApp OTP, then voice call | Fastest, near-universal delivery, works without data/app |
| Order confirmation, shipping updates | WhatsApp | SMS (short version) | Highest engagement (40–70% reply rates, ~10x email) |
| Cart recovery reminders | WhatsApp | Email | Conversational, high read-through |
| Promotions, offers | WhatsApp broadcast + Push | Email (for opted-out WhatsApp users) | Higher read-through than email; must respect opt-in |
| Invoices, GST receipts, order reports | Email | — (no fallback needed; not time-critical) | Permanent, searchable, attachable — legal/business record |
| Account/security alerts (password changed, new device login) | Email | SMS for high-risk events | Record-keeping + user needs to see it even if not actively on WhatsApp |
| Merchant Admin alerts (low stock, payout received, new order) | Email + in-app | Push (if mobile app installed) | Merchants check email for business ops more reliably than consumer-style channels |
| In-app real-time (order status, chat replies) | Push | In-app badge/banner | Immediate, only relevant while engaged |

### 3.3 Provider Layer (per channel)

- **SMS:** Requires an SMS gateway provider (e.g., via a DLT-registered route in India — mandatory for transactional/promotional SMS compliance)
- **WhatsApp:** Routes through a BSP (Business Solution Provider — e.g., Gupshup, Interakt, Zoko), consistent with the WhatsApp Commerce architecture decision already made for Social Commerce. **This should be the same integration, not a separate one** — one WhatsApp BSP connection serves both Commerce messaging and Notification messaging.
- **Email:** Transactional Email Service Provider (e.g., SendGrid, Amazon SES, or similar) for reliable delivery + tracking (opens, bounces)
- **Push:** Firebase Cloud Messaging (Android) / Apple Push Notification service (iOS) — standard mobile push infrastructure, ties into the Customer App and Merchant App

### 3.4 Templates & Personalization

- Each notification type has a template per channel (SMS is short/plain, WhatsApp can include images/buttons, Email can be rich/branded)
- Templates pull from the same Brand Voice profile (AI Marketing Engine) where relevant — e.g., promotional WhatsApp/Email copy should sound consistent with the merchant's brand, not generic system text
- Transactional templates (OTP, order confirmation) are platform-standard, not brand-voice-dependent — clarity matters more than personality here

### 3.5 Consent & Compliance (India-specific — critical, not optional)

- **DLT registration** required for all SMS (transactional and promotional) sent to Indian numbers — sender IDs and templates must be pre-registered
- **WhatsApp opt-in requirement** — customers must explicitly opt in before receiving marketing/broadcast messages (transactional messages like order updates have more lenient rules, but marketing broadcasts need clear consent)
- **DND/NDNC registry compliance** for promotional SMS — respect customer preference, provide opt-out
- **Unsubscribe/preference center** — customer-facing settings to control what promotional content they receive on which channel (order/security notifications are non-optional; promotions are opt-in/opt-out)

---

## 4. Data Model Note

Both Authentication and Notifications should share one **user identity record** per person (Customer or Merchant/Staff) containing: verified mobile number, verified email, WhatsApp opt-in status, notification preferences, and channel delivery history. This avoids the situation where OTP, order notifications, and marketing messages each maintain separate, inconsistent contact records.

---

## 5. Guardrails Summary

- 2FA mandatory for Super Admin and financially-privileged Staff roles; default-on for Merchant Admin
- Every login/auth event logged to Audit Log (Super Admin)
- No promotional WhatsApp/SMS without explicit opt-in
- DLT + DND compliance built into the SMS/WhatsApp provider layer, not left to individual merchants to manage manually
- Security-critical alerts (password change, new device, payout details changed) always go to Email regardless of other channel preferences — never fully opt-outable

---

## 6. Build Sequence (Recommended)

1. **Shared user identity record** (foundation — mobile, email, WhatsApp opt-in, preferences in one place)
2. **Customer OTP login** + Merchant/Admin email+password login (core auth, both types in parallel — different teams/flows, not dependent on each other)
3. **2FA for Merchant/Super Admin/Staff**
4. **Notification engine + channel router** with SMS and Email providers (most universally needed first)
5. **WhatsApp provider integration** (shared with Social Commerce BSP work — build once, use for both)
6. **Push notifications** (once Mobile Apps are in active development, since this is mobile-app-dependent)
7. **Preference center / consent management UI** (Customer-facing settings + Merchant-facing compliance tools)

---

## 7. Open Questions

1. Which SMS gateway / WhatsApp BSP / Email ESP do we standardize on — build vs. partner decision, similar to the WhatsApp Commerce discussion?
2. Should Merchant Admin 2FA be mandatory from day one, or default-on-with-opt-out initially to reduce onboarding friction, tightening later?
3. Voice-call OTP fallback — build for launch, or add later once SMS delivery data shows how often it's actually needed?
