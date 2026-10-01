# corvus-json-schema-rs

A JSON Schema evaluator for Python (draft 4, 6, 7, 2019-09 and 2020-12) backed by the
[corvus-json-schema](https://crates.io/crates/corvus-json-schema) Rust crate, the Rust port of the Corvus.Text.Json V5
standalone evaluator. It has the same API as the pure-Python
[corvus-json-schema](../corvus-json-schema/README.md) package, so either can be imported in place of the other.

- **Conformant**: passes the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, which the C# and Rust evaluators exclude (they read `1.0` as an
  integer), and all of the suite's annotation tests.
- **Fast**: instances are read in place through CPython's C API (dicts, lists and strings are not copied), so
  validating a Python object costs the evaluation alone. See the pure package's
  [Performance](../corvus-json-schema/README.md#performance) section for measurements of both.
- **Every platform from one wheel per platform**: the extension uses the stable ABI (abi3), so one wheel serves
  CPython 3.10 and every later version. Wheels are built for Linux (x86_64 and aarch64, glibc and musl), macOS
  (universal2: Intel and Apple silicon) and Windows (x64 and arm64).

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
`evaluate`, `JsonSchemaResultsCollector`, `ResultsLevel` and `collect_annotations` work as they do there. Custom
formats and document resolvers are Python callables, called from Rust.

What differs from the pure package:

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
