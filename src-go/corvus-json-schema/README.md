# corvus-json-schema (Go)

A JSON Schema evaluator for Go (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5 runtime
evaluator by way of its Rust port (`src-rs/corvus-json-schema`). It has no dependencies outside the standard library.

This is a stub. It states what is implemented. Installation, benchmarks and the documentation page come later.

```go
import jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"

validator, err := jsonschema.CompileString(`{
    "type": "object",
    "properties": { "id": { "type": "integer", "minimum": 1 } },
    "required": ["id"]
}`)
if err != nil {
    return err
}
validator.IsValidString(`{"id": 3}`) // true
validator.IsValidString(`{"id": 0}`) // false
```

## What is implemented

- **Compilation.** `Compile` (UTF-8 bytes), `CompileString`, `CompileDocument` and `CompileURI` return a `*Validator`
  that is immutable and safe for concurrent use. A schema is compiled once into a node graph with digested keyword
  data (resolved `$ref` and `$dynamicRef` targets, compiled patterns, format kinds, exact numeric bounds, `oneOf` and
  `anyOf` discriminators, evaluated-property analyses) and into fail-fast plans (fused object plans, flat
  compositions, type dispatch, static unevaluated coverage).
- **Options.** `WithDefaultDialect`, `WithAssertFormat`, `WithAssertFormatInLegacyDrafts`, `WithAssertContent`,
  `WithFormat` (custom formats), `WithDocumentResolver`, `WithBaseURI`, `WithEntryPoint` and `WithMaxDepth`. The
  standard metaschemas are embedded.
- **Fail-fast validation.** `IsValid` (a parsed `*Document`), `IsValidBytes` and `IsValidString` report a bool.
  `Validate`, `ValidateBytes` and `ValidateString` also return an error, which is a `*ParseError` for text that is
  not JSON and `ErrDepthExceeded` for a schema that recursed in place beyond the maximum depth.
- **Results and annotations.** `Evaluate`, `EvaluateBytes` and `EvaluateString` report to a `ResultsCollector` at
  the `Basic`, `Detailed` or `Verbose` level. The rows (evaluation path, schema location, instance location, message
  and order) are those of the C# collector and the other ports. `Annotations` and `CollectAnnotations` read the
  annotations out of verbose results.
- **JSON text.** `ParseDocument` and `ParseDocumentString` parse JSON into a `Document`: the UTF-8 text and one flat
  array of values that the evaluator reads in place, with strings read from the text where they have no escapes.
  Parsing is strict RFC 8259. Invalid UTF-8, lone surrogates in `\u` escapes, numbers beyond the range of a float64
  and nesting deeper than 1000 levels are errors. Of duplicate property names, the last value is kept at the position
  of the first.
- **Numbers.** Integers up to 64 bits (signed or unsigned) stay exact, and compare exactly with floats. `multipleOf`
  is decided on the decimal text, so `0.0075` is a multiple of `0.0001`.
- **Patterns.** `pattern` and `patternProperties` have ECMA-262 semantics. Common shapes (literals, anchored class
  sequences, alternatives, separated lists, line lengths) are matched without a regular expression engine. Everything
  else runs on `internal/ecmaregex`.
- **Formats.** The formats of every draft, asserted on request, plus the numeric formats of the Corvus extension
  (`int32`, `uint64`, `double` and the rest).
- **No allocation in the steady state.** Validating a parsed document, or JSON text as bytes or as a string,
  allocates nothing once the validator's pooled buffers have grown.

## Conformance

The tests run the whole [JSON-Schema-Test-Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite)
(required, optional and `optional/format`, every draft) with the same exclusion as the C# and Rust runners
(`draft4/optional/zeroTerminatedFloats.json`, and leap seconds in the format run), and its annotation tests.

```sh
go test -p 4 ./...
```

The suite is the repository's `JSON-Schema-Test-Suite` submodule. Set `JSON_SCHEMA_TEST_SUITE` to use another
checkout. The suite test fails when it is missing.

## Differences from the Rust crate

- Instances are always `Document` values (the tape). There is no counterpart of the Rust `Instance` trait.
- A pattern that is valid only without the ECMA-262 `u` flag is matched by code point, as the Java port does. The Rust
  crate matches such a pattern by UTF-16 code unit.
- A class sequence anchored at both ends with one variable item (`^[a-z]*a$`) is decided by the length of the string,
  where the Rust crate uses the `regex` crate.
- Annotation values and the numbers in messages are written as the schema wrote them.
