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
  later; elsewhere the evaluator constructs the expression). The Roslyn source generator emits the same
  shape: it links the evaluator's compiler and image writer in as source, built over `System.Text.Json`. A
  host without a program compiler emits the documents instead: every root schema document the type builder loaded,
  as UTF-8 static data (`"..."u8`). File-system documents are keyed under the synthetic `corvus-schema:///` scheme, relative
  to the common directory of all file documents, so no build-machine path reaches the generated code;
  documents with an absolute `$id` are keyed by that `$id`. Relative `$ref`s between documents resolve
  within the same scheme, so the keys are consistent.
- **Regular expression match timeout.** Every expression has a match timeout of 1 second
  (`CodeGenerationExtensions.RegexMatchTimeoutMilliseconds`, the one place the generator holds the value). Each
  `[GeneratedRegex]` of the program carries `matchTimeoutMilliseconds: 1000`, and the options the program is
  loaded with set `RegexMatchTimeout` to the same value, for the expressions the evaluator constructs (all of
  them where there is no regex generator, and all of them in a program from the Roslyn source generator). The
  expressions a generated type holds for its own helpers (`[GeneratedRegex(pattern, RegexOptions.None,
  matchTimeoutMilliseconds: 1000)]`, or `new Regex(pattern, RegexOptions.Compiled, TimeSpan)` where there is no
  regex generator) have it too. A match that runs for longer throws `RegexMatchTimeoutException` to the caller.
  A pattern that `EcmaRegexTranslator.IsLinear` proves to match in time proportional to the text has no timeout in
  any of these forms (`matchTimeoutMilliseconds: global::System.Threading.Timeout.Infinite`, or
  `Regex.InfiniteMatchTimeout`), as in the evaluator, which applies the same proof to the expressions it constructs.
  The program generator reads the patterns for it from `SchemaProgramImage.EcmaPatterns`, and an image made without
  them gives every pattern the timeout.
- **Patterns.** Every pattern is the translation `EcmaRegexTranslator` writes for it
  ([EcmaRegexTranslator.md](EcmaRegexTranslator.md)), in the program and in the generated types alike. A pattern
  that is not a valid ECMA-262 regular expression stops the generation: the emitter reads every `pattern` and
  `patternProperties` pattern of a type (`ThrowIfAPatternIsNotValid`), and compiling the program image refuses the
  schema too. An expression of a generated type is constructed with `RegexOptions.CultureInvariant` when its
  translation holds a case-insensitive backreference, the one construct that uses .NET's own case comparison.
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
| Schema compilation and evaluation | `src/Corvus.Text.Json/Corvus/Text/Json/RuntimeEvaluator` in `Corvus.Text.Json` ([design notes](RuntimeEvaluator.md)) |

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
