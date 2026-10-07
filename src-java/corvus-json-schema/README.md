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
  <version>0.1.0</version>
</dependency>
```

```kotlin
implementation("io.github.corvus-dotnet:corvus-json-schema:0.1.0")
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
  regular expression engine; other patterns are translated from ECMA-262 to `java.util.regex`;
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
- `MetaschemasTest`: the embedded metaschemas match `src/Corvus.Text.Json/metaschema`.

## License

Apache 2.0.
