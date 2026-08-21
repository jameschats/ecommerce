# v4 Phase 0 — Portability Foundation

**Goal:** the two cheap-now/expensive-later actions from the roadmap's infra audit — containerize the app and replace the ad-hoc background-timer pattern with a real scheduler — before any v4 feature work adds more state/jobs on top of the current shape. Nothing here changes user-facing behavior; this is entirely a "don't make the next 6 phases harder to migrate" investment.

**Depends on:** nothing. Can start immediately, independent of every other phase.
**Blocks:** nothing hard-blocks on this, but Phases 1–6 all add new recurring/background work (notification retries, social-post scheduling, price-sweep jobs, WhatsApp broadcasts) that should land on Hangfire from day one rather than adding more one-off `BackgroundService` timers.

---

## Scope & checklist

- [x] Dockerfile for `ecomm.api` (multi-stage: SDK build → ASP.NET runtime) — `ecomm.api/Dockerfile`
- [x] Dockerfile for `ecomm.web` SSR (multi-stage: `npm run build` → Node runtime serving the SSR bundle) — `ecomm.web/Dockerfile`
- [x] `docker-compose.yml` at repo root — API + SSR containers, existing bare-metal MySQL and Nginx **untouched**
- [x] Named/bind-mounted volumes declared for DataProtection key ring and uploaded media (`dp-keys`, `uploads`); logs switched to Console-only via `appsettings.Docker.json` (`ASPNETCORE_ENVIRONMENT=Docker`), the stdout-native pattern instead of a mounted rolling-file
- [x] Hangfire added (MySQL storage, same connection string/driver family as EF Core), dashboard at `/admin/jobs` gated to SuperAdmin only (`HangfireAuthorizationFilter`)
- [x] Old `SubscriptionLifecycleService` `BackgroundService` deleted; sweep now runs as a Hangfire recurring job (`ISubscriptionService.RunScheduledLifecycleSweepAsync`, registered on every startup so its cron/target stays in sync with code)
- [x] Built and smoke-tested **locally**: both images build clean, containers start correctly, API container reaches host MySQL via `host.docker.internal` (confirmed by a real MySQL auth response, not a connection failure — the one local blocker was a pre-existing local credential mismatch, unrelated to containerization)
- [x] Full test suite green (291/291) after the `SubscriptionService` constructor change this required
- [ ] **VPS cutover — not yet done, deliberately.** Building/testing locally was step 1 of the cutover plan below; steps 2–4 (parallel-run on the VPS, flip Nginx's upstream, retire the systemd services) are a separate, careful action against the live site — do this as its own explicit step, not bundled into the code changes above
- [ ] Deploy recipe (`documents/deployment.md` §10-WAV) updated once the container path is confirmed live

## Explicitly out of scope for this phase

- **Containerizing MySQL.** Real merchant data (bazaar, lakme) lives there today; migrating it into a container now is pure risk for zero benefit — actual cloud migration will move it to a managed service (RDS/Azure Database for MySQL) directly, not a self-managed containerized MySQL. Stays bare-metal.
- **Containerizing Nginx.** A cloud target's native ingress/load balancer replaces Nginx's job anyway (ALB/App Gateway); containerizing it now would be work that gets thrown away at actual migration time. Host Nginx keeps reverse-proxying to the containers' published ports exactly as it proxies to the systemd services today — zero Nginx config change beyond the upstream port staying the same.
- **Redis backplane / shared DataProtection store / secrets manager.** Correctly deferred until a second app instance actually exists (see roadmap §1.9) — building them now would be premature.
- **Splitting Hangfire into a separate worker process/container.** Runs in-process inside the API container for now; revisit if job volume ever justifies isolating it.

---

## Design decisions

**Volumes are the whole risk surface here.** Three things currently live on local disk and must not become ephemeral when the app becomes a container:
1. DataProtection key ring (`{ContentRoot}/dp-keys`, or `DataProtection:KeysPath`) — losing this on every deploy re-breaks every encrypted `TenantPaymentAccounts` secret, exactly the incident already logged in project memory. Bind-mount a host directory to the same path inside the container; never let it live inside the image layer.
2. Uploaded media (`LocalDiskStorage`, served at `/uploads`) — same treatment, bind-mount the existing uploads directory.
3. Logs (Serilog rolling file) — two options: (a) bind-mount `ecomm.api/logs/` like today, or (b) switch to stdout/stderr logging and let Docker/the future cloud platform collect it (the more container-native pattern). **Recommendation: switch to stdout for the containerized path** — rolling-file logs inside a container are the wrong pattern going forward, and this is the cheapest moment to make that change since nothing depends on the file path yet. Keep the file sink too during the parallel-run verification window, drop it once containers are the only path.

**Hangfire storage:** MySQL-backed (`Hangfire.MySql` or equivalent Pomelo-compatible provider), since MySQL is already the platform's DB and this avoids introducing a second storage dependency. Hangfire manages its own tables automatically on first run — this is normal Hangfire behavior and doesn't need a hand-authored numbered migration the way the rest of the schema does; just document that `Hangfire*` tables in the schema belong to the library, not the app.

**Dashboard access:** Hangfire's built-in dashboard is real operational value (job history, retries, failures) but must not be publicly reachable — gate it with `IDashboardAuthorizationFilter` checking the existing Admin/SuperAdmin role, mounted at an internal path (e.g. `/admin/jobs`), same auth posture as every other admin surface.

---

## Implementation notes

- API Dockerfile: standard ASP.NET multi-stage build; final stage copies published output, sets `ASPNETCORE_URLS`, keeps the app listening on the same internal port the systemd service uses today (`127.0.0.1:5090` per current Nginx config) so the host Nginx `proxy_pass` target doesn't need to change — only what's *behind* that port changes.
- SSR Dockerfile: same pattern for the Angular Universal Node server (currently port `4010`).
- `docker-compose.yml` env: reuse the exact same environment variables the systemd units already inject (`ConnectionStrings__Default`, `DataProtection__KeysPath`, `Sms__Provider`, `Email__Provider`, JWT settings, etc.) — this is a lift of existing config into compose's `env_file`, not new config design.
- Hangfire wiring: `services.AddHangfire(...).AddHangfireServer()` in `Program.cs`; migrate `SubscriptionLifecycleService`'s body (already a clean `ISubscriptionService.RunLifecycleSweepAsync` call) into `RecurringJob.AddOrUpdate("subscription-lifecycle-sweep", () => svc.RunLifecycleSweepAsync(...), Cron.Hourly)` (or whatever cadence `Billing:SweepIntervalHours` currently specifies) — remove the old `BackgroundService` registration once confirmed working.

## Deploy notes

New deploy sequence for `ecomm.api`/`ecomm.web` (replaces the relevant parts of the existing §10-WAV recipe — DB migrations still run exactly as today, unaffected by this phase):

```
git pull → docker compose build → docker compose up -d → health check (unchanged endpoint)
```

**Cutover plan (risk mitigation, since this changes deploy mechanics for a live site with real merchant traffic):**
1. Build and run the containers alongside the existing systemd services first, on different local ports, verify parity (health checks, a full manual smoke pass on bazaar) before touching the live Nginx upstream.
2. Cut Nginx's `proxy_pass` targets over to the container ports.
3. Stop (don't delete) the `wavcomm-api`/`wavcomm-ssr` systemd units — keep them installed-but-disabled for one deploy cycle as an instant rollback path (`systemctl disable --now docker-compose-stack && systemctl enable --now wavcomm-api wavcomm-ssr`), remove them only after the containerized path has run cleanly through at least one real deploy.
4. Update `documents/deployment.md`'s §10-WAV once the container path is the confirmed live one — the old recipe stays documented in git history, not deleted mid-transition.

## Verification

- `docker compose up -d` locally (or on the VPS) → both containers healthy, `/api/health` and `/api/health/ready` return 200
- Restart the containers → DataProtection-encrypted secrets (Razorpay key) still decrypt correctly (proves the volume mount works, not just that the app starts)
- Upload a product image, restart containers, confirm the file still resolves at its `/uploads` URL
- Hangfire dashboard reachable only as Admin, shows the lifecycle sweep job registered and its next scheduled run
- Force a sweep run manually via the dashboard → confirm identical behavior to the old timer (same log line, same DB effect)
- Full smoke pass on bazaar (storefront load, checkout, admin login) against the containerized stack before cutting Nginx over

**Status:** ✅ Done — cutover completed live on the VPS, 2026-08-21. Both `wavcomm-api`/`wavcomm-ssr` systemd services are disabled (not deleted — instant rollback: `docker compose down && systemctl enable --now wavcomm-api wavcomm-ssr`). Nginx needed **zero config changes** — the containers bind the exact same ports (5090/4010) it already proxied to.

Two real bugs surfaced during the actual cutover that local testing hadn't caught, both fixed and committed:

1. **MySQL unreachable from the container.** MySQL binds `127.0.0.1` only, and `ufw` (default-deny) blocks the Docker bridge subnet entirely — bridge networking couldn't reach it at all (`Connect Timeout`, not `Access denied` like the local test earlier). Rather than widen MySQL's listen address or open a firewall rule for a container-networking convenience, fixed with `network_mode: host` on the `api` service — no bridge translation at all, `127.0.0.1` inside the container is genuinely the host's own loopback.
2. **Every real SSR request returned 400** — `@angular/ssr`'s own SSRF guard (`allowedHosts`) and forwarded-header trust (`trustProxyHeaders`) both default to reject-everything when unset. **Correction to the first fix attempted here:** this was already solved for the systemd deploy, via two systemd drop-in files documented in `documents/deployment.md` §10-WAV since 2026-07-29 (`wavcomm-ssr.service.d/allowedhosts.conf`/`trustproxy.conf`) — missed on first pass because `cat`-ing the base unit file doesn't show its drop-in directory. First attempt used a blanket `NG_ALLOWED_HOSTS=*`; corrected to match the systemd path's existing, more precise allowlist instead, so the two deploy paths agree rather than silently drifting apart. That existing allowlist doesn't cover per-tenant custom domains (`Tenant.CustomDomain`) either — a pre-existing gap, carried forward unchanged, not introduced here.

The storefront was briefly down (roughly 90 seconds) during the first cutover attempt, before bug #2 was caught and rolled back to the systemd services immediately. Root-caused and fixed calmly without live-traffic pressure, then re-verified (including reproducing the exact failure with real subdomain `Host` + `X-Forwarded-*` headers before attempting cutover again) before the successful second attempt.

`documents/deployment.md` §10-WAV still needs updating to reflect the container-based deploy sequence — not yet done.
