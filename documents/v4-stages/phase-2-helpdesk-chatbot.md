# v4 Phase 2 — Helpdesk: FAQ Wiring, Chatbot, Livechat

**Goal:** the three genuinely-missing pieces from the helpdesk design doc — everything else (unified ticket model, structured FAQ content, grounded-answer discipline) is already built. This phase wires them together into a real bot and a real widget, not a rebuild.

**Depends on:** Phase 1 (WhatsApp connector, for the bot's WhatsApp channel — the storefront widget and chatbot core don't need to wait for it, only the WhatsApp piece does).
**Blocks:** Phase 3's Shopping Assistant, which converges into this same bot rather than being built separately.

**Correction to the original roadmap scope:** `Data/Entities/Faq.cs` already exists — Question/Answer/Category/DisplayOrder/IsPublished, tenant-scoped — and its own doc comment already names it as the bot's future retrieval corpus. **No new FAQ content model is needed.** The real gap is that nothing reads it programmatically yet.

---

## Scope & checklist

- [x] Chatbot core: grounded answering over `Faq` + live catalog + live order data, extending the exact discipline already proven in `SupportDraftService` — done 2026-08-21
- [x] Escalation logic (grounding-confidence, sentiment, explicit request, judgment-call, exchange-count triggers) — done 2026-08-21
- [ ] Livechat widget (storefront) — bot-first, reusing existing `NotificationHub`/`ConversationRealtime` SignalR transport
- [x] Merchant controls: on/off toggle, active hours, "what the bot couldn't answer" log — done 2026-08-21 (backend; FAQ review itself already existed as the FAQ admin CRUD, untouched)
- [ ] WhatsApp channel for the bot, via Phase 1's `IWhatsAppProvider` session-message capability
- [x] Full conversation-history handoff into the existing `SupportTicket`/`ShopperMerchant`-axis model on escalation — turned out to need zero extra work: a bot conversation already IS that model from message one (per the design decision below), so "handoff" is just `IsBotActive=false` + an admin notification, not a data migration

### Implementation notes (chatbot core, shipped 2026-08-21)

- `Features/Support/ChatbotService.cs` (`IChatbotService.HandleShopperMessageAsync`) — the whole classify → fetch → compose pipeline described in "Design decisions" below, built exactly as specified: no changes to `IAiService`, all new complexity in this one orchestration layer.
- **Persistence reuses `IShopperConversationService.ReplyAsShopperAsync`** for the customer's own message (same realtime push, admin notification, reopen-on-reply logic already tested) — the chatbot only adds new logic for the *bot's own* replies (`AppendBotMessageAsync`), via a new `MessageAuthorType.Bot` so the UI can eventually distinguish "AI Assistant" from a human merchant reply.
- `ChatbotConversationStates` + `ChatbotUnansweredQuestions` (migration 266) — the only genuinely new tables. State tracks `IsBotActive`/`UnresolvedExchangeCount`/`EscalationReason` per conversation; once escalated the bot goes permanently silent on that thread (verified by test) rather than re-evaluating triggers on every subsequent message.
- Escalation triggers implemented exactly per the table below: judgment-call is a regex (`refund|return|discount|compensat|goodwill|exception|cancel my order|chargeback|dispute`) checked *before* any AI call — zero cost, zero model-confidence involved; frustration/explicit-request come from one combined classify call (`{"frustrated","wantsHuman","topic"}`) rather than two separate ones; no-grounded-data comes from the compose call's own `{"answer","grounded"}` JSON, matching `SupportDraftService`'s "say so honestly" discipline but made machine-checkable instead of text-sniffed; exchange-limit is plain counting, 3 unresolved turns.
- **Known v1 scope limit, deliberately flagged rather than silently assumed**: requires an authenticated shopper (`HandleShopperMessageAsync` needs a real `shopperUserId`) — anonymous token-based conversations (the existing anonymous contact-form flow) aren't wired to the bot yet, since the bot's order-lookup grounding needs a real customer identity the same way `SupportDraftService`'s linked-order grounding does. Anonymous visitors would need either a different grounding strategy or to sign in first — not resolved here.
- 12 new tests (`ChatbotServiceTests.cs`) covering: grounded FAQ answer, real order grounding (not fabricated), ungrounded → escalate + log, frustration escalates before compose ever runs, explicit request escalates, judgment-call escalates with zero AI calls (3 phrasings), already-escalated stays silent forever on that thread, 3-exchange limit, classify-failure degrades gracefully instead of breaking the chat, and cross-customer ownership is rejected.

### Implementation notes (merchant controls, shipped 2026-08-21)

- `Features/Support/HelpdeskSettingsService.cs` — reuses the existing per-tenant `Settings` key-value pattern already established by `StoreSettingsService`/`CheckoutSettingsService`, not a new settings mechanism. `ChatbotEnabled` defaults to true (opt-out, matching every other v4 AI feature's default posture) — disabling it makes `ChatbotService` skip all bot logic entirely (no AI calls, no state row even created) so every thread is human-owned from message one, same as before this phase existed.
- **Active hours change only post-escalation messaging, never the escalation triggers themselves** — this is the resolved solo-seller design decision applied literally: outside the configured window the customer sees "...though it may not be until our support hours resume" instead of the standard handoff line, but judgment-call/frustration/exchange-limit/no-grounded-data all still fire exactly the same. Compares in UTC as a known v1 simplification rather than the store's own configured `Timezone` setting — flagged, not silently assumed correct for non-UTC merchants.
- `GET/PUT /api/admin/helpdesk/settings` + `GET /api/admin/helpdesk/unanswered` (`HelpdeskAdminController`) — the content-gap feedback loop: every ungrounded question the bot hit is logged (already wired into `ChatbotService` from the core slice) and now readable by the merchant, newest first.
- 8 new tests (2 in `ChatbotServiceTests.cs` for disabled-bot and outside-hours-messaging, 6 in `HelpdeskSettingsTests.cs` for defaults/round-trip/validation/tenant isolation). Full suite: 349/349 passing.
- **Still no frontend for any of this** — no livechat widget, no merchant settings screen for the toggle/hours, no "what the bot couldn't answer" report UI. Backend is real and tested; nothing is user-visible yet.

## Explicitly out of scope for this phase

- **Convergence with the Shopping Assistant** — that's Phase 3, once the assistant exists to converge with. This phase builds the support-only capability set; Phase 3 adds the sales capability set onto the same bot.
- **Bot-initiated refund/return approval** — per the resolved decision, the bot can *log* a return/refund request but never approves one. Not a v1-vs-later question, just permanently out of scope for the bot itself.

---

## Design decisions

### The chatbot is `SupportDraftService`'s discipline, extended for live customer conversation

`SupportDraftService`'s system prompt already encodes nearly every guardrail this phase needs: facts-only from assembled context, never invent dates/status/stock, never promise a refund/discount/exception ("those are the merchant's to decide" — already matches the resolved refund-approval decision), honest "I'll check" instead of guessing. The chatbot reuses this exact posture — same grounding sources, same refusal behavior — but:
- Answers the **customer directly**, not drafting for a merchant to review
- Is **multi-turn**, not one-shot per ticket
- Needs the customer's **own live authenticated order lookup** (SupportDraftService already reads order/shipment/tracking for a given ticket; the bot needs the same data reachable by "which order is this customer asking about," not just "the order already linked to this ticket")
- Needs live **catalog** grounding (stock, price, variants) — SupportDraftService doesn't touch this today since merchant-drafted replies are rarely about live stock in the same way

### No changes to `IAiService` itself

The bot needs to *do* things (look up an order, check stock, eventually add to cart in Phase 3) — this could mean adding tool/function-calling to the core AI abstraction, but that would change a contract every other AI feature (Growth, SEO, SupportDraftService) already depends on, for the benefit of exactly one new feature. Instead: a small orchestration layer **above** `IAiService`, not inside it —
1. Classify what the message needs (an LLM completion call, same `IAiService` every other feature uses)
2. Fetch the actual data in plain C# via existing services (`OrderService`, `ProductService`, `FaqService` — no new data-access pattern)
3. Compose the final answer via a second `IAiService` call, grounded in step 2's real data, same discipline as `SupportDraftService`

This keeps the AI abstraction stable and puts all the new complexity in a feature-specific layer that can evolve without touching what Growth/SEO/SupportDraftService rely on.

### The one deliberate exception to "nothing goes live without human review"

Every other AI surface in v4 (Growth, SEO, product images, campaigns) is draft-then-approve — this is the one place that's genuinely different, and it's worth naming explicitly rather than letting it slide past as an inconsistency. A livechat widget that waited for merchant approval on every message wouldn't be livechat. This is safe specifically because of two things working together, not because the principle is being quietly dropped: **the grounding discipline above** (it literally cannot say something not in the assembled facts) and **hair-trigger escalation** (the moment it's not confident, the topic is a judgment call, or the customer is frustrated, a human takes over). The bot is allowed to answer live; it is never allowed to decide.

### Escalation triggers — how each one gets built

| Trigger | Implementation |
|---|---|
| No grounded data to answer | Same signal `SupportDraftService` already produces (it already says so plainly rather than guessing) — the orchestration layer checks for this and hands off instead of sending that response to the customer |
| Frustration/anger | New: a lightweight LLM classification call per incoming message (not a dedicated sentiment model/vendor — reuses `IAiService` again) |
| Explicit human request | Simple intent classification, same mechanism as above |
| Judgment call (refund/exception/goodwill) | Rule-based, not AI-judged: any request matching return/refund/discount/exception intent escalates unconditionally, per the resolved refund decision — this one is deliberately not left to model confidence |
| 3+ unresolved exchanges | Plain conversation-turn counting, no AI involved |

### Solo-seller behavior (per resolved decision)

The bot attempts full resolution before escalating when a merchant has no active human coverage — concretely: the "no human available" state changes what happens *after* escalation (the widget shows "we'll respond within X hours" and the conversation queues into the existing Inbox) but does **not** change the escalation triggers themselves. The bot doesn't try harder or take more risks when unsupervised — it still hands off on the same judgment-call/frustration/refund triggers as always. What's different is only the *messaging* around what happens next, matching how the design doc's own "active hours" merchant control already frames this.

### Livechat widget reuses existing real-time infra end to end

The widget doesn't need a new transport or a new persistence model — every escalated (or even non-escalated, for history) conversation writes into the existing `SupportTicket`/`ConversationAxis.ShopperMerchant` model, over the existing `NotificationHub`/`ConversationRealtime` SignalR pipeline that already pushes shopper↔merchant messages live. A livechat session literally *is* a `ShopperMerchant` conversation from message one — escalation doesn't "convert" it into a ticket, it already was one, the same "one backend model, many surfaces" principle the design doc states as its first principle.

---

## Implementation notes

- `Features/Support/` gains the chatbot orchestration (intent classification → grounded data fetch → response composition), the escalation-trigger evaluation, and a new lightweight "unanswered question" log table (feeds the merchant-facing content-gap feedback loop)
- Frontend: new storefront livechat widget component (bot-first UI, "AI Assistant" label, status-change messaging on escalation); merchant admin gets a livechat settings page (on/off, active hours) and a "what the bot couldn't answer" report, likely alongside the existing FAQ admin UI since it's literally that content's own gap list
- WhatsApp: same orchestration, new entry point — Phase 1's `IWhatsAppProvider` session-message path replaces the widget's SignalR transport, everything else (grounding, escalation, ticket persistence) is identical

## Verification

- Ask the bot a question it can answer from `Faq` content → correct, grounded answer, no invention
- Ask about a specific order ("where's my order") authenticated as that customer → correct live status, no fabricated delivery date
- Ask something the FAQ/catalog/order data doesn't cover → bot admits it doesn't know and hands off, rather than guessing
- Send a message with clear frustration → escalates even though the bot could technically attempt an answer
- Ask for a refund → always escalates, regardless of value, per the resolved decision — confirm no code path can auto-approve one
- Escalated conversation appears in the merchant's existing Inbox with full prior bot conversation attached — human agent never sees "please repeat your issue"
- Toggle livechat off in merchant settings → widget disappears from storefront
- Simulate no-human-coverage (outside active hours) → bot still attempts full resolution first; only post-escalation messaging changes
- WhatsApp: same question asked over WhatsApp gets the same grounded answer as the widget

**Status:** not started.
