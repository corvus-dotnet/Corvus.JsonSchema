# corvus-json-schema (Java)

A JSON Schema evaluator for Java and Kotlin (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5
runtime evaluator (`Corvus.Text.Json.RuntimeEvaluator`) and its Rust and TypeScript ports. Java 17 or later; one jar
with no dependencies.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`), and all
  of the suite's annotation tests.
- **Fast**: a schema compiles to JVM bytecode, one method per subschema holding only the checks it needs, which the JIT
  then optimises as it would hand-written code. See [Performance](#performance).
- **No allocation**: validating a parsed document, or JSON text through the validator's reused buffers, allocates
  nothing in the steady state.
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the same
  rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them.

## Install

```xml
<dependency>
  <groupId>io.github.corvus-dotnet</groupId>
  <artifactId>corvus-json-schema</artifactId>
  <version>0.1.3</version>
</dependency>
```

```kotlin
implementation("io.github.corvus-dotnet:corvus-json-schema:0.1.3")
```

## Usage

```java
import io.github.corvusdotnet.jsonschema.Validator;

Validator validator = Validator.compile("""
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "properties": { "id": { "type": "integer", "minimum": 1 } },
      "required": ["id"]
    }""");

validator.isValid("{\"id\": 3}"); // true
validator.isValid("{\"id\": 0}"); // false
```

From Kotlin:

```kotlin
val validator = Validator.compile("""{"type": "array", "items": {"type": "string"}}""")
validator.isValid("""["a", "b"]""") // true
```

A `Validator` is immutable and safe to share between threads. Instances can be given as JSON text (`String` or UTF-8
`byte[]`), parsed into the thread's reused buffers, or as a `JsonDocument` parsed once and validated any number of
times:

```java
JsonDocument document = JsonDocument.parse(bytes);
boolean valid = validator.isValid(document);
```

`isValid` reports a schema that recursed in place beyond the maximum depth as invalid; `validate` throws a
`SchemaEvaluationDepthException` instead. An instance that is not JSON throws a `JsonParseException`.

### Options

`Validator.compile(schema, options)` takes a `CompileOptions` (the Java counterpart of `JsonSchemaEvaluatorOptions`):

```java
CompileOptions options = CompileOptions.builder()
    .defaultDialect(Dialect.DRAFT7)        // for schemas without $schema (default 2020-12)
    .assertFormat(true)                    // true, false, or null to follow the vocabularies (the default)
    .format("even", s -> s.length() % 2 == 0)
    .documentResolver(uri -> uri.startsWith("https://example.com/") ? load(uri) : null)
    .baseUri("https://example.com/root.json")
    .entryPoint("#/$defs/item")
    .maxDepth(128)
    .build();
```

| Option | Meaning |
|---|---|
| `defaultDialect` | Dialect for schemas without `$schema` (default `DRAFT202012`). |
| `assertFormat` | `true` asserts `format`, `false` never does; `null` follows the vocabularies (2020-12 `format-assertion`). |
| `assertFormatInLegacyDrafts` | With `assertFormat` unset, also assert `format` in drafts 4 to 7. |
| `assertContent` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `true`). |
| `format` | A custom format assertion by name; it receives the string, or a number's JSON text. |
| `documentResolver` | Resolves remote `$ref`s by absolute URI. The standard metaschemas are built in. |
| `baseUri` | Base URI of the root document. |
| `entryPoint` | Evaluate from a subschema, e.g. `#/$defs/item`. |
| `maxDepth` | Depth limit for in-place recursion on a cycle (default 128). |

An unresolvable reference or an invalid pattern throws `SchemaCompilationException`. `Validator.compileFromUri`
compiles a document fetched through the resolver (or a standard metaschema).

### Results and annotations

```java
JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.create(ResultsLevel.DETAILED);
validator.evaluate(JsonDocument.parse("{\"id\": 0}"), collector); // false
for (SchemaResult r : collector.results()) {
    // r.isMatch(), r.message(), r.evaluationLocation(), r.schemaEvaluationLocation(), r.documentEvaluationLocation()
}

JsonSchemaResultsCollector verbose = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
validator.evaluate(JsonDocument.parse("{\"id\": 3}"), verbose);
verbose.collectAnnotations(); // {"" -> {"title" -> {"#" -> "\"Person\""}}, ...}
```

The levels and rows are those of the C# collector: `BASIC` records failures without message text, `DETAILED` adds the
text, `VERBOSE` records every keyword, passing ones and annotations included. Collecting runs the evaluator's
interpreter over the compiled graph.

## Regular expressions

`pattern`, `patternProperties` and the `regex` format follow ECMA-262, as JSON Schema specifies, with the grammar of
ECMAScript 2025. A pattern is read with the `u` flag, and a pattern that is not valid with it is read as a pattern
with no flag, which accepts what many schemas hold (`\&`, a lone brace). The `regex` format accepts only the `u`
flag grammar.

A pattern that is valid with the `u` flag matches characters. A pattern that is valid only without it matches UTF-16
code units, as ECMA-262 says, and a character beyond the Basic Multilingual Plane is two of them. So `^\-?.$` does
not take "𐐀" (U+10400), and `^\-?..$` does. Such a character written in the pattern is two atoms: a
quantifier after it repeats its second half, a class holds each half, and a class range from one such character to
another is not valid. Without the flag `\u{...}` is not an escape, and the escapes of the two halves of a pair are
two atoms. A group name is read by character in both grammars.

A JSON string cannot hold a surrogate that is not half of a pair. The parser refuses the escape of one, such as
`"\ud800"`, in a schema and in an instance, with a `JsonParseException`. So no pattern is matched against one.

- `\p{...}` covers every property, value and alias ECMA-262 lists: General_Category, Script, Script_Extensions and
  the 53 binary properties. The data is Unicode 17 on every JDK. It does not depend on the Unicode version of the JDK
  the library runs on.
- Modifier groups (`(?i:...)`, `(?m:...)`, `(?s:...)`, `(?i-s:...)`) ignore case, match at line terminators and let
  the dot match everything as ECMA-262 defines them. Group names can be any ECMAScript identifier, can be written
  with `\u` escapes, and can be shared by groups in separate alternatives.
- A lookbehind can be of any length. One that `java.util.regex` cannot bound is run by a search whose time grows with
  the square of the length of the string. The search is held to the place the lookbehind stands at, so a repetition
  inside a repetition, as in `(?<=(?:[ax]+){2,})$`, does not try the ways to split the text after that place.
- An iteration of a counted repetition that matches nothing does not end the repetition while iterations are still
  due, so `(^|x){2}a` matches "xa" as ECMA-262 defines, where `java.util.regex` does not. Such a repetition is
  written with a copy of its body for each iteration it must run. A body with one way to match, such as a
  backreference, needs no copies: when one iteration of it matches nothing, so do the ones after it.
- A backreference reads what ECMA-262 says its group holds, where `java.util.regex` reads something else. A group
  that did not take part reads as the empty string. The groups of a repeated body are unset when an iteration
  starts, so `^(?:(a)|x)*\1$` takes "ax" and not "axa", and an iteration that matched nothing leaves no group set. A
  lookbehind captures as if run backwards, so `(?<=\1(a))b` takes "aab". A lookahead takes the first way its body
  matches, so after `(?=(a)?b)` the group holds "a" before "ab" and nothing before "b". A group of a lookaround is
  the one its own iteration set, so `^(?:(?=([ab]))[ab](?:c|ca)\1)*$` takes "acaa" and not "acab". Inside a
  case-insensitive group a backreference compares by ECMA-262's case folding, the same on every JDK:
  `^(?i:(.)\1)$` takes "sſ" and "kK" (U+017F, U+212A) and not "iİ" (U+0130). A reference to a group of several
  characters is compared one character at a time, as ECMA-262 says: `^(?i:(..)\1)$` does not take "ßSsẞ" (U+00DF,
  U+1E9E), which V8 13.6 takes, as it compares the two texts by full case folding. `java.util.regex` compares the
  characters for which its own case-insensitive comparison is that of ECMA-262, and the others are compared by
  classes of equivalent characters written into the translation. Which characters those are is found by asking the
  engine of the JVM the library runs on, once. Java 17 fails on a character beyond the Basic Multilingual Plane,
  Java 21 lacks the pairs that Unicode 15.1 and 16 added, and every JDK equates "i" with U+0130 and U+0131. So the
  answers are the same on every JDK, and the text is not: `^(?i:(.)\1)$` is 1,370 characters on Java 25, 2,128 on
  Java 21 and 5,729 on Java 17.


The patterns are run by `java.util.regex`, after translation. A pattern that is valid with the `u` flag is run
with the meaning ECMA-262 gives it. There is one exception, and there are limits of size. A pattern that falls under
either is refused. Compiling a schema with it throws `SchemaCompilationException` with a message that starts
"Unsupported regular expression" and names the reason. It is never run with another meaning. The `regex` format
still accepts it, as it is a valid pattern.

**The exception is a group of a lookbehind that only matching backwards can tell.** ECMA-262 matches a lookbehind
backwards, from its last term to its first, and `java.util.regex` matches forwards. The library writes a lookbehind
whose groups are read, in it or after it, when what the backward match captures can be told while matching
forwards. That covers groups at a fixed distance from the end of the lookbehind, alternatives, and a repetition of a
body of fixed length that stands first, such as `(?<=([abc])+)x\1`. Any other is refused, with one of two reasons.

- "a backreference to a group inside a lookbehind whose text depends on the lookbehind being matched backwards", as
  for `(?<=(x*?)a*)\1`, `(?<=(a)+b*)x\1` and `(?<=(a|ab)c*)\1`;
- "a backreference inside a lookbehind to a group of that lookbehind that is not at a fixed distance from it", as
  for `(?<=\1a*(b))x` and `(?<=\1(a)*)x`.

A lookbehind none of whose groups is read is not affected, whatever its body.

**The limits of size.** A pattern is refused when its translation would be too large. The limits are far beyond
what a pattern of ordinary size comes to. None of the 98,839 patterns of the
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) reaches one, on
Java 17, 21 or 25.

- "a pattern with this many ways through the groups it reads". A pattern with a backreference that
  `java.util.regex` would read wrongly is written once for each way through the choices that decide what the groups
  it reads hold. The ways through one part of the pattern are written as a tree, so what they start with is written
  once. There may be 65,536 ways through one part, 16,777,216 characters of translation, 134,217,728 characters
  written on the way to it, and 200 iterations of a lookahead written one by one. Sixteen groups that may each be
  set or not, all of them read, are 65,536 ways, and are written: `(?:(a)?(b)?...(p)?\1\2...\16)*` comes to 5.3
  million characters. Seventeen such groups are refused, and so are sixteen that are each read three times, which
  come to more text than that. A lookahead whose group is read after it, over a counted repetition, is written once
  for each iteration: `(?=((?:a|){0,200}?)a)\1a$` comes to 4,216 characters, and a count of 201 is refused. The smallest pattern found that writes too much on the way is
  `.*(?:(?=((?:(a|))*(?:(a)|(b)|){3,16}?\b))(?:\3)*?\1a$)*\2`: a lookahead with two repetitions whose groups are
  read, inside a repetition, with a reference to it inside another. With a count of 15 it is written, in 376,889
  characters;
- "a repetition this long of a body that can match the empty string". A repetition that must run two or more times,
  over a body that matches the empty string in some places only and has more than one way to match, is written
  with a copy of the body for each iteration, and there may be 200 copies: `(^|x){200}` is written and `(^|x){201}`
  is refused. A body with one way to match is not copied, so `^(?i:(.)\1{5})$` and any other count over a
  backreference are written;
- "a pattern too large for java.util.regex to compile": more than `java.util.regex` compiles on the stack of the
  thread, which is some thousands of groups on a stack of 1 MB.

The translator keeps one more reason, "a backreference to a group of a lookaround, in a repetition that can go back
to before the backreference", for a reference it could not give a run of the lookaround of its own. No pattern is
known that it refuses: none of 215,803 generated patterns does.

**Slow patterns.** The library sets no time limit on a match. A pattern that is deliberately hard, with nested
repetitions over groups that are read, can be slow: the translation finds some places by search, and
`java.util.regex` matches by backtracking, where V8 answers in milliseconds.

Of the 98,839 patterns of the test data, ten do not answer for all their texts within five seconds on one JDK or
another, with four of them running at once. Each nests repetitions whose bodies can match nothing around groups
that are read, or holds a lookbehind that is searched for. Run alone on Java 21, each gives V8's answers. The time
is for all the texts of the pattern (between 50 and 160 of them), with the start of the JVM and the translation:

- 1.7 s, `lookarounds`: `^(?:b?(?=(?:((?:.|b[ab]))?\1){1,2}(?:a{2}(?:c(b)){1,2})?(?:(?:c(c))?((?:c|.[^a]))*|((?:[bc]|ba))??))(?:b\2|[ab][bc]\1)a{2,}(?<=\3.?)\4c){2}\2`
- 4.7 s, `empty-loops`: `(?:(?:(?<=$|(|a)\b)(|-)?|.x){4,}?\1){2,}a`
- 15.2 s, `backreferences`: `^(x){0}\1(?<n0>(\3{2}){0,2}\2(?:(?!$)(\k<n0>\1)?){3,}|(?:(?<n1>(?<=x)[^a])|(?<n1>\6+?)){2,}(?:(?<n2>\2(?:(?<n3>a(x)*)|(?<n3>\7?a)){3,}?\3{2})|(?<n2>[^a]-))+|(?:(?:x){2,4}|\11*){1,3}\11{2}x){3,}?x$`
- 16.1 s, `backreferences`: `^(.|.)*?(?<n0>(-??){3,5}\3(?:(?<n1>x\4*x|(?<!a)){0,2}\3a?){2,}?|a()){2,3}\k<n1>x$`
- 18.9 s, `engine-faults`: `^(?=((?:(?:(|a)(?:(?:(?:(a|)){1,3}?(?:.?)??))*?(?:.?)+))*[ax]))\1$`
- 20.0 s, `lookarounds`: `(?:(?:(?<=$|(|a)\b)(|-)?|.x){4,}?\1){2,}a`
- 24.5 s, `empty-loops`: `((?!\Bx??|)-??|-(?:a|(?:a*|[ax]){2,}?){2}|(.(?<!a)|\B|($){3,}(\B|(?!$)){2,}?){4,}a*?){4}x`
- 30.6 s, `case-folding` with the `i` flag: `^S|^((?:(?!.[\w\u017f\u212a\u0130]|\u{10400})(..\u01c6)*?(?<n0>\2?.|-\k<n0>+\u1fd3)??)*)+(?:\u01c5.)+?$`
- 62.2 s, `empty-loops`: `^(?:((?!|-$)|(?=).|(?<=[^a])){2}){0}(?:\1|(?:(?<!^)(?:[^a]*\b|[^a]){2,3}|[^a]){3,4}(?:(|)|)*){3}a$`
- 289.5 s, `backreferences`: `^((?<n0>\k<n0>?|(?:\1?(|a)\k<n0>|()(x)*){0,2}){1,2}?((?:a){0,0}?^x{1,2})?|(a|)){1,}?\4\3-$`

## How it works

The pipeline follows the C# evaluator stage for stage, as the Rust and TypeScript ports do: the loader identifies
documents, resources, anchors, dialects and vocabularies; the compiler builds one node per schema location with its
keywords digested and `$ref`s resolved, and analyses evaluated-property marking, in-place cycles and `oneOf`/`anyOf`
discriminators. Then, where the C# evaluator interprets fused plans, this port generates bytecode (with
[ASM](https://asm.ow2.io/), shaded into the jar), as the TypeScript port generates JavaScript:

- each node becomes a static method of a hidden class; structurally identical nodes share one;
- property names are dispatched by length, then compared as 64-bit words (a trie over the words, switching on a
  distinguishing byte where many names share a prefix), and `required` is a bit mask filled in the same pass;
- `$ref`/`allOf` chains of object schemas are checked in one merged pass; `oneOf`/`anyOf` narrow by a discriminator
  property or by type; `unevaluated*` is decided from the coverage known at compile time;
- common pattern shapes (literals, class sequences, separated lists, line lengths) match the UTF-8 bytes without a
  regular expression engine; other patterns are translated from ECMA-262 to `java.util.regex`, with Unicode data of
  the library's own;
- numbers compare exactly across longs and doubles, and `multipleOf` is decided on the decimal digits of the text.

[OPTIMIZATIONS.md](OPTIMIZATIONS.md) maps each technique to its counterpart in the other Corvus evaluators.

## Performance

Measured with [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s 37 corpora, each
implementation in its own container pinned to the same 8 CPUs, the median of 3 runs. Every harness warms up for 2
seconds (at least 100 passes) and reports its last warm-up pass, so each runtime is measured after its JIT has
settled. Figures are the geometric mean of Java's time over the other's (below 1 means Java is faster), and how many
corpora Java is faster on.

| Java (JDK 25) over | Warm validation | Parse |
|---|---|---|
| [Blaze](https://github.com/sourcemeta/blaze) | 0.42 (35 of 37) | 2.7 (5 of 37) |
| Corvus Rust | 0.76 (32 of 37) | 7.7 (0 of 37) |

Against Corvus .NET 5.7.6 the same protocol was run with each engine as a process on the host (JDK 25 with an AOT
cache, .NET 10 compiled ReadyToRun, both pinned to the same 12 CPUs, the median of 5 runs).

| Java (JDK 25) over | Warm validation | Parse |
|---|---|---|
| Corvus .NET, interpreting the schema | 0.57 (33 of 37) | 1.3 (5 of 37) |
| Corvus .NET, with [runtime code generation](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/RuntimeEvaluator.md#runtime-code-generation) | 1.13 (15 of 37) | 1.3 (6 of 37) |

Once warm, validation is faster than Blaze, the Rust crate and the .NET interpreter on most corpora, and a little
slower than the code .NET generates. Start-up is the JVM's weak point: compiling a schema takes tens of milliseconds and the
first validation runs before the JIT has compiled the generated code, so a process that validates a few documents and
exits is better served by a native implementation.

[corvus-json-schema-bench](../corvus-json-schema-bench) has the harnesses and how to run them.

## Tests

```sh
./mvnw test
```

- `SuiteTest`: the JSON-Schema-Test-Suite (the repository's submodule, or `-DjsonSchemaTestSuite=...`), every case
  fail-fast and through a collector at each level. `SUITE_DRAFT` and `SUITE_FILTER` narrow it. Run it with
  `-DcorvusArgLine=-Dcorvus.jsonschema.interpret=true` to evaluate with the interpreter instead of compiled code.
- `AnnotationSuiteTest`, `ResultsTest`: the suite's annotation tests and the results expectations shared with the C#,
  Rust and TypeScript evaluators.
- `AllocationTest`: every keyword path, compiled and interpreted, allocates nothing in the steady state (run again
  without escape analysis).
- `PatternShapesTest`, `EcmaRegexValidatorTest`, `FastDoubleTest`, `RuntimeTest`: the regex-free matchers, the regex
  format's reader and the number conversion against their straightforward counterparts.
- `EcmaRegexOracleTest`: the translation of ECMA-262 patterns against what V8 answers, for the 61,383 patterns of
  `v8_oracle.json` (the file the Go port's pattern package is tested with). The patterns the translator refuses are
  counted by reason. It also holds the patterns that fuzzing against V8 found refused or answered wrongly, with
  V8's answers. `EcmaRegexTest` and `EcmaUnicodeTest` cover the property escapes, the modifier groups, lookbehind,
  backreferences (tables of patterns with V8's answers for each kind the translation writes specially) and the
  Unicode data, which `gen-ecma-unicode-data.ps1` generates.
- `MetaschemasTest`: the embedded metaschemas match `src/Corvus.Text.Json/metaschema`.

## License

Apache 2.0.
