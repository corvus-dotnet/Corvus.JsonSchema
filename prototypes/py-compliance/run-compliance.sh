#!/usr/bin/env bash
# End-to-end JSON-Schema-Test-Suite compliance run for the corvus-py Python engine — the faithful peer of
# the TypeScript harness's run-compliance.sh.
#
#   1. build the codegen generator (ComplianceGenerator.csproj)
#   2. run it to emit one importable PACKAGE per test-group + manifest.json into a temp out dir (offline
#      remotes resolved via FakeWebDocumentResolver — exactly as the TS / C# runners do)
#   3. run suite-runner.py which imports each package's `evaluate` and tallies pass/fail per dialect
#
# Exit code propagates from the runner: non-zero if any non-allow-listed case FAILs (CI gate).
#
# Usage: ./run-compliance.sh [testsBaseDir] [outDir]
#   testsBaseDir  the JSON-Schema-Test-Suite `tests` dir (default: <repo>/JSON-Schema-Test-Suite/tests)
#   outDir        where packages + manifest land (default: ./out-suite)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/../.." && pwd)"

TESTS_BASE_DIR="${1:-$REPO_ROOT/JSON-Schema-Test-Suite/tests}"
OUT_DIR="${2:-$HERE/out-suite}"
CONFIG="${COMPLIANCE_CONFIG:-Release}"
PY="${COMPLIANCE_PYTHON:-/tmp/pygate/bin/python}"

if [ ! -d "$TESTS_BASE_DIR" ]; then
  echo "error: test suite not found at $TESTS_BASE_DIR" >&2
  exit 2
fi

# Start from a clean out dir so stale packages from a previous run never leak into the tally.
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "== build ($CONFIG) =="
dotnet build "$HERE/ComplianceGenerator/ComplianceGenerator.csproj" -c "$CONFIG"

echo
echo "== generate packages + manifest =="
echo "  tests:  $TESTS_BASE_DIR"
echo "  outDir: $OUT_DIR"
dotnet run --project "$HERE/ComplianceGenerator/ComplianceGenerator.csproj" -c "$CONFIG" --no-build -- "$TESTS_BASE_DIR" "$OUT_DIR"

echo
echo "== run compliance suite (import each package + evaluate) =="
"$PY" "$HERE/suite-runner.py" "$OUT_DIR"