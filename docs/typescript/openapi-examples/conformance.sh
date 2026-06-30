#!/usr/bin/env bash
# Generate, compile, and run the conformance test for the generated TypeScript OpenAPI clients.
#
# The recipes under `petstore-*/client` import the runtime via a RELATIVE path to the package's built
# `dist/index.js` so they type-check in place (see regenerate.sh). To EXECUTE the generated client under
# Node we instead generate a parallel copy whose runtime import is the package NAME
# (`@endjin/corvus-json-client-runtime`), resolved via a `node_modules` symlink — exactly how a real
# consumer imports the published package. That decouples execution from the recipe's on-disk depth.
#
# Steps: build the CLI, build the runtime, link it into node_modules, regenerate each recipe's client
# into `conformance/<version>/client` with the package-name runtime import, compile that tree to
# `conformance/dist`, then run the node:test conformance suite against the compiled clients.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)" # repo (worktree) root

echo "building CLI (Debug)..."
dotnet build "$ROOT/src/Corvus.Json.Cli/Corvus.Json.Cli.csproj" -c Debug >/dev/null
DLL="$(ls "$ROOT"/src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll | tail -1)"

# Build the client-runtime package and link it as @endjin/corvus-json-client-runtime in node_modules.
echo "building @endjin/corvus-json-client-runtime..."
(cd "$ROOT/packages/corvus-json-client-runtime" && npm install >/dev/null 2>&1 && npm run build >/dev/null)
mkdir -p "$HERE/node_modules/@endjin"
ln -sfn ../../../../../packages/corvus-json-client-runtime "$HERE/node_modules/@endjin/corvus-json-client-runtime"

# Generate the executable client copy with the package-name runtime import.
rm -rf "$HERE/conformance"
mkdir -p "$HERE/conformance"
# The `tr46` ambient declaration the model runtime needs (mirrors the recipe's root globals.d.ts), kept
# inside the conformance tree so it sits under the conformance tsconfig's rootDir.
cp "$HERE/globals.d.ts" "$HERE/conformance/globals.d.ts"
for dir in "$HERE"/petstore-*/; do
  version="$(basename "$dir")"
  spec="$dir/openapi.json"
  out="$HERE/conformance/$version/client"
  echo "  conformance/$version  <-  $version/openapi.json"
  mkdir -p "$out"
  dotnet "$DLL" openapi-client "$spec" \
    --engine TypeScript \
    --outputPath "$out" \
    --tsClientRuntimeModule "@endjin/corvus-json-client-runtime" \
    --force >/dev/null
done

echo "compiling conformance clients..."
(cd "$HERE" && npx tsc -p tsconfig.conformance.json)

echo "running node:test conformance suite..."
(cd "$HERE" && node --test conformance.test.mjs)
echo "OK — conformance suite passed."
