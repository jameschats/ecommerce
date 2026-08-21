# v4 Phase 1 — Notification Router, WhatsApp Foundation, 2FA

**Goal:** turn the current flat email/SMS dispatcher into the real "one engine, many channels" router the design doc asks for, add the consent model that's a legal prerequisite for any WhatsApp/marketing send, and close the one real gap in auth (2FA) — all before Phase 2 (Helpdesk) and Phase 4 (WhatsApp Commerce) both need the WhatsApp connector this phase builds once.

**Depends on:** Phase 0 (Hangfire — retry/fallback delivery attempts belong there, not a new bespoke timer).
**Blocks:** Phase 2's WhatsApp channel for the chatbot, Phase 4's WhatsApp Commerce — both reuse this phase's BSP connector rather than integrating WhatsApp a second and third time.

This phase has **three genuinely independent tracks** — sequence notes below, but they don't need to happen in strict order by a single person/team.

---

## Track A — Notification Channel Router ✅ Done (2026-08-21)

### Scope & checklist
- [x] `INotificationChannel` abstraction — one interface, one implementation per channel (Email, SMS today; WhatsApp, Push added this phase/later)
- [x] Existing `SmtpEmailSender`/`Msg91SmsSender`/logging providers wrapped as channel implementations — **reused, not rewritten**
- [x] Central router: notification type → primary channel + fallback chain, dispatches through `INotificationChannel`, retries/fallback-on-failure scheduled via Phase 0's Hangfire (not a new timer)
- [x] `NotificationTemplates` gains a `WhatsApp` channel value (schema already supported arbitrary channel strings — confirmed, no migration needed)
- [x] Delivery history (`NotificationHistory`) extended to record which channel actually delivered, including fallback attempts — `AttemptGroupId`/`AttemptNumber` (migration 263), one row per attempt, same group id links a primary attempt to its fallback(s)

### Implementation notes (what actually shipped)
- `Features/Notifications/INotificationChannel.cs` — `NotificationRecipient` (Email/Phone/UserId) + `INotificationChannel`; `EmailNotificationChannel`/`SmsNotificationChannel` wrap the existing `IEmailSender`/`ISmsSender` untouched.
- `Features/Notifications/NotificationRouter.cs` — the actual dispatcher (template lookup, `{{token}}` rendering, `NotificationHistory` writes, chain walk) moved here from `NotificationService`. Hardcoded v1 chain table lives as a `static readonly Dictionary` inside the router, per the design decision below. A channel absent from DI (WhatsApp, until Track C ships) or unable to reach the recipient is silently skipped — zero further router changes needed once WhatsApp/Push register themselves.
- `NotificationService` is now a thin facade: `SendEmailAsync`/`SendSmsAsync` build a single-contact-point `NotificationRecipient` and call the router — **existing call sites and their behavior are unchanged** (a recipient with only `Email` set naturally can't be delivered to by the SMS channel, so the chain resolves to Email exactly as before).
- Hangfire retry: wrapped behind a new `IBackgroundJobScheduler` (real impl calls `BackgroundJob.Schedule`) purely for testability — the static Hangfire API throws without a configured `JobStorage`, which unit tests don't set up. One scheduled retry (5 min) when every channel in the chain fails on the first attempt; the retry itself (`RetryOnceAsync`) never reschedules another, so a permanently undeliverable recipient stops after two total attempts, not forever.
- `ecomm.tests/NotificationRouterTests.cs` — 8 new tests: primary success, fallback-on-failure with history correlation, skip-if-undeliverable, skip-if-channel-not-registered, all-fail schedules exactly one retry, retry doesn't reschedule, missing-template-for-a-channel skips ahead, and the `NotificationService` facade never cross-sends. Full suite: 310/310 passing.

### Deliberately NOT done this pass — flagged, not decided
**`OrderService.NotifyOrderAsync`'s 7 call sites still call `SendEmailAsync` and `SendSmsAsync` independently** (both fire whenever both contact points exist), not the router's genuine single-recipient multi-channel dispatch. Migrating them to call `INotificationRouter.DispatchAsync` directly with one recipient object carrying both Email+Phone would be a **real behavior change** — customers would get order updates on ONE channel (falling back only on failure) instead of both today. That's arguably the actual point of "one engine, many channels," but it changes what a live customer receives, so it wasn't done silently — needs an explicit decision before it ships.

### Design decisions
- **Ship with the design doc's own channel-assignment table as the hardcoded v1 default** (OTP→SMS, order updates→WhatsApp, invoices→Email, etc.) rather than building a per-tenant admin-configurable routing UI now — that's real scope beyond what this phase needs to unblock Phase 2/4. Note it as a natural follow-up once real delivery data exists to justify per-tenant tuning, mirroring how `AuthProviderService` already lets admins toggle login methods — same pattern, deferred until there's a reason to need it.
- **Security-critical alerts (password changed, new device, payout details changed) are hardcoded non-opt-outable** in the router itself, not just a UI convention — enforced in code so a future preference-center bug can't accidentally silence them.

---

## Track B — Consent & Preferences ✅ Backend done (2026-08-21); UI deferred

### Scope & checklist
- [x] New `UserNotificationPreference` table: UserId, Channel, Category (`transactional` | `marketing`), IsOptedIn, UpdatedAt
- [x] WhatsApp-specific opt-in timestamp — generalized as `OptedInAt` on every row (not WhatsApp-only), since the underlying need ("evidence of *when* consent was given, not just current state") applies the same way to any channel; re-opting-in stamps a fresh timestamp, opting out clears it
- [x] Router (Track A) checks this table before any `marketing`-category send; `transactional` sends are never gated by it — verified: absence of a row, or `IsOptedIn=false`, is never inferred as consent
- [ ] Preference center UI — **still deferred, correctly**: no marketing-category send exists anywhere in the codebase yet (the Growth/AI Marketing Engine generates campaign content but has no send pipeline), so there's genuinely nothing real to control yet, exactly as this track's own scope note anticipated. Backend API is ready (`GET`/`PUT /api/account/notification-preferences`) for whenever that UI gets built.

### Implementation notes (what actually shipped)
- `database/migrations/264_notification_preferences.sql` + `Data/Entities/UserNotificationPreference.cs` — `ITenantScoped`, unique on `(TenantId, UserId, Channel, Category)`.
- `NotificationRouter.DispatchAsync` gained a `category` parameter (default `"transactional"`, so every existing call site is unaffected). For `"marketing"`, each channel in the chain is checked against this table before being tried — no opted-in row means the channel is skipped exactly like `CanDeliverTo == false`; if every channel in the chain is skipped this way, `DispatchAsync` returns `false` **without** scheduling a Hangfire retry (a consent block isn't a delivery failure — retrying it 5 minutes later can't change anything). This also fixed a small pre-existing inefficiency in Track A's own retry logic (it was scheduling a pointless retry even when a recipient had no reachable channel at all, e.g. no email or phone on file).
- Self-service API on `AccountController` (`Features/Account/`) — reused the existing profile/address self-service pattern rather than a new controller. `IAccountService.ListNotificationPreferencesAsync`/`SetNotificationPreferenceAsync`.
- 9 new tests: 3 in `NotificationRouterTests.cs` (blocked-by-default, reaches-an-opted-in-channel, transactional-never-gated) + 6 in `NotificationPreferenceTests.cs` (create/round-trip, opt-out clears timestamp, re-opt-in stamps fresh timestamp, upsert not duplicate, invalid category rejected, per-user scoping). Full suite: 319/319 passing.

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
