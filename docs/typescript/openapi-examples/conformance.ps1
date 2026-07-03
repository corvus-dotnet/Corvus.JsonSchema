<#
.SYNOPSIS
    Generates, compiles, and runs the conformance suite for the generated TypeScript OpenAPI clients, strict-type-checks the browser playground samples against their generated clients, and exercises FetchApiTransport.

.DESCRIPTION
    The recipes under `petstore-*/client` import the runtime via a RELATIVE path to the package's built
    `dist/index.js` so they type-check in place (see regenerate.ps1). To EXECUTE the generated client
    under Node we instead generate a parallel copy whose runtime import is the package NAME
    (`@endjin/corvus-json-client-runtime`), resolved via a `node_modules` symlink — exactly how a real
    consumer imports the published package. That decouples execution from the recipe's on-disk depth.

    Steps: build the CLI, build the runtime, link it into node_modules, regenerate each recipe's client
    into `conformance/<version>/client` with the package-name runtime import, compile that tree to
    `conformance/dist`, then run the node:test conformance suite against the compiled clients.

    Cross-platform PowerShell (run with `pwsh`).
#>
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path # repo (worktree) root

Write-Host 'building CLI (Debug)...'
dotnet build (Join-Path $root 'src/Corvus.Json.Cli/Corvus.Json.Cli.csproj') -c Debug | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed' }
$dll = Join-Path $root 'src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll'

# Build the client-runtime package and link it as @endjin/corvus-json-client-runtime in node_modules.
Write-Host 'building @endjin/corvus-json-client-runtime...'
Push-Location (Join-Path $root 'packages/corvus-json-client-runtime')
try {
    npm install 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'client-runtime npm install failed' }
    npm run build | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'client-runtime build failed' }
}
finally { Pop-Location }

$endjinDir = Join-Path $here 'node_modules/@endjin'
New-Item -ItemType Directory -Force -Path $endjinDir | Out-Null
$link = Join-Path $endjinDir 'corvus-json-client-runtime'
# Delete only the existing symlink (never -Recurse a link — that would delete the target's contents).
$existingLink = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
if ($existingLink) { $existingLink.Delete() }
New-Item -ItemType SymbolicLink -Path $link -Target (Join-Path $root 'packages/corvus-json-client-runtime') | Out-Null

# Generate the executable client copy with the package-name runtime import.
$conformance = Join-Path $here 'conformance'
Remove-Item -Recurse -Force -Path $conformance -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $conformance | Out-Null
# The `tr46` ambient declaration the model runtime needs (mirrors the recipe's root globals.d.ts), kept
# inside the conformance tree so it sits under the conformance tsconfig's rootDir.
Copy-Item -Path (Join-Path $here 'globals.d.ts') -Destination (Join-Path $conformance 'globals.d.ts')

foreach ($dir in Get-ChildItem -Path $here -Directory -Filter 'petstore-*' | Sort-Object Name) {
    $version = $dir.Name
    $spec = Join-Path $dir.FullName 'openapi.json'
    $out = Join-Path $conformance "$version/client"
    Write-Host "  conformance/$version  <-  $version/openapi.json"
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    dotnet $dll openapi-client $spec --engine TypeScript --outputPath $out --tsClientRuntimeModule '@endjin/corvus-json-client-runtime' --force | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "generation failed for $version" }
}

Push-Location $here
try {
    Write-Host 'compiling conformance clients...'
    npx tsc -p tsconfig.conformance.json
    if ($LASTEXITCODE -ne 0) { throw 'conformance tsc failed' }

    # ── Playground OpenAPI-TS sample type-checks ──
    # The browser playground runs each sample's usercode.ts via esbuild (transpile only, NO type-check), so a
    # sample whose call does not match its generated client's API fails SILENTLY at runtime (e.g. omitting a
    # cookie-parameter argument sends an empty body). Generate each playground sample's client from its spec and
    # strict-tsc the usercode against it — the gate that catches sample/API drift the esbuild path cannot.
    Write-Host 'type-checking playground OpenAPI-TS samples...'
    $samplesRoot = Join-Path $root 'docs/playground-openapi-typescript/src/Corvus.Text.Json.OpenApi.TypeScript.Playground/Samples'
    $sampleOut = Join-Path $here 'sample-typecheck'
    Remove-Item -Recurse -Force -Path $sampleOut -ErrorAction SilentlyContinue
    try {
        foreach ($dir in Get-ChildItem -Path $samplesRoot -Directory | Sort-Object Name) {
            $spec = Join-Path $dir.FullName 'openapi.json'
            $user = Join-Path $dir.FullName 'usercode.ts'
            if (-not (Test-Path $spec) -or -not (Test-Path $user)) { continue }
            $out = Join-Path $sampleOut $dir.Name
            New-Item -ItemType Directory -Force -Path $out | Out-Null
            dotnet $dll openapi-client $spec --engine TypeScript --outputPath $out --tsClientRuntimeModule '@endjin/corvus-json-client-runtime' --force | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "sample client generation failed for $($dir.Name)" }
            Copy-Item $user (Join-Path $out 'usercode.ts')
            Copy-Item (Join-Path $here 'globals.d.ts') (Join-Path $out 'globals.d.ts')
            Set-Content -Path (Join-Path $out 'tsconfig.json') -Value '{ "compilerOptions": { "strict": true, "exactOptionalPropertyTypes": true, "target": "es2022", "module": "esnext", "moduleResolution": "bundler", "lib": ["es2023", "dom", "esnext.disposable"], "noEmit": true, "skipLibCheck": true }, "include": ["globals.d.ts", "usercode.ts"] }'
            Write-Host "  sample: $($dir.Name)"
            npx tsc -p (Join-Path $out 'tsconfig.json')
            if ($LASTEXITCODE -ne 0) { throw "playground sample '$($dir.Name)' usercode.ts does not type-check against its generated client" }
        }
    }
    finally { Remove-Item -Recurse -Force -Path $sampleOut -ErrorAction SilentlyContinue }

    Write-Host 'running node:test conformance + parity + auth + fetch-transport suites...'
    node --test conformance.test.mjs parity.test.mjs auth.test.mjs fetch-transport.test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'conformance suite failed' }
}
finally { Pop-Location }
Write-Host 'OK - sample type-checks + conformance/parity/auth/fetch-transport suites passed.'
