# corvus-json-schema-rs

A JSON Schema evaluator for Python (draft 4, 6, 7, 2019-09 and 2020-12) backed by the
[corvus-json-schema](https://crates.io/crates/corvus-json-schema) Rust crate, the Rust port of the Corvus.Text.Json V5
standalone evaluator. It has the same API as the pure-Python
[corvus-json-schema](../corvus-json-schema/README.md) package, so either can be imported in place of the other.

- **Conformant**: passes the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, which the C# and Rust evaluators exclude (they read `1.0` as an
  integer), and all of the suite's annotation tests.
- **Fast**: instances are read in place through CPython's C API (dicts, lists and strings are not copied), so
  validating a Python object costs the evaluation alone. Over the jsonschema-benchmark corpora it takes 0.35 of the
  pure package's time and 0.72 of jsonschema-rs's (geometric means); see the pure package's
  [Performance](../corvus-json-schema/README.md#performance-results) section for the figures.
- **Every platform from one wheel per platform**: the extension uses the stable ABI (abi3), so one wheel serves
  CPython 3.10 and every later version. Wheels are built for Linux (x86_64 and aarch64, glibc and musl), macOS
  (universal2: Intel and Apple silicon) and Windows (x64, and arm64 from Python 3.11, CPython's oldest arm64 Windows
  release), and CI tests each on its own platform.

## Install

```sh
pip install corvus-json-schema-rs
```

## Usage

```python
import corvus_json_schema_rs as cjs

validate = cjs.compile({"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}})
validate({"id": 3})                    # True: a value as json.loads produces it, read in place
validate.is_valid_json('{"id": "3"}')  # False: JSON text, parsed in Rust
```

`compile` takes the same options as the pure package (`default_dialect`, `assert_format`, `formats`,
`resolve_document`, `base_uri`, `entry_point`, `max_depth` and the rest, as keywords or a `CompileOptions`), and
three more: `pattern_match_limit` (below), and `regex_edition` and `regex_unicode_version`, the edition of ECMA-262 and
the version of Unicode patterns are read with (by default ECMAScript 2020 for 2019-09 and 2020-12, which name it,
today's for the drafts before them, and Unicode 17). And
`evaluate`, `JsonSchemaResultsCollector`, `ResultsLevel` and `collect_annotations` work as they do there. Custom
formats and document resolvers are Python callables, called from Rust.

What differs from the pure package:

- The limits on regular expressions. Both packages have `pattern_match_limit` (the seconds one match of a
  `pattern` or `patternProperties` regular expression against one string may take, `1.0` unless given, and `None` or
  `0` turns it off), `PatternMatchLimitError` (the instance is then neither valid nor not valid) and
  `PatternTooLargeError` (a `SchemaCompilationError`). What is left between them:
  - The limit error has its message and no attributes here. In the pure package it has `pattern`, `limit` and
    `most_characters`.
  - The memory of a match: here a match of any pattern is abandoned when its backtracking stack reaches 1,048,576
    entries (24 MB). The pure package bounds the length of the text for a pattern that the `regex` package runs
    (1,048,576 characters, and fewer for a pattern with groups that its engine keeps for every repetition). Both
    raise `PatternMatchLimitError`.
  - `compile` matches each pattern of `patternProperties` against the property names the schema declares, so it
    can raise `PatternMatchLimitError` here. The pure package's `compile` runs no match.
  - The limits of size are 1,048,576 bytes of pattern, 1,000 levels of groups and about 16,000 terms here. In the
    pure package they are 32,768 characters of pattern, 100 levels, 32,768 characters of the pattern as it is
    written for Python's engines, and 4,096 copies of what a count repeats.
  - The time of a match is counted by the clock in both. Here the engine runs every pattern it is given and looks at
    the clock as it goes. In the pure package a pattern that is proved to match in time proportional to
    the text has no limit, and for any other the error comes between nine tenths of the limit and about twice the
    limit.
  - `format: regex` has one rule in both: a text over a limit of reading is not valid, and any other is valid or
    not by the grammar, whatever it would cost to compile. The limits of reading are each package's own.
- There is no generated source: no `Validator.source` and no `generate_module`.
- `is_valid_json(text)` validates JSON text (`str` or `bytes`) without creating Python objects for it.
- Subclasses of `dict`, `list`, `str`, `int` and `float` (an `OrderedDict`, a string enum) are read as their bases,
  and tuples as arrays.
- Integers beyond 64 bits are compared as the nearest double, as by a JSON parser without arbitrary precision.
- A string with a lone surrogate (which `json.loads` can produce) is read with U+FFFD in its place.

## How it works

The crate compiles the schema once into its node graph and fail-fast plans (see
[src-rs/corvus-json-schema](../../src-rs/corvus-json-schema/README.md)). The evaluator is generic over an `Instance`
trait (a value shown as one of the six JSON kinds, with arrays and objects read in place). This package implements it
over Python objects through the stable C API: a dict is walked with `PyDict_Next` and searched by scanning its keys
(or, when large, by lookup), a string is read as the UTF-8 that CPython keeps with it (no copy for ASCII strings), and
a list is read by index. Anything the reader cannot take exactly (a tuple, a key that is not a string, a lone
surrogate, a number beyond a double, very deep nesting) makes the call convert the instance to a `serde_json::Value`
and evaluate that instead, so results never depend on which path ran.

## Building

```sh
pip install maturin
maturin develop --release   # into the active virtual environment
maturin build --release     # a wheel for this platform
```

The Rust toolchain is pinned in `../rust-toolchain.toml`. The tests are `tests/test_binding.py` and, through
`--impl corvus_json_schema_rs`, the pure package's suite and results tests.

## License

Apache 2.0.

The package is built on the `corvus-json-schema` Rust crate, which matches patterns with the `corvus-regex-ecma262`
crate, whose Unicode tables are derived from the Unicode Character Database, which is under the Unicode License V3.
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) has that notice, and the one of the `regress` crate, from whose
tables those were written. The file is in the source distribution and in every wheel, beside `LICENSE`.
