# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ByteBazaar — an e-commerce platform (czone.com.pk-style PC hardware store) with a public storefront, admin panel, and **dynamic categories/filters**: admins define categories and their filterable attributes as data; the storefront filter sidebar and admin product forms are generated from those definitions at runtime. Build plan and milestone status: `docs/PLAN.md` and `README.md`.

## Commands

```powershell
docker compose up -d postgres                        # PostgreSQL 16 on localhost:5433 (NOT 5432 — see below)

# Backend (ASP.NET Core 10, http://localhost:5080, Swagger at /swagger)
dotnet run --project backend/src/ByteBazaar.Api      # applies migrations + seeds on startup
dotnet build backend/ByteBazaar.sln
dotnet test backend/ByteBazaar.sln
dotnet test backend/ByteBazaar.sln --filter "FullyQualifiedName~CatalogServiceTests.FilterByAttribute"   # single test
dotnet ef migrations add <Name> --project backend/src/ByteBazaar.Infrastructure --startup-project backend/src/ByteBazaar.Api   # dotnet-ef is a local tool (backend/dotnet-tools.json); run `dotnet tool restore` in backend/ first

# Storefront (Next.js, http://localhost:3000)
cd storefront; npm run dev        # npm run build to type-check + production build

# Admin (Vite React SPA, http://localhost:5173)
cd admin; npm run dev             # npm run build runs tsc + vite build

# Regenerate typed API client (API must be running on :5080)
./scripts/generate-api-client.ps1
```

Seeded admin login: `admin@bytebazaar.local` / `Admin123$`.

**Port gotcha:** this machine has a native Windows PostgreSQL service on 5432, so the Docker Postgres is published on **5433**. All backend connection strings already use 5433 — don't "fix" them back to 5432.

## Architecture

Three deployables in one monorepo, integrated only through the REST API contract:

- `backend/` — ASP.NET Core 10 Web API, Clean Architecture. Dependency rule: `Api → Application → Domain`; `Infrastructure` implements `Application/Abstractions/IAppDbContext.cs` and is referenced only by Api's composition root. Entities live in Domain, business rules in Application (services + FluentValidation validators), EF Core/Identity/migrations in Infrastructure.
- `storefront/` — Next.js (app router), server components fetch via `lib/api.ts`. All catalog pages use `force-dynamic` so builds succeed without the API running — keep that property when adding pages.
- `admin/` — Vite React SPA (Ant Design + TanStack Query + react-hook-form/zod). `src/lib/api.ts` holds the axios instance with 401→refresh→retry interceptor; `src/lib/types.ts` mirrors the API DTOs.

### The API contract is the coupling point

Storefront (`storefront/lib/api.ts`) and admin (`admin/src/lib/types.ts`) each hand-maintain TypeScript types that must match the backend DTOs (`backend/src/ByteBazaar.Application/DTOs/`). If you change a DTO or route, update all three plus `packages/api-client` (regenerate from Swagger). JSON is camelCase; enums serialize as strings ("Select", "Checkbox", "Active").

### Dynamic attribute/filter engine (the core feature — spans multiple files)

1. `AttributeDefinition` rows belong to a category (`type`, `options`, `isFilterable`, `filterWidget`); a category's *effective* definitions include its ancestors'.
2. `Product.Attributes` is a `Dictionary<string,string>` stored as Postgres **jsonb** (GIN-indexed) — mapped in `Infrastructure/Persistence/AppDbContext.cs` with a ValueConverter/ValueComparer.
3. `Application/Services/CatalogService.cs` builds the `/filters` response (attribute options + brands + price range with counts) and composes product queries from URL params: multi-values within an attribute are OR'd, attributes AND'd across.
4. Attribute predicates are **provider-specific** in `AppDbContext.BuildAttributeFilter`: jsonb containment on Npgsql, `ContainsKey` + indexer on the InMemory provider used by tests. Any change to attribute querying must keep both paths equivalent — `backend/tests/ByteBazaar.Tests/CatalogServiceTests.cs` covers the semantics.
5. Consumers render from data, never hardcode filters: `storefront/components/FilterSidebar.tsx` (filter state lives entirely in URL search params) and `admin/src/pages/ProductEditPage.tsx` (product form fields generated from the selected category's definitions, zod schema built at runtime).

### Auth flow

JWT access tokens (~15 min, `Api/Services/JwtTokenService.cs`) + rotating refresh tokens persisted in DB and delivered as httpOnly cookie `bb_refresh`. Both frontends call with `credentials: 'include'`/`withCredentials`. Admin endpoints require roles `Admin` or `Staff`. CORS policy "Frontends" allows localhost:3000 and localhost:5173 with credentials — new frontend origins must be added there (`Api/Program.cs`).

### Startup behavior

`Program.cs` applies migrations and runs `Infrastructure/Persistence/DbSeeder.cs` (roles, admin user, category tree, attribute definitions, brands, ~30 products) only when the DB is reachable; otherwise it logs a warning and still starts. Seeding is idempotent — safe to restart.

### Storefront revalidation hook

Admin-side catalog changes are meant to call `storefront/app/api/revalidate/route.ts` (`POST { path, secret }`, secret from `REVALIDATE_SECRET`). Wiring the backend to call it is still TODO (M3 leftover).
