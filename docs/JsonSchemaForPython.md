# JSON Schema for Python

The Corvus JSON Schema evaluator for Python comes in two packages with the same API, so either can be imported in
place of the other. Both cover draft 4, 6, 7, 2019-09 and 2020-12, and give the same results and annotations as every
other Corvus implementation.

| Package | What it is | Choose it when |
|---|---|---|
| [corvus-json-schema](https://pypi.org/project/corvus-json-schema/) | Pure Python: it compiles a schema into specialised Python code. Its one dependency is [`regex`](https://pypi.org/project/regex/). | You want no native code, or a schema as a standalone Python module. |
| [corvus-json-schema-rs](https://pypi.org/project/corvus-json-schema-rs/) | Backed by the [Rust crate](JsonSchemaForRust.md), reading Python objects in place through CPython's C API. | You want the fastest validation. |

- **Conformant.** Both pass the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft),
  and all of its annotation tests. The pure package passes `draft4/optional/zeroTerminatedFloats.json` too, which the
  others exclude.
- **Fast.** The pure package validates six times faster than fastjsonschema, the other generator of Python code. The
  Rust-backed package takes about a third of the pure package's time, and 0.72 of jsonschema-rs's.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- Python 3.10 or later, typed (`py.typed`). The Rust-backed package has wheels for Linux (x86_64 and aarch64, glibc
  and musl), macOS (Intel and Apple silicon) and Windows (x64 and arm64).

## Install

```sh
pip install corvus-json-schema      # pure Python
pip install corvus-json-schema-rs   # backed by Rust
```

## Validate

```python
import corvus_json_schema as cjs  # or: import corvus_json_schema_rs as cjs

validate = cjs.compile({
    "type": "object",
    "properties": {"id": {"type": "integer", "minimum": 1}},
    "required": ["id"],
})

validate({"id": 3})  # True
validate({"id": 0})  # False
```

`compile` takes the schema as a parsed value or as JSON text. The validator is a function: call it with any value
`json.loads` produces. The Rust-backed package also validates JSON text directly, without creating Python objects for
it: `validate.is_valid_json('{"id": 3}')`.

## Options

`compile` takes these options as keyword arguments, or as a `CompileOptions`:

| Option | Meaning |
|---|---|
| `default_dialect` | The dialect of a schema without `$schema`: `Dialect.DRAFT4` to `Dialect.DRAFT202012` (the default). |
| `assert_format` | `True` asserts `format`, `False` never does; `None` (the default) follows the schema's vocabularies. |
| `assert_format_in_legacy_drafts` | With `assert_format` unset, assert `format` in drafts 4 to 7 too. |
| `assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `True`). |
| `formats` | Custom formats by name, such as `{"even": lambda s: len(s) % 2 == 0}`. |
| `resolve_document` | `(uri) -> schema`, JSON text or `None`, for remote references. The standard metaschemas are built in. |
| `base_uri` | The base URI of the root document. |
| `entry_point` | A subschema to validate against, such as `#/$defs/item`. |
| `max_depth` | The deepest the evaluator recurses in place (default 128); beyond it, `SchemaEvaluationDepthError`. |

A reference that cannot be resolved, or an invalid pattern, raises `SchemaCompilationError` when the schema is
compiled.

## Results and annotations

```python
collector = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.DETAILED)
validate.evaluate({"id": 0}, collector)  # False
for r in collector.results:
    ...  # r.is_match, r.message, r.evaluation_location, r.schema_evaluation_location, r.document_evaluation_location

verbose = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.VERBOSE)
validate.evaluate({"id": 3}, verbose)
cjs.collect_annotations(verbose)  # {"": {"title": {"#": "Person"}}, ...}
```

`BASIC` records the failures without messages, `DETAILED` adds the messages, and `VERBOSE` records every keyword,
passing ones and annotations included. Validation without a collector runs the fast path, so collecting costs nothing
when it is not used.

## Standalone modules

The pure package can write a schema out as a Python module that depends only on its small runtime:

```python
from pathlib import Path

Path("person_validator.py").write_text(cjs.generate_module(schema))
```

## Performance

Measured with jsonschema-benchmark's corpora and protocol on CPython 3.12 (warm validation, geometric means):

| Comparison | Time ratio | Faster on |
|---|---:|---:|
| corvus-json-schema / fastjsonschema | 0.17 | 34 of 35 corpora |
| corvus-json-schema-rs / corvus-json-schema | 0.35 | |
| corvus-json-schema-rs / jsonschema-rs | 0.72 | 34 of 35 corpora |

fastjsonschema supports drafts 4 to 7 only, and jsonschema-rs cannot run two of the 37 corpora. The
[pure package's README](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-py/corvus-json-schema#performance-results)
has the figures for each corpus.

## Links

- Packages: [corvus-json-schema](https://pypi.org/project/corvus-json-schema/) and
  [corvus-json-schema-rs](https://pypi.org/project/corvus-json-schema-rs/) on PyPI
- Source and READMEs: [src-py](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-py)
- The other languages: see [Other languages](OtherLanguages.md)
