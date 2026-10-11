# corvus-json-schema (Go)

A JSON Schema evaluator for Go (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5 runtime
evaluator (`Corvus.Text.Json.RuntimeEvaluator`) by way of its Rust port (`src-rs/corvus-json-schema`). Go 1.25 or
later. It has no dependencies outside the standard library.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`), and all
  of the suite's annotation tests.
- **Fast**: a schema compiles once into a node graph and fail-fast plans, each holding only the checks its subschema
  needs, with an object's keywords fused into one pass over its properties. See [Performance](#performance).
- **No allocation**: validating a parsed document, or JSON text through the validator's reused buffers, allocates
  nothing in the steady state. The exceptions are an asserted `regex`, `idn-hostname` or `idn-email`
  format, a `hostname` with an `xn--` label, a custom format (which is given a copy of the string), and
  a `multipleOf` whose divisor has more than 18 significant digits.
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
| `WithPatternMatchLimit` | The time one match of a `pattern` or `patternProperties` regular expression may take (default one second, `NoPatternMatchLimit` for none). See [Limits on patterns](#limits-on-patterns). |

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

Measured with [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s 37 corpora, each
implementation in its own container pinned to the same 8 CPUs, the median of 3 runs. The Go, Java and .NET JIT
harnesses warm up for 2 seconds (at least 100 passes) and report the last warm-up pass. The others are the
benchmark's own harnesses. Figures are the geometric mean of Go's time over the other's (below 1 means Go is
faster), and how many corpora Go is faster on.

| Go 1.27 over | Warm validation | Cold validation | Compile | Parse |
|---|---|---|---|---|
| [santhosh-tekuri/jsonschema](https://github.com/santhosh-tekuri/jsonschema) v6 | 0.028 (35 of 35) | 0.038 (35 of 35) | 0.35 (35 of 35) | 0.22 (35 of 35) |
| [Blaze](https://github.com/sourcemeta/blaze) | 0.68 (31 of 37) | 0.59 (34 of 37) | 0.14 (37 of 37) | 0.34 (37 of 37) |
| Corvus Rust | 1.29 (0 of 37) | 1.14 (7 of 37) | 0.60 (36 of 37) | 0.93 (30 of 37) |
| Corvus .NET, native AOT, interpreting the schema | 1.13 (7 of 37) | 0.98 (21 of 37) | 0.67 (35 of 37) | 0.85 (25 of 37) |
| Corvus .NET, the JIT with [runtime code generation](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/RuntimeEvaluator.md#runtime-code-generation) | 2.15 (1 of 37) | 0.004 (37 of 37) | 0.016 (37 of 37) | 0.16 (37 of 37) |
| Corvus Java | 1.89 (2 of 37) | 0.016 (37 of 37) | 0.008 (37 of 37) | 0.19 (37 of 37) |

santhosh-tekuri/jsonschema does not compile two of the corpora (cspell and ui5-manifest).

Go has no code generation at run time, so a schema is interpreted from compiled plans, as in the Rust crate. Warm
validation is faster than Blaze on most corpora and within about 30% of the Rust crate. The engines that generate
code for a schema (Java, and .NET on the JIT) validate about twice as fast once warm. They are 60 to 120 times
slower to compile a schema, and far slower on the first pass.

[corvus-json-schema-bench](../corvus-json-schema-bench) has the harnesses and how to run them.

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics, and the answers are those of V8. A pattern is read with the
grammar of the `u` flag and matched by Unicode code point. A pattern that grammar refuses is read with the Annex B
grammar of a pattern with no flag, which many schemas need (`\-` outside a class, `\_`, a lone `{`). Such a pattern is
matched by UTF-16 code unit, as ECMA-262 matches a pattern with no `u` flag. The two differ only for a string with a
character beyond the Basic Multilingual Plane, which is two code units. `^.$` matches `"\ud801\udc00"` (the one
character U+10400), and `^\-?.$` does not, because the dot takes one unit of the two. A pattern that neither
grammar accepts is a `*CompileError`.

The answers are V8's except where V8 itself departs from ECMA-262, and there the module follows ECMA-262. A
quantifier whose first count is greater than its second is an error whatever the size of the numbers, so
`a{2147483648,2147483647}` is refused, which V8 takes. A group name used twice is an error unless the two groups
are in separate alternatives of one disjunction, so `(?<m>a)(?:x|(?<m>b))` is refused, which V8 takes.
`\p{WSpace}` is not a property name ECMA-262 lists, so the pattern is read with no flag, as the characters
`p{WSpace}`, where V8 reads it as `\p{White_Space}`. `Katakana_Or_Hiragana` (`Hrkt`) is a Script value that no
character has, so `\p{sc=Hrkt}` is valid and matches nothing, where V8 refuses it. And the properties are those of
Unicode 17, so the scripts Sidetic, Tolong_Siki, Beria_Erfe and Tai_Yo are known, which V8 13.6 does not know.

A JSON string can name half of a surrogate pair with an escape, as in `"\ud800"`. That is not Unicode text, and a
`Document` is UTF-8 and cannot hold it. `ParseDocument` returns a `*ParseError` for it, and `IsValidString` and
`IsValidBytes` report it as not valid, whatever the schema. So a pattern is never matched against such a string.

A pattern with a lookaround or a backreference runs on a backtracking matcher. A repetition whose body cannot
consume a character, such as `(?:\b|(?=a)|){4,5}`, is run once when its minimum is one or more and not at all when it
is zero, which is what ECMA-262 says it matches. A counted repetition is not tried at a place where the rest of the
pattern, with the iterations the repetition still owes, needs more of the string than is left. The matcher looks at
the next character before it takes one of two ways, as V8 does. And where a pattern has no backreference it remembers
the places from which every way has failed, so a repetition inside a repetition such as `^(?=.)(a+)+$` takes time
proportional to the pattern times the string, where a backtracking matcher takes time that doubles with each
character.

### Limits on patterns

A schema and an instance may both come from outside. No pattern and no string is to make a validation run without
end, or hold memory out of proportion to what it was given. So a match has a limit and a pattern has limits of size.

**The limit of a match.** One match of a `pattern` or `patternProperties` regular expression against one string may
take one second (`DefaultPatternMatchLimit`). `WithPatternMatchLimit` sets another time, and
`WithPatternMatchLimit(jsonschema.NoPatternMatchLimit)` turns the limit off for a caller who trusts both the schema
and the instances. A match that reaches the limit is abandoned, and the validation returns a
`*PatternMatchLimitError`, which `errors.Is` matches with `ErrPatternMatchLimit`. It is never an answer.

```go
validator, err := jsonschema.CompileString(
	`{"pattern": "^(?:(a+)\\1*)+$"}`,
	jsonschema.WithPatternMatchLimit(50*time.Millisecond),
)
if err != nil {
	fmt.Println(err)
	return
}
_, err = validator.ValidateString(`"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab"`)
var limit *jsonschema.PatternMatchLimitError
fmt.Println(errors.Is(err, jsonschema.ErrPatternMatchLimit), errors.As(err, &limit)) // true true
fmt.Println(limit.Pattern, limit.Limit, limit.Memory)                                // ^(?:(a+)\1*)+$ 50ms false
```

| Entry point | A match that reached its limit |
|---|---|
| `Validate`, `ValidateBytes`, `ValidateString` | `false` and the `*PatternMatchLimitError`. |
| `Evaluate`, `EvaluateBytes`, `EvaluateString` | `false` and the error, at every level. What the collector holds is not a report of the instance. |
| `IsValid`, `IsValidBytes`, `IsValidString` | `false`. They return a `bool` alone, and also return `false` for `ErrDepthExceeded`. Use `Validate` where the two must be told from an instance that is not valid. |
| `Compile`, `CompileString`, `CompileDocument`, `CompileURI` | The error, when the match was of a pattern against a property name the schema declares, which is done when the schema is compiled. |

The limit is on each match and not on the validation. Only the backtracking matcher can take long. It counts its
steps and reads a monotonic clock every 4,096 of them, which is every 17 to 35 microseconds as measured, so a match
ends within that of its limit, and a match that ends before its first check never reads the clock. With the limit
on, the matcher's stack of choices is capped at 1,048,576 entries (24 MB), and a match that reaches the cap is
abandoned with the same error, its `Memory` field set. `(?:a?){2147483647}` on the empty string ends that way in
under a tenth of a second, where it took 5.7 GB before the Go runtime ended the process. The memory of failed places
is 8 MB at most. With the limit off neither cap applies.

The other matchers take time proportional to the string and are not stopped. The standard library's `regexp` cannot
be stopped at all, and takes time proportional to its program times the string (5 to 13 ns for each instruction and
byte, as measured). So it is given no program estimated at more than 10,000 instructions, and with the limit on it
is given a string only when the program times the string, at 16 ns each, is within the limit. Any other string goes
to the backtracking matcher. Validation still allocates nothing in the steady state with the limit on.

`regexp` also keeps the ranges of a class for every copy of it that a counted repetition writes out, so it is given
no program estimated to hold more than 2 MiB. `^[\p{L}\p{N}_-]{1,450}$`, a pattern of 25 characters, held 10 MB
there. It now runs on the backtracking matcher, which holds a class once, and holds 7 KB.

**The limits of size.** A pattern over one of these is refused when the schema is compiled, with a
`*PatternTooLargeError`, which `errors.Is` matches with `ErrPatternTooLarge` and which names the keyword, the limit
and its value. It is not the error of a pattern that is not valid, which is a `*CompileError`. The pattern is not
read past the limit, so the message says it is too large to read and nothing of whether it is valid.

| Limit | Value | What is counted |
|---|---|---|
| `length` | 1,048,576 | The bytes of the pattern, as UTF-8. |
| `nesting` | 1,000 | The depth of its groups. |
| `size` | 1,048,576 | Bytes counted as the pattern is read. 64 for each term (a character, a class, a group, an assertion, with its quantifier) and 32 for each further alternative. 8 for each range of a class as its members are read, for each range that case folding or a negated property builds, and for each enclosing alternative and each group of the same name that a named group is compared with. 1 for each character with a case variant in a class that is folded. |

The syntax tree, the compiled program and the time to make them are proportional to the size. The largest pattern of
each of 24 kinds that the limits admit compiled in 110 ms at most and kept 4.7 MB, on a machine under load. A
literal of 16,384 characters is the longest the size admits, and `\p{L}` can be written 16,384 times. The number of
groups has no limit of its own, since each is a term. The `regex` format reads a string of the instance as a pattern
with the same limits, and a string over one is not a valid regular expression to it.

**The cache of compiled patterns.** Identical patterns share one matcher through a cache for the whole process. It
holds 1,024 patterns at most, and 64 MiB at most in the bytes counted for them. Over either bound the patterns
compiled first go, oldest first. A validator holds the patterns of its own schema, so a pattern that has left the
cache is still matched by every validator that has it, and is compiled again only for a schema compiled later. A
pattern counted as more than 2 MiB is not kept at all. A pattern is counted as an estimate of what it holds that is
not below it: its source, its texts and class sequences, four times its size as it is read for the engine, and for
a pattern on `regexp` the estimate of that package's program and 33 KB for its automaton over ASCII text. So a
process that compiles schemas from outside for as long as it runs does not grow with the patterns it has seen: 6,000
schemas with patterns of their own left the heap where the first 1,024 had (10 MB). Nothing reads the cache when an
instance is evaluated.

**What still reaches the limit of a match.** These are answered by no backtracking matcher in reasonable time, and
now end in the error after one second. V8 has no limit. It does not answer the first two kinds in 90 seconds either,
and needs more than 268 million backtracks for the searches. It does answer the two lookaheads, in 100,002
backtracks and in 4, because it looks further ahead than one character before it takes a way.

- A repetition around a group that is read back, such as `^(?:(a+)\1*)+$` or `(a*)*\1b` on some thirty characters.
  A backreference turns the memory of failed places off, since what a group holds changes what follows.
- A repetition that counts to a bound around another, such as `^(?:a{0,1000}){0,1000}$` on 100,000 characters.
- A lookahead that reads to the end of the string from every position, such as `(?=.*c)b` on 100,000 characters
  with no `c`, or `(?!.*c)b$` on 100,000 characters that end in `c`.
- A backreference to a group of tens of thousands of characters, such as `(.{0,50000})\1x` on 100,000 characters.
- A sequence of tens of thousands of characters searched for at every position, such as `[a-z]{50000}[0-9]` or
  `(?:[ab]{1000}){50}c` on 100,000 characters.
- A body that can be empty repeated more times than the stack has entries, such as `(?:a?){2147483647}`, which ends
  at the cap on memory.

No pattern of the ECMA-262 regular expression test data (98,839 patterns, 4,268,313 answers) reaches a limit of
either kind.

## Differences from the Rust crate

- Instances are always `Document` values (the tape). There is no counterpart of the Rust `Instance` trait.
- A pattern that is valid only without the ECMA-262 `u` flag is matched by UTF-16 code unit, as ECMA-262 says and
  as the Rust crate does. Here the engine is the module's own and reads the code units from the UTF-8 text.
- A class sequence anchored at both ends with one variable item (`^[a-z]*a$`) is decided by the length of the
  string, where the Rust crate uses the `regex` crate.
- Annotation values and the numbers in messages are written as the schema wrote them.

## Unicode

The module takes no Unicode data from the Go toolchain. The standard library's `unicode` package follows the
toolchain (Unicode 15 in Go 1.25 and 1.26, Unicode 17 in Go 1.27), and so do the `\p` classes and the case folding
of `regexp` and the case functions of `strings`. A module that read them would give different answers with
different toolchains.

Every property the module reads is in `internal/ucd`, whose tables hold Unicode 17. They are the general
categories, the scripts and script extensions, the binary properties ECMA-262 lists, simple case folding and the
simple uppercase mapping. `pattern` and `patternProperties` build their `\p{...}` classes and their case-insensitive groups from
those tables, and a pattern that runs on the standard library's `regexp` is handed explicit ranges. The `hostname`,
`idn-hostname` and `idn-email` formats read the same tables. RFC 5892 defines the IDNA2008 code point classes by
rules over Unicode properties and not for one version of Unicode, and the module applies them to Unicode 17. A URI
is normalized by lowering the letters A to Z only, as RFC 3986 and RFC 3987 specify.

The results are therefore the same with every Go release from 1.25, and they change only when the tables are
generated again. `internal/ucd/gen_tables.ps1` writes them from `unicodetables.rs` of the `regress` crate, a file
that crate generates from the Unicode Character Database. The crate is only where the data is read from, and no port
depends on it (the Rust crate writes its own tables from the file this script writes):

```powershell
pwsh internal/ucd/gen_tables.ps1 -RegressTables ~/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/regress-0.12.0/src/unicodetables.rs
```

`TestNoToolchainUnicodeData` fails if a file of the module that is not a test imports `unicode`, hands `regexp` a
Unicode class or a case-insensitive flag, or calls a case function of `strings` or `bytes`.

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
- `internal/ecmaregex`: the engine against answers recorded from V8 (`testdata/v8_oracle.json`, and
  `testdata/v8_cases.json` for matching by UTF-16 code unit, for repetitions that cannot consume a character and
  for what is a pattern, with ECMA-262's answer where V8 departs from it).
- `internal/ucd`: the Unicode tables. They are compared with the `unicode` package when the toolchain carries the
  same version of Unicode (Go 1.27).
- `TestNoToolchainUnicodeData`: no file of the module but a test reads the toolchain's Unicode data.
- `TestEmbeddedMetaschemasAreCurrent`: the embedded metaschemas match `src/Corvus.Text.Json/metaschema`.
- `TestPlansAgreeWithTheGeneralEvaluatorOnTheBenchmarkCorpora`: the plans against the general evaluator on the
  jsonschema-benchmark corpora. It runs when `JSONSCHEMA_BENCHMARK` names a checkout.
- `example_test.go`: the samples in this README and in `docs/JsonSchemaForGo.md`.

## License

Apache 2.0.

The Unicode tables of `internal/ucd` are derived from the Unicode Character Database, which is under the Unicode
License V3. [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) has that notice, and the one of the `regress` crate
that the tables were read from. The file is part of the module, beside `LICENSE`.
