---
name: corvus-java-evaluator
description: >
  Work on the Java port of the V5 standalone schema evaluator (src-java/corvus-json-schema, Maven
  artifact io.github.corvus-dotnet:corvus-json-schema): loader, compiler, the ASM bytecode generator,
  results collector and annotations, the allocation tests, the conformance and annotation suites,
  the Kotlin smoke test, the jsonschema-benchmark and Bowtie integrations, and performance measurement.
  USE FOR: changing or debugging the Java evaluator, measuring it against Blaze and the other Corvus
  evaluators, running its Bowtie harness, releasing it to Maven Central, keeping it in parity with
  the C# runtime evaluator.
  DO NOT USE FOR: the C# runtime evaluator itself (see corvus-standalone-evaluator and
  docs/RuntimeEvaluator.md), the TypeScript port (corvus-typescript-evaluator).
---

# Java Standalone Evaluator

## Overview

`src-java/corvus-json-schema` ports the `Corvus.Text.Json.RuntimeEvaluator` pipeline to Java 17, by way of the Rust and
TypeScript ports. `SchemaLoader` and `SchemaCompiler` build the node graph (`SchemaNode`) and its analyses (marking,
in-place cycles, discriminators). `CodeGen` then generates one static method per node into a hidden class, with ASM
shaded into the jar under `io.github.corvusdotnet.jsonschema.internal.asm`; `Evaluator` interprets the same graph for
results collection, and for nodes that are not compiled. `JsonDocument` is the instance model: the UTF-8 text plus a
flat `long[]` tape, two longs per value. The package README describes the design; `OPTIMIZATIONS.md` maps every
optimisation to its counterpart in the C#, Rust and TypeScript evaluators. Keep it current: a technique added to any of
the other evaluators should be checked off (or ruled out, with the reason) there.

## Build and test

Java 17 or later; the wrapper fetches Maven. From `src-java/corvus-json-schema`:

```bash
./mvnw test                     # every test, then AllocationTest again without escape analysis
./mvnw test -Dtest=SuiteTest -DcorvusArgLine=-Dcorvus.jsonschema.interpret=true   # the suite on the interpreter
./mvnw install -P release -Dgpg.skip   # as CI does: adds the sources and javadoc jars (doclint fails on warnings)
```

The suites read the repository's `JSON-Schema-Test-Suite` submodule. Every required, optional and format test must
pass (`draft4/optional/zeroTerminatedFloats.json` is excluded, as in the C# runner), and all annotation assertions.
`SuiteTest` also asserts that every suite schema compiles to bytecode; a node that falls back to the interpreter is a
performance bug, so don't relax that count.

`MetaschemasTest` checks the embedded metaschemas (`src/main/resources`) against `src/Corvus.Text.Json/metaschema`.

`src-java/kotlin-smoke` uses the installed jar from Kotlin (`./mvnw test` there after `install`), and
`src-java/corvus-json-schema-bowtie` runs the Bowtie harness over IHOP on the required and annotation suites
(`./mvnw package`). CI (`.github/workflows/java.yml`) runs all of these on Linux x64 and arm64, Windows and macOS, with
Java 17, 21 and 25.

## Zero allocation

Validation must allocate nothing in the steady state, compiled or interpreted, from a `JsonDocument`, a `String` or a
`byte[]`. `AllocationTest` measures every keyword path with `ThreadMXBean.getCurrentThreadAllocatedBytes`, and runs
again with `-XX:-DoEscapeAnalysis` so that a path that is only allocation-free after C2 scalar replacement fails. Add a
case for every new keyword path. Scratch space comes from the `Evaluator` (its arena and buffers, found through the
validator's owner-thread cache and then a `ThreadLocal`); formats read the UTF-8 bytes through `Utf8Chars` with reused
matchers (`Formats.Context`).

## Generated code

- HotSpot never JIT-compiles a method of more than 8000 bytes of bytecode, so one runs interpreted, often 10 times
  slower. `CodeGen` reads each method's size from the class file and regenerates an oversized node in its compact form
  (hashed name dispatch), or leaves it to the interpreter. Keep that guard in mind when generating more code inline.
- To see what was generated, set `-Dcorvus.jsonschema.dump=<dir>` and disassemble the class with `javap -c -p`.
- Constants reach the generated code as class data (`MethodHandles.classDataAt`, through a dynamic constant whose name
  must be `_`).

## Performance work

- In process: `src-java/corvus-json-schema-bench` (`Compare`, see its README) runs the jsonschema-benchmark corpora with
  corvus, networknt and kmp. Profile with async-profiler (`-agentpath:.../libasyncProfiler.so=start,event=cpu,...`).
- Against Blaze, Corvus .NET and Corvus Rust: build the images and run `Compare-Images.ps1` (see the bench README),
  pinned with `-CpuSet`. Every harness must warm up by time (2 seconds, at least 100 passes) and report the last
  warm-up pass, timed inside the loop; a pass timed after the loop can run code that was only compiled inlined into
  the loop's on-stack-replacement compilation, and read several times too slow on the JVM. Compare like with like.
- Measure before and after on the same machine, interleaved.

## Parity with the C# evaluator

When the C# collecting mode changes (paths, messages, row order, which subschema results are kept), update
`Evaluator`'s collecting paths and `ResultsTest`, which reproduces the C# `ResultsTests`/`ResultPathTests`
expectations. Format recognition follows `SchemaCompiler.GetFormatKind` (`Formats.Kind`). Patterns are translated from
ECMA-262 by `EcmaRegex`, as the .NET evaluator's translator does (see corvus-ecma-regex).

## Releasing to Maven Central

Versioned independently, with its history in `src-java/corvus-json-schema/VERSIONHISTORY.md`. Bump `<version>` in
`pom.xml` and `corvus.version` in `src-java/kotlin-smoke/pom.xml`, add the history entry, and merge;
`.github/workflows/maven-publish.yml` publishes and tags `java-v<version>`. See `docs/ReleaseProcess.md`, "The Java
library". Never push a `java-v` tag by hand.

## Integrations

- `src-java/corvus-json-schema-bench/jsonschema-benchmark/`: the `implementations/corvus-java` directory for
  jsonschema-benchmark.
- `src-java/corvus-json-schema-bowtie/`: the Bowtie harness (`BowtieHarness`) and its image.
- Docs: `docs/JsonSchemaForJava.md`, its row in `docs/OtherLanguages.md`, and the home page's language list.
