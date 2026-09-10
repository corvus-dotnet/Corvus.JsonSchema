# Validation Handler Guide

> **Retired.** The validation handlers (`ValidationHandlers/**` in `Corvus.Text.Json.CodeGeneration`) and
> the standalone evaluator generator no longer exist. Generated types and standalone evaluators validate
> through the schema evaluation program described in
> [StandaloneEvaluatorInternals.md](StandaloneEvaluatorInternals.md), which is compiled and run by
> `Corvus.Text.Json.RuntimeEvaluator`.

## Where keyword semantics live now

| Concern | Location |
|---|---|
| Schema loading, `$id`/anchor/`$ref`/`$dynamicRef` resolution, dialect and vocabulary detection | `src/Corvus.Text.Json.RuntimeEvaluator/Compilation/SchemaLoader.cs` |
| Keyword compilation into the node graph (one `SchemaNode` per subschema), whole-graph passes | `src/Corvus.Text.Json.RuntimeEvaluator/Compilation/SchemaCompiler.cs` |
| Node data (type masks, bounds, property maps, discriminators, plans, flags) | `src/Corvus.Text.Json.RuntimeEvaluator/Compilation/SchemaNode.cs` |
| Evaluation (flag mode and collecting mode, fused per-node plans) | `src/Corvus.Text.Json.RuntimeEvaluator/Evaluation/Evaluator.cs` |
| Format, number, string and equality helpers shared with the core library | `Corvus.Text.Json.Internal.JsonSchemaEvaluation`, `JsonElementHelpers` |

The design, the optimisation table and the results-collector conventions are in
[RuntimeEvaluator.md](RuntimeEvaluator.md).

## Adding or changing a keyword

1. **Analysis side.** Keywords are still declared for the type builder (`IKeyword` implementations in the
   vocabulary projects) so that generated *types* reflect them; see [AddingKeywords.md](AddingKeywords.md).
2. **Evaluation side.** Add the keyword to `SchemaCompiler.CompileNode` (dispatch is by keyword name
   length, then bytes), store what the evaluator needs on `SchemaNode`, and implement the assertion in
   `Evaluator` for both `FastMode` and `CollectingMode`. Report results through the collector with the
   keyword name so that results and annotations match the generated-model conventions.
3. **Tests.** Add a unit test under `tests/Corvus.Text.Json.RuntimeEvaluator.Tests/Unit` and, if the
   keyword is in the JSON Schema Test Suite, confirm the suite tests there and in the generated suite
   projects (`tests/Corvus.Text.Json.SchemaTestSuite.Tests`, `tests/Corvus.Text.Json.EvaluatorTestSuite.Tests`)
   pass; all three run the same engine.
