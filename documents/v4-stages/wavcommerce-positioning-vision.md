# WavCommerce positioning & vision (strategic note)

Captured 2026-08-29. A founder-level positioning decision that frames how the platform is pitched and where product investment should concentrate. Not a build spec — it's the "north star" the Marketing Studio and AI onboarding should serve.

## The repositioning
**Don't** position WavCommerce as *"Create your online store."* — Shopify, WooCommerce, Dukaan, Instamojo already own that framing and it's a commodity.

**Do** position it as *"Run your entire business online."* — a full business operating system, not just a storefront builder.

## The mental model
```
                  WAVCOMMERCE
                       │
      ┌────────────────┼─────────────────┐
      ↓                ↓                 ↓
    STORE           MARKETING          SALES
      │                │                 │
 Products            AI Ads            WhatsApp
 Inventory           Reels             Customers
 Orders              Posts             CRM
 Pricing             Campaigns         Loyalty
      │                │                 │
      └────────────────┼─────────────────┘
                       ↓
                   OPERATIONS
                       │
              Payments / Shipping
                       │
                       ↓
                    ANALYTICS
```
Five pillars: **Store · Marketing · Sales (WhatsApp/CRM/Loyalty) · Operations (Payments/Shipping) · Analytics.** The storefront is one pillar, not the whole product.

## The India-first AI hook (the differentiator)
> "Tell WavCommerce what you want to sell. AI builds your store, product descriptions, Instagram posts, WhatsApp campaigns and promotional creatives."

One prompt → store + catalog copy + social posts + WhatsApp campaigns + creatives. This is where the **AI Marketing Engine / Marketing Studio becomes strategically central** — it's not a side feature, it's the wedge that makes "run your entire business online" real and distinctively Indian.

## Implications for the roadmap (why this matters for what we build)
- **Marketing Studio ([marketing-studio-plan.md](marketing-studio-plan.md)) is a flagship pillar, not an add-on** — prioritise it. Reinforces the user's earlier "focus on marketing solidly."
- **Sales pillar** = WhatsApp commerce + CRM + loyalty — several pieces already exist or are planned (WhatsApp provider code; loyalty/referral are gaps in [marketing-engine-v2-plan.md](marketing-engine-v2-plan.md)). This note elevates them from "nice to have" to a named pillar.
- **AI onboarding** ("tell it what you sell → it builds everything") ties together the existing AI store setup + sample catalog + themes + the Marketing Studio into one first-run experience — a future cross-cutting flow worth designing once the Studio exists.
- Keep the **modular / extractable** discipline (Marketing Studio §3.10 seam) — a "business OS" of pillars is exactly the shape that benefits from clean module boundaries.

## Status
Positioning note only — no code change implied yet. Current active build: **AI Marketing Studio** (continuing). This note raises its strategic priority and adds "Sales pillar (WhatsApp/CRM/loyalty)" + "one-prompt AI onboarding" as future named workstreams.
