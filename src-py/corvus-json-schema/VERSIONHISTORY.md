# Version History

The version history of `corvus-json-schema`, the Python port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.0

The first release of the Python port of `Corvus.Text.Json.RuntimeEvaluator`, by way of its TypeScript port.

### New features

- **A JSON Schema evaluator for Python.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,967 tests of the JSON-Schema-Test-Suite, including `draft4/optional/zeroTerminatedFloats.json`, which the C# and TypeScript runners exclude. A schema is compiled once into specialised Python, one function per schema node with leaf schemas tested inline, that validates the values `json.loads` produces. It is pure Python, with the `regex` package for the Unicode properties of ECMA-262 patterns.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.

- **Standalone modules.** A schema can be emitted as a Python module that depends only on the package's small runtime. The `corvus-json-schema` command line generates modules and validates instances.
