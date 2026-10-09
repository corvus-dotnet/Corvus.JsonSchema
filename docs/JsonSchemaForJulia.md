# JSON Schema for Julia

[CorvusJsonSchema](https://github.com/JuliaRegistries/General/tree/master/C/CorvusJsonSchema) is the Corvus JSON Schema evaluator for
Julia: draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime evaluator (see
[Runtime Evaluator](RuntimeEvaluator.md)), and gives the same results and annotations. It needs Julia 1.10 or later
and has no dependencies.

A schema is compiled once into a node graph and fail-fast plans: each plan holds only the checks its subschema needs,
and an object's keywords are fused into one pass over its properties. Any number of instances can then be validated
against it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, and all of its annotation tests.
- **Fast.** See [Performance](#performance).
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Allocation-free validation.** In the steady state, validating a parsed document, or JSON text, allocates nothing.
  The exceptions are an asserted `regex`, `idn-hostname` or `idn-email` format, a `hostname` or `email` with an `xn--`
  label, a custom format (which is given a copy of the string), and a `multipleOf` whose divisor has more than 18
  significant digits.
- **Ready when loaded.** The evaluator is compiled into the package image, so the first validation in a process does
  not wait for Julia's compiler.

## Install

```julia-repl
pkg> add CorvusJsonSchema
```

## Validate

```julia
using CorvusJsonSchema

validator = compile_schema("""{
    "type": "object",
    "properties": {"id": {"type": "integer", "minimum": 1}},
    "required": ["id"]
}""")

isvalid(validator, """{"id": 3}""")  # true
isvalid(validator, """{"id": 0}""")  # false
```

A `Validator` is not modified by validation and is safe to use from several tasks at once. A schema that is not JSON
throws a `ParseError`, and one that cannot be compiled (an unresolvable reference, an invalid pattern, a pattern that
is [refused](#patterns)) throws a `CompileError`.

## JSON text

`isvalid` takes JSON text as a string or as a vector of UTF-8 bytes. It parses the text into buffers the validator
reuses and validates it in place, so in the steady state it allocates nothing. To validate the same text more than
once, parse it into a `Document`: the UTF-8 text and one flat array of values, with strings read in place where they
have no escapes.

```julia
validator = compile_schema("""{"type": "array", "items": {"type": "integer"}}""")

# Parse once, validate any number of times.
document = parse_document("[1, 2, 3]")
isvalid(validator, document)  # true

# JSON text is parsed into buffers the validator reuses.
isvalid(validator, """[1, "two"]""")  # false
isvalid(validator, Vector{UInt8}("[4, 5]"))  # true
```

`isvalid` reports text that is not JSON, and a schema that recursed in place beyond the maximum depth, as invalid.
`validate` throws for those instead: a `ParseError`, or a `DepthExceededError`.

```julia
validator = compile_schema("""{"type": "object"}""")

try
    validate(validator, """{"id": 3""")
catch err
    println(sprint(showerror, err))
end
# invalid JSON at offset 8: unexpected end of input

# isvalid reports text that is not JSON as invalid.
isvalid(validator, """{"id": 3""")  # false
```

## Options

`compile_schema` and `compile_schema_uri` take keyword arguments after the schema:

| Keyword | Meaning |
|---|---|
| `default_dialect` | The dialect of a schema without `$schema` (default `Draft202012`). |
| `assert_format` | `true` asserts `format`, `false` never does. With `nothing`, the default, the schema's vocabularies decide. |
| `assert_format_in_legacy_drafts` | With `assert_format` at `nothing`, assert `format` in drafts 4 to 7 too. |
| `assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom formats, as a dictionary from the name to a function from the string (or a number's JSON text) to whether it is valid. |
| `resolver` | A function from an absolute URI to the document (a `Document`, JSON text, or `nothing`), for remote references. The standard metaschemas are built in. |
| `base_uri` | The base URI of the root document. |
| `entry_point` | A subschema to validate against, such as `#/$defs/item`. |
| `max_depth` | The deepest the evaluator recurses in place (default 128). |

A schema that has a `$` in it is easiest to write as a `raw` string.

```julia
item = parse_document("""{"type": "string", "format": "even"}""")
validator = compile_schema(raw"""{"$defs": {"item": {"$ref": "item.json"}}}""";
    default_dialect=Draft201909,  # for schemas without $schema (default Draft202012)
    assert_format=true,           # true or false. With nothing the vocabularies decide
    formats=Dict("even" => value -> iseven(length(value))),
    resolver=uri -> uri == "https://example.com/item.json" ? item : nothing,
    base_uri="https://example.com/root.json",
    entry_point=raw"#/$defs/item",
    max_depth=128)

isvalid(validator, "\"four\"")  # true
isvalid(validator, "\"three\"")  # false
```

## Results and annotations

```julia
validator = compile_schema("""{"properties": {"id": {"type": "integer"}}, "required": ["name"]}""")
instance = parse_document("""{"id": "seven"}""")
collector = ResultsCollector(Detailed)
evaluate(validator, instance, collector)  # false

for r in results(collector)
    if r.evaluation_location != "" && r.message != ""
        println(r.evaluation_location, " at \"", r.document_evaluation_location, "\": ", r.message)
    end
end
# /properties/id at "/id": The value was expected to match the subschema.
# /properties/id/type at "/id": The value was expected to be of type 'integer'
# /required at "/name": Required property not present 'name'
```

Each row is a `SchemaResult` with `is_match`, `message`, `evaluation_location`, `schema_evaluation_location` and
`document_evaluation_location`. `Basic` records the failures without messages, `Detailed` adds the messages, and
`Verbose` records every keyword, passing ones and annotations included.

```julia
validator = compile_schema("""{
    "title": "Person",
    "properties": {"name": {"title": "Name", "type": "string"}}
}""")
collector = ResultsCollector(Verbose)
evaluate(validator, """{"name": "Ada"}""", collector)  # true

# Instance location, then keyword, then schema location, then the value as JSON text.
found = collect_annotations(collector)
found[""]["title"]["#"]  # "Person"
found["/name"]["title"]["#/properties/name"]  # "Name"
```

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics. The common shapes of pattern are matched with no regular
expression engine. Any other pattern is translated into one that the PCRE2 library Julia bundles matches with the
same meaning, with every character class written from the package's own Unicode 17 data, so a pattern means the same
on every Julia version.

A pattern that is valid ECMA-262 and cannot be written for PCRE2 with exactly the same meaning is refused: compiling
the schema throws a `CompileError` that names the construct. These are a few forms of backreference (inside a
lookbehind, or to a group that a lookbehind, a lookaround or a skipped repetition captures differently in the two
engines), a case-insensitive backreference to text outside ASCII letters, and more than 253 lookbehinds of no fixed
length in one pattern.

PCRE2 backtracks, and it limits the steps and the memory of a match. A match that reaches a limit throws
`CorvusJsonSchema.EcmaRegex.MatchError`, from `isvalid` as from `validate` and `evaluate`. It is never reported as a
string that does not match. Validate inside `try` when the schema's patterns are not trusted.

## Performance

Measured with [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s 37 corpora, each
implementation in its own container pinned to the same 8 CPUs, the median of 3 runs. The Julia, Go, Java and .NET JIT
harnesses warm up for 2 seconds (at least 100 passes) and report the last warm-up pass. The others are the
benchmark's own harnesses. Each figure is the geometric mean of Julia's time over the other's (below 1 means Julia is
faster), with how many corpora Julia is faster on.

| Julia 1.13 over | Warm validation | Cold validation | Compile | Parse |
|---|---|---|---|---|
| [Blaze](https://github.com/sourcemeta/blaze) | 0.80 (28 of 37) | 0.83 (26 of 37) | 0.27 (37 of 37) | 0.43 (37 of 37) |
| Corvus Go | 1.15 (2 of 37) | 1.33 (3 of 37) | 1.80 (0 of 37) | 1.21 (4 of 37) |
| Corvus Rust | 1.47 (3 of 37) | 1.58 (0 of 37) | 1.17 (8 of 37) | 1.19 (2 of 37) |
| Corvus .NET, native AOT, interpreting the schema | 1.29 (2 of 37) | 1.36 (2 of 37) | 1.27 (5 of 37) | 1.04 (13 of 37) |
| Corvus .NET, the JIT with [runtime code generation](RuntimeEvaluator.md#runtime-code-generation) | 2.45 (0 of 37) | 0.006 (37 of 37) | 0.030 (37 of 37) | 0.20 (37 of 37) |
| Corvus Java | 2.17 (2 of 37) | 0.023 (37 of 37) | 0.015 (37 of 37) | 0.23 (37 of 37) |

Against [JSONSchema.jl](https://github.com/JuliaIO/JSONSchema.jl), which jsonschema-benchmark does not have, the
two were run in one process with their timed passes interleaved, on the corpora whose drafts JSONSchema.jl
implements (4, 6 and 7). A warm pass with CorvusJsonSchema takes 0.4% to 4.2% of JSONSchema.jl's time on 34 of them,
1.6% at the geometric mean. On the thirty-fifth, ui5, JSONSchema.jl takes about seven minutes a pass.

A schema is interpreted from compiled plans, as in the Rust crate and the Go module, and not turned into generated
code, as it is in Java and in .NET on the JIT. Julia compiles each generated function through LLVM before its first
run, which made compiling a schema take tens of seconds for a large one. The interpreter is compiled into the package
image instead, so a process that loads the package compiles nothing to validate.

## Links

- Package: [the General registry](https://github.com/JuliaRegistries/General/tree/master/C/CorvusJsonSchema)
- Source and README: [src-jl/CorvusJsonSchema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-jl/CorvusJsonSchema)
- The other languages: see [Other languages](OtherLanguages.md)
