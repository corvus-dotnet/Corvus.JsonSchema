#!/usr/bin/env pwsh
# Regenerate the OpenAPI TypeScript playground's vendored browser assets:
#   - wwwroot/corvus-runtime.js         — the shared @endjin/corvus-json-runtime (the MODEL runtime) bundled
#     (deps inlined) into one self-contained ESM. The in-browser-transpiled model module imports it.
#   - wwwroot/corvus-client-runtime.js  — the shared @endjin/corvus-json-client-runtime (the CLIENT transport
#     runtime) bundled into one self-contained ESM. The generated client imports it.
#   - wwwroot/lib/esbuild/*             — esbuild-wasm (the browser ESM wrapper + the .wasm binary).
#
# Run after changing packages/corvus-json-runtime or packages/corvus-json-client-runtime, or to bump
# esbuild-wasm. Requires node/npm and a built prototypes/ts-bench/node_modules (native esbuild + the runtimes'
# deps). Cross-platform (pwsh 7+).
#
#   -Check : only re-bundle the two runtime bundles and FAIL if a committed copy has drifted (the CI drift
#            guard); skips the esbuild-wasm vendoring. Regenerate + commit to fix.
[CmdletBinding()]
param([switch] $Check)
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here '../..')).Path
$www = Join-Path $here 'src/Corvus.Text.Json.OpenApi.TypeScript.Playground/wwwroot'
$esbuildWasmVersion = '0.28.1'

$esbuild = Join-Path $root 'prototypes/ts-bench/node_modules/.bin/esbuild'
if ($IsWindows) { $esbuild = "$esbuild.cmd" }
$runtimeBundle = Join-Path $www 'corvus-runtime.js'
$clientBundle = Join-Path $www 'corvus-client-runtime.js'

Write-Host 'Bundling corvus-runtime.js (model runtime: runtime + lossless-json + temporal + tr46)...'
& $esbuild (Join-Path $root 'packages/corvus-json-runtime/src/index.ts') --bundle --format=esm --target=es2022 "--outfile=$runtimeBundle"
if ($LASTEXITCODE -ne 0) { throw 'esbuild bundle (model runtime) failed' }

Write-Host 'Bundling corvus-client-runtime.js (client transport runtime)...'
& $esbuild (Join-Path $root 'packages/corvus-json-client-runtime/src/index.ts') --bundle --format=esm --target=es2022 "--outfile=$clientBundle"
if ($LASTEXITCODE -ne 0) { throw 'esbuild bundle (client runtime) failed' }

if ($Check) {
    & git diff --quiet HEAD -- $runtimeBundle $clientBundle
    if ($LASTEXITCODE -ne 0) {
        Write-Host '::error::playground-openapi-typescript vendored runtime bundle is stale - run docs/playground-openapi-typescript/regenerate-vendored-assets.ps1 and commit the result.'
        & git --no-pager diff --stat HEAD -- $runtimeBundle $clientBundle
        exit 1
    }
    Write-Host 'corvus-runtime.js and corvus-client-runtime.js are up to date.'
    return
}

Write-Host "Vendoring esbuild-wasm@$esbuildWasmVersion (browser ESM + wasm)..."
$tmp = New-Item -ItemType Directory -Path (Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString()))
try {
    Push-Location $tmp
    & npm init -y | Out-Null
    & npm install "esbuild-wasm@$esbuildWasmVersion" | Out-Null
    Pop-Location
    $lib = Join-Path $www 'lib/esbuild'
    New-Item -ItemType Directory -Force -Path $lib | Out-Null
    Copy-Item (Join-Path $tmp 'node_modules/esbuild-wasm/esm/browser.min.js') (Join-Path $lib 'browser.min.js') -Force
    Copy-Item (Join-Path $tmp 'node_modules/esbuild-wasm/esbuild.wasm') (Join-Path $lib 'esbuild.wasm') -Force
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
Write-Host 'Done. Regenerated corvus-runtime.js, corvus-client-runtime.js and lib/esbuild/.'