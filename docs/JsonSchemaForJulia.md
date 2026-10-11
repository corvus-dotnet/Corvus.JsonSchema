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
same meaning, with every character class written from the package's own Unicode 17 data. The answers are those of V8
on every Julia version, with one exception for [Julia 1.10](#julia-110).

What is exact:

- Every construct but the two below, for every pattern, whether it is valid with the `u` flag or only without it.
  That includes a backreference to a group that has not taken part, the groups of a repeated body, an iteration that
  matches the empty string, and a backreference inside a lookbehind to a group at its end.
- A pattern that is valid only without the `u` flag (one with an identity escape such as `\-` or `\&`) matches the
  UTF-16 code units of the string, as ECMA-262 says. A character beyond the Basic Multilingual Plane is two code
  units, so `^\-?.$` does not match U+10400, which `^.$` matches. A quantifier after such a character in the pattern
  repeats its second half, `\u{...}` is the letter `u` counted, and a class range from one such character to
  another is an error.
- A case-insensitive backreference such as `(.)(?i:\1)`, in every pattern that is valid with the `u` flag, with
  Julia 1.13. PCRE2 makes the comparison, by Unicode simple case folding, as ECMA-262 does with the `u` flag. It
  compares one character at a time. V8 13.6 compares the whole text by full case folding there, and says that
  `^(..)\1$` with the `i` flag matches U+00DF, `S`, `s`, U+1E9E. The package follows ECMA-262 and says that it does
  not.
- A backreference to a group of a lookbehind, where the lookbehind has a fixed length, where the group is at a
  fixed distance from its end (`(?<=a*(x))-\1`), and where the lookbehind is a term of the pattern itself and starts
  with a repetition of a body of a fixed length (`(?<=([abc])+)x\1`) or with alternatives of fixed lengths
  (`(?<=(ab|a)x)-\1`).

A pattern that is valid ECMA-262 and cannot be written for PCRE2 with exactly the same meaning is refused: compiling
the schema throws a `CompileError` that names the construct. It is never run with another meaning. These are:

- A case-insensitive backreference in a pattern that is only valid without the `u` flag (one with an Annex B form
  such as `\&`), where the group can hold a character that ECMA-262 compares differently there than PCRE2 does,
  such as `k`, `s`, or a character beyond the Basic Multilingual Plane that has a case variant. `^(.)(?i:\1)\&?$`
  is refused. No pattern that is valid with the `u` flag is refused for this.
- A backreference to a group inside a lookbehind of no fixed length, where the lookbehind is not a term of the
  pattern itself (`(?:(?<=(a)+)x\1)+`), or where the group holds what depends on the order in which ECMA-262 tries
  the ways through the lookbehind from its end (`(?<=(a*)(x+))-\1`).
- A backreference inside a lookbehind to a group that the lookbehind matches before it, where the group is not in
  an end of the lookbehind of a fixed length (`(?<=\1(a+))`).
- Repetitions that have to be written twice inside one another past 65,536 characters, more than 252 lookbehinds of
  no fixed length in one pattern, and a pattern too large for PCRE2 to compile (counted groups nested several deep).

**The lookbehind exception.** A pattern that is valid with the `u` flag is run, and answers as V8 does, with one
named exception. Two uses of a lookbehind need it to be matched backwards, as ECMA-262 matches it, which PCRE2 cannot
do, and they are refused with an error that says which:

- a group captured inside a lookbehind of no fixed length and read after it, outside the forms that are written,
  such as `(?<=(a*)(x+))-\1` ("a backreference to a group inside a lookbehind of no fixed length, where the group holds
  what only matching the lookbehind backwards can tell");
- a backreference inside a lookbehind to a group of it that is not in an end of it of a fixed length, such as
  `(?<=\1(a+))x` ("a backreference inside a lookbehind to a group that the lookbehind matches before it").

A lookbehind with no group that a backreference reads is never refused for this. The limits on the size of a
translation are the only other refusals of such a pattern.

PCRE2 backtracks, and it limits the steps and the memory of a match. A match that reaches a limit throws
`CorvusJsonSchema.EcmaRegex.MatchError`, from `isvalid` as from `validate` and `evaluate`. It is never reported as a
string that does not match. Validate inside `try` when the schema's patterns are not trusted.

A JSON string with the escape of a surrogate that is not half of a pair, such as `"\ud800"`, is not Unicode text. The
parser refuses a document that has one, an instance or a schema, so a pattern is never matched against such a string.

### Julia 1.10

Julia 1.10 bundles PCRE2 10.42, whose case folding is that of Unicode 14. A case-insensitive backreference there
does not hold equivalent the two characters of each of the thirty pairs below, which Unicode 15.1 and 16 made
equivalent and which V8 and Julia 1.13 hold equivalent. So `^(.)(?i:\1)$` matches `ƛꟜ` with Julia 1.13 and
does not with Julia 1.10. Nothing else differs between the two: a literal, a class and every other construct read the
package's own tables on every Julia. The package does not work around this. The fix is to update to Julia 1.13, where
the answers are exact.

| Code points | Characters | Code points | Characters |
|---|---|---|---|
| U+019B, U+A7DC | ƛ Ƛ | U+0264, U+A7CB | ɤ Ɤ |
| U+0390, U+1FD3 | ΐ ΐ | U+03B0, U+1FE3 | ΰ ΰ |
| U+1C89, U+1C8A | Ᲊ ᲊ | U+A7CC, U+A7CD | Ꟍ ꟍ |
| U+A7DA, U+A7DB | Ꟛ ꟛ | U+FB05, U+FB06 | ﬅ ﬆ |
| U+10D50, U+10D70 | 𐵐 𐵰 | U+10D51, U+10D71 | 𐵑 𐵱 |
| U+10D52, U+10D72 | 𐵒 𐵲 | U+10D53, U+10D73 | 𐵓 𐵳 |
| U+10D54, U+10D74 | 𐵔 𐵴 | U+10D55, U+10D75 | 𐵕 𐵵 |
| U+10D56, U+10D76 | 𐵖 𐵶 | U+10D57, U+10D77 | 𐵗 𐵷 |
| U+10D58, U+10D78 | 𐵘 𐵸 | U+10D59, U+10D79 | 𐵙 𐵹 |
| U+10D5A, U+10D7A | 𐵚 𐵺 | U+10D5B, U+10D7B | 𐵛 𐵻 |
| U+10D5C, U+10D7C | 𐵜 𐵼 | U+10D5D, U+10D7D | 𐵝 𐵽 |
| U+10D5E, U+10D7E | 𐵞 𐵾 | U+10D5F, U+10D7F | 𐵟 𐵿 |
| U+10D60, U+10D80 | 𐵠 𐶀 | U+10D61, U+10D81 | 𐵡 𐶁 |
| U+10D62, U+10D82 | 𐵢 𐶂 | U+10D63, U+10D83 | 𐵣 𐶃 |
| U+10D64, U+10D84 | 𐵤 𐶄 | U+10D65, U+10D85 | 𐵥 𐶅 |

The package's own tables hold Unicode 17, which added 28 pairs more: U+A7CE and U+A7CF, U+A7D2 and U+A7D3, U+A7D4
and U+A7D5, and U+16EA0 to U+16EB8 each with the character 27 places on (U+16EBB to U+16ED3). A case-insensitive
literal or class holds them equivalent on every Julia. A case-insensitive backreference does not, with Julia 1.10 or
1.13, and neither does V8 13.6, whose data is Unicode 16.

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
