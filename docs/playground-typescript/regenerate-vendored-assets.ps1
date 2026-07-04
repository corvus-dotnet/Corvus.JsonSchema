#!/usr/bin/env pwsh
# Regenerate the TypeScript playground's vendored browser assets:
#   - wwwroot/corvus-runtime.js  — the shared @endjin/corvus-json-runtime bundled (lossless-json, the Temporal
#     polyfill and tr46 inlined) into one self-contained ESM. The in-browser-transpiled module imports it.
#   - wwwroot/lib/esbuild/*      — esbuild-wasm (the browser ESM wrapper + the .wasm binary), the in-browser
#     transpiler/bundler.
#
# Run after changing packages/corvus-json-runtime, or to bump esbuild-wasm. Requires node/npm and a built
# prototypes/ts-bench/node_modules (the native esbuild + the runtime's deps). Cross-platform (pwsh 7+).
#
#   -Check : only re-bundle corvus-runtime.js and FAIL if the committed copy has drifted (the CI drift guard);
#            skips the esbuild-wasm vendoring. Regenerate + commit to fix.
[CmdletBinding()]
param([switch] $Check)
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here '../..')).Path
$www = Join-Path $here 'src/Corvus.Text.Json.TypeScript.Playground/wwwroot'
$esbuildWasmVersion = '0.28.1'

$esbuild = Join-Path $root 'prototypes/ts-bench/node_modules/.bin/esbuild'
if ($IsWindows) { $esbuild = "$esbuild.cmd" }
$runtimeBundle = Join-Path $www 'corvus-runtime.js'

Write-Host 'Bundling corvus-runtime.js (runtime + lossless-json + temporal + tr46)...'
& $esbuild (Join-Path $root 'packages/corvus-json-runtime/src/index.ts') --bundle --format=esm --target=es2022 "--outfile=$runtimeBundle"
if ($LASTEXITCODE -ne 0) { throw 'esbuild bundle failed' }

if ($Check) {
    & git diff --quiet HEAD -- $runtimeBundle
    if ($LASTEXITCODE -ne 0) {
        Write-Host '::error::playground-typescript vendored corvus-runtime.js is stale - run docs/playground-typescript/regenerate-vendored-assets.ps1 and commit the result.'
        & git --no-pager diff --stat HEAD -- $runtimeBundle
        exit 1
    }
    Write-Host 'corvus-runtime.js is up to date.'
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
Write-Host 'Done. Regenerated corvus-runtime.js and lib/esbuild/.'