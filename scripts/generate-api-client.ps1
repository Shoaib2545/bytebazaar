<#
.SYNOPSIS
    Regenerates packages/api-client/src/schema.d.ts from the running ByteBazaar API.

.DESCRIPTION
    Fetches the OpenAPI document from the API's Swagger endpoint and runs
    openapi-typescript to produce TypeScript types for the whole API surface.

    PREREQUISITE: the API must be running on http://localhost:5080.
        docker compose up -d postgres
        dotnet run --project backend/src/ByteBazaar.Api

.EXAMPLE
    ./scripts/generate-api-client.ps1
    ./scripts/generate-api-client.ps1 -SwaggerUrl http://localhost:5080/swagger/v1/swagger.json
#>
[CmdletBinding()]
param(
    [string]$SwaggerUrl = "http://localhost:5080/swagger/v1/swagger.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outFile = Join-Path $repoRoot "packages/api-client/src/schema.d.ts"

# Fail fast with a clear message if the API is not running.
try {
    Invoke-WebRequest -Uri $SwaggerUrl -Method Head -UseBasicParsing -TimeoutSec 5 | Out-Null
}
catch {
    Write-Error ("Cannot reach {0}. Start the API first:`n" -f $SwaggerUrl + `
        "  docker compose up -d postgres`n" + `
        "  dotnet run --project backend/src/ByteBazaar.Api")
}

Write-Host "Generating $outFile from $SwaggerUrl ..."
npx --yes openapi-typescript $SwaggerUrl -o $outFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "openapi-typescript failed with exit code $LASTEXITCODE"
}

Write-Host "Done. Review and commit the diff in packages/api-client/src/schema.d.ts."
