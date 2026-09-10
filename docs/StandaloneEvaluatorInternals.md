# Schema Evaluation Program Internals

This document describes how generated types and standalone evaluators validate instances since the
per-type validation emitters were retired. The evaluation engine itself is described in
[RuntimeEvaluator.md](RuntimeEvaluator.md).

## Overview

The V5 generator no longer emits validation code. Every generated type's `EvaluateSchema` and every
standalone evaluator's `Evaluate` call into a single **schema evaluation program** emitted once per
compilation, which the `Corvus.Text.Json.RuntimeEvaluator` compiles into an in-memory node graph on first
use. The typed model (accessors, conversions, builders, mutable documents, enums, constants) is unchanged;
only the `JsonSchema` nested class of each type shrank to an entry-point reference and a one-line
`Evaluate`.

This gives generated types the runtime evaluator's throughput (several times faster than the retired
emitted validators on the Sourcemeta corpora), full annotation collection in verbose mode for typed
models as well as standalone evaluators, and it removes roughly fourteen thousand lines of emitter code
from the generator.

## What is emitted

Per compilation, `RuntimeProgramGenerator` (`src/Corvus.Text.Json.CodeGeneration/RuntimeProgramGenerator.cs`)
emits one `CorvusJsonSchemaProgram` class in the default namespace:

- **Program image or documents.** When the generator host can compile ahead of time (the CLI, through
  `CSharpLanguageProvider.Options.ProgramCompiler`), the class carries the compiled program image as base64
  UTF-8 literals, decoded and loaded with `JsonSchemaEvaluator.FromProgramImage` on first use, plus one
  `[GeneratedRegex]` method per pattern the image needs, wired in through `RegexProvider` (on .NET 8 and
  later; elsewhere the evaluator constructs the expression). A host without the evaluator (the Roslyn
  source generator today) emits the documents instead: every root schema document the type builder loaded,
  as UTF-8 static data (`"..."u8`). File-system documents are keyed under the synthetic `corvus-schema:///` scheme, relative
  to the common directory of all file documents, so no build-machine path reaches the generated code;
  documents with an absolute `$id` are keyed by that `$id`. Relative `$ref`s between documents resolve
  within the same scheme, so the keys are consistent.
- **Entry points.** One `ForEntryPoint("<document key>#<pointer>")` per generated type (its
  `SchemaDocument` and `SchemaLocation`, which are still emitted as constants) and per standalone
  evaluator root. Entry points share one compiled program, so each subschema is compiled once even when
  many types reference it.
- **Options.** The dialect for documents without `$schema` (from the fallback vocabulary) and whether
  `format` is asserted (`CorvusTextJsonAlwaysAssertFormat`).

Per type, the `JsonSchema` class holds:

```csharp
private static readonly JsonSchemaEvaluator Evaluator = CorvusJsonSchemaProgram.Entry(9);

internal static bool Evaluate(IJsonDocument parentDocument, int parentIndex, IJsonSchemaResultsCollector? resultsCollector = null)
{
    return Evaluator.Evaluate(parentDocument, parentIndex, resultsCollector);
}
```

A standalone evaluator (`EmitEvaluator = true`, or the `SchemaEvaluationOnly`/`Both` generation modes)
is a `<Name>Evaluator` class with the same public `Evaluate<TElement>(in instance, collector)` entry
point as before, plus an `Evaluate(IJsonDocument, int, collector)` overload, over the root's entry
point.

## Where the pieces live

| Concern | Location |
|---|---|
| Program and evaluator shim emission | `src/Corvus.Text.Json.CodeGeneration/RuntimeProgramGenerator.cs` |
| Entry-point registration, document hand-off | `CSharpLanguageProvider.GetProgramEntry`, `ISchemaProgramLanguageProvider` |
| Document collection at generation time | `JsonSchemaTypeBuilder.GetSchemaDocuments` (every registered `LocatedSchema`'s root document) |
| Per-type shim | `CodeGeneratorExtensions.JsonSchema.cs`, `AppendRuntimeProgramEvaluateMethod` |
| Schema compilation and evaluation | `src/Corvus.Text.Json.RuntimeEvaluator` ([design notes](RuntimeEvaluator.md)) |

## Consumer requirements

Generated code references `Corvus.Text.Json.RuntimeEvaluator`, so a project that uses the source
generator or CLI output must reference that package alongside `Corvus.Text.Json`. Nothing else changes:
`EvaluateSchema`, the results collector levels, and `JsonSchemaAnnotationProducer` behave as before.

## Roadmap

Programs emitted by the CLI are pre-compiled (see [RuntimeEvaluatorPrecompilation.md](RuntimeEvaluatorPrecompilation.md)
for the design and measurements); programs emitted by the Roslyn source generator are still compiled from
the embedded documents on first use, until the schema compiler and its document model are linked into the
generator. Emitting per-node C# was measured and rejected: the fused plans in the compiler reach the same
throughput without generated evaluation code.
