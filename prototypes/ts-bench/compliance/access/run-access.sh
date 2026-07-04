#!/usr/bin/env bash
# End-to-end run of the corvus-ts generated-accessor "access" suites (produce / RMW / build / union match /
# format brands / collector / typed interface). This is DISTINCT from validation compliance
# (../run-compliance.sh, driven off the JSON-Schema-Test-Suite); these suites exercise the GENERATED API
# surface against hand-written feature schemas.
#
#   1. build the bowtie codegen worker (../../bowtie-worker) — public codegen libs only, the same
#      CreateDefault codegen the spike used for its default mode;
#   2. run run-access.mjs from prototypes/ts-bench (so the generated runtime's bare imports —
#      lossless-json / @js-temporal/polyfill / tr46 — and esbuild resolve via ts-bench/node_modules):
#      for every suite it codegens the module, esbuild-transpiles it + the .test.ts, runs the test, and
#      checks the suite's own "N passed, M failed" tally.
#
# Exit code propagates: non-zero if ANY suite reports a failure / error (CI gate).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TS_BENCH="$(cd "$HERE/../.." && pwd)"
WORKER_PROJ="$TS_BENCH/bowtie-worker/BowtieCodegenWorker.csproj"
CONFIG="${ACCESS_CONFIG:-Release}"

echo "== build codegen worker ($CONFIG) =="
dotnet build "$WORKER_PROJ" -c "$CONFIG"

echo
echo "== run access suites (codegen + esbuild transpile + node) =="
# Run from prototypes/ts-bench so Node's upward node_modules walk finds the runtime deps + esbuild.
( cd "$TS_BENCH" && node "$HERE/run-access.mjs" )
