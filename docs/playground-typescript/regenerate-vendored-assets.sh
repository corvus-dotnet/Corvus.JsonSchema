#!/usr/bin/env bash
# Regenerate the TypeScript playground's vendored browser assets:
#   - wwwroot/corvus-runtime.js  — the shared @endjin/corvus-json-runtime bundled (deps inlined) into one
#     self-contained ESM. The in-browser-transpiled module imports it as "/corvus-runtime.js".
#   - wwwroot/lib/esbuild/*      — esbuild-wasm (browser ESM wrapper + wasm binary), the in-browser transpiler.
#
# Run this after changing packages/corvus-json-runtime, or to bump esbuild-wasm. Requires node/npm and a built
# prototypes/ts-bench/node_modules (for the native esbuild + the runtime's deps).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
WWW="$HERE/src/Corvus.Text.Json.TypeScript.Playground/wwwroot"
ESBUILD_WASM_VERSION=0.28.1

echo "1/2  bundling corvus-runtime.js (runtime + lossless-json + temporal + tr46)…"
"$ROOT/prototypes/ts-bench/node_modules/.bin/esbuild" \
  "$ROOT/packages/corvus-json-runtime/src/index.ts" \
  --bundle --format=esm --target=es2022 \
  --outfile="$WWW/corvus-runtime.js"

echo "2/2  vendoring esbuild-wasm@$ESBUILD_WASM_VERSION (browser ESM + wasm)…"
TMP="$(mktemp -d)"
( cd "$TMP" && npm init -y >/dev/null 2>&1 && npm install "esbuild-wasm@$ESBUILD_WASM_VERSION" >/dev/null 2>&1 )
mkdir -p "$WWW/lib/esbuild"
cp "$TMP/node_modules/esbuild-wasm/esm/browser.min.js" "$WWW/lib/esbuild/browser.min.js"
cp "$TMP/node_modules/esbuild-wasm/esbuild.wasm" "$WWW/lib/esbuild/esbuild.wasm"
rm -rf "$TMP"

echo "Done. Regenerated:"
ls -la "$WWW/corvus-runtime.js" "$WWW/lib/esbuild/"
