# Competitor notes — Zoho Commerce

**Goal:** capture what Zoho Commerce's post-signup "Getting Started" page does well, and decide what our merchant admin should borrow.

Observed 2026-07-22 on a fresh trial store (`MDFashion`), first screen after onboarding.

---

## What they show

**Shell.** Left nav is seven collapsible groups — Home · Items · Sales · Online Store · Marketing · Reports · Integrations — with **Store Actions** pinned to the bottom. Top bar carries a scope-switchable search ("Search in Customers"), a **store switcher**, and a persistent **"Trial expires in 14 days. Subscribe"** line. A **"Get expert advice — at no extra cost"** card sits in the sidebar, and a **"Need help setting up your store?"** chat widget is docked bottom-right on every screen.

**Home has two tabs: `Dashboard` | `Getting Started`.** Getting Started is a permanent tab, not a wizard that disappears.

**Block 1 — "Complete these few steps to publish your store"**, with a progress bar and `1/5`. Five rows, each: status check · label · optional state line · right-aligned action link.

| Step | Action link |
|---|---|
| Make your online store stand out with the right theme | Configure Theme ✅ |
| Set your own domain for your online store | Add Domain |
| Add all the items that you'll be selling | Add Items |
| Set up shipping zones to deliver your items efficiently | Add Shipping Zone |
| Connect payment gateways to start accepting online payments | Configure Online Payments |

Note the domain row: it displays `Added Domain: mdfashion-932461856.zohoecommerce.com ↗` **while still counting as incomplete.** The auto-assigned subdomain works, but the step stays open to nudge toward a custom domain — and the merchant can click through to see the live value.

**Block 2 — "Try placing a test order yourself."** A highlighted card: *"This step lets you experience how the order process works from start to finish"* → **See How It Works**.

**Block 3 — "Here are some ways to improve your online store."** A 2-column discovery grid, each with icon, one-line benefit and a chevron: Set Up Taxes · Create Coupons · Manage Collections · Shipping Integration · Enable Digital Downloads.

**Block 4 — Help.** "Watch a quick overview video" (*What's new in Zoho Commerce 2.0*) plus Help Center · Academy · Forum.

---

## What we already have (don't rebuild)

`Features/Dashboard/DashboardService.cs` **already implements this pattern**, and the shape is nearly identical to Zoho's:

```
ChecklistItem(Key, Label, Description, Done, ActionLabel, ActionLink)
DashboardDto(Summary, Checklist, ChecklistDone, ChecklistTotal)
```

Computed from real store state (`DashboardService.cs:32-47`), not stored flags — same approach. Our five: **product · design · details · tax · pages**.

`Features/Onboarding/` is **signup only** — slug suggestion, availability, account creation. It has no post-signup guidance, which is fine; the checklist is the right home for that.

---

## Review of the ideas

| Idea | Verdict | Notes |
|---|---|---|
| **Re-aim the checklist at first revenue** | ✅ **Do first — this is the real finding** | See below. Costs almost nothing. |
| Show resulting value on completed rows | ✅ Cheap and good | "Added Domain: …↗", product count, gateway name. `ChecklistItem` needs one nullable field. |
| Keep a step open despite a working default | ✅ Adopt | Exactly our `slug.basedomain` vs `CustomDomain` case. |
| **"Try placing a test order"** | ✅ **Strongest idea to steal** | We already ship a **Mock payment gateway** — a guided test order is genuinely easy for us. |
| Secondary "ways to improve" discovery grid | ✅ Adopt | We have ~45 admin routes; discovery is almost certainly our weak point. |
| Persistent Getting Started tab | ✅ Adopt | Ours lives on the dashboard; a tab that survives completion is better. |
| Trial countdown + Subscribe in top bar | ✅ Adopt | `Tenant.TrialEndsAt` exists. Quiet, always-visible beats a modal. |
| "Need help setting up your store?" widget | ✅ Already planned | This is exactly [ai-support](../ai-support/README.md) **A2** — merchant↔platform live chat. Zoho validates the placement. |
| "Get expert advice at no extra cost" | 🟡 Later | Their services lead-gen. Only meaningful once you have onboarding capacity to sell. |
| Left-nav consolidation into ~7 groups | 🟡 Worth reviewing | Ours is flatter and longer. Real work, no urgency. |
| Academy / Forum / app marketplace | ⛔ Skip | No community or ecosystem to put behind them. An empty forum is worse than none. |

---

## The finding that matters

**Our checklist never mentions payments, shipping or domain.**

| | Zoho | Ours (before) |
|---|---|---|
| Products | ✅ | ✅ |
| Theme / design | ✅ | ✅ |
| **Payments** | ✅ | ❌ missing |
| **Shipping** | ✅ | ❌ missing |
| **Domain** | ✅ | ❌ missing |
| Tax | secondary | ✅ |
| Content pages | secondary | ✅ |

Zoho's list is aimed at *"publish and take money."* Ours was aimed at *content completeness* — it asked a merchant to write an About page before it asked them to connect a payment gateway.

> **Correction (made while implementing).** An earlier draft of this note claimed a merchant could finish the
> checklist "5/5 and still be unable to accept an order." **That was wrong.** `OnboardingService.SignupAsync`
> deliberately seeds a flat-rate *Standard Delivery* method **and** `CodEnabled=true` (lines 128–136), with the
> comment *"so the store can check out on day one."* A fresh store **can** transact — COD, flat-rate shipping,
> default subdomain. The real gap is narrower but still worth fixing: nothing ever prompts the merchant to accept
> **online** payments, review the generic shipping rate, or connect their own domain.

That seeding also invalidates the obvious implementation. "Has a shipping method" and "can take money" are both
**true from the moment of signup**, so naïve checks would have rendered permanently green and taught merchants to
ignore the list. The honest signals are narrower: `TenantPaymentAccount.IsEnabled` (a gateway actually switched
live, COD explicitly not counting), and `ShippingMethod.UpdatedAt != null || any ShippingZone` (the seed edited or
zones added).

All three features already exist — `TenantPaymentAccount` (m161), `TenantShippingAccount` + zones (m170),
`Features/Domains` + `Tenant.CustomDomain` (m168). They were simply never surfaced where a new merchant looks.

**Shipped revision** — six rows, ordered by what blocks revenue:

1. Add your first product → `/admin/products/new` *(shows the count)*
2. **Accept online payments** → `/admin/payments` *(shows "Razorpay connected" or "Cash on Delivery only")*
3. **Set your shipping rates** → `/admin/shipping` *(shows "Using the default flat rate" until edited)*
4. Design your storefront → `/admin/pages`
5. Add your store details, incl. GST → `/admin/store-settings`
6. **Add your own domain** → `/admin/domain` *(stays open on the auto-subdomain, shows the live URL)*

Tax folded into store details; content pages moved to the discovery grid (M10c) where Zoho puts them.

---

## The test-order idea

Worth calling out separately because it's the one thing here that would meaningfully change merchant confidence, and it's cheap for us specifically.

A merchant's real fear before going live is *"will this actually work when a stranger buys something?"* Zoho answers it by walking them through placing an order themselves. We're well positioned: `Payments:Provider=Mock` already exists, and the whole flow — cart → checkout → order → invoice PDF → notification email → admin order screen — is built and tested.

A guided "place a test order" that runs against the mock gateway, then drops the merchant into the resulting admin order detail, would demonstrate the entire post-purchase machine in about ninety seconds. It also surfaces misconfiguration (no shipping zone for their pincode, missing store details on the invoice) *before* a real customer hits it.

Open question worth deciding: whether test orders are flagged and excluded from analytics, or simply deleted afterwards. Flagged-and-excluded is safer — a deleted order leaves confusing gaps in invoice numbering.

---

## Where this landed

Folded into the merchant-admin plan as **[M10 — Activation & first revenue](../v2-stages/v2-merchant-admin-plan.md)**
(AREA 9), sub-phased cheapest-first:

| | | |
|---|---|---|
| **M10a** | Re-aim the setup checklist + current-value line | ✅ **done** — `DashboardService.cs`, no migration |
| **M10b** | Guided test order (reuses draft-orders, COD) | ✅ **done** — migration `179` |
| **M10c** | Feature discovery grid | ✅ **done** |
| **M10d** | Trial countdown chrome | ✅ **done** |
| **M10e** | `Dashboard | Getting started` tab split | ✅ **done** |

The chat widget is **not** part of M10 — it's merchant↔platform live chat, already specced as
[ai-support](../ai-support/README.md) A2/C1b.

---

**Status:** 📝 Notes captured 2026-07-22 → planned as M10 → **M10a–e built the same day** (migration `179`;
173 tests green). Not yet driven through the running admin UI — see the M10 note in the merchant-admin plan.
