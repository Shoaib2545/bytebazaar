# @bytebazaar/api-client

TypeScript type definitions for the ByteBazaar REST API, generated from the API's
OpenAPI (Swagger) document using [openapi-typescript](https://github.com/openapi-ts/openapi-typescript).

The generated file is `src/schema.d.ts`. It contains the `paths` / `components`
interfaces describing every endpoint, request body, and response shape exposed by
the backend, so the storefront and admin frontends can consume the API with
compile-time safety. Renaming a C# DTO field then breaks the frontend build in CI
instead of breaking production.

## Prerequisites

**The API must be running** before you can (re)generate the types, because the
generator fetches the live OpenAPI document from the API's Swagger endpoint:

```
http://localhost:5080/swagger/v1/swagger.json
```

Start the API first:

```powershell
docker compose up -d postgres          # from the repo root
dotnet run --project backend/src/ByteBazaar.Api
```

## Generating the types

From this directory:

```powershell
npm run generate
```

or without installing anything:

```powershell
npx openapi-typescript http://localhost:5080/swagger/v1/swagger.json -o src/schema.d.ts
```

or from the repo root using the wrapper script:

```powershell
./scripts/generate-api-client.ps1
```

## Usage

```ts
import type { paths, components } from "@bytebazaar/api-client";

type ProductDetail =
  paths["/api/catalog/products/{slug}"]["get"]["responses"]["200"]["content"]["application/json"];
```

Pair with a typed fetch wrapper such as
[openapi-fetch](https://github.com/openapi-ts/openapi-typescript/tree/main/packages/openapi-fetch)
if you want fully typed request/response calls.

## Notes

- `src/schema.d.ts` is committed to the repo so consumers do not need a running
  API just to build. Regenerate it whenever the backend contract changes and
  commit the diff.
- If the committed file still contains the placeholder banner, it has not been
  generated yet — start the API and run the generate script.
