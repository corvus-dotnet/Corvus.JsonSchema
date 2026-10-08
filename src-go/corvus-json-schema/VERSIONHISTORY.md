# Version History

The version history of `github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema`, the Go port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.1

V0.1.1 fixes a panic when compiling a schema with one form of pattern. There are no API changes. Version 0.1.0 is affected.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The module matches this form without the pattern engine and keeps the excluded set as one bit for each ASCII character, in two 64-bit words. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time as a bit number of 128 or more, which indexed past the two words. `Compile` panicked with an index out of range and returned no error, for a schema that is valid. Such a pattern is now matched by the pattern engine, and `Compile` succeeds. A pattern of this form whose excluded set is all ASCII was never affected.

## V0.1.0

The first release of the Go port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Go.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It needs Go 1.25 or later and has no dependencies outside the standard library.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through an engine in the module that translates to the standard library's `regexp` where the two agree and backtracks otherwise.

- **The same results with every Go release.** The module reads Unicode properties and case mappings from its own Unicode 17 tables and never from the toolchain, whose Unicode version differs between Go releases. Patterns and the `hostname`, `idn-hostname` and `idn-email` formats give the same answers with Go 1.25, 1.26 and 1.27.

- **Allocation-free validation.** Validating a parsed `Document`, or JSON text through the validator's reused buffers, allocates nothing in the steady state.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
