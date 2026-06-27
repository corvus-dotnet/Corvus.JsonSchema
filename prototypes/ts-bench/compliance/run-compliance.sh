#!/usr/bin/env bash
# End-to-end JSON-Schema-Test-Suite compliance run for the corvus-ts TypeScript engine.
#
#   1. build the codegen generator (ComplianceGenerator.csproj)
#   2. run it to emit one TS module per test-group + manifest.json into a temp out dir
#   3. run suite-runner.mjs (from prototypes/ts-bench, so esbuild + lossless-json resolve) which esbuild-
#      transpiles every module and tallies pass/fail per dialect
#
# Exit code propagates from the runner: non-zero if any case FAILs (CI gate).
#
# Usage: ./run-compliance.sh [testsBaseDir] [outDir]
#   testsBaseDir  the JSON-Schema-Test-Suite `tests` dir (default: <repo>/JSON-Schema-Test-Suite/tests)
#   outDir        where modules + manifest land (default: prototypes/ts-bench/compliance/out-suite)
#
# The default out dir lives UNDER prototypes/ts-bench so the bundled runtime's bare imports
# (lossless-json, @js-temporal/polyfill, tr46) resolve via Node's upward node_modules walk. If you pass a
# custom out dir outside that tree, Node won't find those packages and every module fails to load.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TS_BENCH="$(cd "$HERE/.." && pwd)"
REPO_ROOT="$(cd "$HERE/../../.." && pwd)"

TESTS_BASE_DIR="${1:-$REPO_ROOT/JSON-Schema-Test-Suite/tests}"
OUT_DIR="${2:-$HERE/out-suite}"
CONFIG="${COMPLIANCE_CONFIG:-Release}"

if [ ! -d "$TESTS_BASE_DIR" ]; then
  echo "error: test suite not found at $TESTS_BASE_DIR" >&2
  exit 2
fi

# Start from a clean out dir so stale modules from a previous run never leak into the tally.
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "== build ($CONFIG) =="
dotnet build "$HERE/ComplianceGenerator.csproj" -c "$CONFIG"

echo
echo "== generate modules + manifest =="
echo "  tests:  $TESTS_BASE_DIR"
echo "  outDir: $OUT_DIR"
dotnet run --project "$HERE/ComplianceGenerator.csproj" -c "$CONFIG" --no-build -- "$TESTS_BASE_DIR" "$OUT_DIR"

echo
echo "== run compliance suite (esbuild transpile + evaluate) =="
# Run the node runner from prototypes/ts-bench so its node_modules (esbuild + lossless-json) resolve.
( cd "$TS_BENCH" && node "$HERE/suite-runner.mjs" "$OUT_DIR" )
