# Marketing Engine v2 — pending items & build plan

Backlog for the next round of AI-marketing improvements. Compiled 2026-08-29 from a code-vs-docs audit of `ecomm.api/Features/` against `ai-marketing-engine-design.md`, `phase-4-marketing-engine-gaps.md`, `ai-engines-shipped.md`. M1–M4 all shipped and map to real code; this doc is what's **left**.

## Already shipped (do not rebuild)
Growth generation (Brand Kit, 5 content types, Content Library, image gen, campaign builder) · **M1** campaign email sends + Hangfire scheduling + bulk-generate + export pack · **M2** festival + content calendar (~35 IN festivals) · **M3** blog + AI writer (storefront `/blog`, JSON-LD, sitemap) · **M4** SEO assistant (keyword ideas + briefs). Adjacent: product SEO "Improve with AI", coupons/discounts, **basic** abandoned-cart email (opt-in, single email @4h), newsletter subscribe + subscriber list, 5 static customer segments, WhatsApp provider code (unverified).

## Pending — grouped, with priority & blocker status

### A. Deliverability foundation (HIGHEST VALUE — build first)
Today sends go through a single platform SMTP; envelope stays the platform address, so SPF/DKIM don't cover the tenant. At any real volume this lands in spam. Everything else in marketing is worth less if mail doesn't arrive.
- [ ] Dedicated ESP integration (Amazon SES or SendGrid) behind the existing `IEmailSender`/`EmailSenderFactory` seam. *Buildable now.*
- [ ] Per-tenant sending-domain authentication (custom SPF/DKIM/DMARC) with a guided DNS-record setup flow (mirrors the custom-domain verification we already have). *Buildable now.*
- [ ] Bounce + complaint (spam-report) webhook handling → auto-suppression list. *Buildable now.*
- [ ] List-Unsubscribe header + one-click unsubscribe compliance; suppression honored on every send. *Buildable now.*

### B. Lifecycle automations / flows (HIGH VALUE)
Only a single fixed abandoned-cart email exists. This is the biggest functional gap vs. a Shopify-class suite.
- [ ] Automation engine: trigger → (delay/condition) → action, multi-step. Reuse existing `CustomerEvent` capture for triggers. *Buildable now.*
- [ ] Welcome series (on subscribe / first order). *Buildable now.*
- [ ] Abandoned-cart → configurable multi-step flow (timing, N emails) instead of the one hardcoded email. *Buildable now.*
- [ ] Win-back / lapsed-customer flow. *Buildable now.*
- [ ] Post-purchase / review-request / cross-sell flow. *Buildable now.*
- [ ] Browse-abandon + "back in stock" + "you might like" recommendation emails (reuse Commerce recs). *Buildable now.*
- [ ] AI-suggested send timing (design 2.7). *Buildable now.*

### C. Audiences / segmentation (HIGH VALUE)
Segments are 5 hardcoded buckets.
- [ ] Custom/dynamic segment builder — filters on spend, recency, location, product bought, order count; RFM buckets. *Buildable now.*
- [ ] Unified audience/subscriber management screen (customers + newsletter subs linked), with suppression/unsubscribe state visible. *Buildable now.*

### D. Analytics / attribution (HIGH VALUE, cheap)
Sends happen but ROI is invisible.
- [ ] UTM tagging auto-appended to campaign links. *Buildable now.*
- [ ] Open / click / bounce tracking on campaign emails (pairs with the ESP webhooks in A). *Buildable now.*
- [ ] Campaign-level revenue attribution + a marketing dashboard (sends → clicks → orders → revenue). Reuse `CustomerEvent`. *Buildable now.*
- [ ] A/B testing on subject line / content. *Buildable now.*

### E. Growth / retention primitives (MEDIUM-HIGH)
Genuinely absent in code (not just deferred elsewhere).
- [ ] Referral program (unique referral links/codes, reward on qualified order). *Buildable now.*
- [ ] Loyalty / points / store-credit. *Buildable now.*
- [ ] Automated & personalized coupon issuance (birthday, first-purchase, win-back) + unique one-time code generation. *Buildable now.*

### F. On-site capture (MEDIUM)
- [ ] Pop-ups / opt-in forms / spin-to-win (email capture on storefront), theme-templated like the AI Commerce placements. *Buildable now.*
- [ ] Web-push / browser push. *Buildable now.*

### G. Email content (MEDIUM)
- [ ] Visual email template editor / better HTML (today: AI plain-text → naive `<p>` wrapping in `GrowthCampaignSendService.ToHtml`). *Buildable now.*

### H. SEO polish (LOW-MEDIUM)
- [ ] Per-tenant sitemap base-URL fix (`SitemapController` uses one hardcoded host for all tenants — pre-existing bug). *Buildable now.*
- [ ] SEO site-health monitor (broken links, thin/duplicate content, crawl errors). *Buildable now.*
- [ ] Individual `Review` schema + GTIN/MPN identifiers (only AggregateRating today). *Buildable now, low.*

### I. Channels (BLOCKED on external accounts — sequence later)
- [ ] Social organic posting/scheduling — Meta (IG+FB), Pinterest. *Blocked: dev app + platform review.* Generation already done; only connectors missing.
- [ ] Paid ads — Meta Ads + Google Ads, product-feed sync, ROAS reporting. *Blocked: ad accounts + Marketing API apps.*
- [ ] WhatsApp broadcast/commerce — provider code exists, unverified. *Blocked: live BSP/WABA + approved templates.*
- [ ] SMS marketing (MSG91 sender exists for OTP) — *partially blocked: needs India DLT registration.*

## Recommended build order
1. **A. Deliverability** (SES/SendGrid + domain auth + bounce/unsubscribe) — unblocks the value of every send.
2. **D. Attribution** (UTM + open/click + campaign dashboard) — cheap, makes everything measurable; open/click piggybacks on A's webhooks.
3. **C. Segment builder** — automations and campaigns both need real audiences.
4. **B. Automation engine + welcome/abandoned/win-back** — the biggest revenue-per-effort feature.
5. **E. Referral / loyalty / auto-coupons**, then **F. pop-ups**, **G. email editor**, **H. SEO polish**.
6. **I. External channels** — as accounts/approvals land.

Related: [[ai-marketing-m1-m4-shipped]], [ai-marketing-engine-design.md], [ai-commerce-c1-c4-shipped] (event layer reused for triggers/attribution).
