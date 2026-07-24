# DailyCalendarShop — Deployment

**Status:** values confirmed against the live server (24 Jul 2026). One gap remains — §11.
**Target:** `daily.calendarshop.online` on `62.72.59.84` (Hostinger VPS, Ubuntu 24.04.4, `srv1788459`).

---

## 0. The one rule

> **Two live sites already run on this box — `calendarshop.online` and `wavcommerce.online`. Nothing here may touch them.**

Every path, port, service name and database below is distinct from theirs. The deploy script asserts its target rather than trusting the operator, because `/var/www/ecomm` and `/var/www/dailycal` are one careless tab-completion apart.

---

## 1. What is already on the box

Discovered, not assumed:

| | `ecomm` (CalendarShop) | `wavcomm` (WavCommerce) | `umami` |
|---|---|---|---|
| Domain | calendarshop.online | wavcommerce.online, `*.wavcommerce.online` | analytics.calendarshop.online |
| .NET API | **5080** (127.0.0.1) | **5090** (127.0.0.1) | — |
| Node SSR | **4000** | **4010** | — |
| Docker | — | — | **3000** (127.0.0.1) |
| Services | `ecomm-api`, `ecomm-ssr` | `wavcomm-api`, `wavcomm-ssr` | docker |
| Root | `/var/www/ecomm` | `/var/www/wavcomm` | — |
| Env file | `/etc/ecomm/api.env` | `/etc/wavcomm/api.env` | — |
| Database | `ecommerce` | `wavcommerce` | — |

**Resources:** 7.8 GB RAM (5.5 GB available), 96 GB disk (88 GB free), .NET **9.0.17**, Node **v22.23.1**, nginx with certbot-managed TLS. A third stack fits comfortably.

**Conventions worth copying exactly:**
- Ports step by **+10**: API `5080 → 5090 → 5100`, SSR `4000 → 4010 → 4020`.
- Services are `<site>-api` / `<site>-ssr`, running as `www-data`.
- API config comes from `EnvironmentFile=/etc/<site>/api.env` — **not** `appsettings.Production.json`.
- SSR entry point is `/var/www/<site>/web/server/server.mjs`.
- Nginx: a file in `sites-available/`, symlinked into `sites-enabled/`, proxying to `127.0.0.1:<port>`.

---

## 2. Our slot

| Resource | Value |
|---|---|
| Domain | `daily.calendarshop.online` |
| .NET API port | **5100** |
| Node SSR port | **4020** |
| Services | `dailycal-api.service`, `dailycal-ssr.service` |
| Root | `/var/www/dailycal/{api,web,uploads}` |
| Env file | `/etc/dailycal/api.env` |
| Database | `dailycalendarshop` |
| DB user | `dailycal@localhost` (scoped to that one schema) |
| Nginx | `sites-available/dailycal` |

**DNS:** `A` record `daily.calendarshop.online → 62.72.59.84`. Add this **first** — certbot cannot issue a certificate until it resolves.

---

## 3. ⚠️ Finding: the SSR processes bind to all interfaces

```
LISTEN  *:4000    node   ecomm-ssr
LISTEN  *:4010    node   wavcomm-ssr
```

Both listen on **every interface**, despite `Environment=HOST=127.0.0.1` in their unit files. The variable is ignored because Angular's generated `server.ts` calls `app.listen(port, callback)` with no host argument. The .NET APIs bind correctly to `127.0.0.1`; only the Node side is wrong.

The firewall currently blocks external access to 4000/4010/5080/5090/3000 — verified from outside. So this is defence-in-depth, not an active breach. But it is one firewall rule away from exposing SSR directly, bypassing nginx's TLS, security headers and rate limiting.

**Fixed in this repo** (`ecomm.web/src/server.ts`) — now `app.listen(port, process.env['HOST'] ?? '127.0.0.1', …)`. Our site will bind to loopback correctly.

**Worth back-porting** to `ecomm` and `wavcomm` when either is next deployed. Not urgent, not ours to change unilaterally.

---

## 4. Runtime configuration — the `sed` hazard is fixed

[tech-debt.md](../tech-debt.md#L63-L66) flagged the old approach:

> *"Frontend `api.config.ts` uses `sed`-replaced constants (`API_BASE_URL`, `SITE_URL` = localhost, rewritten at deploy). Works, but a rebuild that skips the `sed` step silently points prod at localhost."*

With three sites on one box and frequent deploys that was a live hazard — a missed or mis-targeted `sed` points one site at another site's API, and fails *silently*.

**Fixed.** `api.config.ts` now resolves its values at module load from `window.__APP_CONFIG__`, set by **`/config.js`** — a plain (non-module) script loaded from `index.html` before the Angular bundle.

**Why a synchronous global rather than an async `fetch('/config.json')`:** two services (`banner.service.ts`, `notification.service.ts`) derive `API_ORIGIN` from `API_BASE_URL` at **module top level**. An async loader resolves *after* those modules are evaluated, leaving them on the localhost default — the same silent bug in a new costume. A global that is already set when the bundle runs has no ordering problem, and none of the ~26 importing files needed to change.

**It now fails loudly.** Served from a non-localhost origin while `API_BASE_URL` still points at localhost ⇒ console error *and* a red banner naming the problem.

**Two places must agree:**

| Runtime | Config source |
|---|---|
| Browser | `/config.js` in `/var/www/dailycal/web/browser/` |
| Node SSR | environment variables on `dailycal-ssr.service` |

`window` does not exist during SSR, so the same file falls back to `process.env`. **If the two disagree, server-rendered HTML and client hydration disagree** — wrong canonical URLs, content changing after load. Set both from the same values in the same deploy step.

> `public/config.js` is committed with **development** defaults and copied into every build. `rsync --delete` would restore that dev copy, so **config.js is written after the rsync** — see §8.

---

## 5. One-time server setup

Paste as one block. Everything is scoped to the new tree.

```bash
set -euo pipefail

# 5.1 Directories
mkdir -p /var/www/dailycal/{api,web,uploads}
chown -R www-data:www-data /var/www/dailycal

# 5.2 Database + a user scoped to this schema only
mysql -u root -p <<'SQL'
CREATE DATABASE IF NOT EXISTS dailycalendarshop
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'dailycal'@'localhost' IDENTIFIED BY 'CHANGE-ME-STRONG';
GRANT ALL PRIVILEGES ON dailycalendarshop.* TO 'dailycal'@'localhost';
FLUSH PRIVILEGES;
SQL

# 5.3 API environment file (secrets live here, never in git)
mkdir -p /etc/dailycal
cat > /etc/dailycal/api.env <<'ENV'
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5100
ConnectionStrings__Default=Server=localhost;Database=dailycalendarshop;Uid=dailycal;Pwd=CHANGE-ME-STRONG;CharSet=utf8mb4;
Jwt__Key=CHANGE-ME-64-RANDOM-CHARS
ENV
chmod 600 /etc/dailycal/api.env
```

**The DB user is scoped to one schema deliberately.** If this app is compromised, or a migration is run against the wrong connection string, that grant is what stops it reaching live `ecommerce` or `wavcommerce` data.

**`utf8mb4_unicode_ci` matters** — Phase 2 stores Tamil (திருக்குறள், பஞ்சாங்கம்). Getting the collation wrong at creation is painful to correct later.

### 5.4 `/etc/systemd/system/dailycal-api.service`
```ini
[Unit]
Description=DailyCalendarShop .NET API
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/dailycal/api
ExecStart=/usr/bin/dotnet /var/www/dailycal/api/ecomm.api.dll
EnvironmentFile=/etc/dailycal/api.env
Restart=always
RestartSec=10
User=www-data

[Install]
WantedBy=multi-user.target
```

### 5.5 `/etc/systemd/system/dailycal-ssr.service`
```ini
[Unit]
Description=DailyCalendarShop Angular SSR
After=network.target

[Service]
WorkingDirectory=/var/www/dailycal/web
ExecStart=/usr/bin/node /var/www/dailycal/web/server/server.mjs
Environment=NODE_ENV=production
Environment=PORT=4020
Environment=HOST=127.0.0.1
# Must match /config.js exactly — see §4
Environment=API_BASE_URL=https://daily.calendarshop.online/api
Environment=SITE_URL=https://daily.calendarshop.online
Restart=always
RestartSec=10
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable --now dailycal-api dailycal-ssr
systemctl status dailycal-api dailycal-ssr --no-pager
```

### 5.6 Nginx — `/etc/nginx/sites-available/dailycal`

⬜ **Mirror the working `ecomm` config rather than this sketch** — see §11. Shape, from the discovered layout:

```nginx
server {
    server_name daily.calendarshop.online;

    # Testing domain — keep it out of Google (§5.7)
    add_header X-Robots-Tag "noindex, nofollow" always;

    location /uploads/ {
        alias /var/www/dailycal/uploads/;
        expires 30d;
        access_log off;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Everything else → Angular SSR. NOT try_files/index.html:
    # an SSR build emits no static index.html, only index.csr.html + index.server.html.
    location / {
        proxy_pass http://127.0.0.1:4020;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    client_max_body_size 25M;   # ZIP image uploads (design.md §10.2)
}
```

```bash
ln -s /etc/nginx/sites-available/dailycal /etc/nginx/sites-enabled/dailycal
nginx -t                       # ALWAYS — see below
systemctl reload nginx
certbot --nginx -d daily.calendarshop.online
```

> `nginx -t` before every reload is not ceremony. **Nginx is shared with two live sites** — a syntax error in *our* file breaks *theirs* on reload.

### 5.7 Keep the test domain out of Google
The `X-Robots-Tag` above, plus a disallow-all `robots.txt`. With 365 Phase-2 content pages, an indexed staging copy competes with the real site later. Free now, expensive to undo.

---

## 6. Migrations

```bash
for f in database/migrations/*.sql; do
  echo "==> $f"
  mysql -u dailycal -p dailycalendarshop < "$f"
done
mysql -u dailycal -p dailycalendarshop -e "SELECT COUNT(*) FROM __schema_migrations;"   # expect 29
```

Forward-only and numbered; each records itself in `__schema_migrations`. Always name the database explicitly — never rely on a default. Never edit an applied script; add a higher-numbered one.

---

## 7. Secrets

`appsettings.json` in the repo holds **dev placeholders only** and is committed. Production values live in `/etc/dailycal/api.env` (mode `600`, outside git): DB password, JWT key, Brevo SMTP password, MSG91 key, UPI details.

The deploy never writes that file. Nothing secret goes in `config.js` — it is served to browsers.

---

## 8. Deploy (the routine loop)

```bash
# --- build locally ---
dotnet publish ecomm.api/ecomm.api.csproj -c Release -o ./publish/api
cd ecomm.web && npm ci && npm run build && cd ..
# output: ecomm.web/dist/ecomm-web/{browser,server}

# --- ship the API ---
rsync -az --delete --exclude 'logs/' \
      ./publish/api/  root@62.72.59.84:/var/www/dailycal/api/

# --- ship the frontend: BOTH browser/ and server/ ---
rsync -az --delete \
      ./ecomm.web/dist/ecomm-web/  root@62.72.59.84:/var/www/dailycal/web/

# --- write runtime config AFTER rsync (--delete would restore the dev copy) ---
ssh root@62.72.59.84 'cat > /var/www/dailycal/web/browser/config.js' <<'EOF'
window.__APP_CONFIG__ = {
  apiBaseUrl: 'https://daily.calendarshop.online/api',
  siteUrl:    'https://daily.calendarshop.online',
  umamiSrc: '', umamiWebsiteId: '', umamiDashboardUrl: '',
};
EOF

# --- restart BOTH services ---
ssh root@62.72.59.84 'systemctl restart dailycal-api dailycal-ssr && \
                      systemctl is-active dailycal-api dailycal-ssr'

# --- verify ---
curl -fsS https://daily.calendarshop.online/api/health/ready   # expect: Healthy
curl -fsS https://daily.calendarshop.online/ | head -20        # expect server-rendered HTML
curl -fsS https://daily.calendarshop.online/config.js          # expect PROD values, not localhost
curl -sI https://calendarshop.online/ | head -1                # confirm the live site is untouched
```

**`--delete` is the dangerous flag.** Correct for `api/` and `web/` (they should exactly match the build), catastrophic if aimed at `uploads/` or another site's tree.

**Do not ship only `browser/`.** The whole `dist/ecomm-web/` goes, including `server/`. Copying just `browser/` leaves SSR running the previous build — the site keeps working while silently serving stale server-rendered pages, which is a genuinely confusing bug to chase.

---

## 9. Deploy script — required behaviours

`deploy-dailycal.sh` (⬜ to write) must:
1. **Assert its target** — refuse to run if any destination path is not under `/var/www/dailycal/`. A guard clause, not a comment.
2. **Never touch** `/var/www/dailycal/uploads/`, `/etc/dailycal/api.env`, or anything under `/var/www/ecomm/` or `/var/www/wavcomm/`.
3. **Run `nginx -t`** before any reload.
4. **Health-check after restart**, exiting non-zero if `/api/health/ready` is not `Healthy`. A deploy that reports success while the API is down is worse than one that fails loudly.
5. **Print target host and paths** before acting.

---

## 10. Rollback

Keep the previous build in `/var/www/dailycal/api.prev` and `web.prev`; rollback is a directory swap plus a restart.

**Migrations do not roll back** — forward-only by design. So deploy schema and code such that old code still works against the new schema where possible (add columns before using them; drop a release later). This matters once real order data exists.

---

## 11. ⬜ Remaining gap

**Need:** `cat /etc/nginx/sites-available/ecomm`

The grep showed its shape — API on 5080 across three locations, SSR on 4000, a `location = /sitemap.xml` proxied to the API, certbot's 443 block and an 80→443 redirect — but not the full directives. I would rather **mirror a config already proven in production** than ship my reconstruction of it. Once I have it, §5.6 gets replaced with the real thing and `deploy-dailycal.sh` follows.

---

## 12. First-deploy checklist

There is no Phase 1 code yet — this branch differs from `main` only by a connection string and documents. **That makes now the ideal time to prove the pipeline, while a broken deploy costs nothing.**

- [ ] DNS `A` record `daily` → `62.72.59.84`
- [ ] Directories + ownership (§5.1)
- [ ] DB + scoped user, migrations 001–029, count = 29 (§5.2, §6)
- [ ] `/etc/dailycal/api.env` with real secrets, mode 600 (§5.3)
- [ ] Both services enabled and active on 5100 / 4020 (§5.4, §5.5)
- [ ] Nginx site + `nginx -t` + TLS (§5.6)
- [ ] `noindex` confirmed (§5.7)
- [ ] `/api/health/ready` returns `Healthy` over HTTPS
- [ ] `config.js` serves production values, not localhost
- [ ] `ss -tlnp` shows SSR on **`127.0.0.1:4020`**, not `*:4020` (§3)
- [ ] **`calendarshop.online` and `wavcommerce.online` still return 200**
