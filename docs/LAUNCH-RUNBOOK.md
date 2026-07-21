# ByteBazaar — Launch Runbook

Operational reference for deploying, verifying, and rolling back ByteBazaar.

Companion documents:
- [`docs/LOAD-TEST-BASELINE.md`](LOAD-TEST-BASELINE.md) — measured performance baseline
- [`docker-compose.prod.yml`](../docker-compose.prod.yml) — production stack
- [`.env.prod.example`](../.env.prod.example) — every required variable

> **Status: this stack has never been deployed.** The compose file, Dockerfiles
> and Caddyfile are reviewed configuration only. Everything in "Known gaps"
> below must be closed before it carries real traffic.

---

## 1. Known gaps — read this first

| # | Gap | Severity | Owner | Status |
|---|---|---|---|---|
| 1 | **No `/health` endpoint.** | **Blocking** | backend | **CLOSED (M7).** `AddHealthChecks` + three `MapHealthChecks`. `/health/live` (process only), `/health/ready` (Postgres fatal; Redis + Meilisearch degrade to 200), `/health`. Compose, Caddy and CI now probe `/health/ready` or `/health/live`. |
| 2 | **No rate limiting.** | **Blocking** | backend | **CLOSED (M7).** `AddRateLimiter`/`UseRateLimiter`. Strict fixed-window policy on `/api/auth/{login,register,refresh}` and `POST /api/checkout`; looser global policy elsewhere; 429 + `Retry-After`. Partitioned by authenticated user id, else client IP. Limits configurable under `RateLimiting:*`. |
| 3 | **The API starts successfully when Postgres is unreachable.** | High | backend | **MITIGATED (M7).** Startup still degrades-and-logs by design (no crash-loop), but `/health/ready` now returns 503 until Postgres is reachable and migrated, so process-up and service-ready are distinguishable. |
| 4 | **`/api/catalog/search` is orphaned.** | Medium | backend | **CLOSED (M7).** Route removed from `CatalogController`. `CatalogService.SearchAsync` is retained — it is the Postgres fallback invoked by `SearchService` when Meilisearch is down, not dead code. `packages/api-client/src/schema.d.ts` still lists the path and should be regenerated from Swagger on the next client build. |
| 5 | **Storefront is not `output: "standalone"`.** | Low | storefront | Open. |
| 6 | **Backend never calls the storefront revalidation hook.** | Medium | backend | Open (out of M7 scope). |
| 7 | **Admin panel has no `data-testid` and no ARIA labels on row actions.** | Low | admin | Open. |

### Health endpoints (gap 1 — closed)

- **`GET /health/live`** — liveness. Returns 200 if the process is serving HTTP.
  Touches no datastore on purpose: a liveness probe that depended on Postgres
  would restart-loop the container during a database incident. This is the
  correct target for an external uptime monitor. Caddy exposes it publicly;
  `/health` and `/health/ready` are 404'd at the edge (they enumerate which
  dependency is down — reconnaissance).
- **`GET /health/ready`** — readiness. 200 when Postgres is reachable **and**
  migrated (Redis/Meilisearch may be `Degraded`, which is still 200 because both
  have working fallbacks); **503** when Postgres is unreachable or has pending
  migrations. This gates the compose healthcheck, Caddy's upstream `health_uri`,
  and the CI startup wait.
- **`GET /health`** — full per-check JSON for humans; same status semantics as
  readiness.

---

## 2. Required environment variables

Copy `.env.prod.example` → `.env.prod`, `chmod 600`, and fill every value.
`docker compose config` fails loudly if any REQUIRED variable is unset.

### API (ASP.NET Core — `__` is the nested-key separator)

| Variable | Required | Notes |
|---|---|---|
| `ConnectionStrings__Default` | yes | Composed in compose from `POSTGRES_*`. Internal hostname `postgres`, port 5432 (the 5433 mapping is a dev-host workaround only). |
| `ConnectionStrings__Redis` | yes | `redis:6379,password=…,abortConnect=false`. |
| `Meilisearch__Url` | yes | `http://meilisearch:7700` internally. |
| `Meilisearch__ApiKey` | yes | Must equal `MEILI_MASTER_KEY`. Minimum 16 bytes. |
| `Meilisearch__IndexName` | no | Defaults to `products`. |
| `Jwt__Key` | yes | **The value in `appsettings.json` is a published development key.** Anyone with it can forge admin tokens. `openssl rand -base64 64`. |
| `Jwt__Issuer` / `Jwt__Audience` | no | Default `ByteBazaar` / `ByteBazaar.Clients`. |
| `Jwt__AccessTokenMinutes` | no | Default 15. |
| `ASPNETCORE_URLS` | yes | Must be set — `appsettings.json` pins `Urls` to `localhost:5080`, which is unreachable from other containers. |

### Storefront (Next.js)

**Build-time (inlined into the client bundle — a rebuild is required to change them):**

| Variable | Required | Notes |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | yes | Public HTTPS API origin as the *browser* sees it. |
| `NEXT_PUBLIC_SITE_URL` | yes | Public storefront origin. Drives canonicals, OpenGraph URLs, `sitemap.xml`, `robots.txt`. **Left at localhost, every canonical tag ships pointing at localhost** — an SEO outage that is invisible until Google reindexes. |
| `NEXT_PUBLIC_IMAGE_HOSTS` | yes | Comma-separated hostnames allowed through the next/image optimizer. Must cover every host serving product/banner/category images; unlisted hosts silently degrade to unoptimized `<img>`. |
| `NEXT_PUBLIC_POSTHOG_KEY` | no | **Leave unset to disable analytics entirely** — with no key the instrumentation is a total no-op (no requests, no localStorage). |
| `NEXT_PUBLIC_POSTHOG_HOST` | no | Default `https://us.i.posthog.com`. Set for EU residency. |

**Runtime:**

| Variable | Required | Notes |
|---|---|---|
| `API_URL` | yes | Server-component API origin. Use the internal `http://api:8080` so SSR never leaves the docker network. |
| `REVALIDATE_SECRET` | yes | Shared secret for `POST /api/revalidate`. Defaults to `dev-secret` — an unauthenticated cache-purge DoS if left. |
| `REDIRECTS_TTL_MS` | no | Redirect-map cache lifetime, default `60000`. |

### Admin (Vite)

| Variable | Required | Notes |
|---|---|---|
| `VITE_API_URL` | yes | Build-time. Same public API origin. |

### Edge / infrastructure

`SITE_DOMAIN`, `API_DOMAIN`, `ADMIN_DOMAIN`, `ACME_EMAIL`,
`ADMIN_ALLOWED_CIDRS`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`,
`REDIS_PASSWORD`, `MEILI_MASTER_KEY` — all required, all documented in
`.env.prod.example`.

---

## 3. Pre-launch checklist

### Secrets & configuration
- [ ] `Jwt__Key` regenerated (**not** the appsettings default) and ≥64 random chars
- [ ] `POSTGRES_PASSWORD`, `REDIS_PASSWORD`, `MEILI_MASTER_KEY`, `REVALIDATE_SECRET` all freshly generated
- [ ] `.env.prod` is `chmod 600` and listed in `.gitignore`
- [ ] `NEXT_PUBLIC_SITE_URL` is the real domain — verify a rendered page's `<link rel="canonical">`
- [ ] `NEXT_PUBLIC_IMAGE_HOSTS` covers the real image CDN
- [ ] `ADMIN_ALLOWED_CIDRS` restricted to office/VPN ranges, not `0.0.0.0/0`
- [ ] CORS origins set: the "Frontends" policy is now driven from `Cors:AllowedOrigins` (compose passes `Cors__AllowedOrigins__0=$PUBLIC_SITE_URL` and `__1=https://$ADMIN_DOMAIN`). The API **refuses to start** outside Development with an empty list, and rejects `*` (the policy allows credentials). Development still defaults to `localhost:3000` / `localhost:5173`.

### Security
- [x] Gap 2 closed: rate limiting on `/api/auth/*` and `POST /api/checkout` (429 + `Retry-After`). Tune via `RateLimiting__Auth__PermitLimit` etc.; set `RateLimiting__Enabled=false` only for a deliberate load test.
- [ ] `Jwt__Key` regenerated — the API now **fails to boot** in a non-Development environment if `Jwt:Key` is missing, under 32 bytes, or still the published appsettings default.
- [ ] `ReverseProxy__Enabled=true` in production (set in `docker-compose.prod.yml`) so the rate limiter partitions on the real client IP via `X-Forwarded-For` rather than Caddy's address. **Leave it false anywhere the API is not behind a trusted proxy** — otherwise clients can spoof their IP past the limiter.
- [ ] Seeded `admin@bytebazaar.local` password changed or the account disabled
- [ ] `/swagger` unreachable publicly (the Caddyfile 404s it; confirm)
- [ ] `dotnet list package --vulnerable --include-transitive` and `npm audit --omit=dev` clean in all three apps
- [ ] Only ports 80/443 published — `docker compose -f docker-compose.prod.yml config | grep published`
- [ ] TLS verified: `curl -sSI https://$SITE_DOMAIN` returns HTTP/2 200 + HSTS

### Data
- [ ] `scripts/backup-postgres.sh` on a schedule (cron/systemd timer), off-host copy
- [ ] `scripts/restore-verify.sh` executed against a real backup — see §5
- [ ] Backup retention set (`RETENTION_DAYS`) and disk headroom checked
- [ ] Meilisearch index built: `POST /api/admin/search-index/reindex`

### Verification
- [ ] `dotnet test backend/ByteBazaar.sln` green
- [ ] E2E suite green against the target environment:
      `E2E_BASE_URL=https://$SITE_DOMAIN E2E_API_URL=https://$API_DOMAIN E2E_ADMIN_URL=https://$ADMIN_DOMAIN npx playwright test`
- [ ] Load test re-run against staging with a production-sized catalog; compare to `docs/LOAD-TEST-BASELINE.md`
- [ ] Manual COD order placed end to end and visible in the admin orders list
- [ ] Gap 1 closed, or the substitute probes above consciously accepted

### Observability
- [ ] Serilog output shipped somewhere durable (compose only keeps 5×10 MB per service)
- [ ] Sentry DSNs configured for API and both frontends
- [ ] External uptime monitor on `https://$SITE_DOMAIN` and the API
- [ ] Disk-space alert on the Postgres volume

---

## 4. Deploy

```bash
git pull --ff-only
cp .env.prod.example .env.prod   # first time only; then fill it in
chmod 600 .env.prod

# Fails loudly on any missing REQUIRED variable — always run this first.
docker compose -f docker-compose.prod.yml --env-file .env.prod config > /dev/null

# ALWAYS back up before deploying. The API applies EF migrations on startup.
scripts/backup-postgres.sh /var/backups/bytebazaar

docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
docker compose -f docker-compose.prod.yml --env-file .env.prod ps
```

Post-deploy smoke:

```bash
curl -fsS https://$API_DOMAIN/api/catalog/categories/tree | head -c 200
curl -fsSI https://$SITE_DOMAIN | head -5
curl -fsS -o /dev/null -w '%{http_code}\n' https://$API_DOMAIN/swagger   # expect 404
```

**First boot takes a few minutes:** Caddy provisions certificates via ACME, and
the API applies migrations and runs `DbSeeder` before it answers. `depends_on:
condition: service_healthy` sequences this, and the API healthcheck has a 60 s
`start_period`.

---

## 5. Backup & restore drill

Backups are worthless until a restore has been proven. Both scripts run from the
repo root and work under Linux and Git Bash.

```bash
# Take a backup (pg_dump custom format, integrity-checked via pg_restore -l)
scripts/backup-postgres.sh /var/backups/bytebazaar

# Prove it restores — into a THROWAWAY database, never over the live one
scripts/restore-verify.sh /var/backups/bytebazaar/bytebazaar-<stamp>.dump
```

`restore-verify.sh` refuses to target the live database, restores into
`bytebazaar_restore_verify`, then compares against the source: row counts for
Categories / AttributeDefinitions / Brands / Products / Orders / OrderItems /
AspNetUsers, foreign-key count, `__EFMigrationsHistory` count, presence of the
jsonb GIN index on `Products`, and a jsonb containment query (the dynamic filter
engine's actual access path). It drops the throwaway database on success unless
`KEEP_RESTORE=1`.

**Schedule:** nightly backup, off-host copy, and run the restore drill at least
monthly and before every migration-bearing deploy.

### Drill executed 2026-07-20 — PASSED

```
==> Dumping 'bytebazaar' from container 'bytebazaar-postgres'
==> OK  ./backups/bytebazaar-20260720T072303Z.dump
    size:        94846 bytes
    toc entries: 235

==> Restore drill
    dump:      ./backups/bytebazaar-20260720T072303Z.dump
    source db: bytebazaar (read-only)
    target db: bytebazaar_restore_verify (throwaway)

==> Source row counts
    Categories             9
    AttributeDefinitions   18
    Brands                 6
    Products               32
    Orders                 17
    OrderItems             19
    AspNetUsers            5

==> Restoring
    pg_restore exited 0

==> Verifying restored row counts
    OK   Categories             9
    OK   AttributeDefinitions   18
    OK   Brands                 6
    OK   Products               32
    OK   Orders                 17
    OK   OrderItems             19
    OK   AspNetUsers            5

==> Verifying schema objects
    OK   jsonb GIN index on Products present (1)
    OK   foreign keys: 18
    OK   EF migrations applied: 4

==> Spot-checking dynamic attributes (jsonb containment)
    OK   products with ram=16GB: 6

==> Dropping throwaway database

RESTORE DRILL PASSED
```

---

## 6. Rollback

### Decide fast

| Symptom | Action |
|---|---|
| Frontend broken, API and data fine | Roll back images only (§6.1) |
| API failing, no migration in this deploy | Roll back images only (§6.1) |
| API failing, deploy included a migration | Images + database (§6.2) |
| Data corruption / bad bulk edit | Restore from backup (§6.2) |
| Certificate or proxy failure only | Fix `deploy/Caddyfile`, `docker compose restart caddy` |

### 6.1 Application rollback (no schema change) — ~2 minutes

Fast because it skips a rebuild. This is why images should be tagged and pushed
to a registry rather than built on the host.

```bash
git checkout <last-known-good-tag>
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
docker compose -f docker-compose.prod.yml --env-file .env.prod ps
```

Verify with the §4 smoke commands.

> **Caution:** EF migrations are applied automatically on startup and are **not
> reversed** by checking out older code. If the bad deploy contained a
> migration, the old code now faces a newer schema. Use §6.2 instead.

### 6.2 Database rollback — expect 10–30 minutes and accept data loss

Everything written since the backup is lost. Announce downtime first.

```bash
# 1. Stop everything that writes, but keep the database up.
docker compose -f docker-compose.prod.yml --env-file .env.prod stop api storefront admin

# 2. Snapshot the CURRENT (broken) state before overwriting it — you may need it
#    for forensics, and it is your only way back if the restore also fails.
scripts/backup-postgres.sh /var/backups/bytebazaar/pre-rollback

# 3. Prove the target backup is good BEFORE destroying anything.
scripts/restore-verify.sh /var/backups/bytebazaar/bytebazaar-<good-stamp>.dump

# 4. Only after that passes, restore over the live database.
#    --clean --if-exists is baked into the dump, so it replaces existing objects.
docker compose -f docker-compose.prod.yml --env-file .env.prod exec -T postgres \
  pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  --clean --if-exists --no-owner --no-privileges --exit-on-error \
  < /var/backups/bytebazaar/bytebazaar-<good-stamp>.dump

# 5. Bring back the matching application version.
git checkout <last-known-good-tag>
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build

# 6. Rebuild the search index — Meilisearch is not covered by the Postgres dump.
curl -fsS -X POST https://$API_DOMAIN/api/admin/search-index/reindex \
  -H "Authorization: Bearer <admin-token>"
```

Then flush stale cache: `docker compose -f docker-compose.prod.yml --env-file .env.prod restart redis`
(Redis holds only cached catalog data, so this is safe).

### Post-rollback verification
- [ ] §4 smoke commands pass
- [ ] E2E suite green against production
- [ ] A test COD order completes and appears in admin
- [ ] Search returns results (confirms reindex worked)
- [ ] Incident written up with the trigger, timeline, and the check that would have caught it
