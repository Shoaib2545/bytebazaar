# ByteBazaar storefront — Next.js (app router)
#
#   docker build -f deploy/storefront.Dockerfile -t bytebazaar-storefront .
#
# IMPORTANT — NEXT_PUBLIC_* are compile-time constants.
# Next.js inlines every NEXT_PUBLIC_* value into the client bundle during
# `next build`. They must therefore be supplied as BUILD ARGS, not just as
# runtime env vars: setting NEXT_PUBLIC_API_URL only in docker-compose
# `environment:` will NOT change what the browser bundle points at. Rebuilding
# the image is the only way to change them. Server-only vars (API_URL,
# REVALIDATE_SECRET, REDIRECTS_TTL_MS) ARE read at runtime and belong in
# `environment:`.
FROM node:24-alpine AS deps
WORKDIR /app
COPY storefront/package.json storefront/package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY storefront/ ./

ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_SITE_URL
ARG NEXT_PUBLIC_IMAGE_HOSTS
ARG NEXT_PUBLIC_POSTHOG_KEY
ARG NEXT_PUBLIC_POSTHOG_HOST
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL \
    NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL \
    NEXT_PUBLIC_IMAGE_HOSTS=$NEXT_PUBLIC_IMAGE_HOSTS \
    NEXT_PUBLIC_POSTHOG_KEY=$NEXT_PUBLIC_POSTHOG_KEY \
    NEXT_PUBLIC_POSTHOG_HOST=$NEXT_PUBLIC_POSTHOG_HOST \
    NEXT_TELEMETRY_DISABLED=1

RUN npm run build

# ---------- runtime ----------
# NOTE: storefront/next.config.ts does not set `output: "standalone"`, so we
# cannot ship the minimal standalone bundle and must carry node_modules. Adding
# `output: "standalone"` to next.config.ts would cut this image by roughly
# 250-400 MB — that file is owned by the storefront app, so it is flagged in
# docs/LAUNCH-RUNBOOK.md as a follow-up rather than changed here.
FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000

RUN apk add --no-cache curl

COPY --from=build /app/package.json /app/package-lock.json ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/next.config.ts ./

USER node
EXPOSE 3000
CMD ["npm", "run", "start"]
