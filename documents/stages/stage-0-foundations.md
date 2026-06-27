# Stage 0 — Foundations

**Goal:** the full V3 database schema + the cross-cutting services every later module depends on.

## Scope & checklist
- [x] Full database schema as sequential SQL (`database/migrations/001`–`015`) — 61 tables, 72 FKs
- [x] Seed data: default tenant, RBAC, admin user, GST rates, shipping method, settings, theme, home page
- [x] EF Core wired (Pomelo MySQL), hand-authored entities, `EcommerceDbContext`
- [x] `ApiResponse<T>` / `PagedResult<T>` envelopes + global exception middleware
- [x] **Serilog** logging (console + rolling file)
- [ ] **Settings Engine** module (read/write `Settings` table) — *table exists, code pending*
- [ ] **Audit Logs** module (interceptor writing `AuditLogs`) — *table exists, code pending*
- [ ] **Media Library** module (upload/list `MediaFiles`/`MediaFolders`) — *table exists, code pending*

## Artifacts
- `database/migrations/*.sql` (+ `README.md`)
- `ecomm.api/Data/`, `ecomm.api/Common/`, `Program.cs`

## Notes
- SQL is the source of truth; entities are hand-mapped (MySQL lowercases table names, so auto-scaffold mis-cases multi-word classes).
- Settings/Audit/Media code is foundational and will be built just-in-time as the first feature that needs each arrives.

**Status:** 🟡 Schema + infra complete; Settings/Audit/Media code pending.
