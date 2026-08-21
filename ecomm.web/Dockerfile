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
# both default to reject-everything when unset - discovered live during the v4 Phase 0 container
# cutover (2026-08-21) when every real request 400'd. Turned out this was already solved for the
# systemd deploy via two drop-in files (documents/deployment.md SS10-WAV) that this Dockerfile-baked
# version now matches, so both paths agree on the same allowlist rather than drifting apart.
# NOTE: this list does not cover per-tenant custom domains (Tenant.CustomDomain) - a pre-existing
# gap in the systemd config this just carries forward unchanged, not something introduced here.
ENV NG_ALLOWED_HOSTS=wavcommerce.online,*.wavcommerce.online,localhost,127.0.0.1
ENV NG_TRUST_PROXY_HEADERS=x-forwarded-for,x-forwarded-host,x-forwarded-proto

ENTRYPOINT ["node", "dist/ecomm-web/server/server.mjs"]
