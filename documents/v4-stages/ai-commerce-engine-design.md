# AI Commerce Engine — Complete Design

**Scope:** Everything that helps a customer *convert* once they're already on the store — personalization, product discovery, and pricing intelligence.
**Not in scope:** Personalized/surveillance pricing (different prices to different individuals) — explicitly excluded on legal/ethical grounds. Everything here uses **market-based** signals only (demand, stock, season, competitor price) — never individual shopper data to set price.
**Relationship to Marketing Engine:** Marketing gets people to the store. Commerce Engine converts them once they're there. Different data, different job, but they share the same underlying product/customer data and the same Brand Voice for any customer-facing copy.

---

## 1. Design Principles

1. **Three independent switches, not one bundle.** Personalization, Shopping Assistant, and Dynamic Pricing are separate modules a merchant can enable individually — a small merchant may want only the Shopping Assistant; a high-volume merchant may run all three.
2. **Cold-start fallback everywhere.** Every AI-driven feature here needs history (orders, browsing, clicks) to work well. New stores with zero data get sensible non-AI defaults (Best Sellers, Trending, manual merchandising) until enough data accumulates.
3. **Market-based only, never individual-based, for pricing.** Prices react to conditions (stock, demand, season, competitors) — never to who is looking at the screen. No exceptions in this design.
4. **Assist, don't replace, merchant judgment.** Recommendations, chat answers, and price suggestions are AI-generated, but merchants retain override controls (pin/exclude products, set price floors/ceilings, edit assistant's knowledge base).
5. **Shared data layer.** All three modules read from the same underlying customer/product/order data — build one data foundation, not three siloed ones.

---

## 2. Module Map

```
AI Commerce Engine
├── 3.1 Personalization & Recommendations
├── 3.2 AI Shopping Assistant
└── 3.3 Dynamic Pricing (market-based)
```

---

## 3.1 Personalization & Recommendations

**What it does:** Rearranges what each visitor sees — homepage, product pages, search results, cart, email — based on their own behavior and the behavior of similar shoppers.

**Sub-features (by placement):**

| Placement | Recommendation type |
|---|---|
| Homepage | Personalized picks based on browsing/purchase history; falls back to Trending/Best Sellers for new/anonymous visitors |
| Product page | "Frequently bought together," "Customers also viewed," "Similar products" |
| Cart | "Complete the look" / cross-sell add-ons before checkout |
| Search results | Re-ranked by relevance to the individual shopper, not just keyword match |
| Post-purchase email | "You might also like" based on what was just bought |
| Abandoned cart | Reminder featuring the exact items left behind, sometimes with a nudge (low stock, price drop) |

**Recommendation strategies (merchant-configurable, can run several at once):**
- Best Sellers
- Trending Now
- Others Also Viewed / Bought
- Personalized Picks (requires shopper history)
- Recently Viewed
- Frequently Bought Together

**Cold-start handling:**
- New store (no order history) → defaults to manually-curated Best Sellers/New Arrivals until enough order data exists
- New/anonymous visitor (no browsing history) → defaults to Trending/Best Sellers store-wide, switches to personalized once the visitor has a few interactions in-session
- Threshold-based auto-switch: platform defines a minimum data threshold (e.g., X orders, Y product views) below which personalized strategies are hidden and only non-personalized fallbacks show

**Merchant controls:**
- Pin specific products to always appear in a placement (e.g., feature a new launch regardless of what AI would rank)
- Exclude products from recommendations (e.g., discontinued stock)
- Toggle which strategies run in which placement

**Data needed:** Browsing behavior, cart activity, purchase history, product catalog relationships (category, attributes) — this is why it should be built on top of the same Products/Orders data already in Merchant Admin, not a separate system.

**Guardrail:** Never surface out-of-stock products as recommendations. Never use recommendations to imply false scarcity/urgency (e.g., fabricated "only 2 left" — must reflect real inventory).

**Credit/cost model:** This is closer to infrastructure than a metered AI feature — likely priced as a plan-tier capability (Starter = basic non-personalized strategies only; Pro+ = full personalization) rather than per-use credits, since it runs continuously in the background rather than being triggered on demand.

---

## 3.2 AI Shopping Assistant

**What it does:** A conversational, on-site chat interface where a customer describes what they want in natural language, and the assistant recommends matching products directly — a sales tool, distinct from the support/Inbox system (though the two can share infrastructure).

**Sub-features:**
- Natural-language product search ("moisturizer for dry sensitive skin under ₹500") → returns matching products with brief reasoning
- Product comparison ("what's the difference between these two") when a shopper is deciding between options
- Availability/variant questions (size, color, stock) answered directly from live catalog data
- Cart actions — assistant can add a recommended product to cart on request, not just describe it
- Handoff to human support when the question is support-related, not sales-related (order status, complaints) — routes into existing Inbox rather than duplicating that system
- Post-conversation insight capture — common questions/objections logged for the merchant, useful signal for what product info pages are missing

**Guardrail (important — same principle as Marketing Engine):**
- Assistant answers only from actual catalog data (real stock, real specs, real price) — never invents product details, availability, or claims
- If it doesn't have grounded data to answer, it says so and offers to connect to human support, rather than guessing

**Cold-start handling:** Works reasonably well even for new stores, since it draws on catalog data (which exists from day one) rather than requiring order history — this is actually the easiest of the three modules for a new merchant to benefit from immediately.

**Credit/cost model:** Usage-based (cost scales with conversation volume) — likely metered similarly to Product Images in the Marketing Engine, since each conversation is a live AI call, not a background process.

---

## 3.3 Dynamic Pricing (Market-Based Only)

**What it does:** Adjusts a product's publicly-shown price automatically based on real market conditions — the same price shown to every shopper at a given moment, but that moment-to-moment price can change based on signals.

**Signals used (all market-based, none individual):**
- Inventory level (low stock → price may firm up or increase slightly; excess stock nearing season-end → price may soften to clear)
- Demand signals (views/purchase velocity trending up or down)
- Competitor pricing (if merchant opts into competitor price tracking for their category)
- Seasonality/time-based rules (e.g., festival season pricing windows, end-of-season clearance schedules)

**Explicitly excluded (per earlier discussion):**
- Any pricing based on individual shopper identity, location-based price discrimination, browsing/purchase history of the specific visitor, or perceived ability/willingness to pay
- Any pricing shown differently to two people looking at the same product at the same time

**Merchant controls (non-negotiable, not optional):**
- **Price floor and ceiling per product** — AI can never price outside merchant-set bounds
- **Manual override** — merchant can lock a price at any time, overriding AI
- **Approval mode vs. auto-apply mode** — merchant chooses whether price changes need manual approval (safer default, recommended for v1) or apply automatically within the set floor/ceiling (available once merchant trusts the system)
- **Change frequency limits** — cap how often a price can move (e.g., max once per day) to avoid shopper-visible price flickering, which damages trust

**Guardrail:** Full price-change history log, visible to the merchant, for every product — transparency and auditability given the legal sensitivity of pricing.

**Recommended default:** v1 ships in **approval mode only** (AI suggests, merchant approves) — auto-apply mode is a v2 feature once there's confidence in the model's suggestions and the merchant relationship.

**Cold-start handling:** Like Personalization, this needs order/demand history to work well. New stores default to static pricing until enough sales data exists to generate meaningful signals.

**Credit/cost model:** Plan-tier gated (likely a Pro+/higher-tier feature) rather than per-use credits, since it runs as a continuous background process like Personalization.

---

## 4. Shared Infrastructure Note

Personalization, Shopping Assistant, and Dynamic Pricing all draw on the **same underlying data**: product catalog, order history, browsing/session behavior. Rather than building three separate data pipelines, this should be one shared **commerce data layer** that all three modules read from — this also means the cold-start problem is solved once, not three times separately.

---

## 5. Cross-Cutting Guardrails Summary

- Never recommend or price out-of-stock items
- Never fabricate scarcity, urgency, or product claims
- Shopping Assistant answers only from real catalog data — no invented specs
- Dynamic Pricing: market-based signals only, hard floor/ceiling controls, full audit log, approval-mode default
- Merchant override available everywhere — AI assists, never fully replaces merchant control in v1

---

## 6. Build Sequence (Recommended)

1. **Shared commerce data layer** (foundation — catalog + order + session data pipeline)
2. **Personalization & Recommendations** — non-personalized fallbacks (Best Sellers/Trending) first, personalized strategies as data accumulates
3. **AI Shopping Assistant** — works from day one since it only needs catalog data, not order history
4. **Dynamic Pricing** — approval-mode only, market-based signals, hard floor/ceiling — last, since it carries the most risk and needs the most order-history data to be trustworthy

---

## 7. How This Connects to the Marketing Engine

| | Marketing Engine | Commerce Engine |
|---|---|---|
| Job | Get people to notice/visit the store | Convert visitors once they're there |
| Data needed | Product catalog, brand voice | Product catalog + browsing/order history |
| Output | Posts, ads, emails, SEO content | On-site recommendations, chat answers, prices |
| Shared foundation | Brand Voice (tone for any customer-facing text) | Same product catalog data |

Both engines plug into the same **Plans & Credits** system in Super Admin, but with different cost models: Marketing Engine is mostly generation-triggered (credits per generation), Commerce Engine is mostly plan-tier gated (continuous background capability) with the Shopping Assistant being the one usage-metered exception.

---

## 8. Open Questions to Resolve

1. Should Dynamic Pricing be offered at all in v1, given its legal sensitivity and data requirements — or deferred entirely to a later phase once the platform has real merchant trust and order-volume scale?
2. Does the Shopping Assistant share a knowledge base/infrastructure with the existing Inbox/Support system, or run as a fully separate module that hands off to Inbox when needed?
3. For competitor price tracking (a Dynamic Pricing signal) — is this something we build, or integrate a third-party pricing-intelligence data source?
