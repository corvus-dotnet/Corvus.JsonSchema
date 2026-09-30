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
- **Patterns:** use ECMA-262 semantics through [`regress`](https://crates.io/crates/regress), with the `u` flag.
- **Formats:** are annotations unless asserted by the vocabulary or by `CompileOptions::assert_format`. The format
  validators follow the C# implementations (RFC 3339 dates and times, IDN hostnames, IRIs, and so on).

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
