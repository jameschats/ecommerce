# WavCommerce Notifications & Messaging Strategy

**Written 2026-08-28.** The cross-channel plan for *how* WavCommerce talks to customers — which
channel, which provider, for which purpose. Complements the per-phase docs: the notification **engine**
(router, channels, consent) is built in [v4 Phase 1](v4-stages/phase-1-notifications-2fa.md); this doc
is the **policy** that runs on top of it.

Related code: `Features/Notifications/` (`NotificationRouter`, `INotificationChannel`, `IEmailSender`,
`ISmsSender`), `Features/WhatsApp/` (`IWhatsAppProvider`).

---

## 1. The one rule everything follows: two separate lanes

Transactional and marketing sends must **never share sending reputation**. If a marketing blast gets
spam-flagged, it drags down deliverability of OTPs and order receipts — so they run on separate
sending identities.

| | **Transactional** | **Marketing** |
|---|---|---|
| Examples | order confirmation, OTP, password reset, shipping, invoice | newsletters, promos, campaigns, re-engagement |
| Consent needed? | **No** — legitimate business necessity | **Yes** — explicit opt-in (DPDP / GDPR) |
| Priority | reliability-critical — must land | engagement — nice to land |
| Email sending domain | `mail.wavcommerce.online` | **separate** `news.wavcommerce.online` |
| Reputation | protected at all costs | isolated so it can't harm transactional |

The code already models this split: `NotificationRouter.DispatchAsync(..., category)` where
`"transactional"` is never gated and `"marketing"` is gated by an opted-in `UserNotificationPreferences`
row (with `OptedInAt` timestamp for DPDP evidence). This doc's job is to keep the **sending identities**
separate at the provider/DNS level too.

## 2. Channel decision rule (when to use what)

> Default to **Email** (cheapest, richest). Add **WhatsApp** when engagement/immediacy matters and the
> customer is on it. Use **SMS only for OTP + urgent alerts** (expensive, tiny). The router already does
> *fallback*, so each event has a primary + backup.

| Channel | Use it for | Don't use it for | Cost (India, rough) |
|---|---|---|---|
| **Email** | receipts, confirmations, invoices (PDF), password reset, verification, newsletters | — | ~₹0.01–0.10 — cheapest |
| **SMS** | login OTP / 2FA, delivery OTP, "out for delivery" | bulk marketing, long content | ~₹0.12–0.25 / msg |
| **WhatsApp** | order/shipping updates, opt-in promos, review asks | anything needing an unapproved template | per-conv: utility ~₹0.11–0.35, marketing ~₹0.78+ |

## 3. Provider-by-purpose

| Purpose | Provider | Category | Status |
|---|---|---|---|
| **Transactional email** | **Brevo now → Amazon SES at scale** (SMTP swap = env only, no code) | transactional | Brevo (temporary), being wired |
| **Marketing email** | **separate lane** — Brevo *marketing* on `news.` subdomain (or a dedicated ESP) | marketing (opt-in) | later |
| **SMS** (OTP + urgent) | **MSG91** | transactional | blocked on India DLT registration |
| **WhatsApp** (order/shipping, utility) | **Gupshup** utility/auth templates | transactional | account live (2026-08-28); needs Meta-approved templates |
| **WhatsApp** (promos) | **Gupshup** marketing templates | marketing (opt-in) | later |

**Why these providers:**
- **Brevo** — fastest to go live (drop-in SMTP, free tier to start). Explicitly *temporary*; because the
  sender is plain SMTP, moving to SES later is an env change only, zero code, zero lock-in.
- **Amazon SES** — the scale destination: cheapest at volume (~$0.10/1k), high deliverability, aligns
  with the "lift-and-shift to AWS later" infra note. (Zoho ZeptoMail is the India-friendly alternative.)
- **MSG91** — already built (`Msg91SmsSender`); India DLT registration is the blocker, not code.
- **Gupshup** — chosen BSP (Phase 1 Track C). Live account uses the `GatewayAPI/rest` line
  (`GupshupWhatsAppProvider`, corrected 2026-08-28).

## 4. Event → channel map (what actually fires)

| Event | Category | Primary → fallback | Providers |
|---|---|---|---|
| Login OTP / 2FA | transactional | **SMS** → WhatsApp auth → Email | MSG91 / Gupshup / Brevo |
| Order confirmation + invoice | transactional | **Email** (PDF) + WhatsApp utility | Brevo + Gupshup |
| Order shipped / status update | transactional | **WhatsApp** → SMS → Email | Gupshup / MSG91 / Brevo |
| Order cancelled / refund | transactional | **Email** → SMS | Brevo / MSG91 |
| Password reset / email verify | transactional | **Email** | Brevo |
| Review request (post-delivery) | transactional | **Email** or WhatsApp utility | Brevo / Gupshup |
| Abandoned cart | **marketing** | **Email** (opt-in) → WhatsApp mktg | marketing lane / Gupshup |
| Back-in-stock / price-drop | **marketing** | **Email** / WhatsApp (opt-in) | marketing lane / Gupshup |
| Newsletter / campaigns / promos | **marketing** | **Email** bulk + WhatsApp mktg | marketing lane / Gupshup |

This mostly matches the router's existing `ChannelChains` (`OrderShipped`/`OrderStatusUpdate` already
prefer WhatsApp → SMS → Email); align the few that differ when each channel is actually wired.

## 5. Compliance guardrails
- **Transactional** always sends. **Marketing** is opt-in only (gated in code), needs an **unsubscribe**
  link on every email, and WhatsApp marketing needs explicit opt-in + Meta *marketing-category* templates.
- **DPDP (India):** marketing consent recorded with a timestamp — `UserNotificationPreferences.OptedInAt`
  already does this.

## 6. Rollout order
1. **Now — Brevo transactional email** (order confirmation, password reset, verification, shipping fallback).
2. **This week — WhatsApp utility templates** (Gupshup) for order/shipping, once Meta approves them.
3. **When DLT clears — SMS OTP** (MSG91) → also lights up mobile-OTP login.
4. **Later — marketing lane:** separate `news.` subdomain + consent capture + campaign sending (AI Growth)
   + WhatsApp marketing templates.
5. **At scale — swap Brevo → SES** for transactional email (env-only).

---

## 7. Concrete config — transactional email via Brevo

**Sending domain:** `mail.wavcommerce.online`

**Operational (one-time, done in Brevo + DNS):**
1. Create the Brevo account.
2. Add sender domain `mail.wavcommerce.online`; add the **SPF + DKIM + DMARC** DNS records Brevo
   generates — *mandatory*, or mail goes to spam.
3. Generate an **SMTP key** (SMTP & API → SMTP).

**Server env** (`/etc/wavcomm/api.env`, then restart `wavcomm-api`):
```
Email__Provider=Smtp
Email__Host=smtp-relay.brevo.com
Email__Port=587
Email__UseSsl=true
Email__Username=<brevo login email>
Email__Password=<brevo SMTP key>
Email__FromAddress=no-reply@mail.wavcommerce.online
Email__FromName=WavCommerce
```
Per-tenant **From-name** and **Reply-To** are already overridden per send from each tenant's
`SenderName` / `ReplyToEmail` settings — the envelope address stays `no-reply@mail.wavcommerce.online`
(for SPF/DKIM alignment), only the display name and reply-to change per merchant.

**Switching to SES later:** change `Email__Host`/`Username`/`Password` to the SES SMTP endpoint +
credentials, verify `mail.wavcommerce.online` in SES, restart. No code change.
