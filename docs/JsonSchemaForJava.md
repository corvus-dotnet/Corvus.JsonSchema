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
| `patternMatchLimit` | The longest one match of a `pattern` or `patternProperties` regular expression may take (default 1 second). `noPatternMatchLimit()` turns the limit off. See [Regular expressions](#regular-expressions). |

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
  answers are the same on every JDK, and the text is not: `^(?i:(.)\1)$` is 1,370 characters on Java 25, 2,139 on
  Java 21 and 5,751 on Java 17.


The patterns are run by `java.util.regex`, after translation. A pattern that is valid with the `u` flag is run
with the meaning ECMA-262 gives it. There is one exception, and there are limits: of size, and on the time of a
match. A pattern that falls under the exception is refused. Compiling a schema with it throws
`SchemaCompilationException` with a message that starts "Unsupported regular expression" and names the reason. It is
never run with another meaning. The `regex` format still accepts it, as it is a valid pattern.

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

**The limit on a match.** A schema and an instance that come from outside can make a backtracking engine run for
as long as they like: `^(?:(a+)\1*)+$` takes time that doubles with each "a" of a string that ends in "b". So one
match of a `pattern` or `patternProperties` regular expression against one string is limited to 1 second, unless the
options say otherwise.

```java
CompileOptions options = CompileOptions.builder()
        .patternMatchLimit(Duration.ofMillis(100)) // or .noPatternMatchLimit()
        .build();
Validator validator = Validator.compile(schema, options);
try {
    validator.isValid(instance);
} catch (PatternMatchLimitException e) {
    // Neither valid nor invalid. e.pattern(), e.limit() and e.isStackLimit() say what happened.
}
```

A match that goes over the limit is abandoned, and the method that was validating throws
`PatternMatchLimitException`. That is so for `isValid`, `validate` and `evaluate`, for compiled code and for the
interpreter, and whether results are collected or not. None of them answers `false` for it. The validator is as it
was, and answers the next instance. `noPatternMatchLimit()` turns the limit off, for a caller that trusts both the
schema and the instances.

`java.util.regex` has no limit of its own, so the library counts what the engine does to the string: each character
it reads, and each time it asks for the length, which it does at every lookahead, word boundary and end anchor. The
clock is read once every 4,096 of those for a translation of up to 1,024 characters, and more often for a longer
one. So a match ends a little after the limit and not at it, and a match that reads the string fewer times than that
reads no clock at all. Where the engine could go on for long without reading, as in a choice between two ways to
match nothing at the end of the string, the translation holds an empty lookahead, which is counted. Nothing is
allocated for the limit. The matchers the library has for the common shapes of pattern use no regular expression
engine, read a string a bounded number of times, and have no limit.

`java.util.regex` matches by recursion, and a repetition of a group takes a level or more of the stack for each
iteration. A match that runs out of stack throws the same `PatternMatchLimitException`, with `isStackLimit()` true,
and not a `StackOverflowError`. The library writes the common forms so that they do not recurse. Alternatives that
are each one character, as in `^(a|b)*$`, are one class. A repetition whose body has one way to match, and that what
follows cannot start inside, as in `^"(?:[^"\\]|\\.)*"$` and `^[a-z]+(?:\.[a-z]+)*@`, is written possessive. Any
other repetition of a group of alternatives or of varying length, such as `^(?:\w+\s?)*$`, still recurses, and on a
thread with a stack of 1 MB runs out after some thousands of iterations.

**The limits of size.** A pattern that comes from outside must not take long to compile or keep much memory. So a
pattern has limits of size. One over a limit is refused when the schema is compiled, with
`PatternTooLargeException`. That is a `SchemaCompilationException`, so code that catches the one catches the other.
Its message names the limit and its value. It says "It is a valid ECMA-262 regular expression, and is too large",
or, for a limit that is met before the pattern is read to its end, "It is too large to be read", and then that
whether it is valid is not known. It never says the pattern is invalid. The limits are fixed, and none is an option.

| Limit | Value | The message ends |
|---|---|---|
| Characters of the pattern | 1,048,576 | it has more than 1,048,576 characters |
| Groups, one inside another | 200 | its groups are nested more than 200 deep |
| Capturing groups | 32,768 | it has more than 32,768 capturing groups |
| Ranges of characters in its classes | 524,288 | its classes hold more than 524,288 ranges of characters |
| Characters of the translation | 1,048,576 | what is written for java.util.regex to run it is more than 1,048,576 characters |
| Ways through one part of the pattern | 8,192 | it is written once for each way through the groups its backreferences read, and one part of it has more than 8,192 ways |
| Characters written on the way to the translation | 16,777,216 | writing it for java.util.regex takes more than 16,777,216 characters of text |
| Copies of the body of a repetition | 200 | it needs more than 200 copies of the body of a repetition |
| Groups times parts, for a pattern written by its ways | 268,435,456 | the number of its groups times the number of its parts is more than 268,435,456 |
| What `java.util.regex` compiles | the stack of the thread | java.util.regex needs more of the thread's stack to compile it than there is |

The first four are read off the pattern. The others are about what the library writes for `java.util.regex`:

- A pattern with a backreference that `java.util.regex` would read wrongly is written once for each way through
  the choices that decide what the groups it reads hold. Thirteen groups that may each be set or not, all of them
  read, are 8,192 ways: `(?:(a)?(b)?...(m)?\1\2...\13)*` is written, in 499,547 characters. Fourteen are refused.
- A repetition that must run two or more times, over a body that matches the empty string in some places only and
  has more than one way to match, is written with a copy of the body for each iteration: `(^|x){200}` is written
  and `(^|x){201}` is refused. So is a lookahead whose group is read after it, over a counted repetition whose body
  has a group that can be empty: `(?=((?:a|(?:)){0,200}?)a)\1a$` is written and a count of 201 is refused.
- The limit on the translation counts the same length on every JDK. Two things are written with what the engine
  of the JVM knows, and are shorter on a later JDK: the comparison of a case-insensitive backreference, and the
  class of a property the engine has a class for, with what corrects it. The limits are applied to the translation
  for Java 17, which is the longest, and the pattern is then translated for the engine that runs it. So a pattern
  is refused on every JDK or on none. `\p{L}` counts 3,103 characters, and is 3,079 on Java 17, 1,472 on Java 21
  and 771 on Java 25: 300 of them in one pattern are accepted and 400 are refused.
- The compiler of `java.util.regex` recurses along a pattern. On a stack of 1 MB it compiled 4,096 classes in a
  row and not 16,384, which are refused as too large. This limit is the one that depends on where the library is
  called: the stack size of the thread, and how deep the caller already is.

A pattern of a few hundred characters meets a limit only where what is written for it multiplies. Of the 98,839
patterns of the [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data),
four are over a limit, the same ones on Java 17, 21 and 25: two would be written in more than 1,048,576
characters and two take more than 16,777,216 on the way. Each has a lookahead with several repetitions whose groups
are read after it, inside a repetition.

The translator keeps one more reason, "a backreference to a group of a lookaround, in a repetition that can go back
to before the backreference", for a reference it could not give a run of the lookaround of its own. No pattern is
known that it refuses: none of 215,803 generated patterns does.

**What the limits were measured against.** A probe of 287 adversarial cases, each a pattern of at most
1,000 characters against a string of at most 100,000 (and eight larger shapes), run each in a JVM of its own: nested
repetitions of what can match nothing, backreferences inside nested loops, counts inside counts, deep nesting, long
alternations, lookbehinds over a long string, case-insensitive backreferences to wide groups, large property
classes, choices that read no character, and repetitions that recurse. Before the limits, with a heap of 1 GB,
93 of them had not answered after 20 seconds, 64 threw `StackOverflowError`, and one took
the whole heap while the schema was compiled. With the limits, on Java 21, with a heap of 256 MB, on a machine with a
load of 5 to 18 from other work:

| | Compiled | Interpreter |
|---|---|---|
| Cases that end in an answer | 134 | 134 |
| Cases that end in `PatternMatchLimitException` | 132 | 132 |
| Cases refused as too large when the schema is compiled | 19 | 19 |
| Cases refused for another reason (not valid, or the lookbehind exception) | 2 | 2 |
| Longest translation of a pattern, and compilation by `java.util.regex` | 464 ms, 27 ms | 501 ms, 37 ms |
| Longest match, which is the limit and what it overran by | 1,046 ms | 1,043 ms |
| Most memory the process held | 266 MB | 263 MB |

No case ran longer, threw anything else, or ran out of memory.

**The cache of compiled patterns.** A pattern is immutable, so schemas with the same pattern share what was
compiled for it, through a cache of the process. A schema may come from outside, so the cache is bounded. It holds
1,024 patterns at most, counted as 64 MiB at most. Over either bound the patterns compiled first go, oldest first. A
pattern counted as more than 2 MiB is not kept in it. These are the bounds of the Go module and the Rust crate. A
validator holds the patterns of its own schema, so a pattern that has left the cache is still matched, and is
compiled again only for a schema compiled later. The cache is read when a schema is compiled and never when an
instance is validated.

A pattern is counted as an estimate of what it holds that is not below it: 512 bytes, 2 for each character of its
source, and 130 for each character of its translation. `java.util.regex` keeps a class of Latin-1 characters as a
table of 256 bytes however short the class is written, which the 130 allows for. Of 59 shapes of pattern compiled on
Java 17, 21 and 25, none held more than a third of what it was counted as. So a pattern whose translation is over
16,131 characters is not kept in the cache, and 64 MiB counted is about 20 MB on the heap or less.

An evaluator keeps a matcher of `java.util.regex` for each pattern of its schema that the engine runs, at a place
the compiler of the schema gives the pattern. So the array of matchers of an evaluator is as long as its own schema
has such patterns, and none for a schema whose patterns the library matches by itself.

A process that compiled 60,000 schemas one after another on one thread, each with two patterns of its own, validated
with each and dropped it, ran in a heap of 96 MB. At each tenth of the way the heap in use after a collection was
between 3 and 7 MB with compiled code and between 6 and 10 MB with the interpreter. Before the cache was bounded it
was 91 MB after 42,000 schemas with compiled code and 81 MB after 30,000 with the interpreter, and both ran out of
heap before the next tenth.

**What is not bounded.**

- **The stack.** `java.util.regex` recurses to compile a pattern and to match it, and has no limit of depth but the
  stack of the thread. The library does not make that impossible. It makes the outcome one of its two errors:
  `PatternTooLargeException` when the schema is compiled, `PatternMatchLimitException` when an instance is
  validated. How long a string a recursing pattern takes depends on the stack size of the thread (`-Xss`, or the
  size given to the `Thread`).
- **The memory of a match.** The limit bounds the time of a match and not what it keeps. `java.util.regex` keeps
  its state on the stack, and for some repetitions the places they failed at, one number for each place of the
  string. In the probe no process held more than the figure above.
- **What a compiled pattern keeps.** `java.util.regex` keeps between 2 and 28 bytes for each character of a
  translation: 2 MB for 1,048,576 characters of literal text, and 30 MB for 116,508 repetitions of a small group in
  as many characters. So the limit on the translation holds a pattern to tens of megabytes and not to a few.
  The cache of the process is bounded (see above), and a validator keeps the patterns of its own schema for as long
  as it lives.
- **The start of a match.** The clock is first read after the first 4,096 reads (fewer for a long translation),
  and the limit runs from there.
- **The formats.** The library's own regular expressions for `format` are not under the limit.

**Slow patterns.** `java.util.regex` matches by backtracking, and the translation finds some places by search, so
a pattern that is deliberately hard can take a second where V8 answers in milliseconds. Of the 98,839 patterns of the
test data, six go over the limit of 1 second for some of their texts, on every JDK and in both modes.
Each nests repetitions whose bodies can match nothing around groups that are read, or holds a lookbehind that is
searched for, and each gave V8's answers when it was run with no limit:

- `^(?=((?:(?:(|a)(?:(?:(?:(a|)){1,3}?(?:.?)??))*?(?:.?)+))*[ax]))\1$` (`engine-faults`)
- `((?!\Bx??|)-??|-(?:a|(?:a*|[ax]){2,}?){2}|(.(?<!a)|\B|($){3,}(\B|(?!$)){2,}?){4,}a*?){4}x` (`empty-loops`)
- `^((?<n0>\k<n0>?|(?:\1?(|a)\k<n0>|()(x)*){0,2}){1,2}?((?:a){0,0}?^x{1,2})?|(a|)){1,}?\4\3-$` (`backreferences`)
- `^S|^((?:(?!.[\w\u017f\u212a\u0130]|\u{10400})(..ǆ)*?(?<n0>\2?.|-\k<n0>+\u1fd3)??)*)+(?:\u01c5.)+?$` (`case-folding`)
- `^(?:((?!|-$)|(?=).|(?<=[^a])){2}){0}(?:\1|(?:(?<!^)(?:[^a]*\b|[^a]){2,3}|[^a]){3,4}(?:(|)|)*){3}a$` (`empty-loops`)
- `^(x){0}\1(?<n0>(\3{2}){0,2}\2(?:(?!$)(\k<n0>\1)?){3,}|(?:(?<n1>(?<=x)[^a])|(?<n1>\6+?)){2,}(?:(?<n2>\2(?:(?<n3>a(x)*)|(?<n3>\7?a)){3,}?\3{2})|(?<n2>[^a]-))+|(?:(?:x){2,4}|\11*){1,3}\11{2}x){3,}?x$` (`backreferences`)

One more went over the limit in some of the four runs and not in others, on a busy machine, as a time
limit will have it. Earlier versions of this page listed ten slow patterns. One of them, which is in two of the
files, is now fast, and so is `^(?:(?:a|)*?x)*?$`, which read a string of 41 characters nine million times: a
repetition that can run no times is written without the empty alternatives of its body, which ECMA-262 never takes
there. Another of the ten is now over a limit of size.

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
