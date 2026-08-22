Logged as items 6–11 in the master draft's pending list:

WavCommerce APIs — timing decision (now vs. later)
App Marketplace/Store — start with 1-2 first-party apps to validate the base
Migration/Onboarding from competitors (Shopify, Zoho Commerce, Instamojo, Dukaan, etc.)
Theme Store — as its own designed surface, not just a nav item
Analytics — real scope still undefined (metrics, dashboards, GA4 integration or native)
Reports — distinct from Analytics, scheduled/exportable documents

A couple of natural dependencies worth noticing as you sequence these: Theme Store and App Marketplace both lean on the APIs item being scoped first (apps/themes need something stable to plug into), and Reports overlaps with the existing Import/Export feature under Products, so those two might end up sharing infrastructure.

No design work done yet on any of these — just captured so nothing gets lost. Let me know whenever you want to tackle one.

---

Added as item 12: Infrastructure/Hosting Architecture — start on Hostinger KVM4 (or similar VPS), migrate to AWS/Azure once scale justifies it.

One thing I flagged inside it worth your attention now, even before the full design happens: the most important decision isn't when to migrate — it's making sure the app is built from day one so that migration is a lift-and-shift, not a rewrite. That mainly means avoiding VPS-specific dependencies and keeping things like storage, database, and background jobs portable early on, even while running cheap. Worth keeping in mind as actual development starts, even before this item gets its own detailed design.

That's 12 pending items now logged: Support model (resolved), Credits system, Vertical-specific features, Super Admin gaps, Analytics ownership, WavCommerce APIs, App Marketplace, Migration/Onboarding from competitors, Theme Store, Analytics scope, Reports, and Infrastructure/Hosting.

---

Added as item 13: Docs site (docs.wavcommerce.online) — for developers (API/webhook reference) and merchants (setup guides, feature how-tos, FAQ/troubleshooting) alike, but the two halves don't share a timeline. Developer docs depend on item 6 (WavCommerce APIs) actually being scoped and built first — writing reference docs against an API that doesn't exist yet just means rewriting them later. Merchant help docs have no such dependency — real merchants are already running real stores today, so this half could be scoped and started independently, whenever it's a priority, likely as a small standalone static site rather than a new platform feature.

That's 13 pending items now logged.

---

Added 2026-08-22 — pending items surfaced while building v4 Phases 1–4 (not part of the original 13, but real open work worth tracking here rather than losing):

**Blocked on external accounts (not code):**
- WhatsApp BSP business account — Gupshup's self-serve signup hit real operational problems (expired sales code, then the whole registration flow wouldn't accept new signups); an email was drafted asking their sales team to route to the Partner/Tech-Provider onboarding track instead of self-serve Starter/Pro. Interakt was built as a parallel fallback provider in the meantime. **Both `GupshupWhatsAppProvider` and `InteraktWhatsAppProvider` are built, tested against fakes, and unverified against a real account** — nothing sends a real WhatsApp message yet.
- Phase 4 Track B (Meta/Pinterest OAuth for social posting) and Track C (Meta Ads/Google Ads OAuth, WhatsApp Commerce) — not started, all need real developer/business accounts with those platforms before any of the connector code can be built or tested.

**Blocked on a design/product decision, not code:**
- WhatsApp channel for Phase 2's chatbot — genuinely unresolved: WhatsApp has no "logged-in session" the way the web widget does, so mapping an inbound phone number to a customer identity, and how a WhatsApp conversation maps onto the existing ticket-per-thread model, needs a real decision before building, not an assumption.
- Phase 3 Track B (server-side event capture / Personalization / Trending) — the plan doc itself flags an unresolved India DPDP Act privacy/consent question that needs a real legal/compliance check before this ships broadly. Not started, deliberately, pending that.
- WhatsApp Commerce conversational checkout (Phase 4 Track C) — flagged in the plan as the single highest-risk item in that whole phase (real UPI payment collection inside a chat thread); explicitly meant to be built last, after catalog sync/notifications/AI-replies are proven live, itself blocked on the WhatsApp account above.

**Genuinely just not started yet (no blocker, just not reached):**
- Phase 4 Track A remainder: bulk keyword research & intent mapping (new AI feature), site health monitoring (broken links/duplicate content/thin pages), content briefs/blog assist.
- Real plan-tier feature gating — flagged in Phase 3 as a small cross-cutting piece several v4 features will eventually want (Personalization, Dynamic Pricing in Phase 5, Performance Marketing in Phase 4); the `Plan` entity already has the fields for it but they're decorative, not enforced anywhere. Worth building once, generically, whenever the first feature genuinely needs it enforced rather than shipping another decorative-only gate.
- Sitemap controller's base-URL resolution (`SitemapController.cs`, `Cors:AngularOrigin` config) reports the same single hardcoded URL for every tenant rather than resolving per-tenant subdomain the way the new `robots.txt` route does — a real but small pre-existing gap, flagged not fixed while building robots.txt.
- Individual `Review` schema markup and GTIN/MPN product identifiers — lower priority than they first looked (AggregateRating alone is sufficient for Google rich-result eligibility; no GTIN field exists on `Product` at all and most Indian D2C sellers don't have registered GTINs), not built without a concrete ask.

**No frontend yet for (backend real and tested, UI not built):**
- Any of the WhatsApp-dependent features above, by definition.