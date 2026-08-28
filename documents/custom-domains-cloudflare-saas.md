# Custom Domains + Automatic HTTPS (Cloudflare for SaaS)

**Status: LIVE, verified end-to-end 2026-08-28** with a real domain
(`store.cherrycommerce.online` → the bazaar store, valid Cloudflare cert, storefront over HTTPS).
Closes the Phase 6 Track D "custom-domain TLS" gap — merchants can connect their own domain and get an
auto-provisioned, auto-renewing HTTPS certificate. **Apex domains are supported but not yet tested — see
§5.**

---

## 1. How it works (request flow)

```
shopper → https://shop.merchant.com
        → (merchant's DNS: CNAME shop → saas-origin.wavcommerce.online, which is PROXIED)
        → Cloudflare edge (terminates TLS with the cert CF issued for shop.merchant.com)
        → fallback origin saas-origin.wavcommerce.online → the VPS (62.72.59.84)
        → nginx (wavcomm block is `default_server`, so it accepts the unknown Host)
        →  /api/ → API :5090  ·  everything else → SSR :4010
        → app resolves the tenant by Host = shop.merchant.com (Tenant.CustomDomain)
```

Cloudflare terminates the visitor's TLS and runs the whole certificate lifecycle (issue + auto-renew).
The origin only ever needs its existing wildcard `*.wavcommerce.online` cert (Cloudflare connects to the
fallback origin over that name in Full SSL mode).

## 2. What's provisioned

**Cloudflare (zone `wavcommerce.online`, Free plan — 100 custom hostnames free):**
- **Cloudflare for SaaS** enabled (SSL/TLS → Custom Hostnames).
- **Fallback origin** = `saas-origin.wavcommerce.online`, backed by a **proxied (orange) A record →
  62.72.59.84**. Status must be "active".
- API token = the same zone token in `deploy-wavcommerce.sh`, edited to add **Zone → SSL and
  Certificates → Edit** (on top of DNS edit). It can create/read/delete custom hostnames.

**Server (VPS):**
- `/etc/wavcomm/api.env` → `Cloudflare__ApiToken`, `Cloudflare__ZoneId`
  (`e31697f914ee5d1b3b88871acf34e1c6`), `Cloudflare__CnameTarget=saas-origin.wavcommerce.online`.
- **nginx** `wavcomm` block: `listen 443 ssl default_server;` + `listen 80 default_server;` — so requests
  arriving with an unknown (custom) Host are routed to the app instead of another site. No other enabled
  site claims default_server, so no conflict.
- **SSR** (`src-ssr-1` container): `NG_ALLOWED_HOSTS=*` (set in the VPS `docker-compose.override.yml` and
  baked in `ecomm.web/Dockerfile`). Angular SSR's host guard can't enumerate dynamic custom domains;
  `*.wavcommerce.online`-only 400'd every custom-domain storefront. Security boundary is nginx (public) +
  the API's tenant resolution (unknown host → no store → 404), not this guard.

**App (code):**
- `Features/Domains/CloudflareSaas.cs` — thin CF client (create / get / delete custom hostnames),
  config-gated (`CloudflareOptions`), no-op when unconfigured.
- `Features/Domains/DomainService.cs` — `Connect` creates the custom hostname; `Verify` marks the store
  live once the edge cert is `active`; `Disconnect` deletes it; status carries `SslStatus`. The old
  `/.well-known` token check remains the fallback when Cloudflare isn't configured.
- `admin-domain.component.ts` — shows the CNAME target + a "🔒 HTTPS active / Certificate: <status>" badge.

## 3. Merchant flow (what a store owner does)

1. Admin → **Custom domain** → enter their domain (a **subdomain** like `shop.` or `www.` — see §5 re apex).
   The app creates the Cloudflare custom hostname (SSL starts `initializing`).
2. At their DNS provider, add **CNAME `shop` → `saas-origin.wavcommerce.online`** (DNS-only if their DNS is
   on Cloudflare; plain CNAME otherwise).
3. Click **Verify**. Cloudflare validates control over HTTP and issues the cert
   (`pending_validation → active`, ~1–5 min). The store then serves on their domain over HTTPS.

## 4. Operational notes / gotchas

- The merchant's CNAME must point at `saas-origin.wavcommerce.online` (the fallback origin), **not** the
  platform apex. `Cloudflare__CnameTarget` drives what the admin UI tells them.
- If the merchant's own DNS is on Cloudflare, their CNAME must be **grey (DNS-only)** — proxying it
  ("orange-to-orange") breaks the SaaS routing.
- Cert stuck at `pending_validation`: the domain isn't resolving to Cloudflare yet (CNAME missing / not
  propagated). Check `dig +short <host>` returns Cloudflare IPs (104.21.x / 172.67.x).
- `/api/*` works but `/products` 400s on the custom host → SSR `NG_ALLOWED_HOSTS` isn't `*` (see §2).
- Inspect a custom hostname:
  `curl -H "Authorization: Bearer $TOKEN" ".../zones/$ZID/custom_hostnames?hostname=<host>"`.

## 5. Apex domains (`merchant.com`, no subdomain) — TODO, supported but untested

Cloudflare for SaaS supports apex domains. The only obstacle is DNS: **you can't CNAME a bare/apex
domain** in standard DNS, and **Namecheap basic DNS doesn't support ALIAS/flattening**. To use an apex:

1. Move the merchant's domain DNS to a provider with apex-CNAME support — **Cloudflare** (CNAME
   flattening), Route 53 (ALIAS), etc. On Cloudflare: add the zone, switch nameservers at the registrar.
2. Point the **apex** `merchant.com` → `saas-origin.wavcommerce.online` (grey). Cloudflare flattens it.
3. Register `merchant.com` (apex) as the custom hostname — same flow as a subdomain.

**Recommended merchant guidance:** subdomains (`shop.`/`www.`) work on *any* DNS with zero fuss; offer
apex as an advanced option (needs their DNS on Cloudflare/ALIAS), typically as `www` + an apex→www
redirect. **We deferred testing the apex path — revisit and verify it once needed.**

## 6. Test record still live
`store.cherrycommerce.online` is connected to the **bazaar** store (tenant 4) as the working proof.
Disconnect it (admin → Custom domain → Disconnect, which also deletes the CF custom hostname) when the
demo is no longer needed.
