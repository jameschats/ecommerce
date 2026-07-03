# V2-9 — Merchant Support & Ticketing

**Goal:** give merchants a first-party, tracked channel to raise complaints/issues to the platform, and give the super-admin a queue to triage and resolve them — the system of record for merchant↔platform support.

## Mode
**In-app ticketing** as the system of record, with **email notifications** on every update and a **WhatsApp line** for low-friction first contact (India). Not email-only (no tracking/SLA) and not an outsourced helpdesk (external dependency + cost) — revisit third-party tools only at high volume.

## Scope & checklist
- [ ] **Raise a ticket** (merchant-admin → *Help & Support*) — category (Billing, Payments, Technical, Feature request, Other), priority, subject, description, attachments (reuse `IMediaStorage`).
- [ ] **Super-admin queue** — all tenants' tickets (the one legitimate cross-tenant read; `IgnoreQueryFilters()`, allowed only in `Features/SuperAdmin` per V2-0), filter by status/priority/tenant/assignee; tenant name on each.
- [ ] **Threaded conversation** — merchant ↔ platform messages + **internal notes** (super-admin-only, `IsInternalNote`).
- [ ] **Lifecycle** — `Open → In Progress → Waiting on Merchant → Resolved → Closed` (+ `Reopened`); assign to a platform admin.
- [ ] **Priority + SLA** — Low/Normal/High/Urgent; **plan-tiered SLA** (Pro/Enterprise get faster first-response targets, ties to `Plans`); `SlaFirstResponseDueAt`; overdue flags in the queue.
- [ ] **Correlation-id link** — a ticket can carry the `CorrelationId` from the error the merchant saw (see V2-10), so the super-admin jumps straight to the diagnostic trace.
- [ ] **Super-admin-initiated tickets (proactive support)** — the platform can open a ticket *to* a merchant, not just merchant→platform. Triggered manually from the store directory (V2-3) or automatically from a diagnostics (V2-10) / health (V2-12) signal — "we noticed X, need a hand?".
- [ ] **CSAT** — optional satisfaction rating on close.
- [ ] **Notifications** — new ticket + each reply/status change ping the recipient via the unified dispatcher (V2-11): in-app bell + email, WhatsApp for urgent.

## Data model
Migrations `190–199`, all tenant-scoped (`TenantId bigint`, V2-0):
```
SupportTickets            TicketId (TKT-yyyy-#####), TenantId, RaisedByUserId, Category,
                          Priority, Subject, Status, AssignedToAdminId?, SatisfactionRating?,
                          SlaFirstResponseDueAt?, CorrelationId?, CreatedAt, LastMessageAt, ResolvedAt
SupportTicketMessages     MessageId, TicketId, TenantId, SenderUserId, SenderRole (Merchant|PlatformAdmin),
                          Body, IsInternalNote, CreatedAt
SupportTicketAttachments  AttachmentId, MessageId, MediaFileId/Url, FileName
```

## Endpoints
- **Merchant:** `POST /api/support/tickets`, `GET /api/support/tickets[/{id}]`, `POST .../{id}/messages`, `POST .../{id}/close`, `POST .../{id}/rate`
- **Super-admin:** `GET /api/superadmin/support/tickets` (all tenants; filters), `GET .../{id}`, `POST .../{id}/messages` (+ internal notes), `POST .../{id}/assign`, `POST .../{id}/status`

## Frontend
- **Merchant-admin** *Help & Support*: ticket list + create form + thread; replies surface in the notification bell.
- **Super-admin**: support **queue** (filters, SLA/overdue flags), ticket detail (thread + internal notes + assign + status + "Diagnose" jump to V2-10), dashboard widget (open / overdue / by category).

## Gate
A merchant raises a ticket → it appears in the super-admin queue with tenant context → a threaded reply notifies the merchant (bell + email) → status transitions work → SLA overdue flags correctly → merchant sees only their own tickets; super-admin sees all. A ticket opened from an error carries its `CorrelationId`.

## Dependencies
V2-0 (tenancy + the `IgnoreQueryFilters` policy), V2-2 (merchant admin), V2-3 (super admin). Notifications ride the V1 baseline now, upgraded by V2-11. Diagnostic jump depends on V2-10.

**Status:** ⬜ Not started.
