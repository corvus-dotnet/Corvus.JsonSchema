# JSON Schema Patterns in .NET - JsonLogic Rule Evaluation

This recipe demonstrates how to evaluate [JsonLogic](https://jsonlogic.com/) rules against JSON data using the `Corvus.Text.Json.JsonLogic` library. It covers the string-based convenience API, typed evaluation with workspaces, data access operators, array operations, custom operators, and build-time rule compilation via the source generator.

## The Pattern

JsonLogic is a standard format for expressing business rules as JSON objects. Each rule is a JSON object whose single key is the operator name and whose value is the array of arguments. Literal values (strings, numbers, booleans, arrays) pass through unchanged.

In Corvus.Text.Json.JsonLogic, rules are compiled to delegate trees on first use and cached for subsequent evaluations. The evaluator is thread-safe and designed for high-throughput scenarios.

## Evaluating Rules

### EvaluateToString — the simplest API

For quick evaluations where both input and output are JSON strings:

```csharp
string? result = JsonLogicEvaluator.Default.EvaluateToString(
    """{"if": [{">=": [{"var": "order.total"}, 100]}, "VIP", "Standard"]}""",
    """{"order": {"customer": "Alice", "total": 150}}""");
// result: "\"VIP\""
```

### Evaluate with parsed documents

For zero-allocation evaluation, parse the rule and data into `JsonElement` values and provide a `JsonWorkspace`:

```csharp
using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"*": [{"var": "order.total"}, 0.9]}""");

using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(orderJson);
using JsonWorkspace workspace = JsonWorkspace.Create();

JsonLogicRule rule = new(ruleDoc.RootElement);
JsonElement result = JsonLogicEvaluator.Default.Evaluate(in rule, dataDoc.RootElement, workspace);
```

The `JsonWorkspace` overload avoids cloning the result, so the returned `JsonElement` remains valid only while the workspace is alive.

## Data Access Operators

| Operator | Description |
|---|---|
| `{"var": "path.to.field"}` | Read a value from the data using dot-notation |
| `{"var": ["path", default]}` | Read with a default value if the field is missing |
| `{"missing": ["a", "b", "c"]}` | Returns an array of which listed fields are missing |

```csharp
// Nested access
JsonLogicEvaluator.Default.EvaluateToString("""{"var": "order.customer"}""", data);
// "Alice"

// Default value
JsonLogicEvaluator.Default.EvaluateToString("""{"var": ["order.discount", 0]}""", data);
// 0

// Find missing fields
JsonLogicEvaluator.Default.EvaluateToString(
    """{"missing": ["order.customer", "order.discount"]}""", data);
// ["order.discount"]
```

## Array Operations

JsonLogic supports `filter`, `map`, `reduce`, `all`, `some`, and `none` for processing arrays:

```csharp
// Filter: in-stock items over $50
JsonLogicEvaluator.Default.EvaluateToString(
    """{"filter": [{"var": "products"}, {"and": [{"var": "inStock"}, {">": [{"var": "price"}, 50]}]}]}""",
    productsJson);

// Map: extract names
JsonLogicEvaluator.Default.EvaluateToString(
    """{"map": [{"var": "products"}, {"var": "name"}]}""",
    productsJson);

// Reduce: sum prices
JsonLogicEvaluator.Default.EvaluateToString(
    """{"reduce": [{"var": "products"}, {"+": [{"var": "accumulator"}, {"var": "current.price"}]}, 0]}""",
    productsJson);
```

## Custom Operators

Register custom operators by implementing `IOperatorCompiler` and passing them to a new `JsonLogicEvaluator`:

```csharp
file class PercentOperator : IOperatorCompiler
{
    public RuleEvaluator Compile(RuleEvaluator[] operands)
    {
        RuleEvaluator valueEval = operands[0];
        RuleEvaluator percentEval = operands[1];

        return (in JsonElement data, JsonWorkspace ws) =>
        {
            EvalResult value = valueEval(in data, ws);
            EvalResult percent = percentEval(in data, ws);

            if (value.TryGetDouble(out double v) && percent.TryGetDouble(out double p))
            {
                return EvalResult.FromDouble(v * p / 100.0);
            }

            return EvalResult.FromDouble(0);
        };
    }
}

var evaluator = new JsonLogicEvaluator(
    new Dictionary<string, IOperatorCompiler>
    {
        ["percent"] = new PercentOperator(),
    });

evaluator.EvaluateToString("""{"percent": [250, 20]}""", "{}");
// 50
```

Custom operators are checked **before** built-in operators, so they can override standard behaviour.

## Source-Generated Rules

For maximum performance, compile rules at build time using the `[JsonLogicRule]` attribute. Add the rule as an `AdditionalFiles` item and reference the source generator:

File: `discount-rule.json`

```json
{
    "if": [
        { ">=": [{ "var": "order.total" }, 100] },
        { "*": [{ "var": "order.total" }, 0.9] },
        { "var": "order.total" }
    ]
}
```

```csharp
[JsonLogicRule("discount-rule.json")]
public static partial class DiscountRule;
```

The generator emits a static `Evaluate` method with constant-folded arithmetic:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = DiscountRule.Evaluate(dataDoc.RootElement, workspace);
```

## Running the Example

```bash
cd docs/ExampleRecipes/024-JsonLogic
dotnet run
```

## Related Patterns

- [025-JSONata](../025-JSONata/) — JSONata expression evaluation (a more expressive query/transformation language)
- [023-JsonPatch](../023-JsonPatch/) — RFC 6902 JSON Patch operations
