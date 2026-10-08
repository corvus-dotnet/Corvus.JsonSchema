# JSON Schema for Go

[corvus-json-schema](https://pkg.go.dev/github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema) is the
Corvus JSON Schema evaluator for Go: draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime evaluator
(see [Runtime Evaluator](RuntimeEvaluator.md)), and gives the same results and annotations. It needs Go 1.25 or later
and has no dependencies outside the standard library.

A schema is compiled once into a node graph and fail-fast plans: each plan holds only the checks its subschema needs,
and an object's keywords are fused into one pass over its properties. Any number of instances can then be validated
against it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, and all of its annotation tests.
- **Fast.** See [Performance](#performance).
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Allocation-free validation.** In the steady state, validating a parsed document, or JSON text, allocates nothing.
  The exceptions are an asserted `regex`, `idn-hostname` or `idn-email` format, a `hostname` with an `xn--` label,
  a custom format (which is given a copy of the string), and a `multipleOf` whose divisor has more than 18 significant digits.

## Install

```sh
go get github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema@latest
```

The package is `jsonschema`:

```go
import jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
```

## Validate

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

A `*Validator` is immutable and safe for concurrent use. A schema that is not JSON gives a `*ParseError`, and one that
cannot be compiled (an unresolvable reference, an invalid pattern) gives a `*CompileError`.

## JSON text

`IsValidString` and `IsValidBytes` parse the text into buffers the validator reuses and validate it in place, so in
the steady state they allocate nothing. To validate the same text more than once, parse it into a `Document`: the
UTF-8 text and one flat array of values, with strings read in place where they have no escapes.

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

`IsValid` and its variants report text that is not JSON, and a schema that recursed in place beyond the maximum depth,
as invalid. `Validate`, `ValidateBytes` and `ValidateString` return an error for those instead: a `*ParseError`, or
`ErrDepthExceeded`.

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

## Options

`Compile`, `CompileString`, `CompileDocument` and `CompileURI` take options after the schema:

| Option | Meaning |
|---|---|
| `WithDefaultDialect` | The dialect of a schema without `$schema` (default `Draft202012`). |
| `WithAssertFormat` | `true` asserts `format`, `false` never does. Without it the schema's vocabularies decide. |
| `WithAssertFormatInLegacyDrafts` | Without `WithAssertFormat`, assert `format` in drafts 4 to 7 too. |
| `WithAssertContent` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `true`). |
| `WithFormat` | A custom format by name: a function from the string (or a number's JSON text) to whether it is valid. |
| `WithDocumentResolver` | A function from an absolute URI to the document, for remote references. The standard metaschemas are built in. |
| `WithBaseURI` | The base URI of the root document. |
| `WithEntryPoint` | A subschema to validate against, such as `#/$defs/item`. |
| `WithMaxDepth` | The deepest the evaluator recurses in place (default 128). |

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

## Results and annotations

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

Each row is a `SchemaResult` with `IsMatch`, `Message`, `EvaluationLocation`, `SchemaEvaluationLocation` and
`DocumentEvaluationLocation`. `Basic` records the failures without messages, `Detailed` adds the messages, and
`Verbose` records every keyword, passing ones and annotations included.

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

## Performance

To be measured. No figures are published for the Go evaluator yet.

| Go over | Warm validation | Parse |
|---|---|---|
| [santhosh-tekuri/jsonschema](https://github.com/santhosh-tekuri/jsonschema) v6 | to be measured | to be measured |
| [Blaze](https://github.com/sourcemeta/blaze) | to be measured | to be measured |
| Corvus Rust | to be measured | to be measured |
| Corvus Java | to be measured | to be measured |
| Corvus .NET | to be measured | to be measured |

The measurements will use [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s 37
corpora, each implementation in its own container pinned to the same CPUs, with every harness warming up for 2 seconds
(at least 100 passes) and reporting its last warm-up pass.

## Links

- Package: [pkg.go.dev](https://pkg.go.dev/github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema)
- Source and README: [src-go/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-go/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
