# DailyCalendarShop — Deployment

**Status:** Draft — the shape is right, the marked ⬜ values need filling in from the live server.
**Scope:** deploying this project to **`daily.calendarshop.online`** on the **same VPS** that already runs the live `calendarshop.online`.

---

## 0. The one rule

> **`calendarshop.online` is live and belongs to a different project. Nothing in this document may touch it.**

Every path, port, service name and database below is deliberately distinct. Before running anything destructive, check which tree you are in. The paths differ by three characters (`ecomm` vs `dcs`) — that is thin, so the deploy script asserts its target rather than trusting the operator.

---

## 1. Topology — two sites, one box

| Resource | `calendarshop.online` (live — **do not touch**) | `daily.calendarshop.online` (this project) |
|---|---|---|
| Database | `ecommerce` | **`dailycalendarshop`** |
| API port | ⬜ (believed `5080`) | ⬜ **`5081`** (proposed) |
| systemd unit | ⬜ (e.g. `ecomm-api.service`) | **`dcs-api.service`** |
| API files | ⬜ (e.g. `/var/www/ecomm/api`) | `/var/www/dcs/api` |
| Web root | `/var/www/ecomm/web` | `/var/www/dcs/web` |
| Uploads | `/var/www/ecomm/uploads` | `/var/www/dcs/uploads` |
| Nginx site | ⬜ `sites-available/ecomm` | `sites-available/dcs` |
| TLS cert | existing | new, via certbot for the subdomain |
| Logs | `…/ecomm/api/logs` | `/var/www/dcs/api/logs` |

**DNS:** an `A` record for `daily` → the VPS IP. Nothing else changes; the apex record is untouched.

---

## 2. ⚠️ Fix this before deploying regularly: `api.config.ts`

[tech-debt.md](../tech-debt.md#L63-L66) already flags it:

> *"Frontend `api.config.ts` uses `sed`-replaced constants (`API_BASE_URL`, `SITE_URL` = localhost, rewritten at deploy). Works, but a rebuild that skips the `sed` step silently points prod at localhost."*

With **one** site that was a minor smell. With **two sites on one box and frequent deploys** it is a live hazard: a missed or mis-targeted `sed` points the new site at the old site's API — and it fails silently, serving the wrong data rather than erroring.

**Fix:** replace the build-time `sed` with a **runtime `config.json`** fetched before Angular bootstraps (or Angular file-replacement build configs). Each web root then carries its own config file, deployed with it, and there is no shared build-time state to get wrong.

This is not optional polish. Do it in the first deploy slice, before the habit of frequent deploys forms around the fragile version.

---

## 3. One-time server setup

Run once. Every command is scoped to the new tree.

```bash
# 3.1 Directories
sudo mkdir -p /var/www/dcs/{api,web,uploads}
sudo chown -R www-data:www-data /var/www/dcs

# 3.2 Database (server-side — the local dailycalendarshop DB does not travel)
mysql -u root -p -e "CREATE DATABASE dailycalendarshop CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
# then apply migrations 001..029 in order (see §5)

# 3.3 A dedicated DB user — do NOT reuse the live site's credentials
mysql -u root -p -e "CREATE USER 'dcs'@'localhost' IDENTIFIED BY '<strong-password>'; \
  GRANT ALL PRIVILEGES ON dailycalendarshop.* TO 'dcs'@'localhost'; FLUSH PRIVILEGES;"
```

**The DB user is scoped to one schema on purpose.** If this app is ever compromised or a migration is run against the wrong connection string, the grant is what stops it reaching the live `ecommerce` data.

### 3.4 systemd — `/etc/systemd/system/dcs-api.service`
```ini
[Unit]
Description=DailyCalendarShop API
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/dcs/api
ExecStart=/usr/bin/dotnet /var/www/dcs/api/ecomm.api.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5081

[Install]
WantedBy=multi-user.target
```

### 3.5 Nginx — `/etc/nginx/sites-available/dcs`
```nginx
server {
    server_name daily.calendarshop.online;

    root /var/www/dcs/web;
    index index.html;

    # Angular SSR/SPA
    location / { try_files $uri $uri/ /index.html; }

    # API
    location /api/ {
        proxy_pass http://localhost:5081;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Uploads — served directly, never wiped by a deploy
    location /uploads/ {
        alias /var/www/dcs/uploads/;
        expires 30d;
        access_log off;
    }

    client_max_body_size 25M;   # ZIP image uploads (§10.2 of design.md)
}
```

```bash
sudo ln -s /etc/nginx/sites-available/dcs /etc/nginx/sites-enabled/dcs
sudo nginx -t          # ALWAYS. A bad config here takes the LIVE site down too.
sudo systemctl reload nginx
sudo certbot --nginx -d daily.calendarshop.online
```

> `nginx -t` before every reload is not ceremony. Nginx is shared with the live site — a syntax error in *our* file breaks *their* site on reload.

### 3.6 Keep the test site out of Google
`daily.calendarshop.online` must serve `X-Robots-Tag: noindex` (and a disallow-all `robots.txt`). With 365 Phase-2 content pages, an indexed staging copy competes with the real site later. Add it to the nginx block now, while it is free.

---

## 4. Secrets

`appsettings.json` in the repo holds **dev placeholders only** and is committed. Production values (DB password, JWT key, Brevo SMTP password, MSG91 key, UPI details) must come from `appsettings.Production.json` **on the server, not in git**, or from environment variables in the systemd unit.

The deploy must **never overwrite the server's `appsettings.Production.json`** — see the exclusion in §6.

---

## 5. Database migrations

Forward-only and numbered; each records itself in `__schema_migrations`.

```bash
# Applied so far: 001–029 (base platform). Phase 1 adds 030, Phase 2 adds 031.
for f in database/migrations/*.sql; do
  mysql -u dcs -p dailycalendarshop < "$f"
done
```

Rules:
- Always name the database explicitly. Never rely on a default.
- Check `SELECT script_name FROM __schema_migrations` before and after.
- **Never** edit an applied script — add a higher-numbered one.

---

## 6. Deploy (the routine loop)

```bash
# --- build locally ---
dotnet publish ecomm.api/ecomm.api.csproj -c Release -o ./publish/api
cd ecomm.web && npm ci && npm run build && cd ..

# --- ship ---
rsync -az --delete \
      --exclude 'appsettings.Production.json' \
      --exclude 'logs/' \
      ./publish/api/   <user>@<host>:/var/www/dcs/api/

rsync -az --delete ./ecomm.web/dist/<app>/browser/  <user>@<host>:/var/www/dcs/web/

# --- migrate, then restart ---
ssh <user>@<host> 'mysql -u dcs -p dailycalendarshop < /tmp/03X_new.sql'
ssh <user>@<host> 'sudo systemctl restart dcs-api && systemctl is-active dcs-api'

# --- verify ---
curl -fsS https://daily.calendarshop.online/api/health/ready   # expect: Healthy
```

**`--delete` is the dangerous flag here.** It is correct for `web/` and `api/` (they should exactly match the build) and **catastrophic** if ever pointed at `uploads/` or at the `/var/www/ecomm/` tree. The script in §7 hard-codes its targets for exactly this reason.

---

## 7. Deploy script — required behaviours

When written (⬜ TODO), `deploy-dcs.sh` must:
1. **Assert its target** — refuse to run if the destination path is not under `/var/www/dcs/`. A guard clause, not a comment.
2. **Never touch** `/var/www/dcs/uploads/`, `appsettings.Production.json`, or anything under `/var/www/ecomm/`.
3. **Run `nginx -t`** before any reload.
4. **Health-check after restart** and exit non-zero if `/api/health/ready` is not `Healthy` — a deploy that reports success while the API is down is worse than one that fails loudly.
5. **Print what it is about to do and to which host** before doing it.

---

## 8. Rollback

Keep the previous published API in `/var/www/dcs/api.prev` and the previous web build in `/var/www/dcs/web.prev`; rollback is a directory swap plus a service restart.

**Database migrations do not roll back** — they are forward-only by design. So a schema change and a code change should be deployed such that the *old code still works against the new schema* wherever possible (add columns before using them; drop columns a release later). This matters more once there is real order data.

---

## 9. First deploy — do it now, while the risk is zero

This branch currently differs from `main` only by a connection string and design documents. **There is no Phase 1 code yet.** That makes this the ideal moment to stand up the whole pipeline: DNS, nginx, TLS, systemd, database, deploy script and health check all get proven while a broken deploy costs nothing.

Checklist:
- [ ] DNS `A` record for `daily` → VPS
- [ ] Directories + permissions (§3.1)
- [ ] `dailycalendarshop` DB + scoped user, migrations 001–029 applied (§3.2, §5)
- [ ] `appsettings.Production.json` on the server with real secrets (§4)
- [ ] `dcs-api.service` running on 5081 (§3.4)
- [ ] Nginx site + `nginx -t` + TLS (§3.5)
- [ ] `noindex` on the test domain (§3.6)
- [ ] `api.config.ts` runtime-config fix (§2)
- [ ] `deploy-dcs.sh` with the guards in §7
- [ ] `/api/health/ready` returns `Healthy` over HTTPS
- [ ] **Confirm `calendarshop.online` is still up and unchanged**

---

## 10. ⬜ Needed from James

| # | What | Why |
|---|---|---|
| 1 | SSH host / user — and whether I run the commands or hand you a script | Everything below the build step |
| 2 | The current manual deploy steps for `calendarshop.online` | So the new script mirrors what already works, rather than inventing a second convention |
| 3 | Existing API port + systemd unit name | To guarantee no collision (5081 is a proposal, not a checked fact) |
| 4 | Existing nginx site filename | So we add a sibling, and never edit theirs |
| 5 | Is `dotnet 9` runtime already installed on the box? | If yes, nothing to do; if it is SDK-only or older, one-time install |
| 6 | Who owns DNS for `calendarshop.online` | To add the `daily` record |