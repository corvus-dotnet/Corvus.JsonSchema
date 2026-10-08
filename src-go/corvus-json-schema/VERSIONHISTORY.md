# Version History

The version history of `github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema`, the Go port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.0

The first release of the Go port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Go.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It needs Go 1.25 or later and has no dependencies outside the standard library.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through an engine in the module that translates to the standard library's `regexp` where the two agree and backtracks otherwise.

- **The same results with every Go release.** The module reads Unicode properties and case mappings from its own Unicode 17 tables and never from the toolchain, whose Unicode version differs between Go releases. Patterns and the `hostname`, `idn-hostname` and `idn-email` formats give the same answers with Go 1.25, 1.26 and 1.27.

- **Allocation-free validation.** Validating a parsed `Document`, or JSON text through the validator's reused buffers, allocates nothing in the steady state.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
