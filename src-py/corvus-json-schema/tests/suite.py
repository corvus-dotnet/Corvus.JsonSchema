"""Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C# SuiteRunner:
required and optional tests with format as an annotation, optional/format with format asserted.

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
remotes_root = suite_root / "remotes"

DRAFTS = {
    "draft4": cjs.Dialect.DRAFT4,
    "draft6": cjs.Dialect.DRAFT6,
    "draft7": cjs.Dialect.DRAFT7,
    "draft2019-09": cjs.Dialect.DRAFT201909,
    "draft2020-12": cjs.Dialect.DRAFT202012,
}

_remote_cache: dict[Path, Any] = {}


def resolve_remote(uri: str) -> Any:
    prefix = "http://localhost:1234/"
    if not uri.startswith(prefix):
        return None
    file = remotes_root / uri[len(prefix) :]
    if not file.is_file():
        return None
    if file not in _remote_cache:
        _remote_cache[file] = json.loads(file.read_text(encoding="utf-8"))
    return _remote_cache[file]


def files(draft: str, sub: str) -> list[Path]:
    d = suite_root / "tests" / draft / sub
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
        help="a test file to skip, e.g. draft4/optional/zeroTerminatedFloats.json (which the Rust evaluator, like C#, excludes)",
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

    def run_file(draft: str, file: Path, label: str, assert_format: bool) -> None:
        nonlocal total, failed
        groups = json.loads(file.read_text(encoding="utf-8"))
        file_total = 0
        file_failed = 0
        for group in groups:
            if args.filter and args.filter not in group["description"] and args.filter not in label:
                continue
            validate: Callable[[Any], bool] | None = None
            source = ""
            compile_error: BaseException | None = None
            try:
                validate, source = build(
                    group["schema"],
                    {
                        "default_dialect": DRAFTS[draft],
                        "assert_format": True if assert_format else None,
                        "resolve_document": resolve_remote,
                    },
                )
            except Exception as e:
                compile_error = e
            for test in group["tests"]:
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
                    # Leap seconds are skipped in the format run, as in the C# runner.
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
        for f in files(draft, ""):
            run_file(draft, f, f"{draft}/{f.name}", False)
        for f in files(draft, "optional"):
            if f"{draft}/optional/{f.name}" not in args.exclude:
                run_file(draft, f, f"{draft}/optional/{f.name}", False)
        for f in files(draft, "optional/format"):
            run_file(draft, f, f"{draft}/optional/format/{f.name}", True)

    for line in failures:
        print(line)
    by_area: dict[str, list[int]] = {}
    for label, t, f in summary:
        depth = 3 if "/optional/format/" in label else 2 if "/optional/" in label else 1
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
