# Version History

The version history of `corvus-json-schema` for Object Pascal (`src-pas/corvus-json-schema`), the Object Pascal port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.0

The first release of the Object Pascal port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Object Pascal.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It is a native port of the Go module and has no dependencies beyond the compiler's run-time library. Free Pascal 3.2.2 or later is the compiler it is built and tested with. The source is in the Delphi dialect and is written to be Delphi-compatible, but it has not been compiled with Delphi.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through an engine in the package that agrees with V8 on 61,383 patterns and 1,057,650 match answers. A pattern runs on an automaton over ASCII text where it allows one, and on a backtracking matcher otherwise. The backtracking matcher has no step budget, so a pattern with nested quantifiers can take exponential time on a text it does not match.

- **The same results whichever compiler builds it.** The package reads Unicode properties and case mappings from its own Unicode 17 tables and never from the compiler's run-time library.

- **Every array read is range checked.** Range checking is on in every unit. On the paths every validation takes the check is one inline comparison, written out in accessor functions that raise `ERangeError` as the compiler's check does.

- **Allocation-free validation.** Validating a parsed `TJsonDocument`, or JSON text through the thread's reused buffers, allocates nothing in the steady state. A thread that has validated should call `JsonSchemaReleaseThreadScratch` before it ends.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them. The rows are the same, byte for byte, as the Go module's for every case of the JSON-Schema-Test-Suite.
