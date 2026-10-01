"""A/B timing of two code-generation settings (environment variables read by codegen.py), in alternating fresh
processes per corpus so that drift in the machine's speed affects both alike.

    python bench/ab.py --schemas <jsonschema-benchmark>/schemas --a CORVUS_PY_PROBE=8 --b CORVUS_PY_PROBE=16 [--rounds 3]

Prints, per corpus, the best time of each side (microseconds per pass) and B/A, then the geometric mean of B/A.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import subprocess
import sys
from pathlib import Path

here = Path(__file__).resolve().parent

ONE = r"""
import json, sys, time, timeit
sys.path.insert(0, sys.argv[3])
import corvus_json_schema
schema = json.load(open(sys.argv[1], encoding="utf-8"))
instances = [json.loads(line) for line in open(sys.argv[2], encoding="utf-8")]
validate = corvus_json_schema.compile(schema)
def run():
    for x in instances:
        validate(x)
start = time.perf_counter(); run(); number = max(1, int(0.05 / max(time.perf_counter() - start, 1e-7)))
print(min(timeit.repeat(run, number=number, repeat=5)) / number * 1e6)
"""


def env_of(assignments: list[str]) -> dict[str, str]:
    env = dict(os.environ)
    for a in assignments:
        k, _, v = a.partition("=")
        env[k] = v
    return env


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--schemas", default=os.environ.get("JSONSCHEMA_BENCHMARK_SCHEMAS"))
    parser.add_argument("--a", action="append", default=[])
    parser.add_argument("--b", action="append", default=[])
    parser.add_argument("--rounds", type=int, default=3)
    parser.add_argument("--only")
    args = parser.parse_args()
    sys.path.insert(0, str(here))
    from corpora import noformat_schema

    only = set(args.only.split(",")) if args.only else None
    envs = (env_of(args.a), env_of(args.b))
    ratios = []
    for corpus in sorted(Path(args.schemas).iterdir()):
        if not (corpus / "instances.jsonl").is_file() or (only and corpus.name not in only):
            continue
        cmd = [
            sys.executable,
            "-c",
            ONE,
            str(noformat_schema(corpus)),
            str(corpus / "instances.jsonl"),
            str(here.parent / "src"),
        ]
        best = [math.inf, math.inf]
        for _ in range(args.rounds):
            for side in (0, 1):
                out = subprocess.run(cmd, env=envs[side], capture_output=True, text=True, check=True).stdout
                best[side] = min(best[side], float(out.strip().splitlines()[-1]))
        ratio = best[1] / best[0]
        ratios.append(ratio)
        print(f"{corpus.name:<26}{best[0]:>12.1f}{best[1]:>12.1f}{ratio:>8.3f}", flush=True)
    print(f"{'geometric mean B/A':<50}{math.exp(sum(map(math.log, ratios)) / len(ratios)):>8.3f}")
    print(json.dumps({"a": args.a, "b": args.b}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
