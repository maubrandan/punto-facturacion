# syntax=docker/dockerfile:1
FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY pos-frontend/package.json pos-frontend/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY pos-frontend/ ./
RUN npm run build

FROM nginx:1.27-alpine
COPY deploy/docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/pos-frontend/browser /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD wget -qO- http://127.0.0.1/ >/dev/null || exit 1
