# v4 Phase 2 — Helpdesk: FAQ Wiring, Chatbot, Livechat

**Goal:** the three genuinely-missing pieces from the helpdesk design doc — everything else (unified ticket model, structured FAQ content, grounded-answer discipline) is already built. This phase wires them together into a real bot and a real widget, not a rebuild.

**Depends on:** Phase 1 (WhatsApp connector, for the bot's WhatsApp channel — the storefront widget and chatbot core don't need to wait for it, only the WhatsApp piece does).
**Blocks:** Phase 3's Shopping Assistant, which converges into this same bot rather than being built separately.

**Correction to the original roadmap scope:** `Data/Entities/Faq.cs` already exists — Question/Answer/Category/DisplayOrder/IsPublished, tenant-scoped — and its own doc comment already names it as the bot's future retrieval corpus. **No new FAQ content model is needed.** The real gap is that nothing reads it programmatically yet.

---

## Scope & checklist

- [ ] Chatbot core: grounded answering over `Faq` + live catalog + live order data, extending the exact discipline already proven in `SupportDraftService`
- [ ] Escalation logic (grounding-confidence, sentiment, explicit request, judgment-call, exchange-count triggers)
- [ ] Livechat widget (storefront) — bot-first, reusing existing `NotificationHub`/`ConversationRealtime` SignalR transport
- [ ] Merchant controls: on/off toggle, active hours, FAQ review, "what the bot couldn't answer" log
- [ ] WhatsApp channel for the bot, via Phase 1's `IWhatsAppProvider` session-message capability
- [ ] Full conversation-history handoff into the existing `SupportTicket`/`ShopperMerchant`-axis model on escalation

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
