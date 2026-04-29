Factory methods for creating custom JSONPath functions from delegates, without implementing `IJsonPathFunction` manually.

### Value function (single argument)

```csharp
using Corvus.Text.Json.JsonPath;

// ceil(value) → value: rounds a number up to the nearest integer
IJsonPathFunction ceil = JsonPathFunction.Value((v, ws) =>
    JsonPathFunctionResult.FromValue((int)Math.Ceiling(v.GetDouble()), ws));
```

### Value function (two arguments)

```csharp
// add(value, value) → value: sums two numbers
IJsonPathFunction add = JsonPathFunction.Value((a, b, ws) =>
    JsonPathFunctionResult.FromValue(a.GetDouble() + b.GetDouble(), ws));
```

### Logical function

```csharp
// is_string(value) → logical: tests whether a value is a string
IJsonPathFunction isString = JsonPathFunction.Logical(v =>
    v.ValueKind == JsonValueKind.String);
```

### Nodes function

```csharp
// first(nodes) → value: returns the first node, or Nothing
IJsonPathFunction first = JsonPathFunction.NodesValue((nodes, ws) =>
    nodes.Length > 0 ? nodes[0] : default);
```

### Registering functions with the evaluator

```csharp
var evaluator = new JsonPathEvaluator(new Dictionary<string, IJsonPathFunction>
{
    ["ceil"] = ceil,
    ["is_string"] = isString,
});

// Use in filter expressions: $[?is_string(@.name)]
JsonElement result = evaluator.Query("$[?is_string(@.name)]", data);
```

### Full control with Create

For arbitrary signatures, use `Create` with explicit type arrays:

```csharp
IJsonPathFunction custom = JsonPathFunction.Create(
    JsonPathFunctionType.ValueType,
    [JsonPathFunctionType.ValueType, JsonPathFunctionType.NodesType],
    (args, ws) =>
    {
        JsonElement threshold = args[0].Value;
        ReadOnlySpan<JsonElement> nodes = args[1].Nodes;
        int count = 0;
        foreach (JsonElement n in nodes)
        {
            if (n.GetDouble() > threshold.GetDouble())
            {
                count++;
            }
        }

        return JsonPathFunctionResult.FromValue(count, ws);
    });
```
