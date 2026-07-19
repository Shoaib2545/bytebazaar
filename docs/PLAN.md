# ByteBazaar — E-Commerce Platform Build Plan

Reference site: https://www.czone.com.pk/ (computer hardware / electronics store, Pakistan)

> Working name **ByteBazaar** (verify domain + SECP availability before branding).
> Alternatives considered: VoltCart, CoreMandi, TechNexa.

Hard requirements:
- Public storefront (catalog, cart, checkout, order tracking)
- Custom admin panel managing everything
- **Dynamic categories** — created/nested/reordered by admin, no code changes
- **Dynamic filters** — filter sidebar generated from admin-defined attributes per category

Timeline: ~19–20 weeks for one full-time full-stack developer (halve for two).

---

## 1. Tech Stack

| Layer | Choice | Notes |
|---|---|---|
| Backend API | **ASP.NET Core 10 Web API** (controllers) | LTS until Nov 2028; REST + Swagger/OpenAPI |
| ORM | **Entity Framework Core 10** + Npgsql | Migrations, LINQ, JSONB support |
| Database | **PostgreSQL** | JSONB + GIN indexes for dynamic attributes; transactions for orders/stock |
| Auth | **ASP.NET Core Identity + JWT** | Access token ~15 min + refresh token (httpOnly cookie); roles: Customer, Staff, Admin |
| Public storefront | **Next.js (React) + TypeScript + Tailwind CSS** | SSR/ISR for SEO — critical for "product X price in Pakistan" Google traffic |
| Admin panel | **React SPA (Vite) + TypeScript** + MUI or Ant Design + TanStack Query + React Hook Form + Zod | SEO irrelevant here; fastest CRUD building |
| API contract | **Generated TypeScript client from OpenAPI** (openapi-typescript / NSwag) | Regenerated in CI; renaming a C# field breaks frontend builds instead of production |
| Validation | **FluentValidation** (server), Zod (client) | Server never trusts the client |
| Background jobs | **Hangfire** | Emails/SMS, CSV imports, search reindexing, scheduled sales |
| Images | Cloudinary or S3-compatible + CDN | Uploaded via API |
| Search | Postgres full-text first → **Meilisearch** at M6 | .NET SDK available |
| Cache | IMemoryCache → **Redis** when needed | Category tree, homepage, hot queries |
| Payments | **COD first**, then PayFast / Safepay / JazzCash / Easypaisa | Webhooks with idempotency keys |
| Testing | xUnit + Testcontainers (API), Playwright (E2E) | |
| Deployment | **Docker Compose** (api + storefront + admin + postgres + redis) on VPS, or Azure | Nginx/Caddy reverse proxy, TLS |
| Monitoring | Serilog, Sentry (API + both frontends), uptime monitor | |
| Analytics | PostHog or GA4 | View → add-to-cart → checkout funnel |

### Repo layout (monorepo)

```
/backend
  src/ByteBazaar.Api            ← controllers, middleware, auth wiring, DI composition root
  src/ByteBazaar.Application    ← business rules: use cases/services, DTOs, FluentValidation
                                  validators, interfaces (IRepository, IEmailSender, IPaymentGateway…)
  src/ByteBazaar.Domain         ← entities, value objects, enums, domain events — no dependencies
  src/ByteBazaar.Infrastructure ← EF Core DbContext + migrations, repository implementations,
                                  external services (email, storage, payments, Meilisearch)
  tests/
/storefront                     ← Next.js public site
/admin                          ← Vite React SPA
/packages/api-client            ← generated TS client shared by both frontends
docker-compose.yml
```

Dependency direction (Clean Architecture): `Api → Application → Domain`, with
`Infrastructure` implementing Application's interfaces and referenced only by Api's
composition root. Domain references nothing; business rules in Application stay
testable without a database.

---

## 2. Core Design: Dynamic Categories & Filters

The schema itself is data. This model is what makes the requirements work:

```
Category            — Id, ParentId (self-ref tree), Name, Slug, ImageUrl, SortOrder,
                      IsActive, MetaTitle, MetaDescription
AttributeDefinition — Id, CategoryId, Name, Code, Type (Select/MultiSelect/Number/Boolean/Text),
                      OptionsJson, IsFilterable, IsRequired, FilterWidget, SortOrder
                      (inheritable by child categories)
Brand               — global, filterable everywhere
Product             — Id, CategoryId, BrandId, Name, Slug, Price, SalePrice, SaleStart/End,
                      Stock, Status (Draft/Active), AttributesJson (jsonb), SEO fields
ProductImage        — ProductId, Url, SortOrder
Cart / CartItem     — supports guest carts (anonymous cookie id), merged on login
Order / OrderItem   — status pipeline, snapshot of price at purchase time
Address, Banner, Coupon, User (Identity)
```

Implementation notes:
- `AttributesJson` → Postgres **jsonb** column (`HasColumnType("jsonb")`) with a **GIN index**.
- Filter endpoint composes `IQueryable` dynamically: loop the category's filterable
  AttributeDefinitions, append a JSONB containment predicate per query param present.
- Category tree: single query + in-memory tree build, cached in IMemoryCache.

Two endpoints power the entire dynamic-filter feature:
- `GET /api/categories/{slug}/filters` → filter sidebar definition (attributes + brands + price range, with counts)
- `GET /api/categories/{slug}/products?brand=asus&ram=16GB&price=50000-150000&sort=price&page=2`

The React sidebar renders whatever `/filters` returns. Admin adds an attribute → sidebar
changes. Zero frontend deploys. Filter state lives in URL search params (shareable,
crawlable, back-button-friendly).

---

## 3. Milestones

### Milestone 0 — Foundation (Week 1)
- Scaffold monorepo: ASP.NET Core 10 solution (Api / Application / Domain /
  Infrastructure projects with enforced dependency direction), Next.js storefront,
  Vite admin SPA.
- Docker Compose with Postgres (+ Redis stub for later).
- API plumbing: Serilog, global error-handling middleware, Swagger, health checks,
  **CORS configured for both frontend origins**.
- CI pipeline: build API → run tests → **generate TS API client from Swagger** →
  build both frontends → deploy to staging.
- **Done when:** `docker compose up` runs everything locally; a commit auto-deploys
  all three apps to staging.

### Milestone 1 — Data Model & Auth (Weeks 2–3)
- EF Core entities + initial migration for the full schema in §2.
- Seed script: 3-level category tree, ~50 products with JSONB attributes, brands.
- Identity + **JWT access tokens (~15 min) + refresh tokens in httpOnly cookies**;
  roles Customer/Staff/Admin; `[Authorize(Roles=...)]` policies.
- Both React apps: login pages, silent token-refresh interceptor in the shared API
  client, protected routes in admin.
- **Done when:** admin logs into admin SPA, customer into storefront, tokens refresh
  silently, Customer token gets 403 on admin endpoints.

### Milestone 2 — Admin Panel: Catalog Management (Weeks 4–6) ★ heart of the requirements
- **Category manager:** CRUD, drag-and-drop tree (dnd-kit), activate/deactivate,
  image upload, SEO fields.
- **Attribute manager:** per-category attribute CRUD (type, options, IsFilterable,
  widget, order) with inheritance from parent categories.
- **Brand manager:** CRUD + logo.
- **Product manager:** form fetches the selected category's attribute definitions and
  **renders fields dynamically** (React Hook Form dynamic fields; Zod schema built at
  runtime from definitions). Multi-image upload with drag-reorder. Draft/Active.
- CSV import/export (**Hangfire** job for large imports, with progress reporting).
- **Done when:** admin creates a brand-new category with brand-new filterable
  attributes and publishes products into it — zero code changes.

### Milestone 3 — Storefront: Catalog & Dynamic Filtering (Weeks 7–9)
- Layout: mega-menu from category-tree endpoint (cached + ISR), search bar, cart icon.
- Homepage: banners / featured / new / deals sections driven by admin-managed content.
- **Category page:** server-rendered product grid; filter sidebar rendered from
  `/filters`; filter state in URL params; client-side refetch on filter change
  (TanStack Query) for instant updates; sorting, pagination, filter value counts.
- Product detail: gallery, **spec table generated from attributes JSON**, related
  products, schema.org Product markup.
- Basic search (Postgres full-text) + results page.
- **ISR revalidation webhook:** admin product/category edits call Next.js revalidate
  so pages update without redeploy.
- **Done when:** adding an attribute in admin instantly produces a working,
  URL-addressable filter on the live category page, and page HTML source contains the
  full product grid (SEO check).

### Milestone 4 — Cart, Checkout & Orders (Weeks 10–12)
- Cart API: guest cart merged on login, stock validation.
- Checkout: PK address form (cities/regions), shipping options, **COD first**, then one
  gateway (PayFast/Safepay/JazzCash) — webhooks with idempotency keys.
- **Stock integrity:** decrement inside a DB transaction with row locking
  (`SELECT ... FOR UPDATE`); explicitly test two-buyers-one-unit race.
- Customer account: order history, tracking by status, addresses, wishlist.
- Admin order pipeline: pending → confirmed → shipped → delivered / cancelled /
  returned; status-change email/SMS via **Hangfire**.
- **Done when:** full purchase → fulfillment → customer sees "shipped"; oversell test
  passes; payment webhook replay is idempotent.

### Milestone 5 — Admin: Operations & Marketing (Weeks 13–14)
- Dashboard: sales, orders today, low-stock alerts, top products (aggregate endpoints
  + Recharts).
- Coupons; scheduled sale prices (start/end dates, applied by Hangfire job).
- Banner/homepage content manager; customer list; staff accounts with permissions.
- Reports: sales by period/category/brand, CSV export.
- **Done when:** the business runs a weekend sale end-to-end without a developer.

### Milestone 6 — Search, Performance & SEO (Weeks 15–16)
- **Meilisearch:** Hangfire-driven indexing on product changes; instant
  search-as-you-type in header; optionally move faceted filtering to it if Postgres
  filter queries slow down.
- **Redis** for hot data (category tree, homepage); ASP.NET Core output caching on
  heavy endpoints; Next.js image optimization; Core Web Vitals green on mobile.
- Auto-generated sitemap.xml; canonical URLs for filtered pages; admin-managed
  redirects.
- Analytics funnel: view → add-to-cart → checkout.
- **Done when:** Lighthouse mobile ≥ 90 on category pages; search suggests as you type.

### Milestone 7 — Testing, Security & Launch (Weeks 17–19)
- API integration tests with **Testcontainers** (real Postgres): filter composition,
  checkout, stock logic. Playwright E2E for money paths across both apps.
- Security: ASP.NET Core rate-limiting middleware on auth/checkout; FluentValidation
  on every endpoint; JWT hardening; CORS locked to real origins; dependency audit;
  Postgres backups **with an actual restore drill**.
- Load-test category/filter endpoints with realistic filter combinations.
- Production: Docker Compose (or Azure), TLS, Sentry, uptime monitoring.
  Soft launch → fix list → public launch.
- **Done when:** live with monitoring green, backup restored successfully at least
  once, E2E suite green in CI.

---

## 4. Sequencing Logic

Admin catalog management (M2) comes **before** the public catalog (M3) because the
storefront's filters are *generated from* admin-defined attributes — building the
public side first would force hardcoded filters rebuilt later. Checkout (M4) waits
until the catalog is real. Meilisearch and Redis are deliberately deferred to M6:
right tools at scale, premature infrastructure on day one.

First internal demo possible at end of M3 (~week 9).
