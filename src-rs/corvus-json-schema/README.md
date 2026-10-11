# corvus-json-schema (Rust)

A JSON Schema evaluator for Rust (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5 runtime
evaluator (`Corvus.Text.Json.RuntimeEvaluator`) and its TypeScript port (`src-ts/corvus-json-schema`).

A schema is compiled once into a node graph with pre-digested keyword data (resolved `$ref`/`$dynamicRef` targets,
compiled patterns, format kinds, exact numeric bounds, oneOf/anyOf discriminators, evaluated-property analyses). Any
number of `serde_json::Value` instances can then be evaluated against it:

- **fail-fast** with `Validator::is_valid` / `Validator::validate`, which does no reporting;
- **exhaustively** with `Validator::evaluate` and a `JsonSchemaResultsCollector` at `Basic`, `Detailed` or `Verbose`
  level. This gives the same rows (evaluation paths, schema locations, instance locations, messages and order) as the C#
  collector, plus annotations (`collect_annotations`, `enumerate_annotations`).

```sh
cargo add corvus-json-schema serde_json
```

```rust
use serde_json::json;

let validator = corvus_json_schema::compile(&json!({
    "type": "object",
    "properties": { "id": { "type": "integer", "minimum": 1 } },
    "required": ["id"]
}))
.unwrap();
assert!(validator.is_valid(&json!({ "id": 3 })));
assert!(!validator.is_valid(&json!({ "id": 0 })));
```

`CompileOptions` covers the same ground as the C# and TypeScript options:
- the default dialect;
- format assertion, including custom format validators;
- content assertion;
- a document resolver for remote references;
- a base URI and an entry point;
- the maximum in-place recursion depth.

The standard metaschemas are embedded.

### JSON text

`JsonDocument::parse` parses JSON text into a document that the evaluator reads in place: one flat array of values,
with strings borrowed from the text where they have no escapes. That is about a quarter of the time of parsing into a
`serde_json::Value`, and validation reads it at least as fast.

```rust
let validator = corvus_json_schema::compile(&serde_json::json!({ "type": "array", "items": { "type": "integer" } }))
    .unwrap();
let document = corvus_json_schema::JsonDocument::parse("[1, 2, 3]").unwrap();
assert!(validator.validate_instance(document.root()).unwrap());
```

It accepts what serde_json accepts: nesting up to 127 levels, no lone surrogates in `\u` escapes, numbers within the
range of a double. Of duplicate property names, the last value is kept at the position of the first. Doubles are
correctly rounded, as serde_json's are with its `float_roundtrip` feature, which this crate turns on: a number in a
schema and the same number in an instance are the same double, whichever parser read each.

### Other instance types

The evaluator reads instances through the `Instance` trait: a value shown as one of the six JSON kinds (`Instance::view`,
and `Instance::kind` for type tests), with arrays and objects read in place through `ArrayView` and `ObjectView`.
`&serde_json::Value` implements it. `Validator::validate_instance` and `Validator::evaluate_instance` take any
implementation, so a host language's own values can be validated without converting them to `serde_json::Value`
first. The Python package [corvus-json-schema-rs](../../src-py/corvus-json-schema-rs/README.md) reads Python objects
this way, through CPython's C API.

## Behaviour

- **Numbers:** compared exactly. Integers are compared as integers and doubles against integers without rounding.
  `multipleOf` is decided on decimal forms, so `0.0075` is a multiple of `0.0001`.
- **Patterns:** use ECMA-262 semantics. A pattern is compiled with the `u` flag. One that is not valid with it (it has
  `\&` or `\-`, say) is compiled without it, as many validators accept such patterns, and is then matched as
  ECMA-262 says for it: by UTF-16 code unit, so a character beyond the Basic Multilingual Plane is two. The `regex`
  format accepts only the grammar of the flag. Common shapes (class sequences, literals and their alternatives,
  separated lists, line lengths) get dedicated matchers, as in the C# evaluator. Patterns within the common subset of
  ECMA-262 and the [`regex`](https://crates.io/crates/regex) crate's syntax are translated and run by `regex`.
  Anything else (lookarounds, backreferences, `\B`, modifier groups) is matched by the crate's own ECMA-262 engine
  (`src/ecmaregex`), a port of the engine of the Go module, which the Object Pascal and OCaml ports also carry. The
  crate depends on `regex` and `serde_json` alone.

  **What is exact.** Everything. A pattern is matched as it is written, whichever matcher has it, and none is
  refused for what an engine cannot do. The answers are V8's for every pattern and text of the
  [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) that a Rust string
  can hold (98,839 patterns, 4,268,313 texts), the records where V8 itself departs from ECMA-262 apart. That covers
  lookaheads and lookbehinds, backreferences into and out of repetitions and lookarounds, named groups, a name shared
  by groups in separate alternatives, modifier groups, repetitions inside repetitions with any count, and
  case-insensitive matching in both grammars.

  **Limits.** A schema and an instance may both come from outside, so a pattern is bounded in what it costs.

  - *A match has a limit of time.* `CompileOptions::pattern_match_limit` is the time one match against one string may
    take. The default is one second (`DEFAULT_PATTERN_MATCH_LIMIT`), and `None` or zero turns it off for a caller who
    trusts both the schema and the instances. A match that reaches it is abandoned, and so is one whose backtracking
    holds 1,048,576 entries (24 MB). The validation then has no answer: the `try_` functions (`try_validate`,
    `try_validate_json`, `try_evaluate` and the rest) return `SchemaEvaluationError::PatternMatchLimit` with the
    pattern and the limit. The engine counts its steps and reads the clock every 4,096 of them, so the limit costs
    nothing that allocates and is met within tens of microseconds.
  - *The functions without the new error.* `is_valid` returns a `bool` and fails closed: it returns `false` for a
    match that reached its limit, as it does for the maximum depth, and never `true`. `validate`,
    `validate_instance`, `validate_json` and the `evaluate` functions report it as the one error their type has for
    a validation that was abandoned (`SchemaEvaluationDepthError`, named when the depth was the one limit). Its
    message says the validation was abandoned at a limit, the maximum depth or the limit of a match, and that the
    `try_` functions say which. At a limit the instance is not known to be not valid, whatever `is_valid` returned.
  - *A pattern has limits of size.* A pattern longer than 1,048,576 bytes, nested more than 1,000 groups deep, or
    counted as more than 1,048,576 as it is read (64 for a term, 32 for an alternative, 8 for a range of a class) is
    refused when the schema is compiled. The message says it is too large, not that it is not valid, and
    `SchemaCompilationError::kind` is `PatternTooLarge`. A pattern of plain characters can be 16,384 characters long.
  - *What the crate keeps is bounded.* The cache of compiled patterns of the process holds 1,024 patterns and 64 MiB
    at most, and the `regex` crate is given no program over 2 MiB.
  - *The format checks take time proportional to the string.* They read strings of the instance and are not under
    the limit of a match. Dates, times, UUIDs, IP addresses and host names are read by scanners that pass over the
    string once, and the others are regular expressions on the `regex` crate, which is linear. `regex` reads the
    string as a pattern, with the limits of size. `idn-hostname`, and the domain of an `idn-email`, read nothing of
    a string over 1,012 bytes: an internationalized host name has 253 characters at most, and its check decodes and
    encodes Punycode, which takes time that grows with the square of a label.
  - *What is not bounded.* The limit is for each match, not for a validation. The matchers that read a string once
    are not stopped, and `regex` is given a string only when it can answer within the limit. With the limit turned
    off, nothing bounds a match: a group read back inside a repetition, as in `^(?:(a+)\1*)+$`, takes time that
    doubles with each character of some strings.

  **Where V8 departs from ECMA-262,** the crate follows ECMA-262. With the `u` flag V8 starts a match between the two
  halves of a surrogate pair, compares a case-insensitive backreference to a group of several characters by full
  case folding, and answers some case-insensitive modifier groups differently from the same pattern with the `i`
  flag. The test data marks those records. V8 13.6 also differs in what it takes as a pattern. It takes
  `\p{WSpace}`, a count whose first number is the greater when the numbers are large
  (`a{2147483648,2147483647}`), and a name of two groups that can both take part in one match
  (`(|((?<m>))(?<m>|))`), none of which ECMA-262 allows, and the crate refuses them. It refuses the Script value
  `Hrkt` (`Katakana_Or_Hiragana`), which is in Unicode's list of values, and the crate takes it (no character has
  it, so it matches nothing).

  **Unicode versions.** Properties and case-insensitive matching use tables of the crate's own, for Unicode 17: the
  same tables as the Go module, and nothing from the Rust standard library or from the `regex` crate, so the answers
  do not depend on the toolchain or on a dependency's release. V8 13.6 has Unicode 16. They agree on every
  character of Unicode 16. The four scripts Unicode 17 adds (`Sidetic`, `Tolong_Siki`, `Beria_Erfe`, `Tai_Yo`) are
  scripts here, where V8 13.6 does not know them. The tables are generated from the Unicode Character Database.

  **Lone surrogates.** A JSON string with the escape of half of a surrogate pair (`"\ud800"`) is not a Rust string.
  `validate_json` and `serde_json` both refuse the document as not valid JSON.
- **Formats:** are annotations unless asserted by the vocabulary or by `CompileOptions::assert_format`. The format
  validators follow the C# implementations (RFC 3339 dates and times, IDN hostnames, IRIs, and so on).

## Benchmarks

### Comparison with other Rust validators

[`corvus-json-schema-bench`](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema-bench) validates the corpora of
[jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) with this crate,
[boon](https://crates.io/crates/boon) and [jsonschema](https://crates.io/crates/jsonschema), in one process. The
engines' passes are interleaved, so drift in the machine's speed affects them alike.

```sh
git clone --depth 1 https://github.com/sourcemeta-research/jsonschema-benchmark.git ../../../jsonschema-benchmark
cd ../corvus-json-schema-bench
cargo run --release -- --schemas ../../../jsonschema-benchmark/schemas [--only a,b] [--engines corvus,boon] \
  [--budget-ms 1000] [--json results/run.json]
```

`--profile N` runs N passes of the first engine and nothing else, for use under a profiler.

### jsonschema-benchmark

[`corvus-json-schema-bench/jsonschema-benchmark`](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema-bench/jsonschema-benchmark) is this crate's implementation of the benchmark's protocol. It
parses every instance, compiles, validates once cold, warms up, and validates once warm. It prints
`cold,warm,compile,parse` in nanoseconds and exits non-zero if an instance is invalid. To add it to a
jsonschema-benchmark checkout:
- copy the directory to `implementations/corvus-rs`;
- add the rules from `Makefile.fragment` to the Makefile;
- add `corvus-rs` to the README's list of implementations.

Until the crate is published to crates.io, the image builds it from this repository at the `CORVUS_REF` build
argument (default `main`).

To run it locally, the binary expects this repository at `/corvus`:

```sh
ln -s "$(git rev-parse --show-toplevel)" /corvus
cd ../corvus-json-schema-bench/jsonschema-benchmark
cargo run --release -- <schema-noformat.json> <instances.jsonl>
```

## Bowtie

[`corvus-json-schema-bowtie`](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema-bowtie) is a [Bowtie](https://github.com/bowtie-json-schema/bowtie) harness for this crate. See
its README.

## Testing

```sh
cargo test
```

This runs:
- **`tests/suite.rs`:** the full [JSON-Schema-Test-Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite)
  from the repository's submodule, covering required and optional tests plus `optional/format` with format asserted.
  Every case runs fail-fast and through a collector at each level, and the verdicts must agree. Exclusions match the
  C# runner: `draft4/optional/zeroTerminatedFloats.json` and leap seconds in the format run. Use `SUITE_DRAFT` and
  `SUITE_FILTER` to narrow the run.
- **`tests/annotations.rs`:** the suite's annotation tests.
- **`tests/results.rs`:** the results-collection expectations shared with the C# and TypeScript evaluators.
- **`tests/metaschemas.rs`:** checks that `src/metaschemas.rs` matches `src/Corvus.Text.Json/metaschema`.
  Regenerate it with `UPDATE_METASCHEMAS=1 cargo test --test metaschemas`.
- **The regular expression engine's own tests** (`src/ecmaregex/tests.rs`): the tests of the Go package it is a port
  of, with V8's answers read from that package's `testdata` (`v8_cases.json`, `v8_oracle.json`) and the sizes the
  ports must agree on (`size_cases.json`). One test compares the Unicode tables with the Go module's.
- **`tests/pattern_engine.rs`, `tests/pattern_anchors.rs` and `tests/pattern_class.rs`:** patterns that an engine
  or a matcher answered wrongly at some point, with V8's answers.
- **`tests/pattern_limits.rs`:** the limit of a match and the limits of size, through the public functions.
- **`tests/allocations.rs`:** validating JSON text allocates nothing in the steady state, with patterns the engine
  matches and with the limit on or off.

The Unicode tables are `src/ecmaregex/tables_gen.rs`. `pwsh tools/gen-ucd-tables.ps1` writes them from the tables of
the Go module (`src-go/corvus-json-schema/internal/ucd/tables_gen.go`), so the two ports hold the same data.

## License

Apache 2.0.

The Unicode tables are derived from the Unicode Character Database, which is under the Unicode License V3.
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) has that notice, and the one of the `regress` crate that the
tables were first read from. The file is in the published crate, beside `LICENSE`.
