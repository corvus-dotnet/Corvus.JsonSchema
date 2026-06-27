#!/usr/bin/env bash
# Builds the corvus-ts Bowtie harness OCI image. corvus-ts is a code generator, so the image bundles a
# self-contained linux-x64 CLI + Node + tsc + the harness; per Bowtie `run` it codegens, compiles and runs.
# Uses the Windows podman.exe (reachable from WSL) — podman.exe builds happily from a \\wsl.localhost path.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
BENCH="$(cd "$HERE/.." && pwd)"
ROOT="$(cd "$BENCH/../.." && pwd)"
CTX="${TMPDIR:-/tmp}/corvus-bowtie-image"

echo "1/3 publishing self-contained linux-x64 CLI -> $CTX/cli ..."
rm -rf "$CTX"; mkdir -p "$CTX"
dotnet publish "$ROOT/src/Corvus.Json.Cli/Corvus.Json.Cli.csproj" -c Release -r linux-x64 --self-contained true -f net9.0 -o "$CTX/cli" >/dev/null

echo "2/3 assembling context ..."
cp "$HERE/Containerfile" "$HERE/package.json" "$BENCH/bowtie-harness.mjs" "$BENCH/bowtie-globals.d.ts" "$CTX/"

echo "3/3 building image (podman.exe) ..."
WCTX=$(wslpath -w "$CTX")
podman.exe build -t corvus-ts -f "$WCTX\\Containerfile" "$WCTX"

echo
echo "Built localhost/corvus-ts. Verify:"
echo "  podman.exe run -i --rm localhost/corvus-ts < $BENCH/bowtie-selftest.ndjson | grep seq"
echo "  bowtie.exe smoke -i image:localhost/corvus-ts"
echo "  ./run-bowtie-suite.sh draft2020-12        # full dialect via real Bowtie"
