#!/usr/bin/env bash
# Regenerate every OpenAPI client recipe's `client/` directory from its spec via the production CLI
# (`corvusjson openapi-client --engine TypeScript`), then strict-type-check the result with `tsc`.
#
# The generated client imports the byte-native transport runtime from
# `@endjin/corvus-json-client-runtime`. To resolve it from the working tree without an install/publish
# step, each recipe points `--tsClientRuntimeModule` at the package's built `dist/index.js` via a
# relative path. The generated models import the model runtime (`./corvus-runtime.js`) which is
# re-emitted into each recipe's `client/models` directory; its third-party deps
# (`@js-temporal/polyfill`, `lossless-json`, `tr46`) resolve from this directory's `node_modules`.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)" # repo (worktree) root

echo "building CLI (Debug)..."
dotnet build "$ROOT/src/Corvus.Json.Cli/Corvus.Json.Cli.csproj" -c Debug >/dev/null
DLL="$(ls "$ROOT"/src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll | tail -1)"

# Relative path from each recipe's client/ directory to the built client-runtime package entry.
CLIENT_RUNTIME="../../../../../packages/corvus-json-client-runtime/dist/index.js"

# Build the client-runtime package if its dist is missing.
if [ ! -f "$ROOT/packages/corvus-json-client-runtime/dist/index.js" ]; then
  echo "building @endjin/corvus-json-client-runtime..."
  (cd "$ROOT/packages/corvus-json-client-runtime" && npm install >/dev/null && npm run build >/dev/null)
fi

for dir in "$HERE"/petstore-*/; do
  spec="$dir/openapi.json"
  echo "  $(basename "$dir")  <-  $(basename "$spec")"
  rm -rf "$dir/client"
  dotnet "$DLL" openapi-client "$spec" \
    --engine TypeScript \
    --outputPath "$dir/client" \
    --tsClientRuntimeModule "$CLIENT_RUNTIME" \
    --force >/dev/null
done

echo "type-checking with tsc..."
(cd "$HERE" && npx tsc)
echo "OK — all recipes generated and strict-type-checked."
