# Version History

The version history of `@corvus-dotnet/json-schema`, the TypeScript port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.1

V0.1.1 makes the generated validators faster on schemas that compose objects with `allOf` and `$ref`, and on schemas whose `unevaluatedProperties` depends on `if`/`then`/`else` or `dependentSchemas`. There are no API changes and no changes to results.

### Other changes

- **Plain object compositions are checked in one pass.** A schema made of `allOf` and `$ref` branches that are plain object schemas (declared properties, `required` and count bounds, with each property name resolving to one schema) used to generate a call per branch, each re-testing that the value is an object and checking its own properties. The generated code now checks an object in one section over the merged property names, as the C# and Rust evaluators' flat fused plan does. On the jsonschema-benchmark corpora this takes about 30 percent off jasmine, about 15 percent off cypress and stale, and about 10 percent off babelrc. See [#986](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/986).

- **Conditional `unevaluatedProperties` needs no run-time tracking.** When an `if`/`then`/`else` branch or a dependency's schema could add evaluated properties, the generated code tracked evaluated properties in a `Set` per object, filled by every contributor. Those contributions now keep their conditions: the code decides each condition once per object (the `if` schema against the instance, or the dependency's property being present) and accepts a property that the unconditional properties, or a branch whose condition holds, names. Contributions under `anyOf`, `oneOf` or `$dynamicRef` still use tracking when they add properties. openapi's generated code no longer creates any of these sets, and validates about 10 percent faster. See [#986](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/986).

## V0.1.0

The first release of the TypeScript port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for JavaScript and TypeScript.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. A schema is compiled once into specialised JavaScript, one function per schema node, that validates parsed JSON values. See [#977](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/977).

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them. See [#977](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/977).

- **Standalone modules.** A schema can be emitted as an ES module that depends only on the package's small runtime, for environments that do not allow evaluating generated code at run time. The `corvus-json-schema` command line generates modules and validates instances. See [#977](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/977).

- **Faster compilation.** Schema compilation was made faster before the first release. See [#980](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/980).
