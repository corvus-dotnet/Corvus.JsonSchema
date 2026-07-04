#!/usr/bin/env bash
# Runs the official JSON Schema test suite against the corvus-ts harness image, via the REAL Bowtie tooling
# (Windows bowtie.exe + podman.exe, reachable from WSL), per dialect. Tallies the Bowtie report directly
# (bowtie summary has an id-keying bug with a localhost/ image). Usage: ./run-bowtie-suite.sh draft2020-12 ...
set -uo pipefail
SUITE=/home/mwa/src/Corvus.JsonSchema/JSON-Schema-Test-Suite/tests
IMG=image:localhost/corvus-ts
for d in "$@"; do
  WP=$(wslpath -w "$SUITE/$d")
  echo "=== running bowtie suite: $d ($(ls "$SUITE/$d"/*.json | wc -l) files) ==="
  t0=$(date +%s)
  # PYTHONUTF8=1 (propagated to the Windows bowtie.exe via WSLENV): bowtie.exe otherwise reads the UTF-8 suite
  # files as cp1252 and sends mojibake (e.g. "π" -> "Ï€"), corrupting non-ASCII instances. Not needed for the
  # Linux bowtie.
  env PYTHONUTF8=1 WSLENV=PYTHONUTF8 bowtie.exe suite -i "$IMG" "$WP" 2>/tmp/bt-$d.err > /tmp/bt-$d.json
  t1=$(date +%s)
  python3 - "$d" "$((t1-t0))" <<'PY'
import sys, json
dialect, secs = sys.argv[1], sys.argv[2]
p = f = e = total = 0
fails, errs = [], []
for line in open(f"/tmp/bt-{dialect}.json"):
    line = line.strip()
    if not line:
        continue
    o = json.loads(line)
    if "results" in o and "expected" in o:
        for i, (r, ex) in enumerate(zip(o["results"], o["expected"])):
            total += 1
            if isinstance(r, dict) and (r.get("errored") or r.get("skipped")):
                e += 1
                if len(errs) < 8: errs.append(str(r)[:90])
            elif isinstance(r, dict) and r.get("valid") == ex:
                p += 1
            else:
                f += 1
                if len(fails) < 15: fails.append(f"seq {o['seq']} test#{i}: got {r}, want {ex}")
print(f"\n{dialect}: {p}/{total} pass, {f} fail, {e} errored  ({secs}s)")
for s in fails: print("  FAIL " + s)
for s in errs[:4]: print("  ERR  " + s)
PY
done
