A `ref struct` that accumulates JSONPath result nodes using pooled memory.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
    """{"items": [1, 2, 3, 4, 5]}"""u8);

// QueryNodes returns a disposable JsonPathResult backed by ArrayPool
using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
    "$.items[*]",
    doc.RootElement);

// Access matched nodes as a ReadOnlySpan<JsonElement>
ReadOnlySpan<JsonElement> nodes = result.Nodes;
Console.WriteLine($"Matched {result.Count} nodes");

foreach (JsonElement node in nodes)
{
    Console.WriteLine(node.GetRawText());
}
```

### Caller-provided initial buffer

Provide a stack-allocated buffer for zero-heap-allocation queries when you know the approximate result size:

```csharp
JsonElement[] buffer = new JsonElement[16];
using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
    "$.items[0:3]",
    doc.RootElement,
    buffer);

// If fewer than 16 nodes match, no ArrayPool rental occurs
ReadOnlySpan<JsonElement> nodes = result.Nodes; // [1, 2, 3]
```
