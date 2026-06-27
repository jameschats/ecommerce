# Stage 7 — Differentiators (V1.1, post-launch)

**Goal:** admin-configurable storefront + deferred items. Schema-ready; built after the launch-critical path.

## Scope & checklist
- [ ] **Theme Engine** — admin controls primary/secondary color, logo, font, button style (`Themes`/`ThemeSettings`)
- [ ] **CMS Home-Page Builder** — compose homepage from sections (Banner, Featured, Categories, Offers, New Arrivals, Best Sellers, Custom HTML); reorder / hide / schedule (`Pages`/`PageSections`/`SectionConfigurations`)
- [ ] **COD operational workflow** — confirm-without-prepay, RTO handling, remittance reconciliation
- [ ] **Wishlist / Save-for-Later**

## Tables (exist)
`Themes, ThemeSettings, Pages, PageSections, SectionConfigurations` (+ COD modeled as a payment method; wishlist table TBD)

## Notes
- Deferred deliberately: expensive, not launch-critical for a single-seller store (a hardcoded homepage + Settings covers V1).
- COD: business override may pull it earlier if customers require it day one.

**Status:** ⬜ Deferred to V1.1.
