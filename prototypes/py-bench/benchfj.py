#!/usr/bin/env python3
"""Fast dev-loop benchmark: a corvus-py variant (ab-gen/<variant>) vs fastjsonschema only.

For iterating on optimizations — fastjsonschema compiles quickly, so this skips the slow pure-Python
`jsonschema` competitor. Per schema, times corvus and fastjsonschema ADJACENTLY (interleaved, median of
ROUNDS), over ONLY the schemas where fastjsonschema compiled and agrees on the valid-count (a fair gap).

    ./generate-ab.sh /path/to/jsonschema-benchmark inline    # generate the variant
    WARMUP=8 ROUNDS=9 python benchfj.py inline /path/to/jsonschema-benchmark
"""

from __future__ import annotations

import copy
import importlib
import json
import math
import os
import statistics
import sys
import time
from pathlib import Path

import fastjsonschema

HERE = Path(__file__).resolve().parent
VARIANT = sys.argv[1]
DATASET = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("/tmp/jsonschema-benchmark")
SCHEMAS = DATASET / "schemas"
WARMUP = int(os.environ.get("WARMUP", "8"))
ROUNDS = int(os.environ.get("ROUNDS", "9"))

sys.path.insert(0, str(HERE / "ab-gen"))

# fastjsonschema no-ops openapi's $dynamicRef (validates in ~1us) — not a fair gap; exclude from the geomean.
SUSPECT = {"openapi"}


def sanitize(name: str) -> str:
    return name.replace("-", "_")


def load(path: Path) -> list[object]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def geomean(xs: list[float]) -> float:
    return math.exp(sum(math.log(x) for x in xs) / len(xs)) if xs else math.nan


def main() -> int:
    names = sorted(d.name for d in SCHEMAS.iterdir() if (d / "schema.json").exists())
    rows: list[tuple[str, float, float, bool]] = []
    print(f"# {VARIANT} (corvus-py) vs fastjsonschema. ns/instance, median of {ROUNDS}. ratio = corvus/fj (>1 = fj faster).\n")
    print(f"{'schema':<22}{'corvus':>11}{'fastjson':>11}{'ratio':>8}  note")
    print("-" * 62)
    for orig in names:
        s = sanitize(orig)
        try:
            cmod = importlib.import_module(f"{VARIANT}.{s}")
        except Exception as exc:  # noqa: BLE001
            print(f"{orig:<22}  corvus import ERR: {exc}")
            continue
        schema = json.loads((SCHEMAS / orig / "schema.json").read_text(encoding="utf-8"))
        try:
            fj = fastjsonschema.compile(schema)
        except Exception:  # noqa: BLE001
            print(f"{orig:<22}  (fastjsonschema did not compile — skipped)")
            continue
        insts = load(SCHEMAS / orig / "instances.jsonl")
        ce = cmod.evaluate

        def fjv(i: object, fj: object = fj) -> bool:
            try:
                fj(i)  # type: ignore[operator]
                return True
            except fastjsonschema.JsonSchemaException:
                return False

        # fastjsonschema MUTATES the instance (it fills `default`s), which would corrupt corvus's timing on a
        # shared list. corvus is read-only, so it always validates the pristine `insts`; fastjsonschema always
        # gets a fresh deep copy (untimed) — for the gate, the warm-up, and each timed round.
        cv = sum(1 for i in insts if ce(i) is True)
        try:
            fv = sum(1 for i in copy.deepcopy(insts) if fjv(i) is True)
        except Exception as exc:  # noqa: BLE001  (fastjsonschema's generated code crashes on some schemas, e.g. krakend)
            print(f"{orig:<22}  (fastjsonschema runtime error: {type(exc).__name__} — skipped)")
            continue
        if cv != fv or orig in SUSPECT:
            note = f"skip: {'valid-count mismatch ' + str(cv) + '!=' + str(fv) if cv != fv else 'fj no-ops $dynamicRef'}"
            print(f"{orig:<22}{'':>30}  {note}")
            continue
        for _ in range(WARMUP):
            fjw = copy.deepcopy(insts)
            for i in insts:
                ce(i)
            for i in fjw:
                fjv(i)
        tc: list[int] = []
        tf: list[int] = []
        for _ in range(ROUNDS):
            fjr = copy.deepcopy(insts)  # untimed: pristine copy for fastjsonschema this round
            a = time.perf_counter_ns()
            for i in insts:
                ce(i)
            b = time.perf_counter_ns()
            for i in fjr:
                fjv(i)
            c = time.perf_counter_ns()
            tc.append(b - a)
            tf.append(c - b)
        nc = statistics.median(tc) / len(insts)
        nf = statistics.median(tf) / len(insts)
        rows.append((orig, nc, nf, True))
        print(f"{orig:<22}{nc:>9.0f}n{nf:>9.0f}n{nc / nf:>7.2f}x")

    print("-" * 62)
    ratios = [nc / nf for _, nc, nf, _ in rows]
    behind = [(nc / nf, n) for n, nc, nf, _ in rows if nc / nf > 1.15]
    ahead = [(nc / nf, n) for n, nc, nf, _ in rows if nc / nf < 0.87]
    print(f"\n# over {len(rows)} fair schemas: geomean corvus/fastjson = {geomean(ratios):.3f}x   (>1 = fastjson faster)")
    print(f"# corvus BEHIND (>1.15x): {len(behind)}   AHEAD (<0.87x): {len(ahead)}")
    if behind:
        print("# biggest deficits: " + ", ".join(f"{n} {r:.1f}x" for r, n in sorted(behind, reverse=True)[:8]))
    return 0


if __name__ == "__main__":
    sys.exit(main())