# Version History

The version history of `corvus-json-schema` for OCaml (`src-ml/corvus-json-schema`), the OCaml port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the Corvus.JsonSchema repository's [VERSIONHISTORY.md](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/VERSIONHISTORY.md).

## V0.1.0

The first release of the OCaml port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for OCaml.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It is a native port of the Go module, with what Go takes from its standard library written as the Object Pascal port has it. It needs OCaml 4.14 or later, is tested with 4.14 and 5.5, and has no dependencies beyond the OCaml standard library.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and machine words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through an engine in the package that agrees with V8 on 61,383 patterns and 1,057,650 match answers. A pattern runs on an automaton over ASCII text where it allows one, and on a backtracking matcher otherwise. The backtracking matcher has no step budget, so a pattern with nested quantifiers can take exponential time on a text it does not match.

- **The same results whichever compiler builds it.** The package reads Unicode properties and case mappings from its own Unicode 17 tables and never from anywhere else.

- **Every array read is bounds checked.** The package is built with the compiler's bounds checks on and uses no unchecked read, no `Obj` and no C code.

- **Allocation-free validation.** Validating a parsed `Document.t`, or JSON text through reused buffers, allocates nothing on the OCaml heap in the steady state. A validator is safe to share between threads and domains, and a caller that validates from several at once gives each its own `Scratch.t`.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
