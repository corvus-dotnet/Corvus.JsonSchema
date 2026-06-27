#!/usr/bin/env bash
# End-to-end run of the E2E mutation benchmark (rmw-e2e.bench.mjs) — the SHIPPING generated engine vs the
# native default and immer, bytes-in -> bytes-out. Standalone + spike-free:
#
#   1. build the bowtie codegen worker (bowtie-worker) — public codegen libs only, the same CreateDefault
#      codegen the spike used for its default mode;
#   2. generate the Catalog mutation module for ./bench-catalog.json into ./rmw-bench-gen/ (gen-rmw-bench.mjs
#      drives the worker via NDJSON, then esbuild-transpiles generated.ts + corvus-runtime.ts);
#   3. run the benchmark with `node --expose-gc` (its built-in correctness gate must pass: every variant's
#      output must structurally equal the native result before timing counts).
#
# Exit code propagates: the benchmark sets a non-zero exit code on any correctness failure.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER_PROJ="$HERE/bowtie-worker/BowtieCodegenWorker.csproj"
CONFIG="${RMW_BENCH_CONFIG:-Release}"

echo "== build codegen worker ($CONFIG) =="
dotnet build "$WORKER_PROJ" -c "$CONFIG"

echo
echo "== generate Catalog module (codegen + esbuild transpile) =="
# Run from prototypes/ts-bench so Node's upward node_modules walk finds the runtime deps + esbuild.
( cd "$HERE" && node "$HERE/gen-rmw-bench.mjs" )

echo
echo "== run E2E mutation benchmark =="
( cd "$HERE" && node --expose-gc "$HERE/rmw-e2e.bench.mjs" )
