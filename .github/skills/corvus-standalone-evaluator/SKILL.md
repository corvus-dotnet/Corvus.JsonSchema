---
name: corvus-standalone-evaluator
description: >
  Generate and use standalone schema evaluators (validation and annotation collection without
  full type generation), and understand the schema evaluation program that every generated
  type validates through: what RuntimeProgramGenerator emits, entry points, document keys,
  consumer package requirements, and the annotation pipeline.
  USE FOR: generating evaluator-only code, collecting annotations from schema validation,
  understanding how generated validation reaches the runtime evaluator, debugging
  validation behavior of generated types.
  DO NOT USE FOR: full type generation (use corvus-codegen), keyword semantics
  (use corvus-keywords-and-validation), the evaluator engine itself (docs/RuntimeEvaluator.md).
---

# Standalone Schema Evaluator and the Evaluation Program

## Overview

Generated types and standalone evaluators no longer contain validation code. The generator emits one
`CorvusJsonSchemaProgram` class per compilation holding the schema documents as UTF-8 static data and
one entry point per generated type and evaluator root; `Corvus.Text.Json.RuntimeEvaluator` compiles
the program on first use and every `EvaluateSchema()` / `Evaluate()` call runs against that graph.

## Generating an evaluator

```csharp
[JsonSchemaTypeGenerator("schema.json", EmitEvaluator = true)]
public partial struct MySchema;
```

```powershell
corvusjson jsonschema schema.json --codeGenerationMode SchemaEvaluationOnly --outputPath ./Evaluator
corvusjson jsonschema schema.json --codeGenerationMode Both --outputPath ./Output
```

The emitted `MySchemaEvaluator` exposes `Evaluate<TElement>(in instance, collector)` and
`Evaluate(IJsonDocument, int, collector)`. Consumers must reference `Corvus.Text.Json.RuntimeEvaluator`
as well as `Corvus.Text.Json`.

## Using it

```csharp
using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
bool valid = MySchemaEvaluator.Evaluate(in element, collector);
foreach (var annotation in JsonSchemaAnnotationProducer.EnumerateAnnotations(collector)) { ... }
```

Flag mode (no collector) fails fast and allocates nothing; Basic/Detailed record failures; Verbose
records every keyword and the annotations.

## Emitted shape

- `RuntimeProgramGenerator.Generate` — the program: documents (file documents keyed under
  `corvus-schema:///<relative path>`, absolute `$id`s verbatim), `TryGetDocument` resolver,
  `Create()` compiling the root with `CompileFromUri` and `ForEntryPoint(...)` per entry, dialect and
  `AssertFormat` options.
- `RuntimeProgramGenerator.GenerateStandaloneEvaluator` — the evaluator shim over one entry.
- `CodeGeneratorExtensions.JsonSchema.cs` `AppendRuntimeProgramEvaluateMethod` — the per-type shim:
  `private static readonly JsonSchemaEvaluator Evaluator = CorvusJsonSchemaProgram.Entry(n);`.
- `CSharpLanguageProvider.GetProgramEntry` assigns entry indices; `ISchemaProgramLanguageProvider`
  receives the documents from `JsonSchemaTypeBuilder.GetSchemaDocuments()`.

## Debugging

- Dump the compiled graph for a schema with the benchmark harness:
  `dotnet run -c Release --project benchmarks/Corvus.Text.Json.RuntimeEvaluator.Benchmarks -- dump <schema.json>`.
- `SchemaLocation`/`SchemaDocument` constants on each type still identify its subschema; the entry
  point string in the program is `<document key>#<SchemaLocation>`.
- The same engine runs in `tests/Corvus.Text.Json.RuntimeEvaluator.Tests`; a failing generated-type
  test usually reproduces there with `JsonSchemaEvaluator.Compile(schema, "#<pointer>")`.

## Cross-References

- `docs/StandaloneEvaluatorInternals.md`, `docs/SchemaEvaluator.md`, `docs/AnnotationSystem.md`
- `docs/RuntimeEvaluator.md` — engine design and optimisations
