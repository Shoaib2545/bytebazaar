# ByteBazaar E2E (Playwright)

Two specs covering the paths where a regression costs money or credibility:

| Spec | Covers |
|---|---|
| `tests/customer-purchase.spec.ts` | browse category → apply a dynamic filter → product detail → add to cart → COD checkout → order confirmation → order visible in `/account/orders` |
| `tests/admin-dynamic-category.spec.ts` | admin creates a category + a filterable attribute + publishes a product, and the storefront filter sidebar renders and applies that filter — the platform's headline "no code change" requirement |

## Determinism

**These tests never assert against seed data.** Every spec provisions its own
category, attribute definition and products under a run-unique slug
(`e2e-…-<base36 timestamp><random>`), asserts against exactly those, and deletes
them in `afterAll`. A reseed, a repricing, or a product rename cannot turn them
red or — worse — falsely green.

Orders are deliberately *not* cleaned up: deleting a product an order references
would either fail on the foreign key or corrupt order history.

## Running

Needs the API, storefront, and (for the admin spec) the admin SPA running.

```bash
cd e2e
npm ci
npx playwright install chromium

npx playwright test                  # both specs
npm run test:customer                # storefront money path only (no admin SPA needed)
npm run test:admin
npx playwright test --headed --debug
npm run report                       # open the last HTML report
```

### Pointing at another environment

| Variable | Default |
|---|---|
| `E2E_BASE_URL` | `http://localhost:3000` (storefront; Playwright's `baseURL`) |
| `E2E_API_URL` | `http://localhost:5080` |
| `E2E_ADMIN_URL` | `http://localhost:5173` |
| `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` | `admin@bytebazaar.local` / `Admin123$` |

```bash
E2E_BASE_URL=https://bytebazaar.pk \
E2E_API_URL=https://api.bytebazaar.pk \
E2E_ADMIN_URL=https://admin.bytebazaar.pk \
npx playwright test
```

Traces, screenshots and video are retained on failure only
(`npx playwright show-trace test-results/<dir>/trace.zip`). CI uploads both as
artifacts.

## Selector notes

Neither frontend has a single `data-testid`, so selectors lean on roles, ARIA
labels, and literal text. Two things this suite learned the hard way:

- **The storefront filter sidebar renders options as `<a>` links**
  (`aria-label="Filter by 16GB"`) with a styled pseudo-checkbox `<span>`, not as
  `<input type="checkbox">`. Filtering works without client JS.
- **The admin runs Ant Design v6**, whose DOM differs from v5: the chosen value
  of a single Select lives in `.ant-select-content` (v5's
  `.ant-select-selection-item` now appears only in multiple/tags mode), and
  `ant-select-content-item` is a *substring* of `ant-select` — so any
  `contains(@class,"ant-select")` XPath silently matches the wrong node.

Both quirks are encapsulated in `fixtures/antd.ts` rather than repeated across
specs. Adding `data-testid` to the filter options, the admin product form fields
and the admin table row actions would make this suite considerably less brittle.
