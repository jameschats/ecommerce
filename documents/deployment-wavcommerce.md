# Deployment — WavCommerce Platform (multi-tenant) on the existing KVM 2 VPS

How to run the **multi-tenant platform** at `https://wavcommerce.online` (+ every
`https://{store}.wavcommerce.online`) as a **second, independent deployment** on the
**same VPS** that already hosts `calendarshop.online` — without touching the calendar store.

> **Same codebase, second deployment.** The platform is the same `main` branch you've been
> building (V2 tenancy). This guide stands up a separate DB + services + Nginx block for it.
> calendarshop.online keeps running exactly as-is.
>
> **The one genuinely new piece vs calendarshop:** merchants get **subdomains**, so you need a
> **wildcard DNS record** and a **wildcard TLS cert** (`*.wavcommerce.online`). Let's Encrypt only
> issues wildcards via a **DNS-01** challenge → we use **Cloudflare DNS** for fully-automated certs.

## Layout on the box (alongside calendarshop)
```
VPS (62.72.59.84)  — runtimes already installed (.NET 9, Node 22, MySQL 8, Nginx, certbot)
├── calendarshop.online   → existing store          (DB: ecommerce, api :5080, ssr :4000)   [untouched]
└── wavcommerce.online     → NEW platform            (DB: wavcommerce, api :5090, ssr :4010)
    ├── wavcommerce.online          apex = marketing + /signup
    └── *.wavcommerce.online         each merchant store
```

---

## 1. DNS on Cloudflare (wildcard)

1. Add `wavcommerce.online` to a (free) **Cloudflare** account → change the domain's **nameservers** at your registrar to the two Cloudflare gave you. Wait for "Active".
2. In Cloudflare **DNS**, add (set proxy status to **DNS only / grey cloud** so Let's Encrypt + WebSockets work directly):
   - `A  @   62.72.59.84`
   - `A  *   62.72.59.84`   ← wildcard: every `{store}.wavcommerce.online` resolves
   - `A  www 62.72.59.84`
3. Verify from the VPS:
   ```bash
   dig +short wavcommerce.online @1.1.1.1        # 62.72.59.84
   dig +short anything.wavcommerce.online @1.1.1.1  # 62.72.59.84 (wildcard)
   ```

---

## 2. Wildcard TLS (Cloudflare DNS-01)

```bash
apt-get install -y python3-certbot-dns-cloudflare

# Cloudflare API token: My Profile → API Tokens → Create → "Edit zone DNS" on wavcommerce.online
mkdir -p /root/.secrets && umask 077
cat > /root/.secrets/cloudflare.ini <<'INI'
dns_cloudflare_api_token = <YOUR_CLOUDFLARE_API_TOKEN>
INI
chmod 600 /root/.secrets/cloudflare.ini

certbot certonly --dns-cloudflare \
  --dns-cloudflare-credentials /root/.secrets/cloudflare.ini \
  -d wavcommerce.online -d '*.wavcommerce.online' \
  -m you@email.com --agree-tos --no-eff-email
# → /etc/letsencrypt/live/wavcommerce.online/{fullchain,privkey}.pem  (auto-renews)
```

---

## 3. Database (fresh, separate)

```bash
mysql <<'SQL'
CREATE DATABASE IF NOT EXISTS wavcommerce CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'wavcomm'@'127.0.0.1' IDENTIFIED BY '<STRONG_DB_PASSWORD>';
GRANT ALL PRIVILEGES ON wavcommerce.* TO 'wavcomm'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL

# Separate source checkout so its build config can't clash with calendarshop's.
git config --global credential.helper store
git clone https://<user>:<github_pat>@github.com/jameschats/ecommerce.git /srv/wavcomm/src

# Apply ALL migrations (001–111): clean multi-tenant schema + default tenant + seeded plans.
cd /srv/wavcomm/src
for f in database/migrations/*.sql; do
  name=$(basename "$f")
  if [ "$(mysql -N wavcommerce -e "SELECT COUNT(*) FROM __schema_migrations WHERE script_name='$name'" 2>/dev/null || echo 0)" = "0" ]; then
    echo "Applying $name"; mysql wavcommerce < "$f"
  fi
done
mysql wavcommerce -e "SELECT COUNT(*) applied FROM __schema_migrations; SELECT Name,Slug FROM Plans;"
```

---

## 4. Secrets — `/etc/wavcomm/api.env`

```bash
mkdir -p /etc/wavcomm
tee /etc/wavcomm/api.env >/dev/null <<'ENV'
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5090
DOTNET_ROOT=/usr/lib/dotnet
ConnectionStrings__Default=Server=127.0.0.1;Database=wavcommerce;Uid=wavcomm;Pwd=<STRONG_DB_PASSWORD>;CharSet=utf8mb4;
Jwt__Key=<openssl rand -base64 48>
Cors__AngularOrigin=https://wavcommerce.online
Tenancy__BaseDomain=wavcommerce.online
Tenancy__DefaultTenantId=1
Payments__Provider=Razorpay
Payments__RazorpayKeyId=<rzp_key_id>
Payments__RazorpayKeySecret=<rzp_key_secret>
Media__UploadPath=/var/www/wavcomm/uploads
Media__PublicBaseUrl=
ENV
chmod 600 /etc/wavcomm/api.env
mkdir -p /var/www/wavcomm/uploads && chown -R www-data:www-data /var/www/wavcomm/uploads
```
> `Tenancy__BaseDomain=wavcommerce.online` is what turns on subdomain→store resolution.
> The apex + `www` resolve to the default tenant (1); `{slug}.wavcommerce.online` → that store.

---

## 5. Build & publish

```bash
cd /srv/wavcomm/src
dotnet publish ecomm.api/ecomm.api.csproj -c Release -o /var/www/wavcomm/api

# Point the frontend at the platform domain (browser is already subdomain-aware; this sets the
# SSR fallback + SEO base + the SSR host allow-list).
sed -i "s|http://localhost:5080/api|https://wavcommerce.online/api|" ecomm.web/src/app/core/api.config.ts
sed -i "s|http://localhost:4200|https://wavcommerce.online|"        ecomm.web/src/app/core/api.config.ts
sed -i 's|"allowedHosts": \[.*\]|"allowedHosts": ["localhost","127.0.0.1","wavcommerce.online","www.wavcommerce.online",".wavcommerce.online"]|' ecomm.web/angular.json

cd ecomm.web && npm ci && npm run build      # first run: analytics prompt → N
mkdir -p /var/www/wavcomm/web && cp -r dist/ecomm-web/* /var/www/wavcomm/web/
mkdir -p /var/www/wavcomm/api/logs
chown -R www-data:www-data /var/www/wavcomm
```
> The `.wavcommerce.online` entry (leading dot) lets the SSR server accept every merchant subdomain.

---

## 6. Services (systemd)

```bash
tee /etc/systemd/system/wavcomm-api.service >/dev/null <<'UNIT'
[Unit]
Description=WavCommerce Platform API
After=network.target mysql.service
[Service]
WorkingDirectory=/var/www/wavcomm/api
ExecStart=/usr/bin/dotnet /var/www/wavcomm/api/ecomm.api.dll
EnvironmentFile=/etc/wavcomm/api.env
User=www-data
Restart=always
RestartSec=5
SyslogIdentifier=wavcomm-api
[Install]
WantedBy=multi-user.target
UNIT

tee /etc/systemd/system/wavcomm-ssr.service >/dev/null <<'UNIT'
[Unit]
Description=WavCommerce Platform SSR
After=network.target
[Service]
WorkingDirectory=/var/www/wavcomm/web
ExecStart=/usr/bin/node /var/www/wavcomm/web/server/server.mjs
Environment=PORT=4010
Environment=HOST=127.0.0.1
User=www-data
Restart=always
RestartSec=5
SyslogIdentifier=wavcomm-ssr
[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable --now wavcomm-api wavcomm-ssr
sleep 4
systemctl is-active wavcomm-api wavcomm-ssr
curl -s http://127.0.0.1:5090/api/health; echo
curl -s -o /dev/null -w "SSR %{http_code}\n" http://127.0.0.1:4010/
```

---

## 7. Nginx — wildcard server block (uses the wildcard cert from step 2)

```bash
tee /etc/nginx/sites-available/wavcomm >/dev/null <<'NGINX'
server {
    listen 443 ssl;
    server_name wavcommerce.online *.wavcommerce.online;

    ssl_certificate     /etc/letsencrypt/live/wavcommerce.online/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/wavcommerce.online/privkey.pem;

    client_max_body_size 25M;

    location /uploads/ { alias /var/www/wavcomm/uploads/; expires 30d; access_log off; try_files $uri =404; }

    # SignalR (notification bell) — WebSocket upgrade
    location /hubs/ {
        proxy_pass http://127.0.0.1:5090;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5090;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;      # tenant resolution + anti-spoof (Nginx sets it)
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    location / {
        proxy_pass http://127.0.0.1:4010;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
server {                       # 80 → 443 redirect
    listen 80;
    server_name wavcommerce.online *.wavcommerce.online;
    return 301 https://$host$request_uri;
}
NGINX

ln -sf /etc/nginx/sites-available/wavcomm /etc/nginx/sites-enabled/wavcomm
nginx -t && systemctl reload nginx
```
> `X-Forwarded-Host $host` matters: the API resolves the tenant from it, and because **Nginx sets it
> from the real `$host`**, a client can't spoof it. (SSR→API on loopback sets its own — that's trusted, it's your code.)

---

## 8. Smoke test
```bash
curl -s https://wavcommerce.online/api/health; echo
curl -s https://wavcommerce.online/api/plans | head -c 200; echo
```
In a browser:
- **https://wavcommerce.online/signup** → create a test store `teststore` → land on the success card.
- **https://teststore.wavcommerce.online** → the store's storefront (SSR); **/admin** → sign in with the email/password you set → manage only that store.
- Confirm a second store is fully isolated, and each subdomain has valid HTTPS (wildcard cert).

---

## 9. Go-live hardening
- [ ] Change the seeded admin password (`admin@ecommerce.local` / `Admin@123`) on the **wavcommerce** DB immediately.
- [ ] Real **Razorpay live** keys; strong `Jwt__Key` (`openssl rand -base64 48`).
- [ ] Daily backup of the **wavcommerce** DB (cron `mysqldump wavcommerce | gzip > …`).
- [ ] Confirm `ufw` still allows only SSH + Nginx (5090/4010/3306 bind 127.0.0.1).
- [ ] Razorpay **Route** (per-merchant payouts) + **Subscriptions** webhook (V2-1 Phase 3 / V2-5) when you take real money.
- [ ] Umami (optional): a second website id for wavcommerce; or reuse the analytics subdomain.

## Redeploy (after code changes)
```bash
cd /srv/wavcomm/src && git checkout -- ecomm.web/src/app/core/api.config.ts && git pull
# re-apply the step-5 sed lines, apply any new migrations, then:
dotnet publish ecomm.api/ecomm.api.csproj -c Release -o /var/www/wavcomm/api
cd ecomm.web && npm ci && npm run build
rm -rf /var/www/wavcomm/web/* && cp -r dist/ecomm-web/* /var/www/wavcomm/web/
chown -R www-data:www-data /var/www/wavcomm
systemctl restart wavcomm-api wavcomm-ssr
```

## Troubleshooting
| Symptom | Fix |
|---|---|
| Subdomain shows apex/tenant-1 content | `Tenancy__BaseDomain` not set to `wavcommerce.online`, or Nginx not forwarding `Host`/`X-Forwarded-Host`. |
| Blank shell / "Invalid host" on a subdomain | `.wavcommerce.online` missing from `angular.json` allowedHosts (step 5) — rebuild. |
| Wildcard cert fails | Cloudflare token lacks **Edit zone DNS**, or nameservers not yet on Cloudflare. |
| `502` on a subdomain | `wavcomm-api`/`wavcomm-ssr` down — `journalctl -u wavcomm-api -e`. |
| WebSocket/notifications fail | `/hubs/` block missing the `Upgrade`/`Connection` headers, or Cloudflare proxy (orange cloud) on — set DNS-only. |
```
