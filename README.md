# ByteBazaar

An e-commerce platform for computer hardware / electronics with a public
storefront, a custom admin panel, **dynamic categories** (nested, admin-managed,
no code changes) and
**dynamic filters** (filter sidebar generated from admin-defined attributes per
category, stored as JSONB in PostgreSQL).

Full build plan, milestones, and data model: [docs/PLAN.md](docs/PLAN.md).

## Architecture

```
                          +---------------------------+
                          |        PostgreSQL 16      |
                          |  (Docker, host port 5433) |
                          |  JSONB attributes + GIN   |
                          +-------------^-------------+
                                        | EF Core 10 / Npgsql
                                        |
+---------------------+     +-----------+-----------+     +---------------------+
|     Storefront      |     |      Backend API      |     |     Admin Panel     |
|  Next.js + React    +----->  ASP.NET Core 10      <-----+  React SPA (Vite)   |
|  Tailwind CSS       | REST|  REST + Swagger       | REST|  Ant Design         |
|  localhost:3000     |     |  JWT + refresh cookie |     |  TanStack Query     |
+---------------------+     |  localhost:5080       |     |  localhost:5173     |
                            +-----------+-----------+     +---------------------+
                                        |
                          +-------------v-------------+
                          |   packages/api-client     |
                          | openapi-typescript types  |
                          | generated from Swagger    |
                          +---------------------------+
```

Monorepo layout:

| Path | Contents |
|---|---|
| `backend/` | ASP.NET Core 10 solution (`ByteBazaar.sln`): Api / Application / Domain / Infrastructure + xUnit tests |
| `storefront/` | Next.js public storefront (SSR, SEO) |
| `admin/` | Vite + React admin panel (Ant Design, TanStack Query) |
| `packages/api-client/` | Generated TypeScript API types (openapi-typescript) |
| `scripts/` | Dev utility scripts (e.g. `generate-api-client.ps1`) |
| `docs/PLAN.md` | Full build plan, data model, milestones |
| `.github/workflows/ci.yml` | CI: backend build+test, storefront build, admin build |

## Prerequisites

- **.NET SDK 10.0** (`dotnet --version`)
- **Node.js 24+** and npm
- **Docker Desktop** (for PostgreSQL and Redis)
- PowerShell or any shell

> **Port note:** the Docker PostgreSQL is published on host port **5433**
> (not 5432) because a native Windows PostgreSQL service commonly occupies
> 5432. All backend connection strings already point at `localhost:5433`.

## Running the stack

### 1. Database

```powershell
docker compose up -d postgres        # postgres:16 on localhost:5433
# optional: docker compose up -d redis
```

### 2. Backend API — http://localhost:5080

```powershell
dotnet run --project backend/src/ByteBazaar.Api
```

Migrations are applied and seed data (categories, brands, products, admin user)
is inserted automatically on startup. Swagger UI: http://localhost:5080/swagger

Run backend tests:

```powershell
dotnet test backend/ByteBazaar.sln
```

### 3. Storefront — http://localhost:3000

```powershell
cd storefront
npm install
npm run dev
```

### 4. Admin panel — http://localhost:5173

```powershell
cd admin
npm install
npm run dev
```

## Seeded admin credentials

| Field | Value |
|---|---|
| Email | `admin@bytebazaar.local` |
| Password | `Admin123$` |

Log in at the admin panel (http://localhost:5173) or via
`POST /api/auth/login`. Roles: `Admin` (full access), `Staff` (admin endpoints).

## API client generation

Typed API definitions live in `packages/api-client/src/schema.d.ts`, generated
from the running API's OpenAPI document:

```powershell
./scripts/generate-api-client.ps1     # API must be running on :5080
```

See [packages/api-client/README.md](packages/api-client/README.md).

## CI

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs on
every push / pull request to `main`:

- **backend** — .NET 10 restore, build, test against a `postgres:16` service container
- **storefront** — Node 24, `npm ci` + `npm run build`
- **admin** — Node 24, `npm ci` + `npm run build`
