"""Times this package's warm validation over the jsonschema-benchmark corpora in one process, for quick before/after
comparisons while tuning the generated code (bench/corpora.py is the faithful, process-per-run measurement).

    python bench/inprocess.py --schemas <jsonschema-benchmark>/schemas [--save base.json] [--compare base.json]

Prints microseconds per pass over each corpus (the best of seven, each long enough to time) and, with --compare, the
ratio to a saved run and their geometric mean.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
import timeit
from pathlib import Path

here = Path(__file__).resolve().parent
sys.path.insert(0, str(here.parent / "src"))

import corvus_json_schema  # noqa: E402

sys.path.insert(0, str(here))
from corpora import noformat_schema  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--schemas", default=os.environ.get("JSONSCHEMA_BENCHMARK_SCHEMAS"))
    parser.add_argument("--only")
    parser.add_argument("--save")
    parser.add_argument("--compare")
    args = parser.parse_args()
    only = set(args.only.split(",")) if args.only else None
    base = json.loads(Path(args.compare).read_text()) if args.compare else {}
    out: dict[str, float] = {}
    ratios: list[float] = []
    for corpus in sorted(Path(args.schemas).iterdir()):
        if not (corpus / "instances.jsonl").is_file() or (only and corpus.name not in only):
            continue
        schema = json.loads(noformat_schema(corpus).read_text(encoding="utf-8"))
        instances = [json.loads(line) for line in (corpus / "instances.jsonl").read_text(encoding="utf-8").splitlines()]
        validate = corvus_json_schema.compile(schema)

        def run(validate=validate, instances=instances) -> None:  # type: ignore[no-untyped-def]
            for x in instances:
                validate(x)

        start = time.perf_counter()
        run()
        number = max(1, int(0.05 / max(time.perf_counter() - start, 1e-7)))
        us = min(timeit.repeat(run, number=number, repeat=7)) / number * 1e6
        out[corpus.name] = us
        line = f"{corpus.name:<26}{us:>12.1f}"
        if corpus.name in base:
            ratio = us / base[corpus.name]
            ratios.append(ratio)
            line += f"{base[corpus.name]:>12.1f}{ratio:>8.3f}"
        print(line, flush=True)
    if ratios:
        print(f"{'geometric mean':<50}{math.exp(sum(map(math.log, ratios)) / len(ratios)):>8.3f}")
    if args.save:
        Path(args.save).write_text(json.dumps(out, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
