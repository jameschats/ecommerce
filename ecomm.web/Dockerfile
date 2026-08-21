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

ENTRYPOINT ["node", "dist/ecomm-web/server/server.mjs"]
