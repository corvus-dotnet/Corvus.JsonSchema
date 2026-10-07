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
  <version>0.1.0</version>
</dependency>
```

Gradle (Kotlin DSL):

```kotlin
implementation("io.github.corvus-dotnet:corvus-json-schema:0.1.0")
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
