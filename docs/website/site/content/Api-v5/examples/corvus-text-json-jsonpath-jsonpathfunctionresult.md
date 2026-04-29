Create results from custom JSONPath function implementations using the static factory methods.

### Value results

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;

// Return an existing JsonElement directly
JsonPathFunctionResult fromElement = JsonPathFunctionResult.FromValue(someElement);

// Return a new integer value (requires a workspace for element allocation)
JsonPathFunctionResult fromInt = JsonPathFunctionResult.FromValue(42, workspace);

// Return a new double value
JsonPathFunctionResult fromDouble = JsonPathFunctionResult.FromValue(3.14, workspace);

// Return a string value
JsonPathFunctionResult fromString = JsonPathFunctionResult.FromValue("hello");

// Return a boolean as a JSON value (true/false element, not a LogicalType)
JsonPathFunctionResult fromBool = JsonPathFunctionResult.FromValueBool(true);
```

### Logical results

Logical results are used by functions that return `LogicalType` for filter predicates:

```csharp
// Return a logical true/false for use in filter expressions
JsonPathFunctionResult logical = JsonPathFunctionResult.FromLogical(true);
```

### Nothing

Return `Nothing` when the function cannot produce a meaningful result (e.g., type mismatch):

```csharp
public JsonPathFunctionResult Evaluate(
    ReadOnlySpan<JsonPathFunctionArgument> arguments,
    JsonWorkspace workspace)
{
    JsonElement value = arguments[0].Value;

    if (value.ValueKind != JsonValueKind.Number)
    {
        // Type mismatch — signal "no result" to the filter expression
        return JsonPathFunctionResult.Nothing;
    }

    return JsonPathFunctionResult.FromValue((int)Math.Floor(value.GetDouble()), workspace);
}
```
