# Corvus.Text.Json.JsonPath

JSONPath (RFC 9535) query language evaluator for Corvus.Text.Json.

Provides a high-performance, zero-allocation compiled-delegate evaluator for
JSONPath expressions. Compiled expressions are cached and reused automatically.

## Usage

```csharp
using Corvus.Text.Json.JsonPath;

JsonElement data = JsonElement.ParseValue("""{"store":{"book":[{"title":"A"},{"title":"B"}]}}"""u8);
JsonElement result = JsonPathEvaluator.Default.Query("$.store.book[*].title", data);
// result is ["A","B"]
```

## Custom Functions

Register custom functions via the `JsonPathEvaluator` constructor. Use the `JsonPathFunction` factory for simple signatures, or implement `IJsonPathFunction` directly for full control:

```csharp
var evaluator = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction>
    {
        ["ceil"] = JsonPathFunction.Value((v, ws) =>
            JsonPathFunctionResult.FromValue((int)Math.Ceiling(v.GetDouble()), ws)),
        ["is_fiction"] = JsonPathFunction.Logical(v =>
            v.ValueKind == JsonValueKind.String && v.ValueEquals("fiction"u8)),
    });

JsonElement result = evaluator.Query("$.store.book[?ceil(@.price)==9].title", data);
```

## Packages

| Package | Description |
|---------|-------------|
| `Corvus.Text.Json.JsonPath` | Runtime evaluator (this package) |
| `Corvus.Text.Json.JsonPath.CodeGeneration` | Expression → C# source code |
| `Corvus.Text.Json.JsonPath.SourceGenerator` | Roslyn incremental source generator |
