# Build context is the repo root (docker-compose sets it that way).
FROM node:24-slim AS build
WORKDIR /src/ecomm.web
COPY ecomm.web/package.json ecomm.web/package-lock.json ./
RUN npm ci
COPY ecomm.web/. .
RUN npm run build

FROM node:24-slim AS runtime
WORKDIR /app
COPY --from=build /src/ecomm.web/dist/ecomm-web ./dist/ecomm-web

EXPOSE 4010
ENV PORT=4010
ENV HOST=0.0.0.0
# @angular/ssr's SSRF guard (NG_ALLOWED_HOSTS) and forwarded-header trust (NG_TRUST_PROXY_HEADERS)
# both default to reject-everything when unset. This app is genuinely multi-tenant with per-tenant
# custom domains (Tenant.CustomDomain), so a fixed hostname allowlist would be architecturally wrong
# - "*" is correct here because Nginx + TenantResolutionMiddleware are the real trust boundary this
# app relies on, not Angular's generic same-origin SSRF guard. Discovered live, 2026-08-21: the
# currently-deployed (pre-container) build predates whatever @angular/ssr version/behavior enforces
# this, so the next normal (non-container) deploy from current source would hit this exact same
# breakage regardless of containers - see the matching fix applied to the systemd unit.
ENV NG_ALLOWED_HOSTS=*
ENV NG_TRUST_PROXY_HEADERS=x-forwarded-host,x-forwarded-proto,x-forwarded-for

ENTRYPOINT ["node", "dist/ecomm-web/server/server.mjs"]
