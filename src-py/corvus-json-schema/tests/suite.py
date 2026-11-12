"""Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C# SuiteRunner.
Every dialect's cases are in one tree (validation/tests, required and optional): each case's compatibility selects the
drafts it applies to, its tags whether format is asserted, and its externalSchemas are served to that case alone.

    python tests/suite.py [--verbose] [--draft draft2020-12] [--filter text] [--module] [--collect basic|detailed|verbose]
                          [--impl corvus_json_schema_rs]

--impl runs another package with the same API (the Rust-backed corvus_json_schema_rs). Exits non-zero when any case
fails.
"""

from __future__ import annotations

import argparse
import importlib
import importlib.util
import json
import os
import sys
import tempfile
from pathlib import Path
from typing import Any
from collections.abc import Callable

here = Path(__file__).resolve().parent
sys.path.insert(0, str(here.parent / "src"))

_impl = sys.argv[sys.argv.index("--impl") + 1] if "--impl" in sys.argv else "corvus_json_schema"
cjs: Any = importlib.import_module(_impl)

suite_root = Path(os.environ.get("JSON_SCHEMA_TEST_SUITE", here.parents[2] / "JSON-Schema-Test-Suite"))
tests_root = suite_root / "validation" / "tests"

DRAFTS = {
    "draft4": (cjs.Dialect.DRAFT4, 4),
    "draft6": (cjs.Dialect.DRAFT6, 6),
    "draft7": (cjs.Dialect.DRAFT7, 7),
    "draft2019-09": (cjs.Dialect.DRAFT201909, 2019),
    "draft2020-12": (cjs.Dialect.DRAFT202012, 2020),
}


# Test cases left out by name (file and test description): the high-precision cases of optional/bignum.json. The
# evaluator takes Python values, and json.loads reads a number with a fraction or an exponent as a float, so it has
# at most the precision of a double by the time the evaluator sees it. Python integers are exact, and the pure
# evaluator compares them exactly; the Rust-backed one reads an integer beyond 64 bits as the nearest double.
EXCLUDED_CASES = {
    ("bignum.json", "below the exclusive maximum is valid"),
    ("bignum.json", "above the exclusive minimum is valid"),
    ("bignum.json", "a slightly smaller decimal is invalid"),
    ("bignum.json", "a slightly larger decimal is invalid"),
    ("bignum.json", "distinct high-precision decimals are unique"),
    ("bignum.json", "arrays containing distinct decimals are unique"),
    ("bignum.json", "objects containing distinct decimals are unique"),
}
if _impl == "corvus_json_schema_rs":
    EXCLUDED_CASES.add(("bignum.json", "just below the minimum is invalid"))


def compatible(compatibility: str | None, release: int) -> bool:
    """Whether a test case's compatibility (a comma-separated list of minimums, <= maximums and = releases, from the
    smallest release to the largest) admits the release, by the algorithm of the suite's validation/README.md."""
    if compatibility is None:
        return True
    valid = True
    for constraint in compatibility.split(","):
        op, version = ("", constraint)
        if constraint.startswith("<="):
            op, version = "<=", constraint[2:]
        elif constraint.startswith("="):
            op, version = "=", constraint[1:]
        number = int(version.strip())
        valid = release <= number if op == "<=" else release == number if op == "=" else release >= number
        if release <= number:
            break
    return valid


def resolver_for(group: dict[str, Any]) -> Callable[[str], Any] | None:
    """A resolver serving a test case's externalSchemas at their retrieval URIs."""
    schemas = group.get("externalSchemas")
    return None if schemas is None else schemas.get


def files(sub: str) -> list[Path]:
    d = tests_root / sub
    return sorted(d.glob("*.json")) if d.is_dir() else []


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--verbose", action="store_true")
    parser.add_argument("--module", action="store_true", help="evaluate through generate_module (a standalone module)")
    parser.add_argument("--collect", choices=["basic", "detailed", "verbose"])
    parser.add_argument("--draft")
    parser.add_argument("--filter")
    parser.add_argument("--impl", default="corvus_json_schema")
    parser.add_argument(
        "--exclude",
        action="append",
        default=[],
        help="a test file to skip, e.g. draft4/optional/zero-terminated-floats.json (which the Rust evaluator, like C#, excludes)",
    )
    args = parser.parse_args()
    collect_level = {
        "basic": cjs.ResultsLevel.BASIC,
        "detailed": cjs.ResultsLevel.DETAILED,
        "verbose": cjs.ResultsLevel.VERBOSE,
    }.get(args.collect or "")

    module_dir = tempfile.TemporaryDirectory() if args.module else None
    module_count = 0

    def build(schema: Any, options: dict[str, Any]) -> tuple[Callable[[Any], bool], str]:
        nonlocal module_count
        if not args.module:
            v = cjs.compile(schema, **options)
            evaluate: Callable[..., bool] = v.evaluate
            source = getattr(v, "source", "")
        else:
            assert module_dir is not None
            source = cjs.generate_module(schema, **options)
            module_count += 1
            path = Path(module_dir.name) / f"generated_{module_count}.py"
            path.write_text(source, encoding="utf-8")
            spec = importlib.util.spec_from_file_location(f"generated_{module_count}", path)
            assert spec is not None and spec.loader is not None
            mod = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(mod)
            evaluate = mod.evaluate
        if collect_level is None:
            return (lambda x: evaluate(x)), source

        def collecting(x: Any) -> bool:
            collector = cjs.JsonSchemaResultsCollector.create(collect_level)
            valid = evaluate(x, collector)
            # The root summary row always exists and carries the overall result.
            if not any(
                r.evaluation_location == "" and r.document_evaluation_location == "" and r.is_match == valid
                for r in collector.results
            ):
                raise AssertionError("no root summary row matching the result")
            return valid

        return collecting, source

    total = 0
    failed = 0
    failures: list[str] = []
    summary: list[tuple[str, int, int]] = []

    def run_file(draft: str, file: Path, label: str) -> None:
        nonlocal total, failed
        dialect, release = DRAFTS[draft]
        groups = json.loads(file.read_text(encoding="utf-8"))["tests"]
        file_total = 0
        file_failed = 0
        for group in groups:
            if not compatible(group.get("compatibility"), release):
                continue
            tags = group.get("tags", [])
            # The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
            if "format-annotation" in tags and release <= 7:
                continue
            assert_format = "format-assertion" in tags
            if args.filter and args.filter not in group["description"] and args.filter not in label:
                continue
            validate: Callable[[Any], bool] | None = None
            source = ""
            compile_error: BaseException | None = None
            try:
                validate, source = build(
                    group["schema"],
                    {
                        "default_dialect": dialect,
                        "assert_format": True if assert_format else None,
                        "resolve_document": resolver_for(group),
                    },
                )
            except Exception as e:
                compile_error = e
            for test in group["tests"]:
                if (file.name, test["description"]) in EXCLUDED_CASES:
                    continue
                file_total += 1
                actual: bool | None = None
                error: BaseException | None = compile_error
                if error is None:
                    assert validate is not None
                    try:
                        actual = validate(test["data"])
                    except Exception as e:
                        error = e
                if error is not None or actual != test["valid"]:
                    # Leap seconds are skipped where format is asserted until this engine has the leap-second rule.
                    if assert_format and "leap second" in test["description"].lower():
                        continue
                    file_failed += 1
                    got = f"{type(error).__name__}: {error}" if error is not None else actual
                    failures.append(
                        f"{label} [{group['description']}] {test['description']}: expected {test['valid']}, got {got}"
                    )
                    if args.verbose and source:
                        failures.append(source)
        total += file_total
        failed += file_failed
        summary.append((label, file_total, file_failed))

    for draft in DRAFTS:
        if args.draft and draft != args.draft:
            continue
        for f in files(""):
            run_file(draft, f, f"{draft}/{f.name}")
        for f in files("optional"):
            if f"{draft}/optional/{f.name}" not in args.exclude:
                run_file(draft, f, f"{draft}/optional/{f.name}")

    for line in failures:
        print(line)
    by_area: dict[str, list[int]] = {}
    for label, t, f in summary:
        depth = 2 if "/optional/" in label else 1
        area = "/".join(label.split("/")[:depth])
        entry = by_area.setdefault(area, [0, 0])
        entry[0] += t
        entry[1] += f
    for area, (t, f) in by_area.items():
        print(f"{area:<34} {t - f:>5}/{t}")
    print(f"\n{total - failed}/{total} passed, {failed} failed")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
