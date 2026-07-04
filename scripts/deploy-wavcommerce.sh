#!/usr/bin/env bash
#
# One-shot deploy of the WavCommerce multi-tenant platform onto the existing VPS,
# alongside calendarshop.online (separate DB + services + Nginx). Idempotent:
# run it again to redeploy after code changes. Run as root ON THE VPS.
#
# PREREQUISITES (do these first — see documents/deployment-wavcommerce.md §1):
#   - wavcommerce.online DNS on Cloudflare with A @, A *, A www → the VPS IP (grey cloud)
#   - a Cloudflare "Edit zone DNS" API token
#   - runtimes already installed (they are, from calendarshop): .NET 9, Node 22, MySQL, Nginx, certbot
#
# USAGE:  edit the CONFIG block, then:  sudo bash deploy-wavcommerce.sh
# ⚠️  Do NOT commit this file with real secrets filled in.

set -euo pipefail

# ==================== CONFIG — edit these ====================
DOMAIN="wavcommerce.online"
EMAIL="you@email.com"
DB_PASSWORD="CHANGE_ME_strong_db_password"
CF_API_TOKEN="CHANGE_ME_cloudflare_edit_zone_dns_token"
GIT_REPO="https://<github-username>:<github_pat>@github.com/jameschats/ecommerce.git"
API_PORT=5090
SSR_PORT=4010
# ============================================================

SRC=/srv/wavcomm/src
WEB=/var/www/wavcomm
export NG_CLI_ANALYTICS=false        # skip ng's first-run analytics prompt

echo "==> [1/9] Wildcard TLS (Cloudflare DNS-01)"
apt-get install -y python3-certbot-dns-cloudflare >/dev/null
mkdir -p /root/.secrets; umask 077
printf 'dns_cloudflare_api_token = %s\n' "$CF_API_TOKEN" > /root/.secrets/cloudflare.ini
chmod 600 /root/.secrets/cloudflare.ini
if [ ! -d "/etc/letsencrypt/live/$DOMAIN" ]; then
  certbot certonly --dns-cloudflare --dns-cloudflare-credentials /root/.secrets/cloudflare.ini \
    -d "$DOMAIN" -d "*.$DOMAIN" -m "$EMAIL" --agree-tos --no-eff-email
else
  echo "    cert already present — skipping"
fi

echo "==> [2/9] Database (wavcommerce)"
mysql <<SQL
CREATE DATABASE IF NOT EXISTS wavcommerce CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'wavcomm'@'127.0.0.1' IDENTIFIED BY '${DB_PASSWORD}';
GRANT ALL PRIVILEGES ON wavcommerce.* TO 'wavcomm'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL

echo "==> [3/9] Source (${SRC})"
if [ -d "$SRC/.git" ]; then
  git -C "$SRC" checkout -- ecomm.web/src/app/core/api.config.ts ecomm.web/angular.json 2>/dev/null || true
  git -C "$SRC" pull
else
  mkdir -p "$(dirname "$SRC")"
  git clone "$GIT_REPO" "$SRC"
fi
cd "$SRC"

echo "==> [4/9] Migrations (only new ones)"
for f in database/migrations/*.sql; do
  name=$(basename "$f")
  applied=$(mysql -N wavcommerce -e "SELECT COUNT(*) FROM __schema_migrations WHERE script_name='$name'" 2>/dev/null || echo 0)
  if [ "$applied" = "0" ]; then echo "    applying $name"; mysql wavcommerce < "$f"; fi
done

echo "==> [5/9] Secrets /etc/wavcomm/api.env (created once)"
if [ ! -f /etc/wavcomm/api.env ]; then
  mkdir -p /etc/wavcomm
  JWT=$(openssl rand -base64 48)
  cat > /etc/wavcomm/api.env <<ENV
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:${API_PORT}
DOTNET_ROOT=/usr/lib/dotnet
ConnectionStrings__Default=Server=127.0.0.1;Database=wavcommerce;Uid=wavcomm;Pwd=${DB_PASSWORD};CharSet=utf8mb4;
Jwt__Key=${JWT}
Cors__AngularOrigin=https://${DOMAIN}
Tenancy__BaseDomain=${DOMAIN}
Tenancy__DefaultTenantId=1
Payments__Provider=Mock
Media__UploadPath=${WEB}/uploads
Media__PublicBaseUrl=
ENV
  chmod 600 /etc/wavcomm/api.env
  echo "    wrote api.env (Payments=Mock; add Razorpay keys later, then restart wavcomm-api)"
else
  echo "    api.env exists — leaving it (edit by hand to change secrets)"
fi
mkdir -p "${WEB}/uploads"; chown -R www-data:www-data "${WEB}/uploads"

echo "==> [6/9] Build + publish API"
dotnet publish ecomm.api/ecomm.api.csproj -c Release -o "${WEB}/api"

echo "==> [7/9] Build frontend (subdomain-aware) + SSR"
sed -i "s|http://localhost:5080/api|https://${DOMAIN}/api|" ecomm.web/src/app/core/api.config.ts
sed -i "s|http://localhost:4200|https://${DOMAIN}|"        ecomm.web/src/app/core/api.config.ts
sed -i "s|\"allowedHosts\": \[.*\]|\"allowedHosts\": [\"localhost\",\"127.0.0.1\",\"${DOMAIN}\",\"www.${DOMAIN}\",\".${DOMAIN}\"]|" ecomm.web/angular.json
( cd ecomm.web && npm ci && npm run build )
mkdir -p "${WEB}/web"; rm -rf "${WEB}/web"/*; cp -r ecomm.web/dist/ecomm-web/* "${WEB}/web/"
mkdir -p "${WEB}/api/logs"; chown -R www-data:www-data "${WEB}"

echo "==> [8/9] systemd services"
cat > /etc/systemd/system/wavcomm-api.service <<UNIT
[Unit]
Description=WavCommerce Platform API
After=network.target mysql.service
[Service]
WorkingDirectory=${WEB}/api
ExecStart=/usr/bin/dotnet ${WEB}/api/ecomm.api.dll
EnvironmentFile=/etc/wavcomm/api.env
User=www-data
Restart=always
RestartSec=5
SyslogIdentifier=wavcomm-api
[Install]
WantedBy=multi-user.target
UNIT

cat > /etc/systemd/system/wavcomm-ssr.service <<UNIT
[Unit]
Description=WavCommerce Platform SSR
After=network.target
[Service]
WorkingDirectory=${WEB}/web
ExecStart=/usr/bin/node ${WEB}/web/server/server.mjs
Environment=PORT=${SSR_PORT}
Environment=HOST=127.0.0.1
User=www-data
Restart=always
RestartSec=5
SyslogIdentifier=wavcomm-ssr
[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable wavcomm-api wavcomm-ssr >/dev/null 2>&1 || true
systemctl restart wavcomm-api wavcomm-ssr

echo "==> [9/9] Nginx (wildcard server block)"
# Quoted heredoc keeps nginx $vars literal; placeholders are sed-substituted after.
cat > /etc/nginx/sites-available/wavcomm <<'NGINX'
server {
    listen 443 ssl;
    server_name __DOMAIN__ *.__DOMAIN__;

    ssl_certificate     /etc/letsencrypt/live/__DOMAIN__/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/__DOMAIN__/privkey.pem;

    client_max_body_size 25M;

    location /uploads/ { alias /var/www/wavcomm/uploads/; expires 30d; access_log off; try_files $uri =404; }

    location /hubs/ {
        proxy_pass http://127.0.0.1:__API_PORT__;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    location /api/ {
        proxy_pass http://127.0.0.1:__API_PORT__;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    location / {
        proxy_pass http://127.0.0.1:__SSR_PORT__;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
server {
    listen 80;
    server_name __DOMAIN__ *.__DOMAIN__;
    return 301 https://$host$request_uri;
}
NGINX
sed -i "s|__DOMAIN__|${DOMAIN}|g; s|__API_PORT__|${API_PORT}|g; s|__SSR_PORT__|${SSR_PORT}|g" /etc/nginx/sites-available/wavcomm
ln -sf /etc/nginx/sites-available/wavcomm /etc/nginx/sites-enabled/wavcomm
nginx -t && systemctl reload nginx

echo "==> Smoke test"
sleep 4
systemctl is-active wavcomm-api wavcomm-ssr || true
echo -n "health: "; curl -s "https://${DOMAIN}/api/health" || true; echo
echo -n "plans:  "; curl -s "https://${DOMAIN}/api/plans" | head -c 120 || true; echo
echo
echo "✅ Done. Visit https://${DOMAIN}/signup to create the first store."
echo "   Then https://{that-slug}.${DOMAIN}/admin to manage it."
echo "   ⚠️  Change the seeded admin (admin@ecommerce.local / Admin@123) on the wavcommerce DB."
