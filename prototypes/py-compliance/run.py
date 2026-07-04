#!/usr/bin/env python3
"""JSON-Schema-Test-Suite compliance harness for the Python model engine.

For each test group it generates a Python package from the group's schema (via the CLI `--engine Python`),
imports the package, and runs each test instance through the root `evaluate`, comparing to the expected
`valid`. Numbers in the test data are parsed as Decimal to match the runtime's decode_and_parse (exact
numeric validation). Generation / import failures are reported per group so the gaps drive what to build next.

Usage:
    python run.py                 # the covered-keyword subset
    python run.py --all           # every draft2020-12 file
    python run.py type enum       # named files (without .json)
    python run.py --show 40       # also print up to N individual failures
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLI = ROOT / "src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll"
SUITE = ROOT / "JSON-Schema-Test-Suite/tests/draft2020-12"
WORK = Path("/tmp/pycompliance")

# Format is annotation-only by default in the main suite; the optional/format suite asserts it.
_ASSERT_FORMAT = "false"

# The keyword files the current handler family targets.
COVERED = [
    "type", "enum", "const", "required", "properties", "additionalProperties", "patternProperties",
    "pattern", "boolean_schema",
    "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf",
    "minLength", "maxLength", "minItems", "maxItems", "minProperties", "maxProperties",
    "allOf", "anyOf", "oneOf", "not", "if-then-else",
    "items", "prefixItems", "contains", "maxContains", "minContains", "uniqueItems",
    "propertyNames", "dependentRequired", "dependentSchemas",
    "unevaluatedProperties", "unevaluatedItems",
]


def generate(schema: object, name: str) -> Path | None:
    out = WORK / name
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    schema_path = out / "schema.json"
    schema_path.write_text(json.dumps(schema))
    proc = subprocess.run(
        ["dotnet", str(CLI), "jsonschema", str(schema_path), "--engine", "Python",
         "--assertFormat", _ASSERT_FORMAT, "--outputPath", str(out)],
        capture_output=True, text=True,
    )
    return out if proc.returncode == 0 and (out / "__init__.py").exists() else None


def load_evaluate(pkg_dir: Path, name: str):  # noqa: ANN201 - returns a callable.
    parent = str(pkg_dir.parent)
    if parent not in sys.path:
        sys.path.insert(0, parent)
    for module in list(sys.modules):
        if module == name or module.startswith(name + "."):
            del sys.modules[module]
    import importlib

    pkg = importlib.import_module(name)
    return pkg.evaluate


def run_file(stem: str, failures: list[tuple[str, str, str, str]]) -> tuple[int, int]:
    text = (SUITE / f"{stem}.json").read_text()
    groups_schema = json.loads(text)
    groups_data = json.loads(text, parse_float=Decimal)
    total = passed = 0
    for i, group in enumerate(groups_schema):
        name = f"{stem}_{i}"
        pkg_dir = generate(group["schema"], name)
        evaluate = None
        err = None
        if pkg_dir is None:
            err = "GEN-FAIL"
        else:
            try:
                evaluate = load_evaluate(pkg_dir, name)
            except Exception as exc:  # noqa: BLE001 - report any import failure.
                err = f"IMPORT-FAIL: {exc}"

        for j, test in enumerate(group["tests"]):
            total += 1
            if err is not None:
                failures.append((stem, group["description"], test["description"], err))
                continue
            data = groups_data[i]["tests"][j]["data"]
            try:
                got: object = evaluate(data)
            except Exception as exc:  # noqa: BLE001 - report any evaluation failure.
                got = f"EXC:{exc}"
            if got == test["valid"]:
                passed += 1
            else:
                failures.append((stem, group["description"], test["description"], f"got {got} want {test['valid']}"))
    return total, passed


def main() -> int:
    global SUITE, _ASSERT_FORMAT
    parser = argparse.ArgumentParser()
    parser.add_argument("files", nargs="*", help="file stems (without .json); default = covered subset")
    parser.add_argument("--all", action="store_true", help="run every draft2020-12 file")
    parser.add_argument("--optional-format", action="store_true", help="run optional/format/*.json under --assertFormat true")
    parser.add_argument("--show", type=int, default=0, help="print up to N individual failures")
    args = parser.parse_args()

    if args.optional_format:
        SUITE = SUITE / "optional" / "format"
        _ASSERT_FORMAT = "true"

    if args.all or (args.optional_format and not args.files):
        stems = sorted(p.stem for p in SUITE.glob("*.json"))
    elif args.files:
        stems = args.files
    else:
        stems = COVERED

    WORK.mkdir(parents=True, exist_ok=True)
    failures: list[tuple[str, str, str, str]] = []
    grand_total = grand_passed = 0
    print(f"{'file':<22}{'pass':>8}{'total':>8}  rate")
    for stem in stems:
        if not (SUITE / f"{stem}.json").exists():
            print(f"{stem:<22}  (missing)")
            continue
        total, passed = run_file(stem, failures)
        grand_total += total
        grand_passed += passed
        rate = f"{100 * passed / total:5.1f}%" if total else "  n/a"
        print(f"{stem:<22}{passed:>8}{total:>8}  {rate}")

    print("-" * 48)
    rate = f"{100 * grand_passed / grand_total:5.1f}%" if grand_total else "n/a"
    print(f"{'TOTAL':<22}{grand_passed:>8}{grand_total:>8}  {rate}")

    if args.show:
        print("\nfailures:")
        for stem, group, test, detail in failures[: args.show]:
            print(f"  [{stem}] {group} / {test}: {detail}")

    return 0


if __name__ == "__main__":
    sys.exit(main())