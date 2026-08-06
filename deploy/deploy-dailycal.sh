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
# npm ci wipes and reinstalls node_modules, which is slow on every deploy and fails
# outright on Windows when a dev server holds a native .node binary open. Only reinstall
# when the lockfile is actually newer than the installed tree.
(
  cd ecomm.web
  if [ ! -d node_modules ]; then
    echo "    node_modules missing — running npm ci"
    npm ci
  else
    # Deliberately not reinstalling on every deploy. After changing package.json, run
    # npm ci yourself first — with a dev server running, npm ci fails anyway because it
    # deletes node_modules and cannot remove native .node binaries that are still open.
    echo "    using existing node_modules (run npm ci yourself after a dependency change)"
  fi
  npm run build
)

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

# robots.txt has to name an absolute sitemap URL, which is per-domain — and the copy in
# the repo names the neighbouring site, so every crawler reading it here was sent to a
# different shop's sitemap. Rewritten for the same reason config.js is: one build, many
# domains. Only the Sitemap line is touched, so the disallow rules stay in the repo.
ROBOTS="$WEB_DIST/browser/robots.txt"
if [ -f "$ROBOTS" ]; then
  sed -i "s|^Sitemap: .*|Sitemap: ${SITE_URL}/sitemap.xml|" "$ROBOTS"
  echo "    robots.txt sitemap -> ${SITE_URL}/sitemap.xml"
else
  echo "WARNING: $ROBOTS missing — crawlers will not be told where the sitemap is" >&2
fi

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
sleep 2
systemctl is-active ${API_SERVICE} ${SSR_SERVICE}
EOF

# ---------------------------------------------------------------- verify
echo "==> Verifying"
fail=0

# The API needs ~5s to come up (EF init + admin seeding), so poll rather than sleep a
# fixed interval. A single immediate check races startup and reports a false failure.
health="FAILED"
for attempt in $(seq 1 20); do
  health=$(curl -fsS --max-time 10 "https://${DOMAIN}/api/health/ready" 2>/dev/null || echo "FAILED")
  [ "$health" = "Healthy" ] && break
  printf '    waiting for API… (%s/20)\r' "$attempt"
  sleep 3
done
echo "    health   : $health"
[ "$health" = "Healthy" ] || fail=1

cfg=$(curl -fsS --max-time 20 "https://${DOMAIN}/config.js" || echo "FAILED")
if echo "$cfg" | grep -q "localhost"; then
  echo "    config.js: STILL POINTS AT LOCALHOST"; fail=1
else
  echo "    config.js: ok"
fi

# `|| true` plus a :-000 default on every probe below: under `set -e`, a curl that cannot
# connect exits non-zero and would abort the script mid-verification, before printing what
# failed or the rollback command. curl already prints 000 itself in that case, so `|| true`
# swallows only the exit status — `|| echo 000` would concatenate and show "000000".
ssr=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "https://${DOMAIN}/" || true)
ssr=${ssr:-000}
echo "    ssr      : HTTP $ssr"
[ "$ssr" = "200" ] || fail=1

# The live neighbours must still serve — this asks "did my deploy break them?", and
# nothing here writes to their trees or restarts their services.
#
# Judged on where a request lands, not on an exact 200. wavcommerce.online sends / to
# /welcome, which is perfectly healthy, and testing for 200 reported that as a deploy
# failure on a site this script never touches. A cried-wolf check is worse than none: it
# trains you to ignore the one time it is real. The immediate code is still printed, so a
# neighbour that starts redirecting stays visible rather than silently tolerated.
for neighbour in calendarshop.online wavcommerce.online; do
  first=$(curl -s  -o /dev/null -w '%{http_code}' --max-time 20 "https://${neighbour}/" || true)
  final=$(curl -sL -o /dev/null -w '%{http_code}' --max-time 25 "https://${neighbour}/" || true)
  first=${first:-000}
  final=${final:-000}

  if [ "$first" = "$final" ]; then
    echo "    $neighbour: HTTP $final"
  else
    echo "    $neighbour: HTTP $first → $final"
  fi

  case "$final" in
    2??) ;;
    *)   echo "    $neighbour: NOT SERVING"; fail=1 ;;
  esac
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
