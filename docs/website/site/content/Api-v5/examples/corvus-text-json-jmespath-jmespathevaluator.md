Evaluate a JMESPath expression against JSON data using the shared evaluator instance.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "locations": [
        {"name": "Seattle", "state": "WA"},
        {"name": "New York", "state": "NY"},
        {"name": "Bellevue", "state": "WA"},
        {"name": "Olympia", "state": "WA"}
      ]
    }
    """u8);

JsonElement result = JMESPathEvaluator.Default.Search(
    "locations[?state == 'WA'].name | sort(@) | {WashingtonCities: join(', ', @)}",
    doc.RootElement);
// result: {"WashingtonCities":"Bellevue, Olympia, Seattle"}
```

### Simple path navigation

```csharp
JsonElement name = JMESPathEvaluator.Default.Search("person.name", data);
JsonElement first = JMESPathEvaluator.Default.Search("items[0]", data);
JsonElement lengths = JMESPathEvaluator.Default.Search("items[*].length(@)", data);
```

### Caller-managed workspace for high-throughput scenarios

When searching many documents with the same expression, pass a shared `JsonWorkspace` to avoid repeated allocation:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (JsonElement item in items)
{
    workspace.Reset();
    JsonElement result = JMESPathEvaluator.Default.Search(
        "metrics.{avg: avg(values), max: max(values)}",
        item,
        workspace);
    // process result...
}
```
