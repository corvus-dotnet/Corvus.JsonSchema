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
| `pattern_match_limit` | The limit on the time of one match of a `pattern` or `patternProperties` regular expression, in seconds (default 1). `nothing` turns it off. A match that reaches it throws a `PatternMatchError`. See [Limits](#limits). |

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
    max_depth=128,
    pattern_match_limit=1)        # seconds for one match of a pattern. With nothing there is no limit

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
  another is an error. A string with such a character is written again as code units for the match, into a buffer
  the package keeps, so the match allocates nothing in the steady state.
- A count of one character or class, whatever the count. `a{1000000000}` and `^\p{L}{1,300000}$` are run. PCRE2
  counts to 65,535, and a larger count is written as blocks of the character that the pattern calls.
- What is a pattern, which is what ECMA-262 says. That is V8's verdict but in three places. `\p{sc=Hrkt}` (the
  Script value `Katakana_Or_Hiragana`, which Unicode lists and no character has) is a pattern that matches nothing,
  and V8 13.6 does not know the name. A count whose first number is the greater is an error however large the
  numbers, so `a{2147483648,2147483647}` is one, which V8 13.6 takes. The four scripts Unicode 17 added (`Sidetic`,
  `Tolong_Siki`, `Beria_Erfe`, `Tai_Yo`) are names here and not in V8 13.6. `\k` in a class is an error in a
  pattern with a named group, as in V8: `(?<n>a)[\k]`.
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
- A pattern PCRE2 does not compile for a reason that is not its size, which no pattern is known to be. (A pattern
  that is valid and too large is not refused in this way. See [Limits](#limits).)

**The lookbehind exception.** A pattern that is valid with the `u` flag is run, and answers as V8 does, with one
named exception. Two uses of a lookbehind need it to be matched backwards, as ECMA-262 matches it, which PCRE2 cannot
do, and they are refused with an error that says which:

- a group captured inside a lookbehind of no fixed length and read after it, outside the forms that are written,
  such as `(?<=(a*)(x+))-\1` ("a backreference to a group inside a lookbehind of no fixed length, where the group holds
  what only matching the lookbehind backwards can tell");
- a backreference inside a lookbehind to a group of it that is not in an end of it of a fixed length, such as
  `(?<=\1(a+))x` ("a backreference inside a lookbehind to a group that the lookbehind matches before it").

A lookbehind with no group that a backreference reads is never refused for this. A pattern over a limit of size is
the only other such pattern that does not compile, and its error is one of its own.

### Limits

A schema and an instance may both come from outside, so a pattern and a match of it have limits. A pattern or a
match over one is an error of its own, which names the limit and its value. It is never an answer.

**The limit on a match.** One match of a `pattern` or `patternProperties` regular expression against one string is
limited to one second. The keyword `pattern_match_limit` of `compile_schema` sets the limit, in seconds, and
`nothing` turns it off, for schemas and instances that are trusted. A match that reaches the limit is given up and
the validation throws a `PatternMatchError`, from `isvalid` as from `validate` and `evaluate`, with a collector or
without one, and from `compile_schema` when the match was of a pattern against a property name the schema
declares. It is never reported as a string that matches or does not. Validate inside `try` when the schema or the
instance is not trusted. The field `limit` of the error says which limit was reached.

| `limit` | What was reached | Value |
|---|---|---|
| `:time` | The time of a match | `pattern_match_limit`, 1 second unless it is set |
| `:memory` | The memory of a match, in PCRE2's interpreter | 64 MiB |
| `:steps` | The steps of a match that could not be timed | See below |

```julia
validator = compile_schema(raw"""{"pattern": "^(?:a|a)+$"}"""; pattern_match_limit=0.05)
isvalid(validator, "\"aaaa\"")  # true

# Each a is matched two ways, so there are 2^40 ways to find that this string does not match.
hostile = "\"" * "a"^40 * "b\""
outcome = try
    isvalid(validator, hostile)
catch error
    error isa PatternMatchError ? error.limit : rethrow()
end
outcome  # time

# A pattern over a limit of size does not compile. It is not said to be invalid.
too_large = try
    compile_schema(raw"""{"pattern": "(?:(?:a{1000}){1000}){1000}"}""")
catch error
    error isa PatternTooLargeError ? error.valid : rethrow()
end
too_large  # true
```

PCRE2 has no clock. It counts steps, and its count does not bound time: it counts the steps of each place a match
is tried from apart, and one step may read the whole string. So a match is run in two stages. The first is the code
PCRE2 compiles, with a number of steps worked out from the pattern and the length of the string so that it cannot
take long (a twentieth to a half of the limit at worst, and microseconds for a match that is not hostile). Nearly
every match ends there, at the cost it had. A match that uses those steps is run again from the start by code that
PCRE2 calls back from before every item of the pattern, which reads the clock, so the match ends with its answer
or at the limit. That code was measured 8 to 30 times slower on patterns that repeat a group, which is why it is
not the code of every match. On the probe of hostile patterns the package keeps (257 patterns of at most 1,000
characters on strings of at most 100,000), a match that was given up took 1.13 seconds at most with the limit at one
second, where 25 of them did not end in a minute before.

What the limit costs, and what it does not bound:

- **A long string and a pattern that repeats a group.** The steps of the first stage are fewer the longer the
  string, so a string of some tens of thousands of characters matched by a pattern that repeats a group, such as
  `^(?:[a-z0-9]+\.)*[a-z0-9]+$`, or searched for a pattern that is not anchored, is run by the slower code. It
  answers as it did, some 5 to 30 times slower on such strings. A class or a character under `*` or `+`
  (`^[a-z]+$`, `^\p{L}+$`) is not slower, on a string of any length. `pattern_match_limit=nothing` runs every match
  as before.
- **Memory.** A repeated group needs memory for each time it has matched. PCRE2's JIT code has a stack of 1 MiB,
  and a match that fills it is run by PCRE2's interpreter, which may take 64 MiB. Past that the match is a
  `PatternMatchError` whose `limit` is `:memory`, with any limit on time or none: `^(?:ab|cd)*$` reaches it on a
  string of some 500,000 characters. A class of many ranges under `*` or `+` no longer needs that memory:
  `^\p{L}+$` threw on a string of 150,000 letters, and answers on a million.
- **With no limit on time** a match has the 4,294,967,295 steps that are the most PCRE2 can be told to allow for
  each place it is tried from, and a match that takes them is a `PatternMatchError` whose `limit` is `:steps`.
  PCRE2 cannot be told to count none.
- **A pattern too large to be timed.** PCRE2, as Julia builds it, holds a compiled pattern of at most 64 KiB, and
  the code that reads the clock is larger than the code that does not. A pattern PCRE2 holds only without it, such
  as an alternation of 3,000 words, is run, and its matches end in their first stage. One that does not is given
  up there, sooner than its limit, with `limit` at `:steps`.
- **The faster matchers.** A pattern the package matches without PCRE2 (a literal, an anchored class sequence, a
  list of alternatives) reads the string a bounded number of times and has no limit.

**The limits on the size of a pattern.** A pattern over one of these does not compile. The error is a
`PatternTooLargeError`, and never the `CompileError` of a pattern that is not valid. Its field `valid` says whether
the whole pattern was read before the limit was met. The message is then "is a valid ECMA-262 regular expression,
and is too large", and otherwise "is too large to read", as nothing is known of the rest of the pattern.

| Limit | Value | The message ends |
|---|---|---|
| The length of the pattern | 1,048,576 bytes of UTF-8 | "its length is over the limit of 1048576 bytes" |
| The nesting of its groups | 200 | "its groups nest over the limit of 200 deep" |
| Its terms and alternatives | 131,072 | "it has over the limit of 131072 terms and alternatives" |
| The translation PCRE2 is given | 1,048,576 bytes | "its translation is over the limit of 1048576 bytes" |
| The steps to write the translation | 524,288 | "its translation takes over the limit of 524288 steps to write" |
| A repetition that is written out twice | 65,536 bytes | "a repetition that is written out is over the limit of 65536 bytes" |
| Lookbehinds of no fixed length | 252 | "it has over the limit of 252 lookbehinds of no fixed length" |
| What PCRE2 holds | 64 KiB compiled, 250 nested parentheses, 65,535 groups | "PCRE2 does not hold it, and says ..." |
| The compiled pattern and those of its lookbehinds | 1,048,576 bytes together | "PCRE2 does not hold it, and says ..." |

The limit a pattern of a few hundred characters meets is PCRE2's 64 KiB. PCRE2 writes a counted group out once for
each time, so a large count of a group of several characters passes it: `(?:ab){6552}` is run and `(?:ab){6553}` is
not, `(?:a{1000}){1000}` is run and `(?:(?:a{1000}){1000}){1000}` is not. A counted character or class is never too
large (`\p{L}{1000000000}` is run). A repetition of what can match the empty string inside another, such as
`(?:(?:a*)*)*`, was written twice at each level and is written once, so a hundred levels are run where twelve were
too large. With a minimum of one or more (`(?:(?:a*)+)+`) it is written twice still, and some twelve levels are too
large. The JIT code of a pattern is at most 4 MiB, and nothing is refused for that: a pattern that would pass it,
such as one of several hundred `\p{L}`, is run by PCRE2's interpreter. On the probe, a pattern of at most 1,000
characters is translated and compiled in 0.4 seconds at most and keeps 2.5 MB at most. The patterns a process
has compiled are kept for the next schema that has them, up to 4,096 patterns and 32 MiB.

The `regex` format reads a string of the instance with the limits of length and of nesting, and a string over one
is not a valid regular expression to it.

On the thirteen files of the
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data), 98,839 patterns
and 4.2 million answers, no pattern is over a limit of size, and with the limit at one second two patterns reach it
with Julia 1.13 and four with Julia 1.10, on one or a few strings each. All are patterns of nested repetitions of
what can match the empty string, such as `^a{2}?(((?!)|((|a){2,}(|a)\B|){2,3}?){2,3}.{3}a{2})\4`. V8 refuses an
empty iteration and answers them at once, where PCRE2 tries each, which is more steps than the first stage of a
match has (157 million for one of them) and more than the code that reads the clock runs in a second. They answer
as V8 does with `pattern_match_limit=nothing`. Every other answer is V8's, but for the two lookbehind uses and the
case-insensitive backreference above, which are refused.

### Surrogates

A JSON string with the escape of a surrogate that is not half of a pair, such as `"\ud800"`, is not Unicode text. The
parser refuses a document that has one, an instance or a schema, so a pattern is never matched against such a string.

### Julia 1.10

Julia 1.10 bundles PCRE2 10.42, whose case folding is that of Unicode 14. A case-insensitive backreference there
does not hold equivalent the two characters of each of the thirty pairs below, which Unicode 15.1 and 16 made
equivalent and which V8 and Julia 1.13 hold equivalent. So `^(.)(?i:\1)$` matches `ƛꟜ` with Julia 1.13 and
does not with Julia 1.10. Nothing else differs between the two: a literal, a class and every other construct read the
package's own tables on every Julia. The package does not work around this. The fix is to update to Julia 1.13, where
the answers are exact.

These thirty are every pair: the package's tests try each character that has a case variant against each other. Of the
thirteen files of the
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data),
Julia 1.10 differs from V8 on 1,026 answers, in twenty patterns of the case-folding file, and each of them is a text
of two characters that are one of these pairs.

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
