# v4 Phase 1 — Notification Router, WhatsApp Foundation, 2FA

**Goal:** turn the current flat email/SMS dispatcher into the real "one engine, many channels" router the design doc asks for, add the consent model that's a legal prerequisite for any WhatsApp/marketing send, and close the one real gap in auth (2FA) — all before Phase 2 (Helpdesk) and Phase 4 (WhatsApp Commerce) both need the WhatsApp connector this phase builds once.

**Depends on:** Phase 0 (Hangfire — retry/fallback delivery attempts belong there, not a new bespoke timer).
**Blocks:** Phase 2's WhatsApp channel for the chatbot, Phase 4's WhatsApp Commerce — both reuse this phase's BSP connector rather than integrating WhatsApp a second and third time.

This phase has **three genuinely independent tracks** — sequence notes below, but they don't need to happen in strict order by a single person/team.

---

## Track A — Notification Channel Router

### Scope & checklist
- [ ] `INotificationChannel` abstraction — one interface, one implementation per channel (Email, SMS today; WhatsApp, Push added this phase/later)
- [ ] Existing `SmtpEmailSender`/`Msg91SmsSender`/logging providers wrapped as channel implementations — **reused, not rewritten**
- [ ] Central router: notification type → primary channel + fallback chain, dispatches through `INotificationChannel`, retries/fallback-on-failure scheduled via Phase 0's Hangfire (not a new timer)
- [ ] `NotificationTemplates` gains a `WhatsApp` channel value (schema already supports arbitrary channel strings — no migration needed there)
- [ ] Delivery history (`NotificationHistory`) extended to record which channel actually delivered, including fallback attempts

### Design decisions
- **Ship with the design doc's own channel-assignment table as the hardcoded v1 default** (OTP→SMS, order updates→WhatsApp, invoices→Email, etc.) rather than building a per-tenant admin-configurable routing UI now — that's real scope beyond what this phase needs to unblock Phase 2/4. Note it as a natural follow-up once real delivery data exists to justify per-tenant tuning, mirroring how `AuthProviderService` already lets admins toggle login methods — same pattern, deferred until there's a reason to need it.
- **Security-critical alerts (password changed, new device, payout details changed) are hardcoded non-opt-outable** in the router itself, not just a UI convention — enforced in code so a future preference-center bug can't accidentally silence them.

---

## Track B — Consent & Preferences

### Scope & checklist
- [ ] New `UserNotificationPreference` table: UserId, Channel, Category (`transactional` | `marketing`), IsOptedIn, UpdatedAt
- [ ] WhatsApp-specific opt-in timestamp (WhatsApp's own rules are stricter than email/SMS — needs its own recorded consent event, not just a boolean)
- [ ] Router (Track A) checks this table before any `marketing`-category send; `transactional` sends are never gated by it
- [ ] Preference center UI — customer-facing (storefront account settings) and merchant-facing (compliance visibility: who's opted in/out) — **last step of this track**, once there's something real to control

### Design decisions
- DLT registration (SMS) and WhatsApp Business template approval are **operational/account-setup tasks with the chosen providers**, not application code — call this out explicitly so it doesn't get missed as "someone else's problem" once the code ships. Owner: whoever sets up the MSG91/WhatsApp BSP business accounts.
- DND/NDNC compliance for promotional SMS is enforced provider-side (MSG91 already handles registry checking on send) — the app's job is just correctly tagging a send as promotional vs. transactional so MSG91 applies the right rules.

---

## Track C — WhatsApp BSP Connector

### Scope & checklist
- [ ] **Blocking prerequisite: BSP vendor comparison** (Gupshup vs. Interakt vs. Zoko — India pricing, WABA onboarding speed, API/docs quality) — do this first, in parallel with Tracks A/B, since nothing else in this track can finish without it
- [ ] `IWhatsAppProvider` interface, modeling **both** message types WhatsApp Business API actually distinguishes: template messages (pre-approved, for business-initiated sends — what Notifications needs now) and session messages (free-form, only within a 24h customer-reply window — not needed until Phase 2's chatbot, but designing the interface to already distinguish them now avoids a rework then)
- [ ] Config-gated provider selection (`WhatsApp:Provider = None|Gupshup|Interakt|Zoko`), same pattern as `Email:Provider`/`Sms:Provider`/`Ai:Provider` — no new architectural idea here, just the established one applied again
- [ ] Business account setup with the chosen BSP (operational, not code) — template message approval takes real calendar time with any BSP, start this the moment the vendor is picked
- [ ] Plugs into Track A's router as a new `INotificationChannel` implementation

### Design decisions
- **This is the one piece of Phase 1 that cannot start until the open vendor question is resolved.** Everything else in this phase (router, consent model, 2FA) is fully unblocked and can proceed in parallel with that research.
- One BSP connection, reused three times: Notifications (this phase), the chatbot's WhatsApp channel (Phase 2), and WhatsApp Commerce (Phase 4) — confirming this now so the interface design doesn't quietly become notifications-only and need widening later.

---

## Track D — 2FA

### Scope & checklist
- [ ] TOTP (authenticator app) as the primary 2FA method — provider-independent, no per-user SMS cost, works offline
- [ ] SMS OTP as the 2FA fallback method, **reusing the existing `OtpService`** (already used for phone verification) rather than building a second OTP system
- [ ] Backup/recovery codes generated at enrollment (design doc's own answer to "lost 2FA device")
- [ ] New columns on `User`: `TwoFactorEnabled`, `TwoFactorSecret` (DataProtection-encrypted, same pattern as `TenantPaymentAccounts` secrets), `TwoFactorEnabledAt`; new `UserTwoFactorBackupCode` table (hashed, single-use)
- [ ] Login flow becomes two-step when 2FA is enabled: email+password succeeds → challenge for TOTP/SMS/backup code → JWT issued only after both
- [ ] Every login attempt and 2FA event logged to the existing `PlatformAccessLog` audit trail — already built, this just needs to write to it

### Design decisions (per this session's resolved answers)
- **Super Admin: mandatory**, no opt-out — unaffected by the Merchant Admin discussion, consistent with the design doc's original stance for the highest-privilege role.
- **Merchant Admin / Staff with financial access: default-on with opt-out at launch**, tightened to mandatory later once onboarding volume and support-ticket load from lost-2FA-device cases are actually known — resolved decision, not re-litigated here.
- Enrollment prompt happens at first login after this ships (existing accounts aren't silently locked out) — soft-enforce with a dismissible reminder for opt-out-eligible roles, hard-block for Super Admin.

---

## Implementation notes

- All four tracks touch different areas of the codebase (`Features/Notifications/`, `Features/Auth/`, a new `Features/WhatsApp/` or extension of Notifications) — genuinely parallelizable across however many people are working this phase.
- Migration numbering: this phase likely needs 2–3 new migrations (`UserNotificationPreference`, `User` 2FA columns + `UserTwoFactorBackupCode`, possibly a WhatsApp-specific opt-in column if not folded into the preference table) — apply in the usual forward-only numbered sequence, no departure from existing convention.

## Verification

- Send each notification type end-to-end through the router and confirm it lands on the channel the assignment table says it should, with fallback firing correctly when the primary channel is deliberately broken (e.g. bad SMS credentials in a test environment)
- Opt a test user out of `marketing` category → confirm a promotional send is suppressed but a transactional one (order confirmation) still goes through
- WhatsApp: send a real template message through the chosen BSP's sandbox once vendor is picked, confirm delivery + `NotificationHistory` records it correctly
- 2FA: enroll a test Super Admin account, confirm login is blocked without a valid TOTP code; confirm a backup code works exactly once then rejects reuse; confirm a Merchant Admin can explicitly opt out and log in with just email+password
- Audit log shows every login/2FA/enrollment event for the test accounts above

**Status:** not started. Track C's vendor comparison should start immediately, in parallel with everything else.
