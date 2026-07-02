<#
.SYNOPSIS
    Regenerates every OpenAPI client recipe's `client/` directory and strict-type-checks the result.

.DESCRIPTION
    For each `petstore-*` recipe, runs the production CLI (`corvusjson openapi-client --engine TypeScript`)
    then type-checks the whole tree with `tsc`.

    The generated client imports the byte-native transport runtime from
    `@endjin/corvus-json-client-runtime`. To resolve it from the working tree without an install/publish
    step, each recipe points `--tsClientRuntimeModule` at the package's built `dist/index.js` via a
    relative path. The generated models import the model runtime (`./corvus-runtime.js`), re-emitted into
    each recipe's `client/models`; its third-party deps (`@js-temporal/polyfill`, `lossless-json`, `tr46`)
    resolve from this directory's `node_modules`.

    Cross-platform PowerShell (run with `pwsh`).
#>
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path # repo (worktree) root

Write-Host 'building CLI (Debug)...'
dotnet build (Join-Path $root 'src/Corvus.Json.Cli/Corvus.Json.Cli.csproj') -c Debug | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed' }
$dll = Join-Path $root 'src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll'

# Relative path from each recipe's client/ directory to the built client-runtime package entry.
$clientRuntime = '../../../../../packages/corvus-json-client-runtime/dist/index.js'

# Build the client-runtime package if its dist is missing.
$clientRuntimeDist = Join-Path $root 'packages/corvus-json-client-runtime/dist/index.js'
if (-not (Test-Path -Path $clientRuntimeDist)) {
    Write-Host 'building @endjin/corvus-json-client-runtime...'
    Push-Location (Join-Path $root 'packages/corvus-json-client-runtime')
    try {
        npm install | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'client-runtime npm install failed' }
        npm run build | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'client-runtime build failed' }
    }
    finally { Pop-Location }
}

foreach ($dir in Get-ChildItem -Path $here -Directory -Filter 'petstore-*' | Sort-Object Name) {
    $spec = Join-Path $dir.FullName 'openapi.json'
    Write-Host "  $($dir.Name)  <-  openapi.json"
    $client = Join-Path $dir.FullName 'client'
    Remove-Item -Recurse -Force -Path $client -ErrorAction SilentlyContinue
    dotnet $dll openapi-client $spec --engine TypeScript --outputPath $client --tsClientRuntimeModule $clientRuntime --force | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "generation failed for $($dir.Name)" }
}

Write-Host 'type-checking with tsc...'
Push-Location $here
try {
    npx tsc
    if ($LASTEXITCODE -ne 0) { throw 'tsc failed' }
}
finally { Pop-Location }
Write-Host 'OK - all recipes generated and strict-type-checked.'
