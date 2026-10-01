"""Runs the JSON-Schema-Test-Suite annotation tests (JSON-Schema-Test-Suite/annotations) through a verbose results
collector, as the C# AnnotationSuiteTests do: every draft, cases filtered by "compatibility", format asserted per
vocabulary, and each assertion compared with the annotations grouped by instance location, keyword and schema
location. Exits non-zero on any failure.

    python tests/annotations.py
"""

from __future__ import annotations

import importlib
import json
import os
import sys
from pathlib import Path
from typing import Any

here = Path(__file__).resolve().parent
sys.path.insert(0, str(here.parent / "src"))

from corvus_json_schema.runtime import equal  # noqa: E402

# --impl corvus_json_schema_rs runs the Rust-backed package, which has the same API.
cjs: Any = importlib.import_module(
    sys.argv[sys.argv.index("--impl") + 1] if "--impl" in sys.argv else "corvus_json_schema"
)

suite_root = Path(os.environ.get("JSON_SCHEMA_TEST_SUITE", here.parents[2] / "JSON-Schema-Test-Suite"))
directory = suite_root / "annotations" / "tests"
DRAFTS = {
    "draft4": (cjs.Dialect.DRAFT4, "4"),
    "draft6": (cjs.Dialect.DRAFT6, "6"),
    "draft7": (cjs.Dialect.DRAFT7, "7"),
    "draft2019-09": (cjs.Dialect.DRAFT201909, "2019"),
    "draft2020-12": (cjs.Dialect.DRAFT202012, "2020"),
}
ORDER = ["3", "4", "6", "7", "2019", "2020"]


def compatible(draft: str, compat: str) -> bool:
    level = ORDER.index(draft)
    if compat.startswith("<="):
        bound = compat[2:]
        return bound in ORDER and level <= ORDER.index(bound)
    return compat in ORDER and level >= ORDER.index(compat)


def resolve_remote(uri: str) -> Any:
    prefix = "http://localhost:1234/"
    if not uri.startswith(prefix):
        return None
    file = suite_root / "remotes" / uri[len(prefix) :]
    return json.loads(file.read_text(encoding="utf-8")) if file.is_file() else None


def main() -> int:
    total = 0
    failed = 0
    for draft, (dialect, compat_level) in DRAFTS.items():
        for file in sorted(directory.glob("*.json")):
            suite = json.loads(file.read_text(encoding="utf-8"))
            for group in suite["suite"]:
                if "compatibility" in group and not compatible(compat_level, group["compatibility"]):
                    continue
                validator = cjs.compile(group["schema"], default_dialect=dialect, resolve_document=resolve_remote)
                for test in group["tests"]:
                    collector = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.VERBOSE)
                    validator.evaluate(test["instance"], collector)
                    produced = cjs.collect_annotations(collector)
                    for assertion in test["assertions"]:
                        total += 1
                        actual = produced.get(assertion["location"], {}).get(assertion["keyword"])
                        expected = assertion["expected"]
                        ok = actual is None if not expected else actual is not None and equal(actual, expected)
                        if not ok:
                            failed += 1
                            print(
                                f"{draft}/{file.name} [{group['description']}] instance {json.dumps(test['instance'])} "
                                f"'{assertion['location']}' {assertion['keyword']}: expected {json.dumps(expected)}, "
                                f"actual {json.dumps(actual)}"
                            )
    print(f"{total - failed}/{total} annotation assertions passed")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
