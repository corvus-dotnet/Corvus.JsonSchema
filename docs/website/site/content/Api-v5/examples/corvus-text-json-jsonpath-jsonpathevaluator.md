Evaluate a JSONPath (RFC 9535) expression against JSON data using the shared evaluator instance.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "store": {
        "book": [
          {"title": "Sayings of the Century", "price": 8.95},
          {"title": "Moby Dick", "price": 8.99},
          {"title": "The Lord of the Rings", "price": 22.99}
        ]
      }
    }
    """u8);

// Query returns a JSON array of matched nodes
JsonElement titles = JsonPathEvaluator.Default.Query(
    "$.store.book[*].title",
    doc.RootElement);
// titles: ["Sayings of the Century","Moby Dick","The Lord of the Rings"]
```

### Zero-allocation query with QueryNodes

Use `QueryNodes` when you need to iterate results without creating a JSON array:

```csharp
using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
    "$.store.book[?@.price < 10].title",
    doc.RootElement);

foreach (JsonElement node in result.Nodes)
{
    Console.WriteLine(node.GetRawText());
}
// "Sayings of the Century"
// "Moby Dick"
```

### Custom functions

Register custom functions to extend the filter expression language:

```csharp
var evaluator = new JsonPathEvaluator(new Dictionary<string, IJsonPathFunction>
{
    ["ceil"] = JsonPathFunction.Value((v, ws) =>
        JsonPathFunctionResult.FromValue((int)Math.Ceiling(v.GetDouble()), ws)),
    ["is_cheap"] = JsonPathFunction.Logical(v =>
        v.ValueKind == JsonValueKind.Number && v.GetDouble() < 10),
});

JsonElement cheap = evaluator.Query(
    "$.store.book[?is_cheap(@.price)].title",
    doc.RootElement);
// cheap: ["Sayings of the Century","Moby Dick"]
```
