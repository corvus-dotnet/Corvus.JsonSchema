# corvus-json-schema (Go)

A JSON Schema evaluator for Go (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5 runtime
evaluator (`Corvus.Text.Json.RuntimeEvaluator`) by way of its Rust port (`src-rs/corvus-json-schema`). Go 1.27 or
later. It has no dependencies outside the standard library.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`), and all
  of the suite's annotation tests.
- **Fast**: a schema compiles once into a node graph and fail-fast plans, each holding only the checks its subschema
  needs, with an object's keywords fused into one pass over its properties. See [Performance](#performance).
- **No allocation**: validating a parsed document, or JSON text through the validator's reused buffers, allocates
  nothing in the steady state.
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the same
  rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them.

## Install

```sh
go get github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema@latest
```

The package is `jsonschema`:

```go
import jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
```

## Usage

```go
validator, err := jsonschema.CompileString(`{
	"type": "object",
	"properties": { "id": { "type": "integer", "minimum": 1 } },
	"required": ["id"]
}`)
if err != nil {
	fmt.Println(err)
	return
}
fmt.Println(validator.IsValidString(`{"id": 3}`)) // true
fmt.Println(validator.IsValidString(`{"id": 0}`)) // false
```

A `*Validator` is immutable and safe for concurrent use. `Compile` takes the schema as UTF-8 bytes, `CompileString`
as a string, `CompileDocument` as a parsed `*Document`, and `CompileURI` fetches it through the document resolver (or
takes a standard metaschema).

Instances can be given as JSON text (`IsValidBytes`, `IsValidString`), parsed into buffers the validator reuses, or
as a `*Document` parsed once and validated any number of times:

```go
validator, err := jsonschema.CompileString(`{"type": "array", "items": {"type": "integer"}}`)
if err != nil {
	fmt.Println(err)
	return
}
// Parse once, validate any number of times.
document, err := jsonschema.ParseDocument([]byte(`[1, 2, 3]`))
if err != nil {
	fmt.Println(err)
	return
}
fmt.Println(validator.IsValid(document)) // true

// JSON text is parsed into buffers the validator reuses.
fmt.Println(validator.IsValidBytes([]byte(`[1, "two"]`))) // false
```

`IsValid` and its variants report a bool. Text that is not JSON is not valid, and neither is an instance on which the
schema recursed in place beyond the maximum depth. `Validate`, `ValidateBytes` and `ValidateString` also return an
error that tells those apart: a `*ParseError` for text that is not JSON, and `ErrDepthExceeded` for the recursion.

```go
validator, err := jsonschema.CompileString(`{"type": "object"}`)
if err != nil {
	fmt.Println(err)
	return
}
valid, err := validator.ValidateString(`{"id": 3`)
fmt.Println(valid, err) // false invalid JSON at offset 8: unexpected end of input

// IsValidString reports text that is not JSON as invalid.
fmt.Println(validator.IsValidString(`{"id": 3`)) // false
```

A schema that is not JSON gives a `*ParseError` from the compile functions, and one that cannot be compiled (an
unresolvable reference, an invalid pattern) gives a `*CompileError`.

### Options

The compile functions take options (the Go counterpart of `JsonSchemaEvaluatorOptions`):

```go
item, err := jsonschema.ParseDocumentString(`{"type": "string", "format": "even"}`)
if err != nil {
	fmt.Println(err)
	return
}
validator, err := jsonschema.CompileString(
	`{"$defs": {"item": {"$ref": "item.json"}}}`,
	jsonschema.WithDefaultDialect(jsonschema.Draft201909), // for schemas without $schema (default 2020-12)
	jsonschema.WithAssertFormat(true),                     // true or false. Without it the vocabularies decide
	jsonschema.WithFormat("even", func(value string) bool { return len(value)%2 == 0 }),
	jsonschema.WithDocumentResolver(func(uri string) *jsonschema.Document {
		if uri == "https://example.com/item.json" {
			return item
		}
		return nil
	}),
	jsonschema.WithBaseURI("https://example.com/root.json"),
	jsonschema.WithEntryPoint("#/$defs/item"),
	jsonschema.WithMaxDepth(128),
)
if err != nil {
	fmt.Println(err)
	return
}
fmt.Println(validator.IsValidString(`"four"`))  // true
fmt.Println(validator.IsValidString(`"three"`)) // false
```

| Option | Meaning |
|---|---|
| `WithDefaultDialect` | Dialect for schemas without `$schema` (default `Draft202012`). |
| `WithAssertFormat` | `true` asserts `format`, `false` never does. Without it the vocabularies decide (2020-12 `format-assertion`). |
| `WithAssertFormatInLegacyDrafts` | Without `WithAssertFormat`, also assert `format` in drafts 4 to 7. |
| `WithAssertContent` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `true`). |
| `WithFormat` | A custom format assertion by name. It receives the string, or a number's JSON text. |
| `WithDocumentResolver` | Resolves remote `$ref`s by absolute URI. The standard metaschemas are built in. |
| `WithBaseURI` | Base URI of the root document. |
| `WithEntryPoint` | Evaluate from a subschema, for example `#/$defs/item`. |
| `WithMaxDepth` | Depth limit for in-place recursion on a cycle (default 128). |

### Results and annotations

```go
validator, err := jsonschema.CompileString(`{"properties": {"id": {"type": "integer"}}, "required": ["name"]}`)
if err != nil {
	fmt.Println(err)
	return
}
instance, err := jsonschema.ParseDocumentString(`{"id": "seven"}`)
if err != nil {
	fmt.Println(err)
	return
}
collector := jsonschema.NewResultsCollector(jsonschema.Detailed)
valid, _ := validator.Evaluate(instance, collector)
fmt.Println(valid) // false
for _, r := range collector.Results() {
	if r.EvaluationLocation != "" && r.Message != "" {
		fmt.Printf("%s at %q: %s\n", r.EvaluationLocation, r.DocumentEvaluationLocation, r.Message)
	}
}
// /properties/id at "/id": The value was expected to match the subschema.
// /properties/id/type at "/id": The value was expected to be of type 'integer'
// /required at "/name": Required property not present 'name'
```

The levels and rows are those of the C# collector. `Basic` records failures without message text, `Detailed` adds
the text, and `Verbose` records every keyword, passing ones and annotations included. Each row is a `SchemaResult`
with `IsMatch`, `Message`, `EvaluationLocation`, `SchemaEvaluationLocation` and `DocumentEvaluationLocation`. A
collector accumulates across evaluations until `Reset`.

```go
validator, err := jsonschema.CompileString(`{
	"title": "Person",
	"properties": { "name": { "title": "Name", "type": "string" } }
}`)
if err != nil {
	fmt.Println(err)
	return
}
collector := jsonschema.NewResultsCollector(jsonschema.Verbose)
valid, err := validator.EvaluateString(`{"name": "Ada"}`, collector)
fmt.Println(valid, err) // true <nil>

// Instance location, then keyword, then schema location, then the value as JSON text.
annotations := collector.CollectAnnotations()
fmt.Println(annotations[""]["title"]["#"])                      // "Person"
fmt.Println(annotations["/name"]["title"]["#/properties/name"]) // "Name"
```

`Annotations` returns the same annotations as a list. Collecting runs the general evaluator over the compiled graph,
not the fail-fast plans.

Every sample above is an example test in `example_test.go`, so `go test` compiles and runs it.

## How it works

The pipeline follows the C# evaluator stage for stage, as the Rust port does. The loader identifies documents,
resources, anchors, dialects and vocabularies. The compiler builds one node per schema location with its keywords
digested and `$ref`s resolved, and analyses evaluated-property marking, in-place cycles and `oneOf`/`anyOf`
discriminators. The node graph then compiles to fail-fast plans, which one evaluator interprets:

- each plan holds only the keywords its node has, grouped by the kind of value they apply to, and a child that only
  tests a type is tested where it is used and never entered;
- an object is checked in one pass over its properties, with names looked up by length and then as 64-bit words, and
  `required` as a bit mask filled in the same pass;
- `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` over object schemas fuse into that one pass,
  which also decides `unevaluatedProperties` from the properties it covered;
- `oneOf`/`anyOf` narrow by a discriminator property or by type;
- instances are a flat tape of two words per value over the UTF-8 text, with strings read in place and numbers
  classified when parsed;
- common pattern shapes (literals, class sequences, separated lists, line lengths) match the UTF-8 bytes without a
  regular expression engine, and other patterns run on `internal/ecmaregex`, an ECMA-262 engine that translates to
  the standard library's `regexp` where the two agree and backtracks otherwise;
- numbers compare exactly across int64, uint64 and float64, and `multipleOf` is decided on the decimal digits of the
  text.

[OPTIMIZATIONS.md](OPTIMIZATIONS.md) maps each technique to its counterpart in the other Corvus evaluators, and lists
what is not done yet.

## Performance

To be measured. No figures are published for this port yet.

| Go over | Warm validation | Parse |
|---|---|---|
| [santhosh-tekuri/jsonschema](https://github.com/santhosh-tekuri/jsonschema) v6 | to be measured | to be measured |
| [Blaze](https://github.com/sourcemeta/blaze) | to be measured | to be measured |
| Corvus Rust | to be measured | to be measured |
| Corvus Java | to be measured | to be measured |
| Corvus .NET | to be measured | to be measured |

The measurements will use [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s
corpora, each implementation in its own container pinned to the same CPUs, with every harness warming up for 2
seconds (at least 100 passes) and reporting its last warm-up pass. [corvus-json-schema-bench](../corvus-json-schema-bench)
has the harnesses and how to run them.

## Differences from the Rust crate

- Instances are always `Document` values (the tape). There is no counterpart of the Rust `Instance` trait.
- A pattern that is valid only without the ECMA-262 `u` flag is matched by code point, as the Java port does. The
  Rust crate matches such a pattern by UTF-16 code unit.
- A class sequence anchored at both ends with one variable item (`^[a-z]*a$`) is decided by the length of the
  string, where the Rust crate uses the `regex` crate.
- Annotation values and the numbers in messages are written as the schema wrote them.

## Unicode

`pattern` reads general categories, scripts and case folding from the standard library's `unicode` package, and the
binary properties and script extensions from tables in `internal/ecmaregex` generated from Unicode 17. Go 1.27 is
the first release whose `unicode` package is Unicode 17, which is why it is the minimum. With an earlier Go the two
would disagree. With Go 1.26, whose `unicode` package is Unicode 15, 11 scripts do not resolve in
`\p{Script=...}`, 116 characters fold case differently, and the general categories are those of Unicode 15.

## Tests

```sh
go test -p 4 ./...
```

- `TestJSONSchemaTestSuite`: the JSON-Schema-Test-Suite (the repository's submodule, or `JSON_SCHEMA_TEST_SUITE`),
  every case fail-fast (as a document, as bytes and as a string) and through a collector at each level.
  `SUITE_DRAFT` and `SUITE_FILTER` narrow it. The test fails when the suite is missing.
- `TestAnnotationSuite` and `results_test.go`: the suite's annotation tests and the results expectations shared with
  the C#, Rust, Java and TypeScript evaluators.
- `TestValidationAllocatesNothingInTheSteadyState`: validation of a document, of bytes and of a string allocates
  nothing in the steady state. It is skipped under the race detector, which makes `sync.Pool` drop values.
- `pattern_test.go`, `document_test.go`, `plan_test.go`: the regex-free matchers against the engine, the parser
  against `encoding/json` and `strconv`, and the name lookup.
- `internal/ecmaregex`: the engine against answers recorded from V8 (`testdata/v8_oracle.json`).
- `TestEmbeddedMetaschemasAreCurrent`: the embedded metaschemas match `src/Corvus.Text.Json/metaschema`.
- `TestPlansAgreeWithTheGeneralEvaluatorOnTheBenchmarkCorpora`: the plans against the general evaluator on the
  jsonschema-benchmark corpora. It runs when `JSONSCHEMA_BENCHMARK` names a checkout.
- `example_test.go`: the samples in this README and in `docs/JsonSchemaForGo.md`.

## License

Apache 2.0.
