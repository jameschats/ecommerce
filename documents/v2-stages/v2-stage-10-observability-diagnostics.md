# V2-10 — Observability & Diagnostics (Super-Admin)

**Goal:** let the super-admin diagnose and **fix** a merchant's issue fast — pull that tenant's logs, transactions, and audit trail into one pane, and remediate in a click. A support ticket (V2-9) is only as good as the diagnosis behind it.

## Core principle: every issue is traceable to `(TenantId, CorrelationId, time)`
- [ ] **`TenantId` log enricher** on every Serilog line (from design-v2 §5 / V2-7).
- [ ] **`CorrelationId` middleware** — assign one per request; stamp it on every log line **and** the `ApiResponse` envelope, so an error the merchant sees shows *"Reference: `req_…`"* → they paste it into a ticket → super-admin pulls the whole request trace. (Pull this forward to V2-0 — cheap, and retrofitting is painful.)

## The four pillars
- [ ] **1. Searchable structured logs** — Serilog → **Seq** (self-hosted on the VPS, one Docker container + Nginx subdomain, like Umami). Query by `TenantId`, level, time, correlation id, endpoint. *(Alternatives: Grafana Loki / OpenSearch.)* Super-admin deep-links to a pre-filtered Seq query per tenant/ticket, or embeds a read-only viewer proxying Seq scoped to one `TenantId`.
- [ ] **2. Transaction inspector** — per-tenant timeline: orders → payment attempts (+ **raw gateway response**) → refunds → invoices → webhook deliveries (V2-8) → notifications sent (V2-11) → import/background jobs. Built from existing tables (`orders`, `payments`, `paymenttransactions`, `refunds`, `invoices`, `webhookdeliveries`, `notificationhistory`, `importjobs`).
- [ ] **3. Audit trail** — per-tenant `AuditLogs` (who did what, when) + **`PlatformAccessLog`** (every super-admin view/action on tenant data — see Security).
- [ ] **4. Health & error dashboards** — per-tenant error rate, failed payments, failed webhooks, job failures, low-stock — *proactive*, not just reactive.

## The payoff: Tenant Diagnostics pane (one screen)
Open a tenant (or click **Diagnose** from a ticket) → recent errors (with correlation ids) · transaction timeline · subscription/billing + plan-limit usage · Hangfire jobs (failures/retries) · config snapshot · recent audit entries.

## Remediation toolkit (quick actions — each audit-logged)
- [ ] Retry a failed **webhook** / replay a stuck **background job**
- [ ] Resend an **invoice** or **notification** (email/SMS/WhatsApp)
- [ ] **Re-verify** a payment against the gateway; **refund** / partial refund
- [ ] **Clear this tenant's cache** (Redis)
- [ ] **Toggle a feature flag** / **extend trial** / **grant credit**
- [ ] **Impersonate** (V2-3) to reproduce what the merchant sees

## Security (this access is sensitive)
- [ ] Role-gated to platform admins; **every** tenant-data view/action written to `PlatformAccessLog` (who, which tenant, when, ideally linked to a ticket id).
- [ ] **Scrub secrets** (payment keys, tokens, passwords) before logs reach Seq.
- [ ] Treat like production DB access — reviewable, minimal, justified.

## Data model
Migrations `200–209`: `PlatformAccessLog`. `CorrelationId` is a log/envelope field (no table). Everything else **reads** existing V1/V2 tables. Seq stores logs outside MySQL.

## Endpoints
`GET /api/superadmin/tenants/{id}/diagnostics` (aggregate pane), `GET .../{id}/logs?level=&from=&to=&correlationId=` (Seq proxy), `GET .../{id}/transactions`, plus the remediation actions (`POST .../{id}/retry-webhook/{deliveryId}`, `.../resend-notification`, `.../clear-cache`, …).

## Gate
Given a `CorrelationId` from a merchant error, the super-admin pulls the full request trace from Seq; the Tenant Diagnostics pane shows recent errors + the transaction timeline with raw gateway responses; a failed webhook is retried and a stuck job replayed from the pane; every such action appears in `PlatformAccessLog`; secrets never appear in Seq.

## Dependencies
V2-3 (super admin), V2-7 (`TenantId` log enricher — or pulled forward), V2-8 (webhook delivery log), V2-11 (notification history/resend). Pairs with V2-9 (support). Seq deployment.

**Status:** ⬜ Not started. **Suggest pulling `CorrelationId` + `TenantId` enricher into V2-0.**
