# JSON Schema Patterns in .NET - JSONata Expression Evaluation

This recipe demonstrates how to evaluate [JSONata](https://jsonata.org/) expressions against JSON data using the `Corvus.Text.Json.Jsonata` library. It covers path navigation, filtering, built-in functions, higher-order functions, object construction, variable and function bindings, safety controls, and build-time expression compilation via the source generator.

## The Pattern

JSONata is a lightweight query and transformation language for JSON data, inspired by XPath. Expressions can navigate, filter, aggregate, and reshape JSON documents. The Corvus.Text.Json.Jsonata implementation passes 100% of the official JSONata test suite (1,665/1,665 tests).

Expressions are compiled to delegate trees on first use and cached. The evaluator is thread-safe and designed for high-throughput, zero-allocation evaluation.

## The Schema

File: `order.json`

```json
{
    "title": "Order",
    "type": "object",
    "required": ["customer", "items"],
    "properties": {
        "customer": { "type": "string" },
        "items": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["name", "price", "quantity"],
                "properties": {
                    "name": { "type": "string" },
                    "price": { "type": "number" },
                    "quantity": { "type": "integer", "format": "int32" }
                }
            }
        }
    }
}
```

## Evaluating Expressions

### EvaluateToString — the simplest API

For quick evaluations where both input and output are JSON strings:

```csharp
var evaluator = JsonataEvaluator.Default;

string? result = evaluator.EvaluateToString("customer", orderJson);
// "\"Alice\""

result = evaluator.EvaluateToString("$sum(items.(price * quantity))", orderJson);
// "244.99"
```

### Evaluate with parsed documents

For zero-allocation evaluation, parse the data into a `JsonElement` and provide a `JsonWorkspace`:

```csharp
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(orderJson);
using JsonWorkspace workspace = JsonWorkspace.Create();

JsonElement result = evaluator.Evaluate(
    "$sum(items.(price * quantity))",
    dataDoc.RootElement,
    workspace);
```

## Path Navigation and Filtering

```csharp
// Simple property
evaluator.EvaluateToString("items[0].name", orderJson);
// "\"Widget\""

// All names (implicit map)
evaluator.EvaluateToString("items.name", orderJson);
// ["Widget","Gadget","Doohickey"]

// Predicate filter
evaluator.EvaluateToString("items[price > 20].name", orderJson);
// ["Widget","Gadget"]

// Computed expression per item
evaluator.EvaluateToString("""items.(name & ": $" & price * quantity)""", orderJson);
// ["Widget: $100","Gadget: $99.99","Doohickey: $45"]
```

## Built-in Functions

JSONata provides 60+ built-in functions. Some highlights:

| Category | Functions |
|---|---|
| Aggregation | `$sum`, `$count`, `$max`, `$min`, `$average` |
| String | `$uppercase`, `$lowercase`, `$trim`, `$substring`, `$split`, `$join` |
| Type | `$type`, `$number`, `$string`, `$boolean`, `$exists` |
| Math | `$abs`, `$floor`, `$ceil`, `$round`, `$power`, `$sqrt` |

```csharp
evaluator.EvaluateToString("$sum(items.price)", orderJson);         // 129.49
evaluator.EvaluateToString("$uppercase(customer)", orderJson);      // "ALICE"
evaluator.EvaluateToString("""$join(items.name, ", ")""", orderJson); // "Widget, Gadget, Doohickey"
```

## Higher-Order Functions

```csharp
// $map — transform each element
evaluator.EvaluateToString(
    """$map(items, function($v) { $v.name & " x" & $v.quantity })""", orderJson);
// ["Widget x4","Gadget x1","Doohickey x10"]

// $filter — select matching elements
evaluator.EvaluateToString(
    """$filter(items, function($v) { $v.price >= 10 })""", orderJson);

// $reduce — fold to a single value
evaluator.EvaluateToString(
    """$reduce(items, function($acc, $v) { $acc + $v.price * $v.quantity }, 0)""", orderJson);
// 244.99

// $sort — custom ordering
evaluator.EvaluateToString(
    """$sort(items, function($a, $b) { $a.price > $b.price }).name""", orderJson);
// ["Doohickey","Widget","Gadget"]
```

## Object Construction and Transformation

JSONata can build new JSON structures inline:

```csharp
string transform =
    """
    {
        "orderSummary": customer & "'s order",
        "lineItems": items.{
            "product": name,
            "lineTotal": price * quantity
        },
        "grandTotal": $sum(items.(price * quantity))
    }
    """;

evaluator.EvaluateToString(transform, orderJson);
// {"orderSummary":"Alice's order","lineItems":[{"product":"Widget","lineTotal":100}, ...], "grandTotal":244.99}
```

## Variable Bindings

Pass external values into expressions using `JsonataBinding`:

```csharp
var bindings = new Dictionary<string, JsonataBinding>
{
    ["taxRate"] = JsonataBinding.FromValue(0.2),
    ["currency"] = JsonataBinding.FromValue("GBP"),
};

JsonElement result = evaluator.Evaluate(
    """$sum(items.(price * quantity)) * (1 + $taxRate) & " " & $currency""",
    dataDoc.RootElement,
    workspace,
    bindings);
// "293.988 GBP"
```

`JsonataBinding.FromValue()` accepts `double`, `string`, `bool`, and `JsonElement`.

## Custom C# Function Bindings

Bind C# functions that can be called from JSONata expressions:

```csharp
var fnBindings = new Dictionary<string, JsonataBinding>
{
    // Simple double→double function (zero-allocation shorthand)
    ["roundUp"] = JsonataBinding.FromFunction((double v) => Math.Ceiling(v)),

    // Binary double→double function
    ["hypot"] = JsonataBinding.FromFunction((double a, double b) => Math.Sqrt(a * a + b * b)),
};

evaluator.Evaluate("$roundUp($sum(items.(price * quantity)) * 1.2)",
    dataDoc.RootElement, workspace, fnBindings);
// 294

evaluator.Evaluate("$hypot(3, 4)", dataDoc.RootElement, workspace, fnBindings);
// 5
```

For more complex functions, use the full `SequenceFunction` delegate:

```csharp
["customFn"] = JsonataBinding.FromFunction(
    (ReadOnlySpan<Sequence> args, JsonWorkspace ws) =>
    {
        // Full access to Sequence API
        double value = args[0].AsDouble();
        return Sequence.FromDouble(value * 2, ws);
    },
    parameterCount: 1)
```

## Safety Controls

### Recursion depth limit

Limit the recursion depth to protect against stack overflows from deeply recursive expressions:

```csharp
try
{
    evaluator.Evaluate(
        "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(10))",
        data, maxDepth: 5);
}
catch (JsonataException ex) when (ex.Code == "U1001")
{
    // Stack overflow protection
}
```

### Execution timeout

Set a time limit for expensive expressions:

```csharp
try
{
    evaluator.Evaluate(
        "($f := function($n) { $n <= 1 ? 1 : $f($n-1) + $f($n-2) }; $f(100))",
        data, timeLimitMs: 100);
}
catch (JsonataException ex) when (ex.Code == "U1001")
{
    // Timeout protection
}
```

## Source-Generated Expressions

For maximum performance, compile expressions at build time using the `[JsonataExpression]` attribute. Add the expression file as an `AdditionalFiles` item and reference the source generator:

File: `total-price.jsonata`

```
$sum(items.(price * quantity))
```

```csharp
[JsonataExpression("total-price.jsonata")]
public static partial class TotalPrice;
```

The generator compiles the expression to optimized C# at build time:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = TotalPrice.Evaluate(dataDoc.RootElement, workspace);
// 244.99
```

## Running the Example

```bash
cd docs/ExampleRecipes/025-JSONata
dotnet run
```

## Related Patterns

- [024-JsonLogic](../024-JsonLogic/) — JsonLogic rule evaluation (a simpler rules engine)
- [023-JsonPatch](../023-JsonPatch/) — RFC 6902 JSON Patch operations
- [018-CreatingAndMutatingObjects](../018-CreatingAndMutatingObjects/) — Mutable document lifecycle
