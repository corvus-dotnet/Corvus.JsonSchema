#!/usr/bin/env bash
# Generate a NAMED variant of the corvus-py validators for a controlled A/B (see ab.py).
# Each variant lands in ab-gen/<variant>/ as an importable package (so ab.py can import both variants of a
# schema into one process, e.g. `base.pulumi` vs `num.pulumi`, without a top-level module collision).
#
# Usage: ./generate-ab.sh <path-to-jsonschema-benchmark> <variant-name>
# The CLI is used as already built — rebuild it yourself between variants (this does not build).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
DATASET="${1:?usage: ./generate-ab.sh <dataset> <variant>}"
VARIANT="${2:?usage: ./generate-ab.sh <dataset> <variant>}"
DLL="$(ls "$ROOT"/src/Corvus.Json.Cli/bin/Debug/net*/Corvus.Json.Cli.dll | tail -1)"

OUT="$HERE/ab-gen/$VARIANT"
rm -rf "$OUT"; mkdir -p "$OUT"
: > "$OUT/__init__.py"   # make the variant an importable namespace package
ok=0; fail=0
for d in "$DATASET"/schemas/*/; do
  n_orig="$(basename "$d")"; n="${n_orig//-/_}"
  if dotnet "$DLL" jsonschema "$d/schema.json" --engine Python --assertFormat false --outputPath "$OUT/$n" >/dev/null 2>&1; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1)); echo "  WARN: $n_orig failed"
  fi
done
echo "variant '$VARIANT': generated $ok validators ($fail failed) into $OUT"