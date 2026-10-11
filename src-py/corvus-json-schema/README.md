# corvus-json-schema

A JSON Schema evaluator for Python (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5
standalone evaluator (`Corvus.Text.Json.RuntimeEvaluator`, see [docs/RuntimeEvaluator.md](../../docs/RuntimeEvaluator.md))
by way of its TypeScript port. It compiles a schema once into specialised Python and validates parsed JSON values
(`json.loads` output) against it. Pure Python: its one dependency is [`regex`](https://pypi.org/project/regex/), for
the ECMA-262 patterns that the standard library's `re` cannot run.

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
     Unicode properties, lookbehind, a backreference to a group that may not have matched and one that ignores case;
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

CPython 3.12.13 on an Intel Core i7-13800H (WSL2, pinned to the performance cores), jsonschema-rs 0.58.3,
fastjsonschema 2.22.2, 2026-10-01. The validation columns are one warm pass over all of a corpus's instances (the best of
five passes after warm-up, the median of three processes); "compile" is the schema compilation (for this package, the
generation of the Python code and, for programs over 64 KB of source, the compilation of only the functions an instance
reaches, on first use).

| Corpus | Instances | corvus (pure) | corvus-rs | jsonschema-rs | fastjsonschema | corvus compile | corvus-rs compile |
|---|---:|---:|---:|---:|---:|---:|---:|
| ansible-meta | 333 | 345.0 µs | 105.1 µs | 286.2 µs | 7.96 ms | 10.92 ms | 876.6 µs |
| aws-cdk | 483 | 88.8 µs | 45.7 µs | 61.5 µs | 229.9 µs | 599.4 µs | 78.8 µs |
| babelrc | 794 | 260.2 µs | 97.4 µs | 130.9 µs | 898.2 µs | 1.54 ms | 189.6 µs |
| clang-format | 133 | 140.0 µs | 29.0 µs | 38.1 µs | 571.0 µs | 8.46 ms | 765.5 µs |
| cmake-presets | 967 | 12.29 ms | 3.65 ms | 6.64 ms | 250.54 ms | 19.01 ms | 1.58 ms |
| code-climate | 2484 | 354.0 µs | 284.6 µs | 329.4 µs | 1.77 ms | 909.5 µs | 121.1 µs |
| cql2 | 109 | 193.3 µs | 61.2 µs | 127.3 µs | 5.07 ms (!) | 12.54 ms | 800.1 µs |
| cspell | 981 | 1.52 ms | 1.16 ms | n/a | 52.35 ms | 12.24 ms | 1.68 ms |
| cypress | 981 | 279.8 µs | 108.8 µs | 185.5 µs | 1.34 ms | 2.76 ms | 267.4 µs |
| deno | 987 | 831.0 µs | 235.4 µs | 313.3 µs | 2.86 ms | 3.28 ms | 333.8 µs |
| dependabot | 967 | 1.10 ms | 334.4 µs | 435.5 µs | 2.84 ms | 2.22 ms | 154.9 µs |
| draft-04 | 563 | 12.55 ms | 4.39 ms | 6.05 ms | 16.83 ms | 2.35 ms | 185.6 µs |
| fabric-mod | 911 | 1.50 ms | 368.0 µs | 608.8 µs | 42.99 ms | 3.11 ms | 300.2 µs |
| geojson | 500 | 49.76 ms | 16.65 ms | 19.56 ms | 2173.86 ms | 10.41 ms | 516.9 µs |
| gitpod-configuration | 986 | 698.3 µs | 222.5 µs | 274.0 µs | 1.82 ms | 3.23 ms | 258.0 µs |
| helm-chart-lock | 3888 | 1.90 ms | 736.7 µs | 1.11 ms | 6.07 ms | 650.1 µs | 104.3 µs |
| importmap | 964 | 253.7 µs | 122.3 µs | 150.6 µs | 825.1 µs | 577.2 µs | 79.0 µs |
| jasmine | 980 | 338.8 µs | 99.0 µs | 197.6 µs | 2.74 ms | 1.39 ms | 123.1 µs |
| jsconfig | 981 | 906.9 µs | 301.4 µs | 338.1 µs | 19.70 ms | 8.38 ms | 1.70 ms |
| jshintrc | 966 | 1.16 ms | 329.9 µs | 523.8 µs | 3.96 ms | 1.80 ms | 207.2 µs |
| krakend | 47 | 698.0 µs | 139.7 µs | n/a | 4.65 ms | 29.33 ms | 4.22 ms |
| lazygit | 280 | 338.5 µs | 77.7 µs | 106.1 µs | 9.18 ms | 10.74 ms | 956.1 µs |
| lerna | 985 | 217.9 µs | 138.2 µs | 156.7 µs | 659.5 µs | 1.45 ms | 113.8 µs |
| nest-cli | 1025 | 579.8 µs | 186.4 µs | 231.9 µs | 3.85 ms | 3.79 ms | 370.0 µs |
| omnisharp | 987 | 719.2 µs | 215.1 µs | 253.6 µs | 2.21 ms | 2.96 ms | 325.3 µs |
| openapi | 107 | 16.90 ms | 5.23 ms | 10.91 ms | 88.9 µs | 13.63 ms | 1.44 ms |
| pre-commit-hooks | 985 | 1.28 ms | 308.9 µs | 467.0 µs | 22.78 ms | 1.44 ms | 195.4 µs |
| pulumi | 3807 | 1.67 ms | 627.3 µs | 786.2 µs | 19.22 ms | 3.54 ms | 291.8 µs |
| semantic-release | 794 | 291.5 µs | 118.5 µs | 168.0 µs | 9.30 ms | 1.43 ms | 125.5 µs |
| stale | 961 | 466.8 µs | 147.5 µs | 214.8 µs | 1.16 ms | 1.25 ms | 157.6 µs |
| stylecop | 983 | 934.6 µs | 253.1 µs | 296.6 µs | 3.26 ms | 3.56 ms | 236.8 µs |
| tmuxinator | 382 | 206.2 µs | 80.0 µs | 98.7 µs | 5.77 ms | 1.67 ms | 153.3 µs |
| ui5 | 942 | 1.33 ms | 624.4 µs | 527.8 µs | 10.50 ms | 20.00 ms | 2.36 ms |
| ui5-manifest | 611 | 6.81 ms | 2.18 ms | 2.47 ms | n/a | 70.67 ms | 8.92 ms |
| unreal-engine-uproject | 859 | 2.17 ms | 400.9 µs | 534.8 µs | 12.30 ms | 2.38 ms | 284.7 µs |
| vercel | 710 | 671.9 µs | 191.0 µs | 293.7 µs | 2.81 ms | 9.46 ms | 653.5 µs |
| yamllint | 966 | 61.3 µs | 61.9 µs | 65.7 µs | 169.1 µs | 310.3 µs | 78.4 µs |

- **corvus-json-schema (pure Python)** is faster than fastjsonschema, the other generator of Python code, on 34 of the
  35 corpora both can run: its warm time is a sixth of fastjsonschema's (geometric mean 5.86 times faster).
  fastjsonschema supports drafts 4 to 7 only, so on openapi, a 2020-12 schema, it applies few of the keywords (its one
  faster time), on cql2 it rejects valid instances (!), and it cannot compile ui5-manifest.
- **[corvus-json-schema-rs](../corvus-json-schema-rs/README.md)**, the same API backed by the Rust evaluator, takes
  0.35 of the pure package's time (geometric mean over all 37 corpora), and 0.72 of jsonschema-rs's (faster on 34 of
  the 35 corpora jsonschema-rs can run; it rejects the patterns of cspell and krakend).

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
  excludes every line terminator, `$` matches only at the end, a backreference to a group that has not matched matches
  the empty string, and a modifier group such as `(?i:...)` folds case by tables of Unicode 16 that the package
  carries). A pattern is read with the `u` flag. One that is not valid with it (an identity escape such as `\-`, a
  literal brace) is read as JavaScript's `RegExp` reads it without the flag. Every answer for the 98,839
  patterns of the [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data)
  is V8's, and one of those patterns passes the size limit below. What is written out for the engines, so that it is
  exact:
  - A backreference inside a case-insensitive group compares character by character by ECMA-262's Canonicalize, from
    the package's own tables, so the answer is the same on every version of Python and of `regex`. `^(?i:(.)\1)$`
    takes `"s\u017f"` and `"k\u212a"` and not `"i\u0130"`, and in a pattern that is valid only without the `u` flag it
    takes none of the three. The comparison is a loop in the pattern that the `regex` package runs. It costs a few
    microseconds for each character compared, where a plain backreference costs a fraction of one, and a group that
    can hold any character makes a translation of about 54,000 characters that takes about 0.2 seconds to compile
    and holds about 3 MB. A group that can hold no letter, such as `(\d+)`, keeps a plain backreference.
  - A lookbehind is matched backwards, as ECMA-262 matches it, so a group of one holds what it holds in V8
    (`(?<=(a+))b\1`), and so does a reference inside one to a group of it (`(?<=\1(a))b`). A group that a lookahead
    sets on only some of the ways it can match (`(?=(a)?b)\1`) and a lookaround group read in the same repetition
    after a choice (`^(?:(?=([ab]))[ab](?:c|ca)\1)*$`) are exact too. Inside a lookaround with a group that is read
    outside it, an iteration that matches nothing is refused as ECMA-262 refuses it, which decides what the group
    holds: `^(?=((?:|a)*))\1$` takes `"a"`.
  - A pattern that is valid only without the `u` flag is read, and matches, by UTF-16 code unit, as V8 does: in
    `^\-?.$` the dot takes half of a character beyond the Basic Multilingual Plane, and a quantifier after such a
    character in the pattern repeats its second half.

  - A `\p{...}` has one of ECMA-262's names, or the pattern is not valid with the `u` flag: a value of
    General_Category by itself or after `General_Category=` or `gc=`, a value of Script after `Script=`, `sc=`,
    `Script_Extensions=` or `scx=`, or a binary property that ECMA-262 lists, in the spelling of the Unicode
    Character Database. The names are those of Unicode 16, from a table that the package carries. The other names
    that the `regex` package has (`\p{Greek}`, `\p{IsLatin}`, `\p{alpha}`, a name in another case) are not taken:
    such a pattern is read without the `u` flag, where `\p` is the letter `p`. The characters of a property are the
    `regex` package's, so they follow its version of Unicode (16 in release 2025.9.1, 17 in 2026.9.29).

  - A repetition whose body can match nothing, with a repetition inside it or around it, is written with a body
    that cannot, where no reference reads a group of it: `^(?:(?:a|)*x?)*$` is written as `^(?:a+x?|x)*$` would be.
    ECMA-262 refuses an iteration that matches nothing once the minimum count is met. Both engines accept it and try
    what follows the repetition twice, at each level, so a text that does not match took a time that doubles with
    each level. Without a reference that reads a group, a pattern only says which texts match, and the two
    writings match the same texts.

  No valid pattern is refused for what it means, and none is answered with another meaning. Two limits remain:
  - both engines backtrack, and neither has V8's short cuts. Where a reference reads a group inside repetitions
    that can match nothing, the pattern is written as ECMA-262 runs it, iteration by iteration, and may take far
    longer than it does in V8. So may a count of millions in a pattern that the `regex` package runs, which takes
    about 270 bytes for each count when it compiles: `(?<=a)b{2000000}` takes 536 MB;
  - a pattern whose translation would pass 65,536 characters raises `SchemaCompilationError`, as does a minimum
    count of 2,147,483,647 or more over something that can match nothing. The message says the pattern is valid and
    too large. That takes a count inside a count around a group that a backreference reads, as in
    `^(?:(?:(a)|b){300}){300}\1$`, or a count of more than 175 around a case-insensitive backreference inside a
    lookbehind, or of more than 1,284 around one outside a lookbehind.

  Where V8 13.6 itself departs from ECMA-262, the package follows ECMA-262. Six such places are known. With the
  `u` flag V8 also starts a match between the two surrogates of a character, so `(?<!^.*)` matches `"\U00010428"`
  there. With the `i` and `u` flags V8 compares a backreference to a group of several characters by full case
  folding, so `^(...)\1$` takes `"\u1e9eS\u00df\u1e9e\u00df\u017f"` there, and `^(.)(.)(.)\1\2\3$`, which means the
  same, does not. V8 answers six patterns of the test data with a case-insensitive modifier group differently
  from the same pattern with the `i` flag. V8 takes `\p{WSpace}`, an alias of White_Space that ECMA-262 does not
  list, so here that pattern is the letter `p` and what follows it. V8 refuses the Script value
  `Katakana_Or_Hiragana` (`Hrkt`), which ECMA-262 has because `PropertyValueAliases.txt` has it, so here
  `\p{sc=Hrkt}` is valid and takes no character, as no character has that value. And V8 takes
  `a{2147483648,2147483647}`, where the first count is the greater, which is not valid here.
- Custom `formats` functions cannot be serialised into standalone modules.
