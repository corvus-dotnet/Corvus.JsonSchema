"""Run the official JSON-Schema-Test-Suite against fastjsonschema and jsonschema-rs (Rust), per dialect.

Same suite corvus-py passes at 7848/7849. Each validator is driven the way it is meant to be used:
schemas parsed as native JSON (float), the dialect set explicitly (suite schemas carry no $schema), remote
refs (http://localhost:1234/...) + draft metaschemas resolved from the suite's remotes/ + bundled metaschemas,
and `format` asserted ONLY for the optional/format tests (annotation-only elsewhere, per the suite's design).
A schema the validator cannot compile counts every test in that group as ERR (can't-run), reported separately.
"""
from __future__ import annotations
import json, sys, copy
from pathlib import Path

import os
SUITE = Path(os.environ.get("SUITE_DIR", "../../JSON-Schema-Test-Suite"))
TESTS, REMOTES = SUITE / "tests", SUITE / "remotes"
DIALECTS = ["draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"]
SCHEMA_URI = {
    "draft4": "http://json-schema.org/draft-04/schema#",
    "draft6": "http://json-schema.org/draft-06/schema#",
    "draft7": "http://json-schema.org/draft-07/schema#",
    "draft2019-09": "https://json-schema.org/draft/2019-09/schema",
    "draft2020-12": "https://json-schema.org/draft/2020-12/schema",
}

import fastjsonschema
import jsonschema_rs
import jsonschema

# ---- remote + metaschema document map ----
docs: dict[str, object] = {}
for p in REMOTES.rglob("*.json"):
    docs[f"http://localhost:1234/{p.relative_to(REMOTES).as_posix()}"] = json.loads(p.read_text())
# bundled draft metaschemas (for the "validate against metaschema" tests), under both #/no-# keys
for V in (jsonschema.Draft4Validator, jsonschema.Draft6Validator, jsonschema.Draft7Validator,
          jsonschema.Draft201909Validator, jsonschema.Draft202012Validator):
    ms = V.META_SCHEMA
    uri = ms.get("$id") or ms.get("id")
    if uri:
        docs[uri] = ms
        docs[uri.rstrip("#")] = ms

def _norm(uri: str) -> str:
    return uri.split("#")[0] if "#" in uri and uri.split("#")[0] else uri

def retrieve(uri: str) -> object:
    for k in (uri, _norm(uri), uri.rstrip("#")):
        if k in docs:
            return docs[k]
    raise KeyError(uri)

RS_VALIDATOR = {
    "draft4": jsonschema_rs.Draft4Validator, "draft6": jsonschema_rs.Draft6Validator,
    "draft7": jsonschema_rs.Draft7Validator, "draft2019-09": jsonschema_rs.Draft201909Validator,
    "draft2020-12": jsonschema_rs.Draft202012Validator,
}

def build_fastjson(schema, dialect, assert_format):
    s = schema
    if isinstance(schema, dict) and "$schema" not in schema:
        s = {"$schema": SCHEMA_URI[dialect], **schema}
    fn = fastjsonschema.compile(s, handlers={"http": retrieve, "https": retrieve}, use_formats=assert_format)
    def run(data):
        try:
            fn(data); return True
        except fastjsonschema.JsonSchemaException:
            return False
    return run

def build_rs(schema, dialect, assert_format):
    rv = RS_VALIDATOR[dialect](schema, validate_formats=assert_format, retriever=retrieve)
    return rv.is_valid

BUILDERS = {"fastjsonschema": build_fastjson, "jsonschema-rs": build_rs}
IMPLS = list(BUILDERS)

def main() -> int:
    # tally[impl][dialect] = [pass, fail, err]
    tally = {i: {d: [0, 0, 0] for d in DIALECTS} for i in IMPLS}
    fails: dict[str, list[str]] = {i: [] for i in IMPLS}
    for dialect in DIALECTS:
        for f in sorted((TESTS / dialect).rglob("*.json")):
            rel = f.relative_to(TESTS / dialect).as_posix()
            assert_format = "format" in Path(rel).parts  # optional/format/*
            for group in json.loads(f.read_text()):
                schema, tests = group["schema"], group["tests"]
                for impl in IMPLS:
                    try:
                        run = BUILDERS[impl](schema, dialect, assert_format)
                    except Exception:
                        tally[impl][dialect][2] += len(tests)
                        continue
                    for t in tests:
                        # deep-copy: fastjsonschema fills `default`s by MUTATING the instance, which would
                        # otherwise corrupt the shared data for the next validator (and any later test).
                        try:
                            ok = run(copy.deepcopy(t["data"])) == t["valid"]
                        except Exception:
                            ok = False
                        if ok:
                            tally[impl][dialect][0] += 1
                        else:
                            tally[impl][dialect][1] += 1
                            if len(fails[impl]) < 4000:
                                fails[impl].append(f"{dialect} :: {rel} :: {group['description']} :: {t['description']}")
    # report
    for impl in IMPLS:
        print(f"\n===== {impl} =====")
        tp = tf = te = 0
        for d in DIALECTS:
            p, fl, e = tally[impl][d]
            tp += p; tf += fl; te += e
            tot = p + fl + e
            print(f"  {d:<14} {p:>5}/{tot:<5} pass  ({100*p/tot:.1f}%)   fail={fl:<4} err(cant-run)={e}")
        grand = tp + tf + te
        print(f"  {'TOTAL':<14} {tp:>5}/{grand:<5} pass  ({100*tp/grand:.1f}%)   fail={tf}  err={te}")
    # dump fails
    for impl in IMPLS:
        Path(f"/tmp/conformance/fails-{impl}.txt").write_text("\n".join(fails[impl]))
    return 0

if __name__ == "__main__":
    sys.exit(main())
