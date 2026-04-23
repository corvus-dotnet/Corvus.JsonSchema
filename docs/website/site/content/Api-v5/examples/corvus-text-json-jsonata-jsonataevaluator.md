Evaluate a JSONata expression against JSON data using the shared evaluator instance.

```csharp
using Corvus.Text.Json.Jsonata;

// Quick evaluation with string inputs
string? result = JsonataEvaluator.Default.EvaluateToString(
    "Account.Order.Product.Price ~> $sum()",
    """{"Account":{"Order":[{"Product":{"Price":10}},{"Product":{"Price":20}}]}}""");
// result: "30"
```

### Structured evaluation with JsonElement

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
        "FirstName": "Fred",
        "Surname": "Smith",
        "Age": 28
    }
    """u8);

JsonElement result = JsonataEvaluator.Default.Evaluate(
    "FirstName & ' ' & Surname",
    doc.RootElement);
// result: "Fred Smith"
```

### Custom variable bindings

Pass external values into the expression as named bindings:

```csharp
using Corvus.Text.Json.Jsonata;

var bindings = new Dictionary<string, JsonataBinding>
{
    ["threshold"] = JsonataBinding.FromValue(100.0),
    ["label"] = JsonataBinding.FromValue("high"),
};

JsonElement result = JsonataEvaluator.Default.Evaluate(
    "$sum(values[$ > $threshold]) & ' = ' & $label",
    data,
    bindings);
```

### Caller-managed workspace for high-throughput scenarios

When evaluating many expressions in a loop, pass a shared `JsonWorkspace` to avoid repeated allocation:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (JsonElement item in items)
{
    JsonElement result = JsonataEvaluator.Default.Evaluate(
        "name & ': ' & $string(score)",
        item,
        workspace);
    // process result...
}
```

### Time-limited evaluation

Set `timeLimitMs` to prevent runaway expressions from blocking indefinitely:

```csharp
try
{
    JsonElement result = JsonataEvaluator.Default.Evaluate(
        expression, data, maxDepth: 100, timeLimitMs: 5000);
}
catch (JsonataException ex) when (ex.Code == "U1001")
{
    // Expression timed out after 5 seconds
}
```
