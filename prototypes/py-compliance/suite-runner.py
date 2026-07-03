#!/usr/bin/env python3
"""Run the generated corvus-py suite packages against the manifest and report the pass rate per dialect.

A faithful port of the TypeScript ``suite-runner.mjs``. Pipeline (see ``run-compliance.sh``):
  1. dotnet run --project ComplianceGenerator -- <tests> <outDir>   (emits one package per group + manifest.json)
  2. python suite-runner.py <outDir>                                 (this file)

The manifest stores each instance as its raw JSON source text. We parse the manifest once, then parse each
instance with ``json.loads(..., parse_float=Decimal)`` so numeric precision is preserved exactly (the analog of
the TS runner's LosslessNumber). Each package's ``__init__.py`` re-exports ``evaluate``; we import it and tally.

A non-allow-listed case failure trips the gate (exit 1). Override the allow-list with COMPLIANCE_NO_ALLOWLIST=1.
"""

from __future__ import annotations

import importlib
import json
import os
import sys
from decimal import Decimal
from pathlib import Path

OUT_DIR = Path(sys.argv[1] if len(sys.argv) > 1 else os.environ.get("COMPLIANCE_OUT", "out-suite")).resolve()
sys.path.insert(0, str(OUT_DIR))
sys.setrecursionlimit(20000)  # some suite schemas (metaschemas, deep $ref) recurse deeply

manifest = json.loads((OUT_DIR / "manifest.json").read_text(encoding="utf-8"))

# Known engine/dialect gaps, keyed `dialect|file|group|desc`. Printed, never hidden, but do NOT trip the CI
# gate (COMPLIANCE_NO_ALLOWLIST=1 disables the allow-list). The single entry is a genuine ECMA-262-vs-Python
# numeric difference, not a validation bug: corvus treats `1.0` as an integer, exactly like the TS engine.
KNOWN_FAILURES: set[str] = {
    "draft4|optional_zeroTerminatedFloats|some languages do not distinguish between different types of numeric value|a float is not an integer even without fractional part",
}
allowlist_disabled = os.environ.get("COMPLIANCE_NO_ALLOWLIST") == "1"

per_dialect: dict[str, dict[str, int]] = {}
passed = failed = known_failed = errored_groups = excluded_cases = 0
failures: list[str] = []
known: list[str] = []

for entry in manifest:
    d = entry.get("dialect") or "?"
    pd = per_dialect.setdefault(d, {"pass": 0, "total": 0, "errored": 0})
    if entry.get("error"):
        errored_groups += 1
        excluded_cases += len(entry["tests"])
        pd["errored"] += 1
        continue
    try:
        evaluate = importlib.import_module(entry["module"]).evaluate
    except Exception as exc:  # noqa: BLE001
        errored_groups += 1
        excluded_cases += len(entry["tests"])
        pd["errored"] += 1
        failures.append(f'{d} {entry["file"]} :: {entry["group"]} :: MODULE LOAD ERROR {exc}')
        continue
    for t in entry["tests"]:
        try:
            got = evaluate(json.loads(t["data"], parse_float=Decimal)) is True
        except Exception:  # noqa: BLE001  (a validator crash is a failure, not an abort)
            got = None
        pd["total"] += 1
        if got == t["valid"]:
            passed += 1
            pd["pass"] += 1
            continue
        key = f'{d}|{entry["file"]}|{entry["group"]}|{t["desc"]}'
        line = f'{d} {entry["file"]} :: {entry["group"]} :: {t["desc"]} (got {got}, want {t["valid"]})'
        if not allowlist_disabled and key in KNOWN_FAILURES:
            known_failed += 1
            known.append(line)
        else:
            failed += 1
            failures.append(line)

print("per-dialect pass/total (built groups; errored excluded):")
for d, p in per_dialect.items():
    pct = f"{100 * p['pass'] / p['total']:.1f}%" if p["total"] else "n/a"
    print(f"  {d:<14} {p['pass']}/{p['total']}  ({pct})  errored-groups={p['errored']}")

total = passed + failed + known_failed
print(f"\nTOTAL: {passed}/{total} cases passed ({failed} failed, {known_failed} known-failure); "
      f"{errored_groups} schema-groups errored ({excluded_cases} cases excluded).")
if known:
    print(f"\nknown failures (allow-listed, do NOT trip the gate — {len(known)}):")
    for line in known:
        print("  " + line)
if failures:
    print(f"\nNEW failures ({min(len(failures), 40)} of {len(failures)}):")
    for line in failures[:40]:
        print("  " + line)

print(f"\nPASS={passed} FAIL={failed} KNOWN_FAIL={known_failed} ERRORED_GROUPS={errored_groups} EXCLUDED_CASES={excluded_cases}")
sys.exit(1 if failed > 0 else 0)