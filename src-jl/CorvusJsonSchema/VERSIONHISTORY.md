# Version History

The version history of `CorvusJsonSchema`, the Julia port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.0

The first release of the Julia port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Julia.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It needs Julia 1.10 or later and has no dependencies.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **Ready when loaded.** One interpreter runs the plans, and a precompile workload puts it in the package image. Nothing is generated or compiled when a schema is compiled, so the first validation in a process does not wait for Julia's compiler.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through a translator in the package that writes each pattern for the PCRE2 that Julia bundles. A pattern that cannot be written with exactly the same meaning is refused when the schema is compiled, with a `CompileError` that names the construct. A match that reaches a PCRE2 limit throws and is never reported as no match.

- **The same results with every Julia release.** The package reads Unicode properties and case mappings from its own Unicode 17 tables and never from Julia or its PCRE2, whose Unicode versions differ between Julia releases. Patterns and the `hostname`, `idn-hostname` and `idn-email` formats give the same answers with Julia 1.10 and 1.13.

- **Allocation-free validation.** Validating a parsed `Document`, or JSON text through the validator's reused buffers, allocates nothing in the steady state.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
