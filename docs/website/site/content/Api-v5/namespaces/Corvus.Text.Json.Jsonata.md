JSONata query and transformation language evaluator. Provides full JSONata expression evaluation with 100% test-suite conformance, custom variable bindings, time-limited evaluation, and both string and `JsonElement` result modes.

Key types include [`JsonataEvaluator`](/api/v5/corvus-text-json-jsonata-jsonataevaluator.html) (the main entry point, with `Evaluate` and `EvaluateToString` methods), and [`JsonataBinding`](/api/v5/corvus-text-json-jsonata-jsonatabinding.html) (for passing external values into expressions as named variables).
