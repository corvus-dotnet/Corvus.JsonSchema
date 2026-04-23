Create named bindings to pass external values and custom functions into JSONata expressions.

### Value bindings

```csharp
using Corvus.Text.Json.Jsonata;

var bindings = new Dictionary<string, JsonataBinding>
{
    ["threshold"] = JsonataBinding.FromValue(100.0),
    ["label"] = JsonataBinding.FromValue("high"),
    ["active"] = JsonataBinding.FromValue(true),
};

JsonElement result = JsonataEvaluator.Default.Evaluate(
    "$threshold & ' is ' & $label",
    data, bindings);
```

### Implicit conversions

`JsonataBinding` supports implicit conversions from common types for concise dictionary initialization:

```csharp
var bindings = new Dictionary<string, JsonataBinding>
{
    ["limit"] = 50.0,        // implicit from double
    ["enabled"] = true,      // implicit from bool
};
```

### Custom function bindings

Register custom functions that can be called from JSONata expressions:

```csharp
var bindings = new Dictionary<string, JsonataBinding>
{
    // Simple numeric function
    ["double"] = JsonataBinding.FromFunction((double x) => x * 2),

    // Two-argument function
    ["add"] = JsonataBinding.FromFunction((double a, double b) => a + b),
};

JsonElement result = JsonataEvaluator.Default.Evaluate(
    "$double(21)",
    data, bindings);
// result: 42
```
