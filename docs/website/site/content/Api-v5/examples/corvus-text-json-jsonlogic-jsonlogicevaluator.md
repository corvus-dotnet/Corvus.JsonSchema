Evaluate a JsonLogic rule against JSON data using the shared evaluator instance.

```csharp
using Corvus.Text.Json.JsonLogic;

// Quick one-liner with string inputs
string? result = JsonLogicEvaluator.Default.EvaluateToString(
    """{"+":[1, 2, 3]}""",
    "{}");
// result: "6"
```

### Evaluating with structured data

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

using ParsedJsonDocument<JsonElement> ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"if": [{">=": [{"var": "age"}, 18]}, "adult", "minor"]}"""u8);
using ParsedJsonDocument<JsonElement> dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"age": 25}"""u8);

JsonLogicRule rule = new(ruleDoc.RootElement);
JsonElement result = JsonLogicEvaluator.Default.Evaluate(in rule, dataDoc.RootElement);
// result: "adult"
```

### Caller-managed workspace for high-throughput scenarios

When evaluating many rules in a loop, pass a shared `JsonWorkspace` to avoid repeated allocation:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (JsonElement data in items)
{
    JsonElement result = JsonLogicEvaluator.Default.Evaluate(
        in rule, in data, workspace);
    // process result...
}
```
