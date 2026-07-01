#!/usr/bin/env bash
# Regenerate the OpenAPI TypeScript playground's vendored browser assets:
#   - wwwroot/corvus-runtime.js         — the shared @endjin/corvus-json-runtime (the MODEL runtime)
#     bundled (deps inlined) into one self-contained ESM. The in-browser-transpiled model module imports
#     it as "./corvus-runtime.js".
#   - wwwroot/corvus-client-runtime.js  — the shared @endjin/corvus-json-client-runtime (the CLIENT
#     transport runtime) bundled into one self-contained ESM. The generated client imports it as
#     "@endjin/corvus-json-client-runtime".
#   - wwwroot/lib/esbuild/*             — esbuild-wasm (browser ESM wrapper + wasm binary), the in-browser
#     transpiler.
#
# Run this after changing packages/corvus-json-runtime or packages/corvus-json-client-runtime, or to bump
# esbuild-wasm. Requires node/npm and a built prototypes/ts-bench/node_modules (for the native esbuild +
# the runtimes' deps).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
WWW="$HERE/src/Corvus.Text.Json.OpenApi.TypeScript.Playground/wwwroot"
ESBUILD="$ROOT/prototypes/ts-bench/node_modules/.bin/esbuild"
ESBUILD_WASM_VERSION=0.28.1

echo "1/3  bundling corvus-runtime.js (model runtime: runtime + lossless-json + temporal + tr46)…"
"$ESBUILD" \
  "$ROOT/packages/corvus-json-runtime/src/index.ts" \
  --bundle --format=esm --target=es2022 \
  --outfile="$WWW/corvus-runtime.js"

echo "2/3  bundling corvus-client-runtime.js (client transport runtime)…"
"$ESBUILD" \
  "$ROOT/packages/corvus-json-client-runtime/src/index.ts" \
  --bundle --format=esm --target=es2022 \
  --outfile="$WWW/corvus-client-runtime.js"

echo "3/3  vendoring esbuild-wasm@$ESBUILD_WASM_VERSION (browser ESM + wasm)…"
TMP="$(mktemp -d)"
( cd "$TMP" && npm init -y >/dev/null 2>&1 && npm install "esbuild-wasm@$ESBUILD_WASM_VERSION" >/dev/null 2>&1 )
mkdir -p "$WWW/lib/esbuild"
cp "$TMP/node_modules/esbuild-wasm/esm/browser.min.js" "$WWW/lib/esbuild/browser.min.js"
cp "$TMP/node_modules/esbuild-wasm/esbuild.wasm" "$WWW/lib/esbuild/esbuild.wasm"
rm -rf "$TMP"

echo "Done. Regenerated:"
ls -la "$WWW/corvus-runtime.js" "$WWW/corvus-client-runtime.js" "$WWW/lib/esbuild/"