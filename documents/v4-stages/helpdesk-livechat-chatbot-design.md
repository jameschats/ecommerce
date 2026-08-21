# Helpdesk, Livechat & Chatbot — Complete Design

**Scope:** Every customer-facing and merchant-facing support surface — Contact form, Inbox/ticketing, FAQ/Knowledge base, Livechat widget, and the AI Chatbot that powers it. Also covers how this connects to WhatsApp AI auto-replies (same underlying bot, different channel) and Merchant→Super Admin escalation.

---

## 1. Design Principles

1. **One backend model, many front-end surfaces.** Contact form, Inbox, Livechat, and WhatsApp all feel different to the user, but underneath they're the same conversation/ticket object with a `source` and `type` field. Build once, don't fragment.
2. **Chatbot is what makes Livechat viable for small merchants.** A livechat widget with nobody behind it is worse than no widget. The bot is the default responder; a human is the exception path, not the assumption.
3. **One knowledge base feeds everything.** FAQs written once by the merchant power the public FAQ page, the chatbot, and WhatsApp auto-replies — never maintained twice.
4. **Never let a bot guess.** It answers only from real data (catalog, orders, merchant-authored FAQs) — never invents policy, stock, or specs. If it doesn't know, it says so and hands off.
5. **Escalate on emotion, not just complexity.** A question the bot technically "could" answer but that carries frustration/anger should still route to a human — de-escalation is not a bot's job.

---

## 2. Surface Map

```
Customer-Facing
├── Contact Form         (async, low-effort, non-urgent)
├── Inbox                (async, tracked conversation — ticket lifecycle)
├── Livechat Widget       (real-time, on storefront — bot-first)
├── WhatsApp              (real-time, conversational commerce channel — bot-first)
└── FAQ / Knowledge Base  (self-serve, also trains the bot)

Merchant-Facing (existing, unchanged)
├── Support/Helpdesk (escalation to Super Admin)
└── Livechat with Super Admin
```

---

## 3. The Chatbot — Core Design

### 3.1 What it's trained on (Knowledge Sources)

| Source | Feeds |
|---|---|
| Merchant-authored FAQ content | General policy questions (shipping time, returns, sizing guide) |
| Product catalog (live data) | Stock, price, variants, specs — always pulled live, never memorized/stale |
| Order data (customer's own orders, authenticated) | "Where's my order," "when will it arrive," order-specific status |
| Brand Voice profile (from AI Marketing Engine) | Tone of responses — matches how the merchant's other content sounds |

**Critical rule:** The bot answers from these sources only. It does not generate plausible-sounding policy or specs it wasn't given — same guardrail principle as the AI Marketing Engine's content generation.

### 3.2 What it can *do*, not just answer

- Look up order status and share it directly
- Check product stock/variant availability in real time
- Recommend products based on a described need (this overlaps with the AI Commerce Engine's Shopping Assistant — see Section 6, they should be the same underlying assistant)
- Initiate a return/refund request (logs it, doesn't necessarily auto-approve — merchant policy decides if this is automatic or needs review)
- Escalate to a human with full conversation context attached (no "please repeat your issue" to the human agent)

### 3.3 Escalation triggers (bot → human)

The bot hands off when:
- It doesn't have grounded data to answer confidently
- The question is a complaint or shows frustration/anger (sentiment-based trigger, not just topic-based)
- The customer explicitly asks for a human
- The request involves a judgment call outside defined policy (e.g., goodwill refund exception, damaged-in-transit dispute)
- Repeated back-and-forth without resolution (e.g., 3+ exchanges without the bot successfully answering)

**On handoff:** full conversation history transfers into the Inbox as a ticket — the human doesn't start from zero.

---

## 4. Livechat Widget

**Behavior:**
- Bot responds first, always, instantly
- Widget shows a subtle "AI Assistant" label so customers know they're talking to a bot initially (transparency — avoid the trust cost of a customer discovering later they weren't talking to a person)
- If escalated, widget shows a status change ("Connecting you to [Merchant Name]'s team") and sets expectation on response time if no human is immediately available
- If merchant has no one actively monitoring (common for small merchants), escalated conversations fall back to Inbox — customer gets a "we'll respond within X hours" message rather than being left in a dead chat window

**Merchant controls:**
- Toggle livechat on/off
- Set active hours for human availability (bot-only outside those hours, clearly indicated)
- Review/edit the FAQ content that trains the bot
- See a log of what the bot couldn't answer — this is valuable signal for gaps in FAQ/product content

---

## 5. Inbox / Ticketing (existing surface, formalized)

- Every conversation — whether it started as Contact form, escalated Livechat, or escalated WhatsApp — lands here as a ticket
- Ticket fields: source (contact_form / livechat / whatsapp / email), status (open/in progress/resolved), priority, assigned staff member, full conversation history including any bot interaction before handoff
- SLA tracking (time to first response, time to resolution) — useful data for the merchant, and rollup-able into Super Admin analytics if platform wants to track support health across stores

---

## 6. Relationship to WhatsApp AI Auto-Replies (already planned in Social Commerce)

**This is the same chatbot, different channel — not a separate build.**

The WhatsApp Commerce module already planned "AI auto-replies for FAQ-shaped product questions." That should be powered by the exact same knowledge base and escalation logic described here, just delivered over WhatsApp instead of the on-site widget. One bot, multiple channels:

```
                    ┌─────────────┐
                    │  Chatbot    │
                    │  (one brain)│
                    └──────┬──────┘
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
     Livechat Widget   WhatsApp        (future: 
     (storefront)      (Social         Customer App
                        Commerce)       in-app chat)
```

Building it this way means a merchant writes their FAQ/policy content once, and it powers support across every channel automatically.

---

## 7. Relationship to AI Commerce Engine's Shopping Assistant

Worth naming directly: the **Shopping Assistant** (AI Commerce Engine, product-discovery focused — "recommend me a moisturizer under ₹500") and the **Support Chatbot** (this doc, support-focused — "where's my order") are conceptually two jobs, but in practice customers don't distinguish "sales question" from "support question" — they just type into the same box.

**Recommendation:** these should be **one assistant with two capability sets**, not two separate bots a customer has to choose between. The design in Section 3 already includes product recommendation as something the bot can do — this is intentional convergence, not overlap to be resolved later.

---

## 8. Guardrails Summary

- Bot never invents policy, stock, pricing, or specs — grounded-data-only, same principle as AI Marketing Engine
- Bot clearly identified as AI, not disguised as a human
- Escalation on sentiment (frustration/anger), not just topic complexity
- Full context transfer on every handoff — human agents never start from zero
- Merchant can always review what the bot couldn't answer — this is a feedback loop for improving FAQ content, not just a support log

---

## 9. Credit/Cost Model

Consistent with the rest of the AI system: bot conversations are usage-metered (live AI calls, similar to the Shopping Assistant and Product Images in earlier designs) — likely bundled into a conversation-volume allowance per plan tier, with overage credits for high-traffic merchants.

---

## 10. Build Sequence (Recommended)

1. **Unified backend ticket/conversation model** (source + type fields) — foundation, since Contact form/Inbox already exist and need to plug into this cleanly
2. **FAQ/Knowledge base** as structured, bot-readable content (not just a static page)
3. **Chatbot core** — grounded answering from FAQ + live catalog data
4. **Livechat widget** with bot-first response + human fallback
5. **Escalation logic** (sentiment + confidence-based handoff) with full context transfer to Inbox
6. **WhatsApp integration** — same bot, new channel (shared build with Social Commerce WhatsApp work)
7. **Convergence with Shopping Assistant** (AI Commerce Engine) into one assistant, two capability sets

---

## 11. Open Questions

1. Should the bot be allowed to auto-approve simple, low-value refund/return requests within a merchant-set threshold, or should every return request always go to a human regardless of value?
2. Active-hours fallback — if a merchant has no human coverage at all (solo seller), should the bot attempt to resolve *everything* it can and only queue truly unresolved items, rather than "escalating" to a human who may not respond for a day?
3. Does the Support Chatbot / Shopping Assistant convergence (Section 7) happen in v1, or do we ship them separately first and merge once both are proven?
