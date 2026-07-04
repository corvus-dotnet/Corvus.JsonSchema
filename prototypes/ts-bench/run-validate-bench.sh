#!/usr/bin/env bash
# End-to-end run of the sustained-load VALIDATION benchmark (validate-sustained.bench.mjs) — the SHIPPING
# generated validator's throughput + GC pressure under sustained load. Standalone + spike-free:
#
#   1. build the bowtie codegen worker (bowtie-worker) — public codegen libs only;
#   2. generate the Catalog module for ./bench-catalog.json into ./rmw-bench-gen/ (gen-rmw-bench.mjs drives
#      the worker via NDJSON, then esbuild-transpiles generated.ts + corvus-runtime.ts) — the SAME module
#      rmw-e2e.bench.mjs uses, so the two benchmarks profile identical documents;
#   3. run the benchmark with `node --expose-gc` (required for the GC-pressure + bytes/op measurements; its
#      built-in correctness gate must accept the valid fixture and reject invalid input before timing counts).
#
# Exit code propagates: the benchmark sets a non-zero exit code on any correctness failure.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER_PROJ="$HERE/bowtie-worker/BowtieCodegenWorker.csproj"
CONFIG="${VALIDATE_BENCH_CONFIG:-Release}"

echo "== build codegen worker ($CONFIG) =="
dotnet build "$WORKER_PROJ" -c "$CONFIG"

echo
echo "== generate Catalog module (codegen + esbuild transpile) =="
# Run from prototypes/ts-bench so Node's upward node_modules walk finds the runtime deps + esbuild.
( cd "$HERE" && node "$HERE/gen-rmw-bench.mjs" )

echo
echo "== run sustained-load validation benchmark =="
( cd "$HERE" && node --expose-gc "$HERE/validate-sustained.bench.mjs" )
