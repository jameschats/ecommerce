# v4 Phase 3 — Commerce Data Layer, Personalization, Shopping Assistant

**Goal:** build the one real missing foundation (server-side browsing/behavior capture), extend the existing rule-based recommendation rails rather than replace them, and add product-discovery/cart capability to Phase 2's bot instead of building a second one.

**Depends on:** Phase 2 (Shopping Assistant capability set is added to that bot, not a new one).
**Blocks:** Phase 5 (Dynamic Pricing needs this phase's demand-signal data), and feeds Phase 6's Analytics work directly (same event data, second consumer).

This phase has **two independent tracks** with different urgency — the Shopping Assistant needs nothing from the data-layer track and can ship first.

---

## Track A — Shopping Assistant (ships first, no dependency on Track B) ✅ Done (2026-08-21)

### Scope & checklist
- [x] Natural-language product discovery ("moisturizer for dry skin under ₹500") added as a new capability on Phase 2's bot orchestration layer
- [x] Product comparison ("what's the difference between these two") — same-message naming only ("compare X and Y" / "X vs Y"); cross-turn pronoun reference ("these two", from earlier messages) is a known v1 gap, flagged below
- [x] Availability/variant Q&A from live catalog data — already covered by Phase 2's own product-topic grounding (`ProductListItemDto.InStock`/`AvailableQty`), no new data access needed
- [x] Cart actions — bot can add a product to cart **on explicit request**, always echoing back what it did from the real cart response, never composed by the model
- [x] Post-conversation insight log — no new code needed. Every topic (order/product/general) now genuinely attempts grounding, so `ChatbotUnansweredQuestions` already broadens automatically: a product search with zero matches naturally can't ground an answer and logs the same way a support question does. "Same table, broader capture" turned out to require zero extra logging logic.

### Implementation notes (what actually shipped)

- Everything landed inside the existing `ChatbotService.cs` (`Features/Support/`) — no second bot, exactly per the design decision. `Classification` gained one field, `WantsToAddToCart`, from the same single classify call (still one JSON round-trip, not two).
- **Cart mutation is deliberately the one path that skips the AI compose step entirely.** When `WantsToAddToCart` is true, the bot resolves a product (named in the message, or falls back to `ChatbotConversationState.LastMentionedProductId` — new column, migration 267 — the product last surfaced in this conversation) and calls `ICartService.AddItemAsync` directly. The confirmation text is built from the *real* `CartDto` response in code, never from the model — a hallucinated "I added it!" that didn't actually happen was the one failure mode worth designing out entirely rather than trusting the grounding discipline to catch.
- **Never guesses which product.** If the message-based search returns multiple plausible matches and there's no prior context to disambiguate, the bot asks which one rather than picking — same "read is safe, write needs an explicit trigger" posture as the design decision states, applied literally: an ambiguous write doesn't happen.
- **Price ceiling ("under ₹500") is a real, code-enforced `ProductQuery.MaxPrice` filter**, not left to the model to eyeball from a dumped list of search results — matches the "never fabricate" discipline extended to filtering, not just facts.
- **Comparison** searches each named side separately (two `BrowseAsync` calls) rather than one fuzzy combined search, so the compose step describes real differences between two actual products instead of guessing which two a single search happened to return.
- 7 new tests covering: single-clear-match add succeeds with zero compose calls, multiple-match asks instead of guessing, no-match-no-context asks for the name, falls back to the last-discussed product across turns, a cart-service failure (e.g. out of stock) is reported without crashing, the price ceiling is actually passed to the catalog query, and comparison searches both sides and labels them in context. Full suite: 364/364 passing.
- `ChatbotReplyDto.CartUpdated` (bool, set true only on a real successful add) — the storefront livechat widget uses this to call the existing Angular `CartService.reload()` so the header cart badge reflects a bot-driven add immediately, without the frontend having to sniff reply text to guess whether something changed.

### Design decisions
- **This is Phase 2's bot gaining a second capability set, not a second bot.** The design doc says this directly, and the orchestration layer built in Phase 2 (classify intent → fetch real data → compose grounded answer) already generalizes to "find matching products" as just another intent alongside "look up my order" — no new architecture, new intents and new data-fetch targets on the existing pattern.
- **Cart mutation is the one genuinely new class of action** this bot takes — everything in Phase 2 was read-only (answering questions, logging a return *request*). Guardrail: the bot may add items to cart on explicit request; it never proceeds to checkout, never applies a discount, never removes items without being asked. Same "read is safe, write needs an explicit trigger and a clear echo" posture, just applied to commerce instead of support.
- **Works from day one, unlike Personalization** — needs only catalog data (already exists for every store from product #1), not accumulated order/browsing history. No cold-start problem here, so no reason to sequence it after Track B.

---

## Track B — Commerce Data Layer + Personalization

### Scope & checklist
- [ ] Server-side event capture: product view, search, add-to-cart, remove-from-cart — a new `CustomerEvent`-shaped table (TenantId, CustomerId nullable, SessionId for anonymous visitors, EventType, ProductId nullable, Metadata JSON, CreatedAt)
- [ ] Anonymous-visitor session identifier (cookie-based, not auth-dependent) — same category of concern as the existing store-unlock cookie, new cookie for this
- [ ] Async/batched writes, not a synchronous INSERT on every page view — see design decision below
- [ ] "Trending Now" strategy (real view/purchase-velocity computation over recent event data) — genuinely new, since today's "Best Sellers" is a merchant-curated Manual collection, not computed
- [ ] "Personalized Picks" strategy, gated behind a minimum per-store event-volume threshold
- [ ] Merchant controls: pin a product to always appear in a placement, exclude a product from recommendations entirely — new fields/UI, small addition on top of the existing catalog admin
- [ ] Verify (don't assume) that existing FBT/RelatedProducts already correctly exclude out-of-stock items, per the design doc's guardrail — carry forward if already true, fix if not

### Explicitly out of scope for this phase

- **Rebuilding Frequently Bought Together or Related Products.** Both are already real, order-history-driven, working well as the cold-start fallback the design doc asks for (§ "Cold-start handling"). This phase adds *new* strategies alongside them, it doesn't touch what's already correct.
- **Real plan-tier gating of Personalization.** The `Plan` entity already has fields for this shape (`MarketingEngineLevel`/`LiveChatLevel`/`HelpdeskLevel`) but they're currently **decorative labels, not enforced in code** (confirmed in the Super Admin audit). Actually wiring plan-tier feature gates is a real, separate piece of work that several v4 features will eventually need (Personalization here, Dynamic Pricing in Phase 5, Performance Marketing in Phase 4) — worth building **once**, generically, rather than bolting a one-off check onto this phase. Flagging it here since Personalization is the first feature that would want it; recommend scoping "real plan-tier feature gating" as its own small cross-cutting task, done once whichever phase first genuinely needs to enforce it rather than ship it decorative-only again.

### Design decisions

**Event writes must not sit in the storefront's hot path.** A synchronous `INSERT` on every product-page view adds latency and write load to the same database serving live checkout traffic, on a platform that already had a real incident from an unrelated write-heavy pattern this session (the AI catalog reseed bug). Correct pattern: buffer events (in-memory queue or a lightweight staging table) and flush in batches via **Phase 0's Hangfire**, not a new bespoke background mechanism. This also means a page view being a few seconds "behind" for personalization purposes is completely fine — nothing here needs real-time consistency the way checkout does.

**Privacy/consent boundary — flagging, not deciding.** Behavioral event capture (what a visitor viewed/searched) is a different category from the marketing consent model built in Phase 1 — most frameworks treat "necessary for the service you're using" (recommendations, cart) differently from "marketing communications," but India's DPDP Act has real implications here that this roadmap hasn't resolved. Worth a specific legal/compliance check before this ships broadly, not something to silently assume is fine because Phase 1's consent model exists. Adding as an open question rather than picking an answer.

**Threshold-based auto-switch, not a hard cutover.** Below the per-store event-volume threshold, "Personalized Picks" simply doesn't appear as an option — the existing Best Sellers/Trending/FBT/RelatedProducts strategies keep serving every store exactly as they do today. No store ever sees a broken or empty personalization slot; it just doesn't offer the personalized tier until there's enough data to make it real.

---

## Implementation notes

- New feature folder, likely `Features/Commerce/Events/` or similar — event capture, the flush job, and the strategy-computation logic (Trending/Personalized Picks) are related but distinct concerns worth keeping separable
- Frontend: `RecentlyViewedService` today is `localStorage`-only — decide whether to keep it as a fast client-side cache for the "Recently Viewed" placement specifically (it's already good at that one job) while the new server-side capture handles everything Personalization actually needs, rather than replacing it wholesale
- Merchant pin/exclude controls likely live alongside existing product admin fields, not a separate new screen

## Verification

- Browse several products anonymously (no login) → events captured against a session id, not lost
- Log in partway through a session → prior anonymous events correctly attribute to the now-known customer (or are deliberately left anonymous — decide and verify whichever is chosen)
- Confirm event writes don't add measurable latency to storefront page loads (batched, not synchronous)
- A store below the volume threshold never shows "Personalized Picks" as an option; a store above it does
- Pin a product to the homepage placement → it appears regardless of what the strategy would otherwise rank; exclude a product → it never appears in any recommendation slot
- Out-of-stock products never appear in any recommendation placement, confirmed across all strategies including the new ones
- Shopping Assistant: ask it to find a product by description → relevant, real (in-stock, correctly priced) results; ask it to add one to cart → cart actually updates, bot confirms what it did; ask an order-status question in the same conversation → same bot, same conversation, correct handoff between capability sets

**Status:** Track A done (2026-08-21). **Track B shipped 2026-08-29** as AI Commerce C1–C4 (build log below).

---

## Track B build log (2026-08-29) — AI Commerce C1–C4

- **C1 — data layer.** `CustomerEvent` (migration 281), `POST /api/events` → in-memory buffer → per-minute Hangfire flush (batched, per-tenant `BeginScope`, off the hot path). Frontend `EventService` (first-party visitor id, batched, SSR-safe) captures view / add-to-cart / remove. Verified end-to-end on prod.
- **C2 — Trending Now.** `GetTrendingAsync`: weighted views + add-to-cart (events) + purchases (orders) over a window, in-stock only. `GET /api/catalog/trending`, self-hiding rail.
- **C3 — Personalized Picks + Recently Viewed.** Category-affinity from the visitor's own events, gated at ≥2 interactions, cold-start → trending. `GET /api/catalog/personalized` + `/recently-viewed`. Rails on the cart page.
- **C4 — merchant pin/exclude + demand→pricing.** `Products.ExcludeFromRecommendations`/`PinnedInRecommendations` (migration 282, admin toggles, applied across strategies) and the Dynamic-Pricing demand signal wired to real 7-day event velocity (was hardcoded 0) — closes the Phase-5 gap.

**Placement note:** storefront home/PLP/PDP are theme-template-driven, so new placements there need a theme section type; the cart page (plain component) hosts the rails today. **Privacy:** first-party, purpose-limited, `BehaviorTrackingEnabled` per-store toggle (default on); the DPDP consent review is still the open item before broad rollout.
