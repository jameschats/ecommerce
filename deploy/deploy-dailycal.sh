#!/usr/bin/env bash
#
# Deploy DailyCalendarShop to daily.calendarshop.online.
#
# Run from the repo root, in Git Bash on Windows or any POSIX shell:
#   ./deploy/deploy-dailycal.sh
#
# Design notes:
#   * Ships a tar stream over ssh rather than rsync — rsync is not available in Git Bash
#     on Windows, ssh and tar are.
#   * Extracts to <dir>.new, then swaps: <dir> -> <dir>.prev, <dir>.new -> <dir>.
#     The live directory is never in a half-written state, and the previous build is
#     always one `mv` away (see ROLLBACK at the bottom of the output).
#   * Touches only /var/www/dailycal/{api,web}. Never uploads/, never /etc/dailycal/api.env,
#     never the ecomm or wavcomm trees.

set -euo pipefail

# ---------------------------------------------------------------- configuration
REMOTE_HOST="${REMOTE_HOST:-root@62.72.59.84}"
SITE="dailycal"
BASE="/var/www/${SITE}"
DOMAIN="daily.calendarshop.online"
API_SERVICE="${SITE}-api"
SSR_SERVICE="${SITE}-ssr"

API_BASE_URL="https://${DOMAIN}/api"
SITE_URL="https://${DOMAIN}"
UMAMI_SRC=""
UMAMI_WEBSITE_ID=""
UMAMI_DASHBOARD_URL=""

# ---------------------------------------------------------------- safety guards
# The whole point: make it impossible to aim this at another site's tree.
case "$BASE" in
  /var/www/dailycal) ;;
  *) echo "REFUSING: BASE is '$BASE', expected /var/www/dailycal" >&2; exit 1 ;;
esac
for forbidden in /var/www/ecomm /var/www/wavcomm /var/www/html /etc; do
  case "$BASE" in
    "$forbidden"|"$forbidden"/*)
      echo "REFUSING: BASE '$BASE' is inside protected path '$forbidden'" >&2; exit 1 ;;
  esac
done

echo "──────────────────────────────────────────────────────────────"
echo " Deploying to : $REMOTE_HOST"
echo " Site         : https://$DOMAIN"
echo " Paths        : $BASE/api  and  $BASE/web   (uploads/ untouched)"
echo " Services     : $API_SERVICE, $SSR_SERVICE"
echo "──────────────────────────────────────────────────────────────"

# ---------------------------------------------------------------- build
echo "==> Building API"
rm -rf ./publish/api
dotnet publish ecomm.api/ecomm.api.csproj -c Release -o ./publish/api

echo "==> Building frontend"
( cd ecomm.web && npm ci && npm run build )

WEB_DIST="ecomm.web/dist/ecomm-web"
[ -d "$WEB_DIST/browser" ] || { echo "Missing $WEB_DIST/browser" >&2; exit 1; }
[ -d "$WEB_DIST/server" ]  || { echo "Missing $WEB_DIST/server — SSR bundle absent" >&2; exit 1; }

# Runtime config, written into the build we are about to ship (see deployment.md §4).
cat > "$WEB_DIST/browser/config.js" <<EOF
window.__APP_CONFIG__ = {
  apiBaseUrl: '${API_BASE_URL}',
  siteUrl:    '${SITE_URL}',
  umamiSrc: '${UMAMI_SRC}',
  umamiWebsiteId: '${UMAMI_WEBSITE_ID}',
  umamiDashboardUrl: '${UMAMI_DASHBOARD_URL}',
};
EOF

# ---------------------------------------------------------------- ship
echo "==> Uploading API"
ssh "$REMOTE_HOST" "rm -rf ${BASE}/api.new && mkdir -p ${BASE}/api.new"
tar -czf - -C ./publish/api . | ssh "$REMOTE_HOST" "tar -xzf - -C ${BASE}/api.new"

echo "==> Uploading frontend (browser/ + server/)"
ssh "$REMOTE_HOST" "rm -rf ${BASE}/web.new && mkdir -p ${BASE}/web.new"
tar -czf - -C "$WEB_DIST" . | ssh "$REMOTE_HOST" "tar -xzf - -C ${BASE}/web.new"

# ---------------------------------------------------------------- swap + restart
echo "==> Swapping in and restarting"
ssh "$REMOTE_HOST" bash -s <<EOF
set -euo pipefail
cd ${BASE}

rm -rf api.prev web.prev
[ -d api ] && mv api api.prev
[ -d web ] && mv web web.prev
mv api.new api
mv web.new web
chown -R www-data:www-data ${BASE}/api ${BASE}/web

systemctl restart ${API_SERVICE} ${SSR_SERVICE}
sleep 3
systemctl is-active ${API_SERVICE} ${SSR_SERVICE}
EOF

# ---------------------------------------------------------------- verify
echo "==> Verifying"
fail=0

health=$(curl -fsS --max-time 20 "https://${DOMAIN}/api/health/ready" || echo "FAILED")
echo "    health   : $health"
[ "$health" = "Healthy" ] || fail=1

cfg=$(curl -fsS --max-time 20 "https://${DOMAIN}/config.js" || echo "FAILED")
if echo "$cfg" | grep -q "localhost"; then
  echo "    config.js: STILL POINTS AT LOCALHOST"; fail=1
else
  echo "    config.js: ok"
fi

ssr=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "https://${DOMAIN}/")
echo "    ssr      : HTTP $ssr"
[ "$ssr" = "200" ] || fail=1

# The live neighbours must be untouched.
for neighbour in calendarshop.online wavcommerce.online; do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "https://${neighbour}/")
  echo "    $neighbour: HTTP $code"
  [ "$code" = "200" ] || fail=1
done

if [ "$fail" -ne 0 ]; then
  cat >&2 <<EOF

DEPLOY FAILED VERIFICATION.

ROLLBACK:
  ssh ${REMOTE_HOST} 'cd ${BASE} && rm -rf api web && mv api.prev api && mv web.prev web && \\
                      systemctl restart ${API_SERVICE} ${SSR_SERVICE}'
EOF
  exit 1
fi

echo "==> Deployed successfully to https://${DOMAIN}"
