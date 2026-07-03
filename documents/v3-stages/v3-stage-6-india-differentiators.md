# AI-6 — Indian Market Differentiators

**Goal:** the moat. Global AI-marketing tools (Jasper, Copy.ai, AdCreative.ai) don't understand Hinglish, Indian festivals, WhatsApp-first marketing, or GST-aware copy. This stage makes that gap decisive.

## Scope & checklist
- [ ] **Festival calendar** — 30+ Indian festivals (Diwali, Navratri, Eid, Pongal, Onam, Holi, Raksha Bandhan, …); **auto-suggest campaigns** ahead of each (ties to AI-4 Campaign Builder + AI-3 festival creatives + AI-5 festival videos).
- [ ] **Hinglish mode** — natural Hindi-English mixed copy, the way Indian SMBs actually market on WhatsApp/Instagram.
- [ ] **Regional languages** — Tamil, Telugu, Kannada, Marathi (beyond the AI-1 EN/Hindi/Tamil/Telugu baseline).
- [ ] **WhatsApp broadcast format** — copy shaped for WhatsApp broadcast lists (length, tone, CTA, opt-out) — the dominant Indian channel.
- [ ] **GST-aware pricing copy** — offers/descriptions that handle "inclusive of all taxes", GST breakups, and compliant discount framing (aligns with V1's GST/HSN + tax-mode engine).

## Supporting scope
- [ ] Festival-aware prompt templates (`AiPromptTemplates`, `Language` + festival variables).
- [ ] Proactive nudges: "Diwali is in 3 weeks — generate your campaign?" surfaced in the dashboard.

## Frontend
Festival calendar view with per-festival "generate campaign" CTAs; language/Hinglish selector across all generators; WhatsApp-broadcast preview.

## Gate
The calendar surfaces upcoming festivals and one-click-generates a themed multi-channel campaign; Hinglish + each regional language produce natural copy; WhatsApp-broadcast format reads correctly; GST-aware copy renders compliant pricing language.

## Dependencies
AI-1 (text), AI-4 (campaigns), optionally AI-3/AI-5 (festival creatives/videos). Builds on V1's GST tax engine for pricing copy.

**Status:** ⬜ Not started. **The India moat.**
