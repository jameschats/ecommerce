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