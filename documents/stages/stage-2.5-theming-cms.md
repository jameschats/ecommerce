# Stage 2.5 — Theming & CMS-lite (pulled forward from V1.1)

**Goal:** make the storefront admin-themable and let the admin arrange the home page — done now, right after the storefront, to avoid retrofitting hardcoded styling/sections. The **full** freeform CMS builder stays in Stage 7.

## Theme Engine (✅ done)
- [x] Backend: `GET /api/theme` (active, public) + admin `GET/PUT /api/admin/theme`
- [x] Frontend `ThemeService`: load theme → apply as **CSS variables** on `:root` (SSR-safe — verified in server HTML); Tailwind `primary`/`primary-dark` mapped to the variables
- [x] Refactored brand colors (buttons, header, hero, links) from hardcoded `blue-*` → themed `primary`
- [x] Admin **theme editor** (colors, font, button style, logo) with **live preview**
- [x] Verified: admin sets color → SSR-rendered storefront reflects it (no flash)

## CMS home-section manager — lite (✅ done)
- [x] Backend: `GET /api/cms/home` (visible, public) + admin `GET/PUT /api/admin/cms/home` (reorder / show-hide / retitle)
- [x] Storefront **home renders from `PageSections`** (ordered, visible) — fallback if API down
- [x] Admin **section manager** screen (up/down reorder, visible toggle, retitle)
- [x] Section types map to blocks: Banner, Categories, FeaturedProducts, NewArrivals, BestSellers
- [x] Verified: admin reorder/hide → SSR home reflects new order + hidden sections removed

## Deferred (stays Stage 7)
- Full freeform builder: custom-HTML sections, per-section config schemas, scheduling windows, dynamic add/remove of arbitrary sections.

## Tables (exist)
`Themes, ThemeSettings, Pages, PageSections, SectionConfigurations`

**Status:** ✅ Done — Theme Engine + CMS-lite home-section manager, both SSR-verified. Full freeform builder remains in Stage 7. Next: the commerce path (Cart → Checkout → Payments).
