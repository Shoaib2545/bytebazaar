/*
 * ByteBazaar — load test for the dynamic category/filter endpoints.
 *
 * Tool: k6 (Grafana k6, OSS, Apache-2.0). Chosen over Artillery because:
 *   - the whole tool is a single static binary / official `grafana/k6` image, so it
 *     runs with zero paid account and zero cloud dependency (Artillery's percentile
 *     reporting and any distributed run push you toward Artillery Cloud);
 *   - built-in per-tag trend metrics give us p50/p95/p99 per *scenario* out of the
 *     box, which is exactly what we need to compare unfiltered vs multi-facet;
 *   - thresholds are first-class, so the same script doubles as a CI perf gate.
 *
 * Run (no local install needed — Docker only):
 *   docker run --rm -i -v "${PWD}:/scripts" grafana/k6 run /scripts/catalog-filters.js
 * or with a local k6 binary:
 *   k6 run load/catalog-filters.js
 *
 * Env vars:
 *   BASE_URL    API root            (default http://host.docker.internal:5080)
 *   VUS         virtual users       (default 20)
 *   DURATION    steady-state length (default 60s)
 *   CACHE_BUST  "1" to append a unique `_cb` param to every request
 *
 * NOTE ON OUTPUT CACHING: /categories/{slug}/products is wrapped in a 60 s
 * ASP.NET output cache keyed on the full query string (anonymous requests only).
 * A default run therefore measures the *realistic* mixed hit/miss path. Set
 * CACHE_BUST=1 to force every request to the cold DB path and measure the true
 * jsonb-filter cost. Unknown query keys are silently ignored by CatalogQueryBinder
 * (verified: `?_cb=999` returns the same totalCount), so `_cb` only varies the
 * cache key — it does not change the result set.
 */
import http from 'k6/http';
import { check } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://host.docker.internal:5080';
const VUS = parseInt(__ENV.VUS || '20', 10);
const DURATION = __ENV.DURATION || '60s';

// Per-shape latency trends so we can report p50/p95/p99 per query shape,
// not just one blended number.
const tUnfiltered = new Trend('lat_unfiltered', true);
const tSingle = new Trend('lat_single_facet', true);
const tMulti = new Trend('lat_multi_facet', true);
const tSorted = new Trend('lat_sorted_paged', true);
const tSearch = new Trend('lat_search', true);
const badBody = new Rate('malformed_body');

// Mirrors the seeded catalog. Attribute codes are passed bare (no prefix) and
// comma-separated values are OR'd within an attribute, AND'd across attributes —
// see backend/src/ByteBazaar.Api/CatalogQueryBinder.cs.
const CATEGORIES = [
  {
    slug: 'laptops',
    brands: ['asus', 'dell', 'hp', 'lenovo', 'msi'],
    price: [154900, 545000],
    attrs: {
      processor: ['Intel Core i5', 'Intel Core i7', 'Intel Core i9', 'AMD Ryzen 5', 'AMD Ryzen 7'],
      ram: ['8GB', '16GB', '32GB'],
      storage: ['256GB SSD', '512GB SSD', '1TB SSD'],
      screen_size: ['13.3"', '14"', '15.6"', '16"'],
    },
  },
  {
    slug: 'graphics-cards',
    brands: ['asus', 'gigabyte', 'msi'],
    price: [98000, 235000],
    attrs: {
      memory: ['8GB', '12GB', '16GB'],
      chipset: [
        'NVIDIA GeForce RTX 4060',
        'NVIDIA GeForce RTX 4070',
        'AMD Radeon RX 7600',
        'AMD Radeon RX 7800 XT',
      ],
    },
  },
  {
    slug: 'processors',
    brands: ['asus', 'gigabyte', 'msi'],
    price: [78000, 145000],
    attrs: {
      cores: ['6', '8', '12', '16'],
      socket: ['LGA1700', 'AM5'],
    },
  },
];

const SEARCH_TERMS = ['rtx', 'ryzen', 'laptop', 'ssd', 'gaming', 'intel', 'monitor', '4070'];

function pick(a) {
  return a[Math.floor(Math.random() * a.length)];
}
function pickN(a, n) {
  const c = [...a];
  const out = [];
  while (out.length < n && c.length) out.push(c.splice(Math.floor(Math.random() * c.length), 1)[0]);
  return out;
}
const CACHE_BUST = __ENV.CACHE_BUST === '1';
let cbSeq = 0;

function qs(params) {
  if (CACHE_BUST) params = { ...params, _cb: `${__VU}-${++cbSeq}-${Date.now()}` };
  return Object.entries(params)
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join('&');
}

// `name` keeps k6's URL grouping sane (otherwise every distinct filter combo
// becomes its own metric row).
function get(url, name, trend) {
  const res = http.get(url, { tags: { name } });
  trend.add(res.timings.duration);
  const ok = check(res, {
    'status 200': (r) => r.status === 200,
  });
  if (ok) {
    try {
      const j = res.json();
      const good = Array.isArray(j.items) && typeof j.totalCount === 'number';
      badBody.add(!good);
    } catch (e) {
      badBody.add(true);
    }
  } else {
    badBody.add(true);
  }
  return res;
}

export const options = {
  discardResponseBodies: false,
  scenarios: {
    // Weighted to look like real browsing: most traffic is an unfiltered or
    // lightly-filtered category page; multi-facet drilldown and search are rarer
    // but far more expensive server-side (jsonb containment + count).
    unfiltered: { executor: 'constant-vus', vus: Math.max(1, Math.round(VUS * 0.35)), duration: DURATION, exec: 'unfiltered' },
    singleFacet: { executor: 'constant-vus', vus: Math.max(1, Math.round(VUS * 0.25)), duration: DURATION, exec: 'singleFacet' },
    multiFacet: { executor: 'constant-vus', vus: Math.max(1, Math.round(VUS * 0.2)), duration: DURATION, exec: 'multiFacet' },
    sortedPaged: { executor: 'constant-vus', vus: Math.max(1, Math.round(VUS * 0.1)), duration: DURATION, exec: 'sortedPaged' },
    search: { executor: 'constant-vus', vus: Math.max(1, Math.round(VUS * 0.1)), duration: DURATION, exec: 'search' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    malformed_body: ['rate<0.01'],
    'lat_unfiltered': ['p(95)<300'],
    'lat_single_facet': ['p(95)<400'],
    'lat_multi_facet': ['p(95)<600'],
    'lat_sorted_paged': ['p(95)<400'],
    'lat_search': ['p(95)<500'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function unfiltered() {
  const c = pick(CATEGORIES);
  const q = qs({});
  get(`${BASE}/api/catalog/categories/${c.slug}/products${q ? '?' + q : ''}`, 'GET /categories/{slug}/products [unfiltered]', tUnfiltered);
}

export function singleFacet() {
  const c = pick(CATEGORIES);
  const code = pick(Object.keys(c.attrs));
  const q = qs({ [code]: pick(c.attrs[code]) });
  get(`${BASE}/api/catalog/categories/${c.slug}/products?${q}`, 'GET /categories/{slug}/products [1 facet]', tSingle);
}

export function multiFacet() {
  const c = pick(CATEGORIES);
  const codes = pickN(Object.keys(c.attrs), Math.min(2, Object.keys(c.attrs).length) + (Math.random() < 0.4 ? 1 : 0));
  const params = {};
  for (const code of codes) {
    // Multi-select within one attribute (OR) on ~half the requests.
    const vals = Math.random() < 0.5 ? pickN(c.attrs[code], 2) : [pick(c.attrs[code])];
    params[code] = vals.join(',');
  }
  // Realistic combos usually pin a brand and/or a price band too.
  if (Math.random() < 0.5) params.brand = pickN(c.brands, 2).join(',');
  if (Math.random() < 0.5) {
    const [lo, hi] = c.price;
    const a = Math.round(lo + Math.random() * (hi - lo) * 0.4);
    params.price = `${a}-${hi}`;
  }
  get(`${BASE}/api/catalog/categories/${c.slug}/products?${qs(params)}`, 'GET /categories/{slug}/products [multi facet]', tMulti);
}

export function sortedPaged() {
  const c = pick(CATEGORIES);
  const params = { sort: pick(['price_asc', 'price_desc']), page: pick([1, 1, 2]), pageSize: pick([12, 24]) };
  if (Math.random() < 0.5) {
    const code = pick(Object.keys(c.attrs));
    params[code] = pick(c.attrs[code]);
  }
  get(`${BASE}/api/catalog/categories/${c.slug}/products?${qs(params)}`, 'GET /categories/{slug}/products [sorted+paged]', tSorted);
}

export function search() {
  const params = { q: pick(SEARCH_TERMS) };
  if (Math.random() < 0.35) params.sort = pick(['price_asc', 'price_desc']);
  if (Math.random() < 0.25) params.page = 2;
  get(`${BASE}/api/search?${qs(params)}`, 'GET /search', tSearch);
}
