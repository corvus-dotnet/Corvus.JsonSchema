# JSON Schema for Java and Kotlin

[corvus-json-schema](https://central.sonatype.com/artifact/io.github.corvus-dotnet/corvus-json-schema) is the Corvus
JSON Schema evaluator for Java and Kotlin: draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime
evaluator (see [Runtime Evaluator](RuntimeEvaluator.md)), and gives the same results and annotations. It runs on Java 17
or later and is one jar with no dependencies.

A schema is compiled once into JVM bytecode: one method per subschema, holding only the checks that subschema needs,
which the JIT then optimises as it would hand-written code. Any number of instances can then be validated against it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, and all of its annotation tests.
- **Fast.** See [Performance](#performance).
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Allocation-free validation.** In the steady state, validating a parsed document, or JSON text, allocates nothing.

## Install

Maven:

```xml
<dependency>
  <groupId>io.github.corvus-dotnet</groupId>
  <artifactId>corvus-json-schema</artifactId>
  <version>0.1.3</version>
</dependency>
```

Gradle (Kotlin DSL):

```kotlin
implementation("io.github.corvus-dotnet:corvus-json-schema:0.1.3")
```

## Validate

```java
import io.github.corvusdotnet.jsonschema.Validator;

Validator validator = Validator.compile("""
    {
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

A `Validator` is immutable and safe to share between threads.

## JSON text

`isValid(String)` and `isValid(byte[])` parse the text into buffers the validator reuses on the calling thread and
validate it in place, so in the steady state they allocate nothing. To validate the same text more than once, parse it
into a `JsonDocument`: the UTF-8 text and one flat array of values, with strings read in place where they have no
escapes.

```java
JsonDocument document = JsonDocument.parse(bytes);
boolean valid = validator.isValid(document);
```

`isValid` reports a schema that recursed in place beyond the maximum depth as invalid; `validate` throws a
`SchemaEvaluationDepthException` instead. Text that is not JSON throws a `JsonParseException`.

## Options

`Validator.compile(schema, options)` takes a `CompileOptions`, built with `CompileOptions.builder()`:

| Option | Meaning |
|---|---|
| `defaultDialect` | The dialect of a schema without `$schema` (default `Dialect.DRAFT202012`). |
| `assertFormat` | `true` asserts `format`, `false` never does; `null` (the default) follows the schema's vocabularies. |
| `assertFormatInLegacyDrafts` | With `assertFormat` unset, assert `format` in drafts 4 to 7 too. |
| `assertContent` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `true`). |
| `format` | A custom format by name: a predicate on the string (or a number's JSON text). |
| `documentResolver` | A function from an absolute URI to the document, for remote references. The standard metaschemas are built in. |
| `baseUri` | The base URI of the root document. |
| `entryPoint` | A subschema to validate against, such as `#/$defs/item`. |
| `maxDepth` | The deepest the evaluator recurses in place (default 128). |

## Results and annotations

```java
JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.create(ResultsLevel.DETAILED);
validator.evaluate(JsonDocument.parse("{\"id\": 0}"), collector);
for (SchemaResult r : collector.results()) {
    // r.isMatch(), r.message(), r.evaluationLocation(), r.schemaEvaluationLocation(), r.documentEvaluationLocation()
}

JsonSchemaResultsCollector verbose = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
validator.evaluate(JsonDocument.parse("{\"id\": 3}"), verbose);
verbose.collectAnnotations(); // instance location -> keyword -> schema location -> value (JSON text)
```

`BASIC` records the failures without messages, `DETAILED` adds the messages, and `VERBOSE` records every keyword,
passing ones and annotations included.

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
  the square of the length of the string.
- An iteration of a counted repetition that matches nothing does not end the repetition while iterations are still
  due, so `(^|x){2}a` matches "xa" as ECMA-262 defines, where `java.util.regex` does not. Such a repetition is
  written with a copy of its body for each iteration it must run.
- A backreference reads what ECMA-262 says its group holds, where `java.util.regex` reads something else. A group
  that did not take part reads as the empty string. The groups of a repeated body are unset when an iteration
  starts, so `^(?:(a)|x)*\1$` takes "ax" and not "axa", and an iteration that matched nothing leaves no group set. A
  lookbehind captures as if run backwards, so `(?<=\1(a))b` takes "aab". A lookahead takes the first way its body
  matches, so after `(?=(a)?b)` the group holds "a" before "ab" and nothing before "b". A group of a lookaround is
  the one its own iteration set, so `^(?:(?=([ab]))[ab](?:c|ca)\1)*$` takes "acaa" and not "acab". Inside a
  case-insensitive group a backreference compares by ECMA-262's case folding, the same on every JDK:
  `^(?i:(.)\1)$` takes "sſ" and "kK" (U+017F, U+212A) and not "iİ" (U+0130). Such a pattern is written once for
  each way through the groups it reads, and a case-insensitive backreference with its comparison written out, so
  the translation can be long: between 12,000 and 25,000 characters for a case-insensitive backreference to a
  group that can hold any character.


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

**The limits of size.** A pattern is refused when its translation would be too large.

- more than 256 alternatives for the ways through the groups it reads, or more than 1,048,576 characters ("a
  pattern with this many ways through the groups it reads"), as for
  `(?:(a)?(b)?(c)?(d)?(e)?(f)?(g)?(h)?(i)?\1\2\3\4\5\6\7\8\9)*`. A lookahead whose group is read after it, over
  a counted repetition whose body can match nothing, is written with a text that doubles with each iteration of the
  count, so a count of 13 or more is refused, as for `(?=((?:a|){0,20}?)a)\1a$`;
- more than 200 copies of a body, or 65,536 characters of them, for a repetition that must run two or more times
  over a body that matches the empty string in some places only, such as `(^|x){201}` ("a repetition this long of a
  body that can match the empty string"). A backreference is such a body, and a case-insensitive one to a group
  that can hold any character is 12,000 characters or more, so a count of five or more over one is refused, as for
  `^(?i:(.)\1{5})$`, and a count of three or more when the group holds two characters;
- more than `java.util.regex` compiles on the stack of the thread, which is some thousands of groups on a stack of
  1 MB ("a pattern too large for java.util.regex to compile").

The translator keeps one more reason, "a backreference to a group of a lookaround, in a repetition that can go back
to before the backreference", for a reference it could not give a run of the lookaround of its own. No pattern is
known that it refuses: none of 125,839 generated patterns does.

A pattern that is deliberately hard, with nested repetitions over groups that are read, can be slow. The translation
finds some places by search, and `java.util.regex` matches by backtracking.

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
| Corvus .NET, with [runtime code generation](RuntimeEvaluator.md#runtime-code-generation) | 1.13 (15 of 37) | 1.3 (6 of 37) |

Once warm, validation is faster than Blaze, the Rust crate and the .NET interpreter on most corpora, and a little
slower than the code .NET generates. Start-up is the JVM's weak point: compiling a schema takes tens of milliseconds and the
first validation runs before the JIT has compiled the generated code, so a process that validates a few documents and
exits is better served by a native implementation.

## Links

- Package: [Maven Central](https://central.sonatype.com/artifact/io.github.corvus-dotnet/corvus-json-schema)
- Source and README: [src-java/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-java/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
