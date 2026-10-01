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

## Behaviour

- **Numbers:** compared exactly. Integers are compared as integers and doubles against integers without rounding.
  `multipleOf` is decided on decimal forms, so `0.0075` is a multiple of `0.0001`.
- **Patterns:** use ECMA-262 semantics with the `u` flag. Common shapes (class sequences, literals and their
  alternatives, separated lists, line lengths) get dedicated matchers, as in the C# evaluator. Patterns within the
  common subset of ECMA-262 and the [`regex`](https://crates.io/crates/regex) crate's syntax are translated and run by
  `regex`, and anything else by [`regress`](https://crates.io/crates/regress).
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
