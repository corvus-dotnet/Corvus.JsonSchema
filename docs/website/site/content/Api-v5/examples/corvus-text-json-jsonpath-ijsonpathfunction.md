Implement `IJsonPathFunction` for full control over custom JSONPath function extensions.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;

/// <summary>
/// A custom ceil() function that rounds a number up to the nearest integer.
/// Signature: (ValueType) → ValueType
/// </summary>
public sealed class CeilFunction : IJsonPathFunction
{
    public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes =>
        [JsonPathFunctionType.ValueType];

    public JsonPathFunctionResult Evaluate(
        ReadOnlySpan<JsonPathFunctionArgument> arguments,
        JsonWorkspace workspace)
    {
        JsonElement value = arguments[0].Value;

        if (value.ValueKind != JsonValueKind.Number)
        {
            return JsonPathFunctionResult.Nothing;
        }

        int ceiled = (int)Math.Ceiling(value.GetDouble());
        return JsonPathFunctionResult.FromValue(ceiled, workspace);
    }
}
```

### Registering the function

```csharp
var evaluator = new JsonPathEvaluator(new Dictionary<string, IJsonPathFunction>
{
    ["ceil"] = new CeilFunction(),
});

// Use in expressions: $.items[?ceil(@.score) > 5]
using JsonPathResult result = evaluator.QueryNodes(
    "$.items[?ceil(@.score) > 5]",
    data);
```

### Simpler alternatives

For simple functions, use the `JsonPathFunction` factory methods instead of implementing the interface directly:

```csharp
// Equivalent to the CeilFunction class above:
IJsonPathFunction ceil = JsonPathFunction.Value((v, ws) =>
    JsonPathCodeGenHelpers.IntToElement((int)Math.Ceiling(v.GetDouble()), ws));
```
