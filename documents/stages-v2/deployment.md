# DailyCalendarShop — Deployment

**Status:** Draft — the shape is right, the marked ⬜ values need filling in from the live server.
**Scope:** deploying this project to **`daily.calendarshop.online`** on the **same VPS** that already runs the live `calendarshop.online`.

---

## 0. The one rule

> **`calendarshop.online` is live and belongs to a different project. Nothing in this document may touch it.**

Every path, port, service name and database below is deliberately distinct. Before running anything destructive, check which tree you are in. The paths differ by three characters (`ecomm` vs `dcs`) — that is thin, so the deploy script asserts its target rather than trusting the operator.

---

## 1. Topology — two sites, one box

⚠️ **There are three sites on this box**, not two: `wavcommerce.online`, `calendarshop.online`, and now this one. Port and service-name collisions are the main risk — run the discovery in §1.1 before choosing anything.

⚠️ **Each site is two processes, not one.** The frontend is Angular **SSR** (`outputMode: "server"`), so besides the .NET API there is a **Node/Express process** (`server.ts`) rendering pages. Nginx proxies to it rather than serving a static `index.html` — the build produces no static index, only `index.csr.html` and `index.server.html`.

| Resource | Live sites (**do not touch**) | `daily.calendarshop.online` (this project) |
|---|---|---|
| Database | `ecommerce`, ⬜ wavcommerce's | **`dailycalendarshop`** |
| .NET API port | ⬜ discover | ⬜ **pick a free one** (e.g. 5082) |
| Node SSR port | ⬜ discover | ⬜ **pick a free one** (e.g. 4002) |
| API service | ⬜ discover | `dcs-api.service` |
| SSR service | ⬜ discover | `dcs-ssr.service` |
| API files | ⬜ discover | `/var/www/dcs/api` |
| Web build | ⬜ discover | `/var/www/dcs/web` (contains `browser/` + `server/`) |
| Uploads | `/var/www/ecomm/uploads` | `/var/www/dcs/uploads` |
| Nginx site | ⬜ discover | `sites-available/dcs` |
| TLS cert | existing | new, via certbot for the subdomain |

**DNS:** an `A` record for `daily` → the VPS IP. The apex and `wavcommerce.online` records are untouched.

---

## 1.1 Discovery — run this first, paste the output back

Nothing below should be chosen until we know what is already taken. All read-only.

```bash
# --- what is listening, and which process owns it ---
sudo ss -tlnp | sort -k4

# --- nginx: which hostnames, which upstreams, which roots ---
ls -l /etc/nginx/sites-enabled/
sudo grep -rnE "server_name|proxy_pass|root " /etc/nginx/sites-enabled/

# --- services and the ports baked into them ---
systemctl list-units --type=service --state=running | grep -Ei 'dotnet|node|api|ssr|ecomm|wav|calendar'
sudo grep -rnE "ASPNETCORE_URLS|ExecStart|Environment|PORT" /etc/systemd/system/*.service

# --- running processes and their working dirs ---
ps -eo pid,user,args | grep -Ei 'dotnet|node' | grep -v grep

# --- databases already present ---
mysql -u root -p -e "SHOW DATABASES;"

# --- runtime + resources ---
dotnet --list-runtimes; node -v; free -h; df -h /var/www
```

`free -h` matters: three .NET APIs plus three Node SSR processes on one VPS is six long-running runtimes. If the box is small, that is the constraint to find now rather than after deploying.

---

## 2. ✅ Runtime configuration — the `sed` hazard is fixed

[tech-debt.md](../tech-debt.md#L63-L66) flagged the old approach:

> *"Frontend `api.config.ts` uses `sed`-replaced constants (`API_BASE_URL`, `SITE_URL` = localhost, rewritten at deploy). Works, but a rebuild that skips the `sed` step silently points prod at localhost."*

With three sites on one box and frequent deploys that was a live hazard — a missed or mis-targeted `sed` points one site at another site's API, and fails *silently*.

**Fixed.** `api.config.ts` now resolves its values at module load from `window.__APP_CONFIG__`, set by **`/config.js`** — a plain (non-module) script loaded from `index.html` before the Angular bundle:

```js
window.__APP_CONFIG__ = {
  apiBaseUrl: 'https://daily.calendarshop.online/api',
  siteUrl:    'https://daily.calendarshop.online',
  umamiSrc: '', umamiWebsiteId: '', umamiDashboardUrl: '',
};
```

**Why a synchronous global rather than an async `fetch('/config.json')`:** two services (`banner.service.ts`, `notification.service.ts`) derive `API_ORIGIN` from `API_BASE_URL` at **module top level**. An async loader resolves *after* those modules are evaluated, leaving them holding the localhost default — the same silent-wrong-target bug in a new costume. A global that is already set when the bundle runs has no ordering problem, and none of the ~26 importing files needed to change.

**It now fails loudly.** If the site is served from a non-localhost origin while `API_BASE_URL` still points at localhost, the app logs a console error *and* renders a red banner naming the problem. The failure this file exists to prevent is no longer silent.

**Two places must agree:**

| Runtime | Config source |
|---|---|
| Browser | `/config.js` in the web root |
| **Node SSR** | **environment variables** on `dcs-ssr.service` — `API_BASE_URL`, `SITE_URL`, `UMAMI_*` |

`window` does not exist during SSR, so the same file falls back to `process.env`. **If the two disagree, server-rendered HTML and client hydration disagree** — wrong canonical URLs, and content that changes after load. Set both from the same values, in the same deploy step.

> `public/config.js` is committed with **development** defaults and is copied into every build. The deploy overwrites it on the server. Because `rsync --delete` would otherwise restore the dev copy, **writing `config.js` must come after the rsync** — see §6.

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

    # Keep the test site out of Google (§3.6)
    add_header X-Robots-Tag "noindex, nofollow" always;

    # Static build artefacts, served straight from disk
    location /config.js { root /var/www/dcs/web/browser; expires -1; add_header Cache-Control "no-store"; }
    location ~ ^/(favicon\.ico|robots\.txt)$ { root /var/www/dcs/web/browser; }
    location /assets/ { root /var/www/dcs/web/browser; expires 30d; access_log off; }

    # Uploads — served directly, never wiped by a deploy
    location /uploads/ {
        alias /var/www/dcs/uploads/;
        expires 30d;
        access_log off;
    }

    # API
    location /api/ {
        proxy_pass http://localhost:5082;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Everything else → Angular SSR (Node). NOT try_files/index.html —
    # an SSR build emits no static index.html, only index.csr.html + index.server.html.
    location / {
        proxy_pass http://localhost:4002;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    client_max_body_size 25M;   # ZIP image uploads (§10.2 of design.md)
}
```

```bash
sudo ln -s /etc/nginx/sites-available/dcs /etc/nginx/sites-enabled/dcs
sudo nginx -t          # ALWAYS. A bad config here takes the LIVE sites down too.
sudo systemctl reload nginx
sudo certbot --nginx -d daily.calendarshop.online
```

> `nginx -t` before every reload is not ceremony. Nginx is shared with two live sites — a syntax error in *our* file breaks *theirs* on reload.

### 3.5b systemd — `/etc/systemd/system/dcs-ssr.service` (Angular SSR)

The frontend is not static files; a Node process renders it.

```ini
[Unit]
Description=DailyCalendarShop Angular SSR
After=network.target

[Service]
WorkingDirectory=/var/www/dcs/web
ExecStart=/usr/bin/node /var/www/dcs/web/server/server.mjs
Restart=always
RestartSec=10
User=www-data
Environment=NODE_ENV=production
Environment=PORT=4002
# Must match /config.js exactly — see §2
Environment=API_BASE_URL=https://daily.calendarshop.online/api
Environment=SITE_URL=https://daily.calendarshop.online

[Install]
WantedBy=multi-user.target
```

⬜ Confirm the server entry filename after the first build (`ls /var/www/dcs/web/server/`) — Angular names it `server.mjs` in recent versions, but verify rather than assume.

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
# build output: ecomm.web/dist/ecomm-web/{browser,server}

# --- ship the API ---
rsync -az --delete \
      --exclude 'appsettings.Production.json' \
      --exclude 'logs/' \
      ./publish/api/   <user>@<host>:/var/www/dcs/api/

# --- ship the frontend: BOTH browser/ and server/ (SSR needs the server bundle) ---
rsync -az --delete ./ecomm.web/dist/ecomm-web/  <user>@<host>:/var/www/dcs/web/

# --- write runtime config AFTER rsync (rsync --delete would restore the dev copy) ---
ssh <user>@<host> 'cat > /var/www/dcs/web/browser/config.js' <<"EOF"
window.__APP_CONFIG__ = {
  apiBaseUrl: 'https://daily.calendarshop.online/api',
  siteUrl:    'https://daily.calendarshop.online',
  umamiSrc: '', umamiWebsiteId: '', umamiDashboardUrl: '',
};
EOF

# --- migrate, then restart BOTH services ---
ssh <user>@<host> 'mysql -u dcs -p dailycalendarshop < /tmp/03X_new.sql'
ssh <user>@<host> 'sudo systemctl restart dcs-api dcs-ssr && systemctl is-active dcs-api dcs-ssr'

# --- verify ---
curl -fsS https://daily.calendarshop.online/api/health/ready    # expect: Healthy
curl -fsS https://daily.calendarshop.online/ | head -20         # expect server-rendered HTML
curl -fsS https://daily.calendarshop.online/config.js           # expect the PROD values, not localhost
```

**`--delete` is the dangerous flag here.** It is correct for `web/` and `api/` (they should exactly match the build) and **catastrophic** if pointed at `uploads/` or anywhere under another site's tree. The script in §7 asserts its target for exactly this reason.

**Do not forget `server/`.** The frontend deploy ships the whole `dist/ecomm-web/` directory, not just `browser/`. Copying only `browser/` leaves the SSR process running the previous build — the site keeps working, silently serving stale server-rendered pages, which is a genuinely confusing bug to chase.

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