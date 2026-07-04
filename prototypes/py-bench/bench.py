#!/usr/bin/env python3
"""Sourcemeta-dataset validation benchmark: corvus-py (production CLI output) vs jsonschema / fastjsonschema
/ jsonschema-rs.

Methodology mirrors the ts-bench (and Sourcemeta's): per schema, validate every instance once (cold pass,
records the valid-count), warm up WARMUP times, then time ROUNDS warm passes over all instances and take the
median. Compile time is excluded — this measures steady-state validation throughput. All validators receive
the same json.loads'd instances. A correctness cross-check records each validator's valid-count per schema; a
fast-but-wrong validator is meaningless, so disagreements are surfaced and the headline metric is
correctness-gated (only a validator that produced the consensus valid-count earns credit on that schema).

    ./generate.sh /path/to/jsonschema-benchmark      # regenerates bench-gen/ via the production CLI
    WARMUP=5 ROUNDS=5 python bench.py /path/to/jsonschema-benchmark
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
from typing import Callable

HERE = Path(__file__).resolve().parent
DATASET = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE / "jsonschema-benchmark"
SCHEMAS = DATASET / "schemas"
GEN = HERE / "bench-gen"
WARMUP = int(os.environ.get("WARMUP", "5"))
ROUNDS = int(os.environ.get("ROUNDS", "5"))

sys.path.insert(0, str(GEN))

import fastjsonschema
import jsonschema
import jsonschema_rs
from jsonschema.validators import validator_for

IMPLS = os.environ.get("IMPLS", "corvus-py,jsonschema,fastjsonschema,jsonschema-rs").split(",")

Validator = Callable[[object], bool]


def load_instances(path: Path) -> list[object]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def sanitize(name: str) -> str:
    return name.replace("-", "_")


def build_validators(orig: str, schema: dict[str, object]) -> dict[str, Validator | tuple[str, str]]:
    out: dict[str, Validator | tuple[str, str]] = {}

    # corvus-py: the generated package's re-exported root `evaluate`.
    try:
        module = importlib.import_module(sanitize(orig))
        evaluate = module.evaluate
        out["corvus-py"] = lambda i, ev=evaluate: ev(i) is True
    except Exception as exc:  # noqa: BLE001
        out["corvus-py"] = ("error", str(exc))

    # python-jsonschema: format annotation-only by default (no format_checker), matching the consensus.
    try:
        cls = validator_for(schema)
        cls.check_schema(schema)
        v = cls(schema)
        out["jsonschema"] = lambda i, v=v: v.is_valid(i)
    except Exception as exc:  # noqa: BLE001
        out["jsonschema"] = ("error", str(exc))

    # fastjsonschema: compiles the schema to Python; raises on invalid.
    try:
        fn = fastjsonschema.compile(schema)

        def _fj(i: object, fn: Callable[[object], object] = fn) -> bool:
            try:
                fn(i)
                return True
            except fastjsonschema.JsonSchemaException:
                return False

        out["fastjsonschema"] = _fj
    except Exception as exc:  # noqa: BLE001
        out["fastjsonschema"] = ("error", str(exc))

    # jsonschema-rs (Rust): format annotation-only to match the consensus.
    try:
        try:
            rv = jsonschema_rs.validator_for(schema, validate_formats=False)
        except TypeError:
            rv = jsonschema_rs.validator_for(schema)
        out["jsonschema-rs"] = lambda i, rv=rv: rv.is_valid(i)
    except Exception as exc:  # noqa: BLE001
        out["jsonschema-rs"] = ("error", str(exc))

    return out


def time_fn(fn: Validator, instances: list[object]) -> dict[str, object]:
    valid = 0
    try:
        for i in instances:
            if fn(i) is True:
                valid += 1
    except Exception as exc:  # noqa: BLE001
        return {"error": str(exc)}
    for _ in range(WARMUP):
        for i in instances:
            fn(i)
    times: list[int] = []
    for _ in range(ROUNDS):
        t0 = time.perf_counter_ns()
        for i in instances:
            fn(i)
        times.append(time.perf_counter_ns() - t0)
    return {"ns": statistics.median(times), "valid": valid}


def geomean(values: list[float]) -> float:
    return math.exp(sum(math.log(v) for v in values) / len(values)) if values else math.nan


def fmt(v: float | None) -> str:
    if v is None:
        return "    -   "
    return (f"{v:.0f} ns" if v < 1000 else f"{v / 1000:.2f} us").rjust(8)


def main() -> int:
    names = sorted(d.name for d in SCHEMAS.iterdir() if (d / "schema.json").exists()) if SCHEMAS.exists() else []
    rows: list[dict[str, object]] = []

    for orig in names:
        if not (GEN / sanitize(orig)).exists():
            continue
        schema = json.loads((SCHEMAS / orig / "schema.json").read_text(encoding="utf-8"))
        instances = load_instances(SCHEMAS / orig / "instances.jsonl")
        validators = build_validators(orig, schema)

        dialect = str(schema.get("$schema", "?")).replace("https://json-schema.org/", "").replace("http://json-schema.org/", "").replace("/schema", "").rstrip("#")
        per: dict[str, float | None] = {}
        valid: dict[str, object] = {}
        for impl in IMPLS:
            spec = validators[impl]
            if isinstance(spec, tuple):
                per[impl] = None
                valid[impl] = "ERR"
                continue
            # fastjsonschema MUTATES its input (fills `default`s); give it a private deep copy so it cannot
            # corrupt the pristine list the OTHER validators (notably jsonschema-rs, which runs after it) see.
            data = copy.deepcopy(instances) if impl == "fastjsonschema" else instances
            r = time_fn(spec, data)
            if "error" in r:
                per[impl] = None
                valid[impl] = "ERR"
                continue
            per[impl] = r["ns"] / len(instances)  # type: ignore[operator]
            valid[impl] = r["valid"]

        rows.append({"name": orig, "n": len(instances), "dialect": dialect, "per": per, "valid": valid})
        base = valid["corvus-py"]
        agree = all(valid[i] in ("-", "ERR", base) for i in IMPLS)
        sys.stderr.write(f"{orig} ({dialect}, {len(instances)} inst): valid {valid} {'' if agree else '  <-- DISAGREEMENT'}\n")

    # ---- report ----
    print("\n# Validation throughput — ns per instance (warm, compile excluded). Lower is better.\n")
    # Ratio column compares corvus-py to a reference impl: the pure-Python jsonschema when present, else the
    # last non-corvus impl in IMPLS (so a filtered 2-way run still shows a head-to-head ratio).
    ref = "jsonschema" if "jsonschema" in IMPLS else next((i for i in reversed(IMPLS) if i != "corvus-py"), None)
    print("schema".ljust(22) + "dialect".ljust(12) + "inst".rjust(7) + "  " + "".join(i.rjust(14) for i in IMPLS) + f"   corvus vs {ref or '-'}")
    print("-" * (22 + 12 + 7 + 2 + 14 * len(IMPLS) + 16))
    for r in rows:
        per = r["per"]  # type: ignore[assignment]
        ratio = f"{per[ref] / per['corvus-py']:.1f}x" if ref and per.get("corvus-py") and per.get(ref) else ""
        print(str(r["name"]).ljust(22) + str(r["dialect"]).ljust(12) + str(r["n"]).rjust(7) + "  "
              + "".join(fmt(per[i]).rjust(14) for i in IMPLS) + "   " + ratio.rjust(8))

    print("\n# Geometric mean ns/instance (over schemas each impl validated):")
    for impl in IMPLS:
        vals = [float(r["per"][impl]) for r in rows if r["per"][impl]]  # type: ignore[index]
        errs = [str(r["name"]) for r in rows if r["valid"][impl] == "ERR"]  # type: ignore[index]
        g = geomean(vals)
        print(f"  {impl.ljust(14)} {f'{g:.1f} ns' if vals else '-'}   (ran {len(vals)}/{len(rows)}"
              + (f'; failed: {", ".join(errs)}' if errs else "") + ")")

    # ---- correctness-gated: consensus valid-count = ground truth; only a correct validator earns credit. ----
    def consensus(r: dict[str, object]) -> int | None:
        counts: dict[int, int] = {}
        for i in IMPLS:
            v = r["valid"][i]  # type: ignore[index]
            if isinstance(v, int):
                counts[v] = counts.get(v, 0) + 1
        return max(counts, key=lambda k: counts[k]) if counts else None

    def correct(impl: str, r: dict[str, object]) -> bool:
        v = r["valid"][impl]  # type: ignore[index]
        return isinstance(v, int) and v == consensus(r) and r["per"][impl] is not None  # type: ignore[index]

    disagree = [str(r["name"]) for r in rows if any(isinstance(r["valid"][i], int) and r["valid"][i] != consensus(r) for i in IMPLS)]  # type: ignore[index]
    print(f"\n# Cross-check: {len(rows) - len(disagree)}/{len(rows)} schemas agree on valid-counts."
          + (f" Disagreements: {', '.join(disagree)}" if disagree else ""))

    print("\n# Geomean ns/instance over each validator's CORRECT schemas only:")
    for impl in IMPLS:
        vals = [float(r["per"][impl]) for r in rows if correct(impl, r) and r["per"][impl]]  # type: ignore[index]
        print(f"  {impl.ljust(14)} {f'{geomean(vals):.1f} ns' if vals else '-'}   (correct on {sum(1 for r in rows if correct(impl, r))}/{len(rows)})")

    wins = {i: 0 for i in IMPLS}
    for r in rows:
        c = [i for i in IMPLS if correct(i, r)]
        if c:
            wins[min(c, key=lambda i: r["per"][i])] += 1  # type: ignore[index]
    print("\n# Fastest CORRECT validator, schemas won: " + "  ".join(f"{i}={wins[i]}" for i in IMPLS))

    # Apples-to-apples: for each non-corvus impl, geomean of EVERY impl over just the schemas where THAT impl is
    # correct (so a faster-but-narrower validator is compared on its own ground, not credited for skipping the
    # hard schemas it cannot handle).
    for refimpl in IMPLS:
        if refimpl == "corvus-py":
            continue
        common = [r for r in rows if correct(refimpl, r)]
        print(f"\n# Geomean ns/instance over the {len(common)} schemas where {refimpl} is CORRECT:")
        for impl in IMPLS:
            vals = [float(r["per"][impl]) for r in common if r["per"][impl]]  # type: ignore[index]
            print(f"  {impl.ljust(14)} {f'{geomean(vals):.1f} ns' if vals else '-'}   (of {len(common)})")
    return 0


if __name__ == "__main__":
    sys.exit(main())