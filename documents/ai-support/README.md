# AI Support — Conversations, Helpdesk & Chatbot

**Goal:** give every store one inbox where shopper questions arrive, get answered — increasingly by AI grounded in real order data — and never fall on the floor.

> Live chat is **presence-derived, never presence-declared.** A widget that shows "online" and then goes unanswered for six hours is worse than no widget — it advertises an absent merchant at the exact moment of purchase intent. So availability is computed from business hours and an active session, and any unanswered chat degrades itself to async within 60 seconds. Async is the floor the product always falls back to; live is the upgrade on top.

Companion to [ai-growth](../ai-growth/README.md). That module wins attention; this one converts and keeps it.

---

## Goal (the "why")

Today a shopper with a question has exactly three ways to reach a merchant on this platform: place an order, cancel an order, or write a review. That is it.

The contact page is **not real**. [`contact.component.ts`](../../ecomm.web/src/app/features/pages/contact/contact.component.ts) renders a form whose `submit()` sets `sent.set(true)` and clears the fields. It makes no HTTP call. The template says so out loud: *"This is a demo form — submissions aren't stored yet."* A shopper fills it in, sees a success message, and their question goes nowhere. That is not a missing feature; it is a live bug that silently loses sales.

Meanwhile the data needed to answer the most common questions is **already in the database and already projected into a DTO**. `OrderDto` carries status, totals, items, payment state and a nested `ShipmentDto` with courier, AWB, ETA and delivery timestamps. What is missing is not the knowledge. It is the channel.

**The bet:** a merchant who answers questions in an hour outsells one who answers in a day, and a bot grounded in real order rows can answer most of them in a second.

---

## Verdict on the idea

| Claim | Verdict | Reasoning |
|---|---|---|
| Merchants need a shopper contact channel | ✅ **Urgent** | The current one is fake. This is a bug, not a roadmap item. |
| A unified inbox benefits the commerce platform | ✅ **Strongly** | Conversion (pre-sales), retention (post-sales), and hard lock-in — conversation history is the stickiest data a merchant owns. |
| AI can answer most questions | ✅ **True, with a ceiling** | WISMO is the #1 volume driver and is fully answerable from `Order` + `Shipment`. But see *the ceiling* below. |
| **Live chat, super-admin ↔ merchant** | ✅ **Adopt — and it's nearly free** | You can staff it (tens of merchants, not thousands of shoppers), it hits the highest-churn-risk moment in the funnel, and both parties are authenticated so the existing hub works almost unchanged. |
| **Live chat, merchant ↔ shopper** | ✅ **Adopt, with derived presence** | Merchant-toggleable, **off by default**. Safe only because availability is computed, not declared, and unanswered chats auto-degrade to async. |
| Live chat gated on **name + email** | ✅ **The tier that converts** | Reuses the C1 signed thread token, so far cheaper than open anonymous chat — and pre-sales is the only case where live genuinely beats async. |
| **Drive-by** anonymous live chat | ⛔ **Probably never** | A visitor-id scheme plus an open socket and its abuse surface, to serve a case identify-first already covers. |
| Helpdesk for merchant↔platform (your own support) | ✅ **Already ~60% built** | `Features/Support/` ships this. Deepen, don't rebuild. |
| Put it in the AI Growth product | ⛔ **No** | Different job, different module. Marketing wins attention; support keeps it. |
| WhatsApp as the channel | 🟡 **Right long-term, not first** | No `IWhatsAppSender` exists; WhatsApp Business API costs per conversation and needs approval. Web widget first (free), WhatsApp when volume justifies it. |

### Why this may matter more than AI Marketing

AI Marketing is more *differentiating*. This is more *load-bearing*. A store with no contact channel leaks sales silently and the merchant never learns why. And unlike marketing content — which a merchant could buy from Canva tomorrow — conversation history creates switching costs that compound every month.

**But it is cheaper.** The foundation (C0–C1) is a form, two tables, and an inbox screen. There is no reason not to do it.

### The ceiling — and it is real

A bot can only resolve what the system can *do*. Two hard limits, both confirmed in code:

- **Customer-initiated returns do not exist at any layer.** Zero hits for RMA / return-request across the codebase. `"Returned"` is only ever set by the Shiprocket webhook or an admin. So a delivered order has **no customer action path at all** — for "I want to return this", the bot can quote policy and hand off, nothing more.
- **Tracking checkpoints are not stored.** Each Shiprocket webhook *overwrites* `Shipment.Status`, and unmapped statuses are explicitly discarded (`Map()` returns `(null, null, false)`). So "out for delivery since Thursday" is unanswerable even though the courier sent that event.

**Sequencing consequence: close the data gaps before building the bot.** A bot launched on top of these gaps is a disappointment merchants switch off and never switch back on. C2 exists for exactly this reason and must not be skipped.

### The risk that needs the most discipline

A bot on a public storefront speaks **as the merchant's brand**. If it invents a return window, promises a delivery date, or quotes a price it inferred, the merchant carries the consequence — potentially a legal one under consumer-protection rules. Every answer must be **retrieval-grounded with an explicit refusal path**, never free generation. Hard rule in C4: *if the retrieval step returns nothing relevant, the bot says it will connect a human. It does not compose an answer.*

---

## What already exists (reuse, don't rebuild)

- **Merchant↔platform ticketing** — `Features/Support/SupportController.cs` has two controllers: `api/support/tickets` (`[Authorize(Roles="Admin")]`, the *merchant* raising a ticket with you) and `api/superadmin/support/tickets` (`[Authorize(Roles="SuperAdmin")]`, your queue, with internal notes). Entities `SupportTicket`/`SupportMessage`, both `ITenantScoped`. Screens: `admin/support` and `superadmin/support`.
- **Grounding data, already DTO-shaped** — `Features/Orders/OrderService.cs` → `OrderDto` (status, items, addresses, payment, `canCancel`) with nested `ShipmentDto` (courier, `TrackingNumber` AWB, status, ETA, `ShippedAt`, `DeliveredAt`). `OrderStatusHistory` holds a real from→to timeline **already being written** and simply not exposed.
- **Policy corpus** — `Features/Policies/`, entity `StorePolicy : ITenantScoped`, six handles (`refund`, `privacy`, `terms`, `shipping`, `contact`, `legal`), per-tenant merchant-editable HTML, sanitized on save. Frequently empty — handle that.
- **Product data for pre-sales** — `ProductDetailDto` carries variants (`VariantOption` Size/Color pairs), specs (`ProductAttributeValue` → `AttributeDefinition`), and stock. Good enough for "does it come in blue", *if* the merchant populated it.
- **Transports** — `IEmailSender` (Logging/Smtp, per-tenant `Reply-To`) and `ISmsSender` (Console/MSG91). **No `IWhatsAppSender` — confirmed absent.**
- **AI layer** — `IAiService`, `AiCreditService.MeterAsync`, `AiUsageLog`. Note all current AI is `[Authorize(Roles="Admin")]` merchant authoring help; none of it is shopper-facing.
- **Realtime** — SignalR `NotificationHub` at `/hubs/notifications`, **`[Authorize]` at class level**, groups by `Context.UserIdentifier`. An anonymous visitor has no token and no user id, so **a public widget cannot reuse this hub.** See below.

### Correcting an assumption

`SupportTicket.OpenedByPlatform` and `SupportMessage.FromPlatform` are **two-party booleans**. They cannot express a third participant. Reusing these tables as-is for shopper conversations would be a mistake.

The right move is neither "reuse as-is" nor "build a parallel system": **generalise the model once.** Replace the booleans with `AuthorType` (`Shopper`|`Merchant`|`Platform`) and add an `Axis` discriminator (`ShopperMerchant`|`MerchantPlatform`) to the thread. One engine, one inbox component, one notification path, two axes — and your own merchant-support queue inherits every improvement built for shoppers. The migration is mechanical (`FromPlatform=true` → `AuthorType=Platform`) and is scoped in C1.

### Realtime: cheap on two axes, expensive on one

The cost of live chat is not uniform, and splitting it by participant is what makes it affordable:

| Axis | Socket cost | Verdict |
|---|---|---|
| Super-admin ↔ merchant | **Near zero** — both authenticated, `NotificationHub` groups by `UserIdentifier` today | Build first |
| Merchant ↔ logged-in shopper | **Near zero** — same mechanism, shopper has a JWT | Build second |
| Merchant ↔ **identified** visitor (name + email first) | **Moderate** — reuses the C1 signed thread token as socket credential | C1b, and this is the one that earns money |
| Merchant ↔ **drive-by** anonymous (no details) | **High + permanent risk** — new visitor-id scheme, open socket, spam/DoS surface | Defer indefinitely |

### Why identified-visitor chat is the tier that matters

Checkout is member-only (`OrdersController` is `[Authorize]`, `Order.UserId` non-nullable), so **a logged-in shopper is by definition someone who already bought.** Live chat for authenticated shoppers therefore only ever covers post-purchase — and post-purchase is precisely where async threads and the bot already suffice. Nobody needs a human in real time to learn their parcel is in transit.

Live chat converts on **pre-sales**: the shopper hesitating over a ₹2,000 saree who wants to ask whether it suits a wedding. That shopper has no account. Build only the cheap authenticated tier and the feature will look complete while missing the case that justifies it.

**The resolution is to ask for name and email before connecting** — not to open the socket to everyone. This costs far less than a general anonymous-visitor scheme because C1 already issues **signed thread tokens** for anonymous email replies; live chat reuses that exact credential to join `conversation-{id}`. No visitor-id scheme, no relaxed `[Authorize]` on an open path, rate-limited at token issuance rather than at the socket. It is also the standard pattern (Intercom, Tawk), it doubles as lead capture, and the small friction filters most abuse.

**Async remains the substrate.** Every conversation is a durable thread first and a live session second, so a dropped socket, a closed tab, or an absent merchant degrades to something that still works. This is what keeps live chat from becoming a liability: there is no state that exists only in a live session.

### Derived presence — the rule that makes optionality safe

A merchant *may* enable live chat. What they may **not** do is declare themselves available. Availability is computed:

- **Business-hours schedule** — per-tenant, with timezone.
- **Active admin session** — a real authenticated session with recent activity, not a saved preference.
- **Rolling response-time** — if median first-response drifts past the promise, presence auto-degrades to async and tells the merchant why.
- **60-second pickup timeout** — an unanswered live chat converts itself into an async thread in front of the shopper ("no one's free right now — we'll email you within 4 hours") and notifies the merchant. **The shopper never watches an empty chat window.**

The merchant keeps the switch. The system keeps the promise.

---

## Naming

Stages are **C0–C6** (Conversations). Avoids `AI-0…AI-6` (already overloaded twice) and `G0–G6` ([ai-growth](../ai-growth/README.md)).

Migration band **`250–259`**. Note the V2 bands reserved in [v2-stages/README.md](../v2-stages/README.md) run
through `230–239`; in particular `190–199` belongs to **V2-9 Merchant Support & Ticketing**, whose ticket model
this module generalises. [ai-growth](../ai-growth/README.md) takes `240–249`.

---

## Scope & checklist

### C0. Make the contact form real ✅ **Built** *(migration `250`)*

- [x] **`ContactMessage` entity + endpoint** — anonymous `POST api/contact` (name, email, phone, subject, body, page URL), `"contact"` rate-limit policy (**5/hour per IP**) alongside the existing `"auth"` one, plus a hidden honeypot field. A honeypot hit returns the **identical success response** and stores nothing, so a bot can't distinguish acceptance from rejection. Input is length-capped by truncation rather than rejection.
- [x] **Wire the real form** — `contact.component.ts` now posts, captures `sourceUrl`, and surfaces a specific message on 429. The *"submissions aren't stored yet"* line is gone.
- [x] **Notify the merchant** — admin bell via `INotificationFeedService.NotifyAdminsAsync` (`Type=ContactMessage`, deep-links to `/admin/messages`). **Email deferred:** `INotificationService.SendEmailAsync` renders a `NotificationTemplate` row keyed by code, so it needs a seeded template — worth doing with the C1 thread notifications rather than a one-off.
- [x] **Admin list** — `/admin/messages`, New/Handled/All filters, mark-handled records who and when and is reversible. Linked under Customers in the admin nav.
- [x] 10 tests in `ecomm.tests/ContactTests.cs`; verified end-to-end against MySQL (message persists, honeypot drops silently, bad email 400s, bell row written, 6th submit 429s).

### C1. Unified conversation engine

- [x] **Generalise the ticket model** ✅ **Built** *(migration `251`)* — `AuthorType` (Shopper/Merchant/Platform) replaces `FromPlatform`; `Axis` (ShopperMerchant/MerchantPlatform) on the thread; existing rows backfilled. Adds reference (`TKT-yyyy-#####`), priority, category, assignee, `FirstResponseAt`/`ResolvedAt`, and the shopper-linkage columns (`ShopperUserId`, `ShopperEmail`, `OrderId`, `ProductId`) that C1's shopper threads will use. `PUT /api/superadmin/support/tickets/{id}/triage` sets priority/category/assignee. Both shipped screens keep working unchanged — `TicketMessageDto.FromPlatform` is now *derived* from `AuthorType`.
  > **Expand-only, deliberately.** `FromPlatform` and `OpenedByPlatform` are backfilled and then **left in place, unused**. The deploy applies migrations *before* restarting the API ([deployment.md §10-WAV](../deployment.md)), so for a few seconds the **old binary reads these tables** — dropping the columns here would 500 the support screens until the restart landed. A later migration drops them once this code is live. The new code still writes both columns so a rollback reads correct data.
- [ ] **Attachments** — `ConversationAttachment` over `IMediaStorage`.
- [x] **Shopper threads** ✅ **Built** — `ShopperConversationService` over the same tables, optionally linked to an `OrderId`/`ProductId`. **An order id is only linked after checking the shopper owns it** — otherwise it's silently dropped, never trusted off the wire.
- [x] **Shopper identity** ✅ **Built** — signed-in shoppers by `UserId`; anonymous by email + a **DataProtection-signed reply token** (`ecomm.conversation.reply.v1`), the same mechanism as the encrypted tenant secrets, so `dp-keys` surviving redeploys already matters. The token names **one conversation and nothing else**: a leaked link exposes one thread, not an account. A token signed by a different key ring is rejected.
- [x] **Endpoints** ✅ — shopper: `POST /api/conversations` (anonymous, rate-limited), `GET /api/conversations` + `/{id}` (own threads only), `GET|POST /api/conversations/thread/{token}`. Merchant: `GET /api/admin/inbox`, `/{id}`, `POST /{id}/messages`, `PUT /{id}/status`, `GET /open-count`.
- [ ] **Merchant inbox UI** — the API is done; the `admin/inbox` screen and the shopper's account view are the remaining piece.
- [ ] **Email round-trip** — merchant replies land in the shopper's mail; `SmtpEmailSender` already sets a per-tenant `Reply-To`. Inbound email parsing is **out of scope** — a reply link back into the thread is enough.
- [ ] **Response-time promise** — merchant sets it, storefront displays it ("usually replies within 4 hours"). Shown whenever live chat is unavailable.

### C1b. Live chat — authenticated axes

Both parties hold a JWT, so `NotificationHub` needs extension rather than redesign.

- [ ] **Conversation groups on the existing hub** — join `conversation-{id}`; reuse the query-string token auth already wired for `/hubs` in `Program.cs`. Messages persist to `ConversationMessage` first, then broadcast — **never socket-only**.
- [ ] **Super-admin ↔ merchant live chat** — the platform queue gains presence, typing indicators and instant delivery. Highest value per unit of work in this document.
- [ ] **Merchant ↔ logged-in shopper live chat** — same mechanism, gated by the merchant's toggle.
- [ ] **Identified-visitor live chat** — widget collects name + email, issues the C1 signed thread token, and that token authorises the socket join. Rate-limit issuance per IP and per email. This is the pre-sales path and the commercially important one; a `Conversation` created this way is also a captured lead.
- [ ] **Derived availability service** — business hours + active session + rolling response-time, exposed as a single `IsLive` the storefront and console both read. No manual "I'm online" control anywhere.
- [ ] **60-second pickup timeout** → auto-degrade to async, in-widget explanation, merchant notified.
- [ ] **Reconnect + backlog fetch** — on reconnect, pull missed messages from the thread. The socket is an accelerator; the thread is the truth.
- [ ] **Merchant chat toggle** — off by default; enabling it explains that presence is computed and cannot be faked.

### C2. Close the grounding gaps *(prerequisite for any bot)*

- [ ] **Expose `OrderStatusHistory` on `OrderDto`** — already written, never surfaced. Cheapest win on this list.
- [ ] **Persist tracking checkpoints** — store raw Shiprocket webhook payloads as append-only `ShipmentCheckpoint` rows instead of discarding unmapped statuses. Enables a real delivery timeline.
- [ ] **Order lookup for logged-out shoppers** — `GET api/orders/lookup?orderNumber=&email=`, strictly rate-limited, returning a *reduced* payload (status + tracking only, never addresses or totals). Removes the biggest friction point in WISMO.
- [ ] **`Faq` entity** — no FAQ exists anywhere today (`Page.Type` is only `Home|Custom`; there is no FAQ section type). Per-tenant Q&A pairs, merchant-editable, storefront-rendered, and the bot's primary retrieval corpus.
- [ ] **Normalise order status vocabulary** — `Order.Status` is a free-form `string` and `"Confirmed"` appears in `OrderService` and the web badge map but not in the documented set. A bot narrating status needs one vocabulary.
- [ ] **Merchant reply to reviews** — `Review` has no reply fields; the merchant has no public voice today. Small change, disproportionate trust value.

### C3. AI draft replies *(merchant-in-the-loop — build this before any autonomous bot)*

- [ ] **Suggested reply** in the merchant inbox — grounded in the linked order, shipment, policies and FAQ; merchant edits and sends. **Zero hallucination risk, because a human ships every word.**
- [ ] **Tone + brand voice** reused from the AI Growth brand kit — one setting, both modules.
- [ ] **Metered per merchant action** via the existing `MeterAsync` — this fits the shipped credit model exactly, with no rethink needed.
- [ ] **Capture the corpus** — every accepted/edited draft is labelled training and retrieval data. This is what makes C4 safe, and it is the real reason C3 comes first.

### C4. Shopper-facing bot *(guarded)*

- [ ] **Retrieval-grounded answers only** — FAQ + policies + (for identified shoppers) their own order and shipment. **No relevant retrieval → no answer → offer a human.** Never free-generate.
- [ ] **Scope allowlist** — order status, shipping/returns policy, stock and variant availability, store hours. Everything else escalates.
- [ ] **Never commits the merchant** — no delivery-date promises, no discounts, no refund approvals, no order modifications.
- [ ] **Instant handoff** — any escalation, low retrieval confidence, or the word "human" creates a C1 thread carrying full context.
- [ ] **Per-tenant kill switch, default off.** Merchant opts in, previews against their own data, and can disable in one click.
- [ ] **Abuse + cost control** — per-conversation message cap, per-visitor rate limit, per-tenant daily ceiling enforced **before** the provider call. Unbounded anonymous volume is the one place this module can lose money.

### C5. Merchant-support intelligence *(your own leverage)*

- [ ] **Bot on the merchant↔platform axis**, grounded in `documents/` — every merchant question answered from documentation is a support hour you personally do not spend.
- [ ] **Deflection before ticket creation** — suggest doc answers as the merchant types.
- [ ] **Ticket clustering** — recurring themes surfaced in super-admin, so the top-10 merchant confusions become a roadmap input.

### C6. Channel expansion *(demand-gated)*

- [ ] **`IWhatsAppSender` + inbound webhook** — WhatsApp is where Indian shoppers actually message. Gated on volume because Business API bills per conversation and needs approval.
- [ ] **Drive-by anonymous live chat (no name/email)** — the genuinely expensive tier: a visitor-id grouping scheme, an open socket path, and the spam/DoS surface that comes with it. **Likely never worth building** — identified-visitor chat (C1b) covers the same commercial case, and the bot (C4) handles the rest without a socket. Revisit only if identify-first friction measurably suppresses chat volume.
- [ ] **CSAT** after resolution, on both axes.

---

## Data model

Migrations `250–259`, tenant-scoped via `ITenantScoped` unless noted.

`ContactMessage` (Name, Email, Phone, Subject, Body, SourceUrl, Status, HandledByUserId) — C0, deliberately standalone so C0 ships without waiting on C1. `Conversation` (Axis, Subject, Status, Priority, Category, Reference, AssignedToUserId, ShopperUserId?, ShopperEmail?, OrderId?, ProductId?, LastMessageAt, FirstResponseAt, ResolvedAt) — generalises `SupportTicket`. `ConversationMessage` (ConversationId, AuthorType, AuthorUserId?, Body, IsInternalNote, IsAiGenerated, IsAiDraftAccepted) — generalises `SupportMessage`. `ConversationAttachment` (MessageId, MediaId). `ChatAvailability` (BusinessHoursJson, Timezone, IsLiveChatEnabled default **false**, PickupTimeoutSeconds default 60, ResponseTimePromise, LastAgentActivityAt) — backs derived presence; `ConversationMessage` gains `DeliveredAt`/`ReadAt` for live delivery state. `Faq` (Question, Answer, Category, DisplayOrder, IsPublished) — C2, bot retrieval corpus. `ShipmentCheckpoint` (ShipmentId, RawStatus, MappedStatus, Location, OccurredAt, RawPayload) — append-only, C2. `BotSettings` (IsEnabled default **false**, AllowedTopics, EscalationMessage, DailyMessageCap, ResponseTimePromise). `BotConversationLog` (ConversationId, Question, RetrievedContext, Answer, Confidence, WasEscalated, CreditsUsed).

Extends existing: `Review` gains `ReplyBody`/`RepliedAt`/`RepliedByUserId`; `OrderDto` gains the status timeline; `AiCreditPricing` gains `support-draft` and `support-answer`.

---

## Endpoints

Public: `api/contact` (POST, anonymous, rate-limited), `api/conversations/thread/{signedToken}` (GET/POST — anonymous reply-link access), `api/orders/lookup` (GET, rate-limited, reduced payload), `api/faq` (GET), `api/bot/ask` (POST, rate-limited, tenant-capped).

Shopper: `api/conversations` (GET list, POST, GET/:id, POST/:id/messages).

Merchant: `api/admin/inbox` (GET list with axis/status/priority filters), `/:id` (GET, PUT status/assignee/priority), `/:id/messages` (POST), `/:id/draft` (POST — AI suggestion), `api/admin/faq` (CRUD), `api/admin/bot-settings` (GET, PUT), `api/admin/chat-availability` (GET, PUT), `api/admin/reviews/:id/reply` (POST).

Live: `api/chat/availability` (GET, public — derived `IsLive` + promise), and hub group `conversation-{id}` on the existing `/hubs/notifications` with server events `MessageReceived`, `TypingStarted`, `AgentJoined`, `DegradedToAsync`.

Platform: existing `api/superadmin/support/tickets` folds into the shared engine, gaining the same filters and draft-reply support.

---

## Frontend

Storefront: a real contact form; an **async** chat widget (thread on load, poll while open — no socket); an FAQ page; an order-lookup page for logged-out shoppers. Account: "My conversations".

Merchant: `features/admin/inbox/` — one component, axis-filtered, so shopper threads and platform tickets share a screen. Replaces the current `admin/support` screen rather than sitting beside it. Plus FAQ management and a bot settings page with a preview sandbox.

Super-admin: `superadmin/support` moves onto the shared engine and keeps internal notes hidden from merchants.

---

## Pricing

| Capability | Tier | Why |
|---|---|---|
| Contact form, inbox, FAQ, order lookup | **Free, all tiers** | Table stakes. Charging for a working contact form is charging to fix a bug. |
| Live chat with platform support (C1b) | **Free — higher tiers get priority** | This is *your* retention tool, not a SKU. Gating it would mean slow-walking the merchants most likely to churn. |
| Merchant→shopper live chat (C1b) | **Paid tier** | Real feature, real value, no per-use cost — a clean tier differentiator. |
| AI draft replies (C3) | **AI add-on**, metered per action | Fits the shipped `MeterAsync` model exactly. |
| Shopper bot (C4) | **AI add-on**, metered + capped | Per-tenant daily ceiling enforced before the provider call. |
| WhatsApp (C6) | **Usage-metered** | Business API bills per conversation; never bundle a per-use external cost. |

Free helpdesk is the acquisition story; metered AI on top is the margin.

---

## Gate

The contact form stores a real row and notifies the merchant by email and bell; a shopper opens a thread from an order page and the merchant answers it from `admin/inbox`, with the shopper reading the reply via a signed link **without logging in**; the same inbox shows a platform ticket under an axis filter, with internal notes invisible to the merchant; `OrderDto` returns the status timeline and a Shiprocket webcheckpoint appends rather than overwrites; a logged-out shopper resolves order status via rate-limited lookup and cannot retrieve addresses or totals; a super-admin and a merchant hold a live chat with instant delivery and typing indicators, and killing the socket mid-conversation loses no message because the thread persisted first; with live chat enabled but outside business hours the storefront shows the async promise and never an "online" badge; an unanswered live chat degrades to async within 60 seconds, tells the shopper so, and notifies the merchant; a merchant with live chat off sees no presence surface at all; an AI draft reply cites the real order and debits exactly the priced credits; with the bot **on**, a question outside the allowlist escalates to a human thread rather than being answered, an out-of-corpus question triggers refusal-and-handoff rather than invention, and the daily cap blocks the provider call rather than reconciling after; with the bot **off** (the default) no shopper-facing AI surface renders; `dotnet test ecomm.tests` green.

---

## Phasing

**C0 immediately — it is a bug fix.** Then C1 (the engine and inbox), then **C1b starting with the super-admin↔merchant axis** — it is the cheapest live chat you will ever build and it protects trial conversion, which is the metric that matters most right now. Prove presence-derivation and auto-degradation on merchants you control before pointing any of it at shoppers.

Then **C2 before any AI**, because a bot on top of missing checkpoints and no FAQ will disappoint. C3 (draft replies) is the highest value-to-risk step in this document: real merchant benefit, zero hallucination exposure, and it builds the corpus that makes C4 safe. C4 only after C3 has produced real Q&A volume. C5 whenever your own support load justifies it. C6 on demand — and note that the bot may reduce anonymous live-chat demand enough that the expensive guest-socket work never needs doing.

**Against [ai-growth](../ai-growth/README.md) — support-first is the chosen order.** C0 → C1 → C1b → C2 runs to completion with no dependency on the marketing module, fixes a live defect, and builds the merchant-retention surface early. Pick up the two shared items (entitlements, brand kit) at C3, then G0–G1 inherits them.

The trade being accepted: AI Marketing is the stronger *upgrade trigger*, so revenue arrives later this way. Worth it if support quality and merchant retention matter more right now than a new paid tier — and C0 is a bug fix that shouldn't wait behind anything regardless.

---

## Dependencies

**C0 → C2 depend on nothing from [ai-growth](../ai-growth/README.md).** This module can be built first, in full, without touching the marketing work. Two shared items (🔗 in G0) are needed only from C3 onward, and whichever module reaches them first should build them:

| Shared item | First needed by | Note |
|---|---|---|
| `IEntitlementService` | C3, and only to gate AI behind a paid tier | The commerce platform needs this anyway — `MaxProducts`/`MaxOrders` are enforced nowhere today. |
| Brand kit (tone) | C3, minimally — reply tone only | Support needs tone + language; the marketing fields (hashtags, emoji) can come later. |

**Not needed by this module at all:** reserve-then-settle credits (that is for long-running image/video jobs — every support AI action is synchronous and the shipped `AiCreditService.MeterAsync` already handles it correctly), the growth scheduler, and the provider registry.

Shipped: `Features/Support/` (to generalise), `INotificationService`, `IAiService` + credits, `Features/Policies/`, `Features/Orders/` + `Shipment`, rate limiting (V2-7), `NotificationHub` + its query-string token auth (extended in C1b, not redesigned), `IMediaStorage` for attachments. Built here: the conversation engine, live chat on authenticated axes, derived presence, checkpoints, FAQ, order lookup, bot guardrails. Shares with AI Growth: the brand kit (tone), the entitlement layer from G0, and `AiCreditPricing`. Not required until C6: Hangfire, WhatsApp transport, inbound email parsing, anonymous sockets.

⚠️ **Scaling note:** `TenantResolutionMiddleware` caches in `IMemoryCache` and its own comment flags Redis as needed for multi-instance. Live chat makes this bite sooner — SignalR across more than one instance needs a Redis backplane, or messages only reach clients on the same node. Single instance is fine today; revisit before scaling out.

---

## Open items for James

1. **Generalising `SupportTicket` touches shipped code** — the merchant and super-admin support screens both move onto the new model. Mechanical, but it is a real migration on live data, so it wants a backup and a test pass. The alternative (a parallel shopper system) is worse: two inboxes, two notification paths, permanently divergent.
2. **C4 needs a per-tenant liability decision.** A bot answering as the merchant's brand is the merchant's legal exposure. Recommend explicit opt-in with a shown disclaimer, plus a visible "AI assistant" label on every bot message — not a silent default.
3. **Returns are the elephant.** The single most common post-purchase request has no system path at all. Whether returns/RMA belongs here, in the commerce core, or on the [platform roadmap](../platform-roadmap-p2.md) is a scoping call worth making deliberately — but a support module that cannot action the #1 support request will keep feeling incomplete until it exists.

---

**Status:** 🟡 **C0 built** (migration `250`) · **C1 model generalised + triage fields built** (migration `251`) — both 2026-07-22. Next: shopper threads on the new `ShopperMerchant` axis (entry points, signed reply links, merchant inbox), then C1b live chat.
