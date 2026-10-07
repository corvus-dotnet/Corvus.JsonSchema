# Version History

The version history of `io.github.corvus-dotnet:corvus-json-schema`, the Java port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.0

The first release of the Java port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Java and Kotlin.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It runs on Java 17 or later, is one jar with no dependencies, and can be used from Kotlin as it is.

- **Compiled to JVM bytecode.** A schema is compiled once into a hidden class, one method per schema node holding only the checks that node needs, which the JIT then optimises as it would hand-written code. Property names are dispatched by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **Allocation-free validation.** Validating a parsed `JsonDocument`, or JSON text through the validator's reused per-thread buffers, allocates nothing in the steady state.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
