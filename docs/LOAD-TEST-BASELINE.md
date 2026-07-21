# Load-test baseline — category & filter endpoints

Measured, not estimated. Both runs below were executed against the live
development stack on 2026-07-20 and the numbers are copied verbatim from k6.

## Tool choice: k6

[Grafana k6](https://github.com/grafana/k6) (Apache-2.0), run via the official
`grafana/k6` container so no local install is needed. Chosen over Artillery
because:

- **No paid tier anywhere on the path.** Artillery pushes percentile reporting
  and any distributed execution toward Artillery Cloud; k6's OSS binary emits
  full p50/p95/p99 locally.
- **Per-tag trend metrics.** We need latency broken out *per query shape*
  (unfiltered vs multi-facet), not one blended number. k6 custom `Trend`
  metrics give that directly.
- **Thresholds are first-class**, so the same script doubles as a CI perf gate
  (it exits non-zero when a percentile budget is breached).

Script: [`load/catalog-filters.js`](../load/catalog-filters.js)

## How to reproduce

```bash
# realistic run (output cache in play)
docker run --rm -i -v "$PWD/load:/scripts" -e VUS=20 -e DURATION=60s \
  grafana/k6 run /scripts/catalog-filters.js

# cold-path run (every request bypasses the output cache)
docker run --rm -i -v "$PWD/load:/scripts" -e VUS=20 -e DURATION=60s \
  -e CACHE_BUST=1 grafana/k6 run /scripts/catalog-filters.js
```

On Windows/Git Bash prefix with `MSYS_NO_PATHCONV=1` or the `/scripts` path gets
rewritten. `BASE_URL` defaults to `http://host.docker.internal:5080`.

## Scenario mix

Weighted to resemble real browsing — most traffic is an unfiltered or lightly
filtered category page; multi-facet drilldown is rarer but much more expensive
server-side.

| Scenario | VUs (of 20) | Shape |
|---|---|---|
| `unfiltered` | 7 (35%) | `GET /api/catalog/categories/{slug}/products` |
| `singleFacet` | 5 (25%) | one attribute, one value |
| `multiFacet` | 4 (20%) | 2–3 attributes, OR'd values within each, plus brand and/or price band ~50% of the time |
| `sortedPaged` | 2 (10%) | `sort=price_asc\|price_desc`, `page`, `pageSize`, sometimes a facet |
| `search` | 2 (10%) | `GET /api/search?q=…`, sometimes sorted/paged |

Categories exercised: `laptops`, `graphics-cards`, `processors`, using their real
seeded attribute codes and values (`processor`, `ram`, `storage`, `screen_size`,
`memory`, `chipset`, `cores`, `socket`).

## Environment

| | |
|---|---|
| Date | 2026-07-20 |
| API | ASP.NET Core 10, `dotnet run`, Development configuration, host process |
| Datastores | Postgres 16, Redis 7, Meilisearch 1.11 — Docker Desktop on Windows 11 |
| Catalog size | 9 categories, 18 attribute definitions, 32 products |
| Load generator | k6 in Docker on the **same machine** as the API |
| Duration | 60 s steady state, 20 VUs, no ramp |

> These are *relative* baselines, not capacity planning figures. The API runs in
> Development configuration on the same host as the load generator, and the
> catalog is 32 products — a production dataset of tens of thousands will shift
> the DB-bound numbers materially. Re-run against staging before launch.

---

## Run 1 — realistic (output cache active)

`/api/catalog/categories/{slug}/products` sits behind a 60-second ASP.NET output
cache keyed on the full query string, for anonymous requests only. This run
measures the mixed hit/miss path a real anonymous visitor sees.

```
checks_total.......: 86287   1435.993633/s
checks_succeeded...: 100.00% 86287 out of 86287
checks_failed......: 0.00%   0 out of 86287

lat_multi_facet....: avg=50.28ms min=1.69ms   med=52.61ms p(90)=77.98ms p(95)=88.8ms  p(99)=121.15ms max=261.58ms
lat_search.........: avg=22.59ms min=6.02ms   med=20.05ms p(90)=35.23ms p(95)=42.64ms p(99)=64.64ms  max=166.39ms
lat_single_facet...: avg=10.1ms  min=777.43µs med=8.19ms  p(90)=16.71ms p(95)=21.77ms p(99)=40.86ms  max=164.56ms
lat_sorted_paged...: avg=10.99ms min=81.6µs   med=8.39ms  p(90)=18.3ms  p(95)=27.79ms p(99)=52.19ms  max=159.95ms
lat_unfiltered.....: avg=10.11ms min=65.5µs   med=8.22ms  p(90)=16.65ms p(95)=21.79ms p(99)=39.79ms  max=485.43ms
malformed_body.....: 0.00% 0 out of 86287

http_req_duration..: avg=13.13ms min=65.5µs   med=8.8ms   p(90)=24.42ms p(95)=43.07ms p(99)=74.26ms  max=485.43ms
http_req_failed....: 0.00% 0 out of 86287
http_reqs..........: 86287 1435.993633/s
data_received......: 90 MB 1.5 MB/s
```

**Summary: 1436 RPS, 0.00% errors, overall p50 8.8 ms / p95 43.07 ms / p99 74.26 ms.**
All thresholds passed.

## Run 2 — cold path (`CACHE_BUST=1`, every request hits the database)

Each request carries a unique `_cb` parameter. Unknown query keys are silently
ignored by `CatalogQueryBinder` (verified: `?_cb=999` returns an identical
`totalCount`), so this varies the cache key only — the result sets are unchanged.

```
checks_total.......: 17940   298.567038/s
checks_succeeded...: 100.00% 17940 out of 17940
checks_failed......: 0.00%   0 out of 17940

lat_multi_facet....: avg=87.76ms min=26.77ms med=81.28ms p(90)=121.37ms p(95)=141.81ms p(99)=188ms    max=1.05s
lat_search.........: avg=71.93ms min=30.16ms med=66.21ms p(90)=102.38ms p(95)=120.3ms  p(99)=146.33ms max=232.06ms
lat_single_facet...: avg=59.86ms min=17.3ms  med=54.3ms  p(90)=88.06ms  p(95)=103.56ms p(99)=147.22ms max=1.01s
lat_sorted_paged...: avg=66.73ms min=17.38ms med=61.57ms p(90)=100.38ms p(95)=117.85ms p(99)=153.23ms max=249.44ms
lat_unfiltered.....: avg=59.31ms min=14.54ms med=54.45ms p(90)=88.13ms  p(95)=103.84ms p(99)=142.32ms max=1.22s
malformed_body.....: 0.00%  0 out of 17940

http_req_duration..: avg=65.6ms  min=14.54ms med=59.79ms p(90)=98.77ms  p(95)=116.11ms p(99)=155.69ms max=1.22s
http_req_failed....: 0.00%  0 out of 17940
http_reqs..........: 17940  298.567038/s
data_received......: 16 MB  273 kB/s
```

**Summary: 299 RPS, 0.00% errors, overall p50 59.79 ms / p95 116.11 ms / p99 155.69 ms.**

## Findings

1. **The output cache is worth 4.8× throughput.** 1436 RPS cached vs 299 RPS
   cold, with p95 improving 43 ms → 116 ms in the other direction. Anything that
   defeats it is expensive — see finding 3.

2. **Multi-facet filtering is the most expensive read path, as expected.**
   Cold p95 of 141.81 ms vs 103.84 ms unfiltered (≈1.4×), and cached p95 88.8 ms
   vs 21.79 ms (≈4×, because varied filter combos have far lower cache hit
   rates). The jsonb GIN index is doing its job — the gap would be far worse on
   a sequential scan — but this is the path to watch as the catalog grows.

3. **Authenticated traffic bypasses the cache entirely.** `CachePolicies.cs`
   only caches anonymous requests, so a logged-in customer browsing categories
   gets Run 2 numbers, not Run 1. With a large logged-in cohort, plan capacity
   against the 299 RPS figure.

4. **No errors and no malformed bodies in 104,227 requests** across both runs.
   Every response had a well-formed `items[]` + `totalCount`.

5. **Worst-case outliers reach ~1.2 s** on the cold path (`max=1.22s`). These
   are cold-start/JIT and connection-pool warmup artifacts in the first seconds
   of the run; p99 stays under 156 ms.

6. **`/api/search` is cheaper than multi-facet catalog filtering** (cold p95
   120.3 ms vs 141.81 ms) — Meilisearch is carrying that path well.

## Caveats and gaps

- **No rate limiting exists anywhere in the API.** `Program.cs` has no
  `AddRateLimiter`/`UseRateLimiter` and nothing throttles `/api/auth/login`,
  `/api/auth/register` or `/api/checkout`. Milestone 7 explicitly requires
  rate limiting on auth/checkout. That is why a single laptop could push 1436
  RPS unimpeded. **Blocking for launch** — see the runbook.
- The 32-product catalog is far too small to be predictive. Row counts drive
  both the filter predicate cost and the facet-count aggregation.
- Load generator and API shared a host, so measured latency includes no real
  network RTT and the two competed for CPU.
- The API ran in Development configuration (`dotnet run`), not a Release
  container.
- Cart/checkout write paths were **not** load-tested. They mutate stock inside
  transactions and are the likeliest place to find lock contention; they were
  left out deliberately to avoid polluting the live database with tens of
  thousands of orders.
