#!/usr/bin/env bash
# Regenerate the corvus-ts validators for the benchmark from a Sourcemeta jsonschema-benchmark dataset.
#
# --assertFormat false is REQUIRED and not optional: the dataset's consensus valid-counts (and the other
# validators ajv/hyperjump/schemasafe) treat `format` as annotation-only (the 2020-12 default). The CLI
# asserts formats by default, so the benchmark MUST opt out — otherwise corvus-ts rejects format-violating
# instances the consensus accepts, producing spurious disagreements and an unfair comparison.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
DATASET="${1:?usage: ./generate.sh <path-to-jsonschema-benchmark>}"

echo "building CLI..."
dotnet build "$ROOT/src/Corvus.Json.Cli/Corvus.Json.Cli.csproj" -c Debug >/dev/null
DLL="$(ls "$ROOT"/src/Corvus.Json.Cli/bin/Debug/net*/Corvus.Json.Cli.dll | tail -1)"

rm -rf "$HERE/bench-gen"; mkdir -p "$HERE/bench-gen"
ok=0; fail=0
for d in "$DATASET"/schemas/*/; do
  n="$(basename "$d")"
  if dotnet "$DLL" jsonschema "$d/schema.json" --engine TypeScript --assertFormat false --outputPath "$HERE/bench-gen/$n" >/dev/null 2>&1; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1)); echo "  WARN: $n failed"
  fi
done
echo "generated $ok validators ($fail failed) with --assertFormat false."
echo "compile: cd '$HERE' && npx tsc -p tsconfig.bench.json && node bench.mjs '$DATASET'"
