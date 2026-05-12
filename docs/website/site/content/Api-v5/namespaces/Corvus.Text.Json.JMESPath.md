JMESPath query language evaluator. Provides full JMESPath expression evaluation with 100% conformance to the official test suite, supporting projections, filtering, slicing, multi-select, pipe expressions, and all built-in functions.

The key type is [`JMESPathEvaluator`](/api/v5/corvus-text-json-jmespath-jmespathevaluator.html), which provides `Search(expression, data)` for evaluating expressions against JSON data. The `Default` singleton caches compiled expressions for efficient repeated evaluation.
