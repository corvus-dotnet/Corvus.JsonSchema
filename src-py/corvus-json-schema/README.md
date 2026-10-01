# corvus-json-schema

A JSON Schema evaluator for Python (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5
standalone evaluator (`Corvus.Text.Json.RuntimeEvaluator`, see [docs/RuntimeEvaluator.md](../../docs/RuntimeEvaluator.md))
by way of its TypeScript port. It compiles a schema once into specialised Python and validates parsed JSON values
(`json.loads` output) against it. Pure Python: its one dependency is [`regex`](https://pypi.org/project/regex/), for
the Unicode properties of ECMA-262 patterns.

For the same evaluator in Rust, behind the same Python API, see
[corvus-json-schema-rs](../corvus-json-schema-rs/README.md).

- **Conformant**: passes all 7,967 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), including `draft4/optional/zeroTerminatedFloats.json`, which the C# and TypeScript evaluators exclude
  because their parsers cannot tell `1.0` from `1` (`json.loads` can).
- **Fast**: see [Performance](#performance).
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the same
  rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them; passes all of the suite's annotation tests.
- **Standalone**: a schema can be emitted as a Python module that depends only on the small runtime.
- Python 3.10 or later; typed (`py.typed`, checked with `mypy --strict` and pyright).

## Install

```sh
pip install corvus-json-schema
```

## Usage

```python
import corvus_json_schema as cjs

validate = cjs.compile({
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "properties": {"id": {"type": "integer", "minimum": 1}},
    "required": ["id"],
    "unevaluatedProperties": False,
})

validate({"id": 3})  # True
validate({"id": 0})  # False
```

`compile` takes the schema as a parsed value or as JSON text, and these options, as keywords or as a `CompileOptions`
(the Python counterparts of `JsonSchemaEvaluatorOptions`):

| Option | Meaning |
|---|---|
| `default_dialect` | Dialect for schemas without `$schema` (`Dialect.DRAFT4` … `Dialect.DRAFT202012`, default 2020-12). |
| `assert_format` | `True` asserts `format`, `False` never does; `None` (the default) follows the vocabularies (2020-12 `format-assertion`). |
| `assert_format_in_legacy_drafts` | With `assert_format` unset, also assert `format` in drafts 4 to 7. |
| `assert_content` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `True`). |
| `formats` | Custom format assertions by name, e.g. `{"even": lambda s: len(s) % 2 == 0}`. |
| `resolve_document` | `(uri) -> schema \| JSON text \| None`, for remote `$ref`s. The standard metaschemas are built in. |
| `base_uri` | Base URI of the root document. |
| `entry_point` | Evaluate from a subschema, e.g. `#/$defs/item`. |
| `max_depth` | Depth limit for in-place recursion on a cycle (default 128); exceeding it raises `SchemaEvaluationDepthError`. |

An unresolvable reference or an invalid pattern raises `SchemaCompilationError` at compile time.

Instances are the values `json.loads` produces: `dict`, `list`, `str`, `int`, `float`, `bool` and `None`. The generated
code tests exact types (`type(x) is dict`), so subclasses such as `OrderedDict` are not objects to it; `True` is a
boolean, never the number 1.

### Results and annotations

```python
validate = cjs.compile(schema)
collector = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.DETAILED)
validate.evaluate({"id": 0}, collector)  # False
for r in collector.results:
    ...  # r.is_match, r.message, r.evaluation_location, r.schema_evaluation_location, r.document_evaluation_location

verbose = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.VERBOSE)
validate.evaluate({"id": 3}, verbose)
cjs.collect_annotations(verbose)  # {"": {"title": {"#": "Person"}}, "/id": {...}}
```

The levels and rows are those of the C# collector: `BASIC` records failures without message text, `DETAILED` adds the
text, `VERBOSE` records every keyword (passing ones and annotations included). Evaluation without a collector runs
the generated code; with one, an interpreter over the same compiled graph evaluates every keyword and reports it
(`collecting.py`), so results collection costs nothing when unused.

### Standalone modules

```python
from pathlib import Path

Path("person_validator.py").write_text(cjs.generate_module(schema))
# from person_validator import validate, evaluate  -- needs only corvus_json_schema.runtime
```

Or from the command line:

```sh
corvus-json-schema generate schema.json -o validator.py
corvus-json-schema validate schema.json doc1.json doc2.json
```

## How it works

The pipeline follows the C# evaluator stage for stage:

1. **Load** (`loader.py`): documents, resources (`$id`/`id`), anchors, `$recursiveAnchor`, and the dialect and
   vocabularies of each resource, including custom metaschemas read through `$vocabulary`.
2. **Compile** (`compiler.py`): one `SchemaNode` per distinct schema location with its keywords pre-digested; `$ref`
   resolved at compile time; `$dynamicRef`/`$recursiveRef` resolved statically wherever the entry resource decides
   them, so a dynamic scope is kept only for references that stay dynamic.
3. **Analyse**: which nodes can mark evaluated properties/items, in-place recursion cycles (only those nodes carry a
   depth guard), and `oneOf`/`anyOf` discriminators.
4. **Generate** (`codegen.py`), as the TypeScript port does: each node becomes one function with exactly the checks it
   needs, compiled once with `compile()`/`exec()`, so a validation is a single call into that code. The analyses that
   select C# plans select the shape of the code:
   - pure-`$ref` chains are elided; leaf schemas (type, `const`/`enum`, string and number keywords) are tested at
     their call sites as one expression, with no call;
   - small objects are checked by direct lookups (`"name" in x`), `required` and `additionalProperties: false` by set
     operations on `x.keys()`, and larger objects by one pass dispatching each name through a dict;
   - an `allOf`/`$ref` composition of plain object schemas checks an object in one pass over the merged names (the C#
     and Rust flat fused plan);
   - `oneOf`/`anyOf` narrow by a discriminator property or by type dispatch;
   - `unevaluatedProperties`/`unevaluatedItems` are decided from static coverage where their contributors allow it,
     with conditions under `if`/`then`/`else` and dependencies decided once per object;
   - patterns that are literals (a prefix, an exact value, alternatives, a substring) match without a regular
     expression; others are ECMA-262 translated to Python's syntax (`pattern.py`) and run by `re`, or by `regex` for
     Unicode properties;
   - structurally identical functions are merged by partition refinement.

Generated code that keeps state (a live dynamic scope or an in-place depth count) evaluates under a lock, so a
validator can be shared between threads.

## Performance

Measured with [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s corpora and harness
protocol (`bench/corpora.py`: a fresh process per corpus and implementation, warm-up, then the best of five passes),
against the Rust-backed [corvus-json-schema-rs](../corvus-json-schema-rs/README.md), the
[jsonschema-rs](https://pypi.org/project/jsonschema-rs/) bindings and [fastjsonschema](https://pypi.org/project/fastjsonschema/)
(another generator of Python code, draft 4 to 7). See [Performance results](#performance-results) for the figures.

```sh
python bench/corpora.py --schemas <jsonschema-benchmark>/schemas --impls corvus,corvus-rs,jsonschema-rs,fastjsonschema
```

### Performance results

To be measured on a quiet machine before release.

## Tests

```sh
pip install -e . pytest
python -m pytest                     # API and results tests
python tests/suite.py                # the JSON-Schema-Test-Suite (--draft, --filter, --verbose; --module through
                                     #   generate_module; --collect basic|detailed|verbose through a results collector;
                                     #   --impl corvus_json_schema_rs for the Rust-backed package)
python tests/annotations.py          # the suite's annotation tests through a verbose collector
```

The suite is read from the repository's `JSON-Schema-Test-Suite` submodule (`git submodule update --init
JSON-Schema-Test-Suite`), or from `$JSON_SCHEMA_TEST_SUITE`.

## Limitations and differences from the C# evaluator

- **Results collection** matches the C# collector's rows; annotation values are the values re-serialised
  (`json.dumps`), not the schema's source text, because schemas arrive as parsed values, and numbers in messages are
  written as the Rust evaluator writes them (`2.0`, `1e+21`).
- **Numbers** are Python's: integers are exact at any size, and floats are doubles. `multipleOf` with a fractional
  divisor is computed exactly on the shortest decimal forms of the doubles.
- **Regular expressions** are ECMA-262 translated to Python's syntax with ECMA's meaning (`\d` and `\w` are ASCII, `.`
  excludes every line terminator, `$` matches only at the end). A pattern only valid without the `u` flag (an identity
  escape such as `\-`, a literal brace) is accepted as JavaScript's `RegExp` would accept it without the flag.
- Custom `formats` functions cannot be serialised into standalone modules.
