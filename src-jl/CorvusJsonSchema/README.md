# CorvusJsonSchema (Julia)

A JSON Schema evaluator for Julia (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5 runtime
evaluator (`Corvus.Text.Json.RuntimeEvaluator`) by way of its Go port (`src-go/corvus-json-schema`) and its Rust port
(`src-rs/corvus-json-schema`). Julia 1.10 or later. It has no dependencies.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`), and all
  of the suite's annotation tests.
- **Fast**: a schema compiles once into a node graph and fail-fast plans, each holding only the checks its subschema
  needs, with an object's keywords fused into one pass over its properties. See [Performance](#performance).
- **No allocation**: validating a parsed document, or JSON text through the validator's reused buffers, allocates
  nothing in the steady state. [What allocates](#what-allocates) lists the exceptions.
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the same
  rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them.
- **Ready when loaded**: the evaluator is compiled into the package image, so the first validation in a process
  does not wait for Julia's compiler.

## Install

```julia-repl
pkg> add CorvusJsonSchema
```

## Usage

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

A `Validator` is not modified by validation and is safe to use from several tasks at once. `compile_schema` takes the
schema as a string, as a vector of UTF-8 bytes, or as a parsed `Document`, and `compile_schema_uri` fetches it
through the document resolver (or takes a standard metaschema).

Instances can be given as JSON text (a string or a vector of bytes), parsed into buffers the validator reuses, or as
a `Document` parsed once and validated any number of times:

```julia
validator = compile_schema("""{"type": "array", "items": {"type": "integer"}}""")

# Parse once, validate any number of times.
document = parse_document("[1, 2, 3]")
isvalid(validator, document)  # true

# JSON text is parsed into buffers the validator reuses.
isvalid(validator, """[1, "two"]""")  # false
isvalid(validator, Vector{UInt8}("[4, 5]"))  # true
```

`isvalid` reports a `Bool`. Text that is not JSON is not valid, and neither is an instance on which the schema
recursed in place beyond the maximum depth. `validate` throws for those instead: a `ParseError` for text that is not
JSON, and a `DepthExceededError` for the recursion.

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

A schema that is not JSON gives a `ParseError` from the compile functions, and one that cannot be compiled (an
unresolvable reference, an invalid pattern, a pattern that is [refused](#patterns)) gives a `CompileError`.

### Options

The compile functions take keyword arguments (the Julia counterpart of `JsonSchemaEvaluatorOptions`). A schema that
has a `$` in it is easiest to write as a `raw` string.

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

| Keyword | Meaning |
|---|---|
| `default_dialect` | Dialect for schemas without `$schema` (default `Draft202012`). The others are `Draft4`, `Draft6`, `Draft7` and `Draft201909`. |
| `assert_format` | `true` asserts `format`, `false` never does. With `nothing`, the default, the vocabularies decide (2020-12 `format-assertion`). |
| `assert_format_in_legacy_drafts` | With `assert_format` at `nothing`, also assert `format` in drafts 4 to 7. |
| `assert_content` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `true`). |
| `formats` | Custom format assertions, as a dictionary (or pairs) from the name to a function. The function receives the string, or a number's JSON text, as a `String`, and returns a `Bool`. |
| `resolver` | A function that resolves remote `$ref`s by absolute URI. It returns a `Document`, JSON text, or `nothing`. The standard metaschemas are built in. |
| `base_uri` | Base URI of the root document. |
| `entry_point` | Evaluate from a subschema, for example `#/$defs/item`. |
| `max_depth` | Depth limit for in-place recursion on a cycle (default 128). |
| `pattern_match_limit` | The limit on the time of one match of a `pattern` or `patternProperties` regular expression, in seconds (default 1). `nothing` turns it off. A match that reaches it throws a `PatternMatchError`. See [Limits](#limits). |

### Results and annotations

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

The levels and rows are those of the C# collector. `Basic` records failures without message text, `Detailed` adds
the text, and `Verbose` records every keyword, passing ones and annotations included. Each row is a `SchemaResult`
with `is_match`, `message`, `evaluation_location`, `schema_evaluation_location` and `document_evaluation_location`. A
collector accumulates across evaluations until `empty!`.

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

`annotations` returns the same annotations as a list. Collecting runs the general evaluator over the compiled graph,
not the fail-fast plans.

Every sample above runs in the package's tests (`test/docs.jl` reads this file), so the test run proves it.

## How it works

The pipeline follows the C# evaluator stage for stage, as the Rust and Go ports do. The loader identifies documents,
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
  regular expression engine, and other patterns run on the `EcmaRegex` submodule, which translates an ECMA-262
  pattern exactly into one for the PCRE2 that Julia bundles;
- numbers compare exactly across `Int64`, `UInt64` and `Float64`, and `multipleOf` is decided on the decimal digits
  of the text.

Julia can generate code for a schema at run time, as the Java and .NET evaluators do, and it was measured and
ruled out. Julia compiles each generated function through LLVM before its first run, at 25 to 50 ms a function, so
a schema of 900 subschemas took 45 seconds to compile. The plans are therefore data that one type-stable interpreter
runs, and a precompile workload puts that interpreter in the package image.

[OPTIMIZATIONS.md](OPTIMIZATIONS.md) maps each technique to its counterpart in the other Corvus evaluators, has the
measurement behind that choice, and lists what is not done yet.

## What allocates

Validating a `Document`, a string or a vector of bytes with `isvalid` or `validate` allocates nothing in the steady
state, which is after the first validations have grown the validator's buffers. `test/allocations.jl` holds the
keyword paths it lists to zero bytes. These allocate:

- an asserted `regex`, `idn-hostname` or `idn-email` format;
- an asserted `hostname` or `email` format, for a host name with a label that starts with `xn--`;
- a custom format, which is given a copy of the string;
- a `multipleOf` whose divisor has more than 18 significant digits, or an exponent out of range;
- a number of more than 19 significant digits, in the rare case that its nearest `Float64` cannot be decided from
  the first 19;
- an object schema with more than 32,768 property names, for each name looked up;
- JSON text whose parsed form is larger than a few megabytes, which is parsed into buffers that are not kept;
- a string with a character beyond the Basic Multilingual Plane that is more than 4 MiB when it is written as
  UTF-16 code units, matched by a pattern that is valid only without the `u` flag (see [Patterns](#patterns)). It
  is written into a buffer that is not kept;
- validations of one validator that overlap, on several tasks, until each has an evaluation state in the
  validator's pool.

Compiling a schema, parsing with `parse_document`, and evaluating with a results collector allocate.

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics. A pattern outside the common shapes is translated by the
`EcmaRegex` submodule into a pattern that PCRE2 matches with the same meaning. The answers are those of V8, on every
Julia version, with one exception for Julia 1.10 that is listed [below](#julia-110).

- **Backreferences and repetitions.** A backreference matches the text its group captured when the group last took
  part, and the empty string when it has not. The groups of a repeated body stop having taken part when an iteration
  starts, and an iteration that matches the empty string is refused once the minimum count is met. PCRE2 does none of
  that by itself, so the translator writes it out. `^(?:(a)|x)*\1-$` matches `ax-`, and `^(?:(?:a|)*x)*$` is answered
  at once on a long text.
- **Lookbehinds.** ECMA-262 matches a lookbehind backwards, and its groups hold what that captures. The translator
  writes a lookbehind whose groups are read after it in a form that captures the same: a lookbehind of a fixed
  length, a group at a fixed distance from the end of one (`(?<=a*(x))-\1`), and a lookbehind that is a term of the
  pattern itself and starts with a repetition of a body of a fixed length (`(?<=([abc])+)x\1`) or with alternatives
  of fixed lengths (`(?<=(ab|a)x)-\1`). The last is a search back from the lookbehind, which takes time that grows
  with the length of the text before it.
- **Case-insensitive backreferences.** `(.)(?i:\1)` compares its text as PCRE2 compares, by Unicode simple case
  folding, which is what ECMA-262 does with the `u` flag. A pattern that is valid with the `u` flag is never refused
  for one. With Julia 1.13 the answers are those of V8 13.6 for every pair of characters. With Julia 1.10 they are
  not for thirty pairs of characters (see [Julia 1.10](#julia-110)). The comparison is one character at a time, as
  ECMA-262's Canonicalize is. V8 13.6 compares the whole text by full case folding where the `i` and `u` flags are
  both set, so it says that `^(..)\1$` matches U+00DF, `S`, `s`, U+1E9E. The package says that it does not.
- **Patterns valid only without the `u` flag.** A pattern that is not valid with the `u` flag (one with an identity
  escape such as `\-` or `\&`) is read with the Annex B grammar, and ECMA-262 then matches it against the UTF-16
  code units of the string. A character beyond the Basic Multilingual Plane is two code units. So `^\-?.$` does not
  match U+10400, which `^.$` matches. In such a pattern a quantifier after such a character repeats its second
  half, a class holds each half, `\u{...}` is the letter `u` counted, and a class range from one such character to
  another is an error, as it runs from a low surrogate down to a high one. A string with no such character is
  matched as it is. One that has one is written again as code units for the match, into a buffer that the package
  keeps and uses again, so in the steady state the match allocates nothing. The name of a group is read by
  character in both grammars.
- **Names, counts and `\k`.** What is a pattern is what ECMA-262 says, which is V8's verdict but in three places.
  The Script value `Katakana_Or_Hiragana`, or `Hrkt`, is in the alias file of Unicode, so `\p{sc=Hrkt}` is a
  pattern. No character has the value, so it matches nothing and `\P{sc=Hrkt}` matches any character. V8 13.6
  does not know the name. A count whose first number is the greater is an error however large the numbers are,
  so `a{2147483648,2147483647}` is one, which V8 13.6 takes. And the package's tables are those of Unicode 17,
  so the scripts it added (`Sidetic`, `Tolong_Siki`, `Beria_Erfe` and `Tai_Yo`) are names here, and are not in
  V8 13.6, whose data is Unicode 16. Like V8, the package takes `\k` in a class as the letter `k` only in a
  pattern with no named group that is not valid with the `u` flag: `(?<n>a)[\k]` is an error.
- **Counts.** A count of one character or class is run whatever the count: `a{1000000000}` and
  `^\p{L}{1,300000}$` are patterns like another. PCRE2 counts to 65,535, so a larger count is written as blocks of
  the character that the pattern calls, which match the same texts and try the numbers of times in the same
  order.
- **Lookbehinds of no fixed length.** PCRE2 takes a lookbehind only when its length is fixed. Any other is a search
  for where its body starts. The body is held to the place the lookbehind stands at, the starts are tried from
  that place backwards, and none is tried where the character before the lookbehind is not one the body can end
  with. `(?<=(?:[ax]+){2,})$` is answered at once on forty characters, where a body that ran on past the
  lookbehind reached the limit on the steps of a match.
- **What is refused.** A pattern that is valid ECMA-262 and that the translator cannot write with exactly the same
  meaning is not run with another meaning. Compiling the schema throws a `CompileError` that names the construct.
  These are:
  - a backreference inside a lookbehind to a group that the lookbehind matches before it, where the group is not in
    an end of the lookbehind of a fixed length (`(?<=\1(a+))`);
  - a backreference to a group inside a lookbehind of no fixed length, where the lookbehind is not a term of the
    pattern itself (`(?:(?<=(a)+)x\1)+`), or where the group holds what depends on the order in which ECMA-262 tries
    the ways through the lookbehind from its end (`(?<=(a*)(x+))-\1`);
  - a case-insensitive backreference in a pattern that is only valid without the `u` flag (one with an Annex B form
    such as `\&`), where the group can hold a character that ECMA-262 compares differently there than PCRE2 does:
    `k`, `s`, U+212A, U+017F and 74 other characters of the Basic Multilingual Plane, and every character beyond it
    that has a case variant. `^(.)(?i:\1)\&?$` is refused, and `^([a-j]+)(?i:\1)\&?$` is run. No pattern that is
    valid with the `u` flag is refused for this;
  - a pattern PCRE2 does not compile for a reason that is not its size, which no pattern is known to be. (A
    pattern that is valid and too large is not refused in this way. See the limits below.)

  No pattern of the JSON-Schema-Test-Suite or of the jsonschema-benchmark corpora is refused.
- **The lookbehind exception.** A pattern that is valid with the `u` flag is run, and answers as V8 does, with one
  named exception. Two uses of a lookbehind need it to be matched backwards, as ECMA-262 matches it, which PCRE2 cannot
  do, and they are refused with an error that says which:
  - a group captured inside a lookbehind of no fixed length and read after it, outside the forms that are written,
    such as `(?<=(a*)(x+))-\1` ("a backreference to a group inside a lookbehind of no fixed length, where the group holds
    what only matching the lookbehind backwards can tell");
  - a backreference inside a lookbehind to a group of it that is not in an end of it of a fixed length, such as
    `(?<=\1(a+))x` ("a backreference inside a lookbehind to a group that the lookbehind matches before it").

  A lookbehind with no group that a backreference reads is never refused for this. A pattern over a limit of size
  is the only other such pattern that does not compile, and its error is one of its own.
- **A pattern and a match have limits.** See [Limits](#limits) below.
- **Surrogates.** A JSON string with the escape of a surrogate that is not half of a pair, such as `"\ud800"`, is
  not a string of Unicode text, and the parser refuses the document ("lone leading surrogate in hex escape"), in an
  instance and in a schema. So no instance holds one, and a pattern is never matched against one. A pattern may
  name one by its own escape (`\ud800` in the pattern, which is `"\\ud800"` in JSON). With the `u` flag it matches
  nothing, as no string has one. Without it, it matches that half of a character beyond the Basic Multilingual
  Plane.

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

**The formats.** A string that `format` asserts is a string of the instance. Every check reads its bytes with a
scanner written for the package, and none is a regular expression, so none has a match to limit. A date, a time, a
duration, a UUID, an IPv4 address, a URI, an IRI, a URI template, a JSON pointer and the local part of an e-mail
address are read in one pass, or in a fixed number of passes. The checks that do more than read have a limit of
length that no valid string passes, and a longer string is answered from its length: an IPv6 address is at most 64
bytes, a host name (and the host of an e-mail address) at most 253, and an internationalized host name (and the host
of an internationalized e-mail address) at most 1,012, which is 253 characters of four bytes. The `regex` format
reads a string of at most 1,048,576 bytes with groups nested at most 200 deep, and a string over one of those is not
a valid regular expression to it. It puts the group names of the string in order to find a name that is written
twice, which takes time in proportion to n log n for n names. So the time of every check is in proportion to the
length of the string, or to that and its logarithm. On a probe of 260 hostile strings of each length (long runs of
what each format repeats, and near misses at their ends), the slowest check of a string of 100,000 characters took
9 ms and of 1,000,000 characters 121 ms, which includes parsing the string. Before this, an internationalized
host name of 1,000,000 characters took more than a minute, and so did a regular expression of 125,000 groups of one
name.

On the thirteen files of the
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data), 98,839 patterns
and 4.2 million answers, no pattern is over a limit of size, and with the limit at one second two patterns reach it
with Julia 1.13 and four with Julia 1.10, on one or a few strings each. All are patterns of nested repetitions of
what can match the empty string, such as `^a{2}?(((?!)|((|a){2,}(|a)\B|){2,3}?){2,3}.{3}a{2})\4`. V8 refuses an
empty iteration and answers them at once, where PCRE2 tries each, which is more steps than the first stage of a
match has (157 million for one of them) and more than the code that reads the clock runs in a second. They answer
as V8 does with `pattern_match_limit=nothing`. Every other answer is V8's, but for the two lookbehind uses and the
case-insensitive backreference above, which are refused.

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
| Corvus .NET, the JIT with [runtime code generation](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/RuntimeEvaluator.md#runtime-code-generation) | 2.45 (0 of 37) | 0.006 (37 of 37) | 0.030 (37 of 37) | 0.20 (37 of 37) |
| Corvus Java | 2.17 (2 of 37) | 0.023 (37 of 37) | 0.015 (37 of 37) | 0.23 (37 of 37) |

Against [JSONSchema.jl](https://github.com/JuliaIO/JSONSchema.jl), which jsonschema-benchmark does not have, the
two were run in one process with their timed passes interleaved, on the corpora whose drafts JSONSchema.jl
implements (4, 6 and 7). A warm pass with CorvusJsonSchema takes 0.4% to 4.2% of JSONSchema.jl's time on 34 of them,
1.6% at the geometric mean. On the thirty-fifth, ui5, JSONSchema.jl takes about seven minutes a pass.

[CorvusJsonSchemaBench](../CorvusJsonSchemaBench) has the harnesses and how to run them.

## Differences from the Go module

- The regular expression engine is PCRE2 after an exact translation, where the Go module has an engine of its own.
  A pattern the translator cannot write exactly is a compile error here, and a match can reach a limit and throw.
  See [Patterns](#patterns).
- The URI, IRI, URI template and e-mail formats read their grammars directly, where the Go module uses the standard
  library's `regexp`.
- A validator keeps one evaluation state and a pool for validations that overlap, where the Go evaluator is a value
  on the stack.
- The options are keyword arguments, and the errors are exceptions.

## Unicode

The package takes no Unicode data from Julia or from its PCRE2. Julia's Unicode tables follow the Julia release,
and so do those of the PCRE2 it bundles (Unicode 14 in Julia 1.10, Unicode 16 in Julia 1.13). A package that read
them would give different answers on different Julia versions.

Every property the package reads is in its own tables, which hold Unicode 17 (`src/ecmaregex/unicode_data.jl`, and
`src/unicode.jl` for the formats). They are the general categories, the scripts and script extensions, the binary
properties ECMA-262 lists and simple case folding. `pattern` and `patternProperties` write their `\p{...}` classes,
`\w`, `\d`, `\s`, `\b` and their case-insensitive groups as explicit ranges from those tables, so PCRE2 is never
asked what a character is. The `hostname`, `idn-hostname` and `idn-email` formats read the same data. A URI is
normalized by lowering the letters A to Z only, as RFC 3986 and RFC 3987 specify.

The results are therefore the same with every Julia release from 1.10, with one exception.

### Julia 1.10

One thing is left to PCRE2, which is the comparison of a case-insensitive backreference such as `(.)(?i:\1)`. PCRE2
compares by Unicode simple case folding, as ECMA-262 does with the `u` flag, in the Unicode version it was built
with.

Julia 1.10 bundles PCRE2 10.42, whose case folding is that of Unicode 14. A case-insensitive backreference there
does not hold equivalent the two characters of each of the thirty pairs below, which Unicode 15.1 and 16 made
equivalent and which V8 and Julia 1.13 hold equivalent. So `^(.)(?i:\1)$` matches `ƛꟜ` with Julia 1.13 and
does not with Julia 1.10. Nothing else differs between the two: a literal, a class and every other construct read the
package's own tables on every Julia. The package does not work around this. The fix is to update to Julia 1.13, where
the answers are exact.

These thirty are every pair. The tests try each character that has a case variant against each other (`what PCRE2
holds equivalent`), and expect exactly these with Julia 1.10. Of the thirteen files of the
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data),
Julia 1.10 differs from V8 on 1,026 answers, in twenty patterns of the case-folding file, and every one of them is a
text of two characters that are one of these pairs. The tests hold those twenty patterns too.

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

## Tests

```sh
julia --project=. -t 4 test/runtests.jl
```

`julia --project=. -e 'using Pkg; Pkg.test()'` runs the same tests.

- `suite.jl`: the JSON-Schema-Test-Suite (the repository's submodule, or `JSON_SCHEMA_TEST_SUITE`), every case
  fail-fast (as a document, as bytes and as a string) and through a collector at each level. `SUITE_DRAFT` and
  `SUITE_FILTER` narrow it. The test fails when the suite is missing.
- `annotations.jl` and `results.jl`: the suite's annotation tests and the results expectations shared with the C#,
  Rust, Go, Java and TypeScript evaluators.
- `allocations.jl`: validation of a document, of bytes and of a string allocates nothing in the steady state.
- `stability.jl`: the hot functions have no dynamic dispatch, and the hot structures no field of an abstract type.
- `pattern.jl`, `document.jl`, `plan.jl`, `unicode.jl`, `api.jl`: the regex-free matchers against the engine, the
  parser, the name lookup, the Unicode tables of the formats, and the public functions.
- `ecmaregex/`: the pattern engine against answers recorded from V8 (`v8_oracle.json`, `v8_fuzz.json`), the patterns
  of the suite, the translator, the validator of the `regex` format, the Unicode data, that a match allocates
  nothing, the limits of a match and the limits of size. `v8tests.jl` has tables of V8's answers for each class of pattern the
  translation once differed on. It reads the suite from `CORVUS_JSON_SCHEMA_TEST_SUITE` when the submodule is
  elsewhere.
- `metaschemas.jl`: the embedded metaschemas match `src/Corvus.Text.Json/metaschema`.
- `differential.jl`: the plans against the general evaluator on the jsonschema-benchmark corpora and on mutations
  of their instances. It runs when `JSONSCHEMA_BENCHMARK` names a checkout.
- `docs.jl`: the samples in this README and in `docs/JsonSchemaForJulia.md`.

## License

Apache 2.0.
