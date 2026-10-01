"""Runs the jsonschema-benchmark corpora through Python validators in fresh processes, as the benchmark's harness does,
and prints a comparison table.

    python bench/corpora.py --schemas <jsonschema-benchmark>/schemas [--runs 3] [--only a,b] [--impls corvus,jsonschema]
    python bench/corpora.py --render bench/results/corpora-<stamp>.json

Each measurement is a fresh process per corpus and implementation: parse the instances, compile the schema, one cold
pass, warm up (at most 1000 passes or --warmup seconds), then the best of five warm passes. The implementations are:

- corvus: this package (pure Python, generated code);
- corvus-rs: the Rust-backed package (corvus_json_schema_rs), when it is installed;
- jsonschema: the reference implementation (pure Python, interpreted);
- fastjsonschema: generated code (pure Python; draft 4, 6 and 7 only);
- jsonschema-rs: Python bindings to the jsonschema Rust crate.

Results are written to bench/results/.
"""

from __future__ import annotations

import argparse
import json
import os
import statistics
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

here = Path(__file__).resolve().parent
IMPLEMENTATIONS = ["corvus", "corvus-rs", "jsonschema", "fastjsonschema", "jsonschema-rs"]

RUNNER = r"""
import json, math, sys, time
impl, corpus, warmup_seconds = sys.argv[1], sys.argv[2], float(sys.argv[3])
schema = json.load(open(sys.argv[4], encoding="utf-8"))
lines = open(corpus + "/instances.jsonl", encoding="utf-8").readlines()

parse_start = time.perf_counter_ns()
instances = [json.loads(line) for line in lines]
parse_end = time.perf_counter_ns()

import_start = time.perf_counter_ns()
if impl == "corvus":
    import corvus_json_schema
elif impl == "corvus-rs":
    import corvus_json_schema_rs
elif impl == "jsonschema":
    import jsonschema
elif impl == "fastjsonschema":
    import fastjsonschema
elif impl == "jsonschema-rs":
    import jsonschema_rs
import_end = time.perf_counter_ns()

compile_start = time.perf_counter_ns()
if impl == "corvus":
    validate = corvus_json_schema.compile(schema)
elif impl == "corvus-rs":
    validate = corvus_json_schema_rs.compile(schema).is_valid
elif impl == "jsonschema":
    validate = jsonschema.validators.validator_for(schema)(schema).is_valid
elif impl == "fastjsonschema":
    inner = fastjsonschema.compile(schema)
    def validate(x, inner=inner, error=fastjsonschema.JsonSchemaException):
        try:
            inner(x)
            return True
        except error:
            return False
elif impl == "jsonschema-rs":
    validate = jsonschema_rs.validator_for(schema).is_valid
compile_end = time.perf_counter_ns()

cold_start = time.perf_counter_ns()
valid = 0
for instance in instances:
    if validate(instance):
        valid += 1
cold_end = time.perf_counter_ns()

# A slow implementation (a cold pass over a second) gets no warm-up and one warm pass.
slow = cold_end - cold_start > 1e9
iterations = 0 if slow else min(1000, math.ceil(warmup_seconds * 1e9 / max(1, cold_end - cold_start)))
for _ in range(iterations):
    for instance in instances:
        validate(instance)

warm = None
for _ in range(1 if slow else 5):
    start = time.perf_counter_ns()
    for instance in instances:
        validate(instance)
    elapsed = time.perf_counter_ns() - start
    warm = elapsed if warm is None else min(warm, elapsed)

print(json.dumps({"cold": cold_end - cold_start, "warm": warm, "compile": compile_end - compile_start,
                  "parse": parse_end - parse_start, "import": import_end - import_start, "valid": valid, "total": len(instances)}))
"""


def strip_formats(value: Any) -> Any:
    """The benchmark's schema-noformat.json (the Makefile drops every ``format`` line of the schema's gron form): a
    ``format`` key goes when its value is a scalar or an empty container, and stays when it holds anything."""
    if isinstance(value, dict):
        return {
            k: strip_formats(v)
            for k, v in value.items()
            if not (k == "format" and (not isinstance(v, (dict, list)) or len(v) == 0))
        }
    if isinstance(value, list):
        return [strip_formats(v) for v in value]
    return value


def noformat_schema(corpus: Path) -> Path:
    out = here / ".corpora" / corpus.name / "schema-noformat.json"
    if not out.is_file():
        out.parent.mkdir(parents=True, exist_ok=True)
        schema = json.loads((corpus / "schema.json").read_text(encoding="utf-8"))
        out.write_text(json.dumps(strip_formats(schema)), encoding="utf-8")
    return out


def measure(impl: str, corpus: Path, runs: int, warmup: float) -> dict[str, Any] | None:
    results: list[dict[str, Any]] = []
    schema = noformat_schema(corpus)
    for _ in range(runs):
        r = subprocess.run(
            [sys.executable, "-c", RUNNER, impl, str(corpus), str(warmup), str(schema)],
            capture_output=True,
            text=True,
            env={**os.environ, "PYTHONPATH": str(here.parent / "src")},
            check=False,
        )
        if r.returncode != 0:
            return {"error": (r.stderr.strip().splitlines() or ["failed"])[-1][:120]}
        results.append(json.loads(r.stdout.strip().splitlines()[-1]))
    out: dict[str, Any] = {
        k: statistics.median(r[k] for r in results) for k in ("cold", "warm", "compile", "parse", "import")
    }
    out["valid"] = results[0]["valid"]
    out["total"] = results[0]["total"]
    return out


def ms(ns: float) -> str:
    return f"{ns / 1e6:9.2f}"


def render(data: dict[str, Any]) -> None:
    impls: list[str] = data["impls"]
    corpora: dict[str, dict[str, Any]] = data["corpora"]
    print(f"\nWarm pass (ms, best of five after warm-up); ratio is to corvus. Python {data['python']}.")
    header = f"{'corpus':<28}" + "".join(f"{i:>16}" for i in impls)
    print(header)
    ratios: dict[str, list[float]] = {i: [] for i in impls}
    for name, row in corpora.items():
        base = row.get("corvus", {}).get("warm")
        line = f"{name:<28}"
        for i in impls:
            r = row.get(i)
            if r is None or "error" in r:
                line += f"{'-':>16}"
                continue
            flag = "" if r["valid"] == r["total"] else "!"
            ratio = f" ({r['warm'] / base:5.2f})" if base and i != "corvus" else ""
            if base and i != "corvus":
                ratios[i].append(r["warm"] / base)
            line += f"{ms(r['warm']).strip() + flag + ratio:>16}"
        print(line)
    print(
        f"{'geometric mean ratio':<28}"
        + "".join(
            f"{(statistics.geometric_mean(ratios[i]) if ratios[i] else float('nan')):>16.2f}"
            if i != "corvus"
            else f"{'1.00':>16}"
            for i in impls
        )
    )
    print("\nCold pass and compile (ms):")
    print(f"{'corpus':<28}" + "".join(f"{i + ' cold':>18}{i + ' compile':>20}" for i in impls[:2]))
    for name, row in corpora.items():
        line = f"{name:<28}"
        for i in impls[:2]:
            r = row.get(i)
            line += f"{'-':>18}{'-':>20}" if r is None or "error" in r else f"{ms(r['cold']):>18}{ms(r['compile']):>20}"
        print(line)
    errors = [(n, i, r["error"]) for n, row in corpora.items() for i, r in row.items() if r and "error" in r]
    if errors:
        print("\nNot run:")
        for n, i, e in errors:
            print(f"  {n} {i}: {e}")
    invalid = [
        (n, i)
        for n, row in corpora.items()
        for i, r in row.items()
        if r and "error" not in r and r["valid"] != r["total"]
    ]
    if invalid:
        print("\nReported invalid instances (!): " + ", ".join(f"{n}/{i}" for n, i in invalid))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--schemas", default=os.environ.get("JSONSCHEMA_BENCHMARK_SCHEMAS"))
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--warmup", type=float, default=2.0, help="seconds of warm-up per run (the benchmark uses 10)")
    parser.add_argument("--only")
    parser.add_argument("--impls", default="corvus,jsonschema,fastjsonschema,jsonschema-rs")
    parser.add_argument("--render")
    args = parser.parse_args()
    if args.render:
        render(json.loads(Path(args.render).read_text(encoding="utf-8")))
        return 0
    if not args.schemas:
        parser.error("pass --schemas <jsonschema-benchmark>/schemas (or set JSONSCHEMA_BENCHMARK_SCHEMAS)")
    impls = args.impls.split(",")
    only = set(args.only.split(",")) if args.only else None
    corpora: dict[str, dict[str, Any]] = {}
    for corpus in sorted(Path(args.schemas).iterdir()):
        if not (corpus / "instances.jsonl").is_file() or (only and corpus.name not in only):
            continue
        row: dict[str, Any] = {}
        for impl in impls:
            row[impl] = measure(impl, corpus, args.runs, args.warmup)
        corpora[corpus.name] = row
        print(corpus.name, {i: (r or {}).get("warm", (r or {}).get("error")) for i, r in row.items()}, flush=True)
    data = {"python": sys.version.split()[0], "impls": impls, "corpora": corpora}
    out = here / "results" / f"corpora-{time.strftime('%Y%m%d-%H%M%S')}.json"
    out.parent.mkdir(exist_ok=True)
    out.write_text(json.dumps(data, indent=1), encoding="utf-8")
    render(data)
    print(f"\nWritten to {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
