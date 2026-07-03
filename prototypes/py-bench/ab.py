#!/usr/bin/env python3
"""Controlled same-run A/B of two generated corvus-py variants (the Python peer of ts-bench's bench-compare).

Per schema, imports BOTH variants' validators into one process and times them ADJACENTLY, interleaved round
by round (v1 pass, then v2 pass, then v1, ...), median over ROUNDS — so machine-state noise hits both equally
(the lesson from ts-bench: two SEPARATE full runs mislead; only a same-run A/B is trustworthy). A correctness
gate records each variant's valid-count and flags any mismatch (a faster-but-wrong variant is meaningless).

    ./generate-ab.sh /path/to/jsonschema-benchmark base      # baseline (current generator)
    # ...change the generator, rebuild the CLI...
    ./generate-ab.sh /path/to/jsonschema-benchmark num       # the variant
    WARMUP=5 ROUNDS=9 python ab.py base num /path/to/jsonschema-benchmark
"""

from __future__ import annotations

import importlib
import json
import math
import os
import statistics
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
V1, V2 = sys.argv[1], sys.argv[2]
DATASET = Path(sys.argv[3]) if len(sys.argv) > 3 else Path("/tmp/jsonschema-benchmark")
SCHEMAS = DATASET / "schemas"
WARMUP = int(os.environ.get("WARMUP", "5"))
ROUNDS = int(os.environ.get("ROUNDS", "9"))

sys.path.insert(0, str(HERE / "ab-gen"))


def sanitize(name: str) -> str:
    return name.replace("-", "_")


def load(path: Path) -> list[object]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def geomean(xs: list[float]) -> float:
    return math.exp(sum(math.log(x) for x in xs) / len(xs)) if xs else math.nan


def main() -> int:
    names = sorted(d.name for d in SCHEMAS.iterdir() if (d / "schema.json").exists())
    rows: list[tuple[str, int, float, float, bool]] = []
    print(f"# A/B: {V1} (baseline) vs {V2} (variant). ns/instance, median of {ROUNDS}. speedup = {V1}/{V2} (>1 = {V2} faster).\n")
    print(f"{'schema':<22}{'inst':>6}{V1:>12}{V2:>12}{'speedup':>10}  note")
    print("-" * 74)
    for orig in names:
        s = sanitize(orig)
        try:
            m1 = importlib.import_module(f"{V1}.{s}")
            m2 = importlib.import_module(f"{V2}.{s}")
        except Exception as exc:  # noqa: BLE001
            print(f"{orig:<22}  import ERR: {exc}")
            continue
        insts = load(SCHEMAS / orig / "instances.jsonl")
        e1, e2 = m1.evaluate, m2.evaluate
        try:
            v1 = sum(1 for i in insts if e1(i) is True)
            v2 = sum(1 for i in insts if e2(i) is True)
        except Exception as exc:  # noqa: BLE001
            print(f"{orig:<22}  run ERR: {exc}")
            continue
        for _ in range(WARMUP):
            for i in insts:
                e1(i)
                e2(i)
        t1s: list[int] = []
        t2s: list[int] = []
        for _ in range(ROUNDS):
            a = time.perf_counter_ns()
            for i in insts:
                e1(i)
            b = time.perf_counter_ns()
            for i in insts:
                e2(i)
            c = time.perf_counter_ns()
            t1s.append(b - a)
            t2s.append(c - b)
        n1 = statistics.median(t1s) / len(insts)
        n2 = statistics.median(t2s) / len(insts)
        ok = v1 == v2
        rows.append((orig, len(insts), n1, n2, ok))
        note = "" if ok else f"MISMATCH {v1}!={v2}"
        print(f"{orig:<22}{len(insts):>6}{n1:>10.0f}n{n2:>10.0f}n{n1 / n2:>9.2f}x  {note}")

    good = [r for r in rows if r[4]]
    speedups = [r[2] / r[3] for r in good]
    faster = sum(1 for r in good if r[3] < r[2] * 0.98)
    slower = sum(1 for r in good if r[3] > r[2] * 1.02)
    tie = len(good) - faster - slower
    print("-" * 74)
    print(f"\n# Geomean speedup ({V2} vs {V1}): {geomean(speedups):.3f}x   (>1 = {V2} faster)")
    print(f"# Per-schema (>2% band): {V2} faster on {faster}, {V1} faster on {slower}, tie on {tie}  (of {len(good)} correct)")
    print(f"# Geomean ns/instance: {V1} {geomean([r[2] for r in good]):.0f}   {V2} {geomean([r[3] for r in good]):.0f}")
    mism = [r[0] for r in rows if not r[4]]
    if mism:
        print(f"# CORRECTNESS MISMATCHES (variant is wrong — disqualifying): {', '.join(mism)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())