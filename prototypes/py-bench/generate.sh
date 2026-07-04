#!/usr/bin/env bash
# Regenerate the corvus-py validators for the benchmark from a Sourcemeta jsonschema-benchmark dataset.
#
# --assertFormat false is REQUIRED and not optional: the dataset's consensus valid-counts (and the other
# validators jsonschema/fastjsonschema/jsonschema-rs) treat `format` as annotation-only (the 2020-12
# default). The CLI asserts formats by default, so the benchmark MUST opt out — otherwise corvus-py rejects
# format-violating instances the consensus accepts, producing spurious disagreements and an unfair comparison.
#
# The generated output directory name is the schema name with '-' -> '_' so it is an importable Python package.
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
  n_orig="$(basename "$d")"
  n="${n_orig//-/_}"
  if dotnet "$DLL" jsonschema "$d/schema.json" --engine Python --assertFormat false --outputPath "$HERE/bench-gen/$n" >/dev/null 2>&1; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1)); echo "  WARN: $n_orig failed"
  fi
done
echo "generated $ok validators ($fail failed) with --assertFormat false."
echo "run: WARMUP=5 ROUNDS=5 $HERE/../.venv-or-gate/python bench.py '$DATASET'"