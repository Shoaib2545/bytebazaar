# ByteBazaar admin — Vite React SPA, served as static files by Caddy.
#
#   docker build -f deploy/admin.Dockerfile -t bytebazaar-admin .
#
# Like Next.js, Vite inlines import.meta.env.VITE_* at build time, so
# VITE_API_URL is a BUILD ARG. Changing it requires rebuilding the image.
FROM node:24-alpine AS build
WORKDIR /app
COPY admin/package.json admin/package-lock.json ./
RUN npm ci
COPY admin/ ./

ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

# ---------- runtime ----------
FROM caddy:2-alpine AS runtime
COPY --from=build /app/dist /srv
COPY deploy/admin.Caddyfile /etc/caddy/Caddyfile
EXPOSE 8080
