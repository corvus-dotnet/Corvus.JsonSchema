#!/usr/bin/env bash
# Regenerate every recipe's `generated.ts` from its schema via the production CLI
# (`corvusjson jsonschema --engine TypeScript`), then point each module at the single
# shared `corvus-runtime.ts` at the examples root.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)" # repo (worktree) root

echo "building CLI (Debug)..."
dotnet build "$ROOT/src/Corvus.Json.Cli/Corvus.Json.Cli.csproj" -c Debug >/dev/null
DLL="$(ls "$ROOT"/src/Corvus.Json.Cli/bin/Debug/net*/Corvus.Json.Cli.dll | tail -1)"

runtime_saved=0
for dir in "$HERE"/[0-9]*-*/; do
  schema="$(ls "$dir"*.json | head -1)"
  echo "  $(basename "$dir")  <-  $(basename "$schema")"
  dotnet "$DLL" jsonschema "$schema" --engine TypeScript --outputPath "$dir" >/dev/null
  if [ "$runtime_saved" -eq 0 ]; then
    mv "$dir/corvus-runtime.ts" "$HERE/corvus-runtime.ts" # keep one shared copy
    runtime_saved=1
  else
    rm -f "$dir/corvus-runtime.ts"
  fi
  sed -i 's#"./corvus-runtime.js"#"../corvus-runtime.js"#' "$dir/generated.ts"
done

echo "regenerated. verify with: npm run build && node dist/<recipe>/demo.js"
