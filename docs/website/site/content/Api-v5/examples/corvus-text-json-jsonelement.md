`JsonElement` is the general-purpose read-only JSON value type in Corvus.Text.Json. Parse through `ParsedJsonDocument<JsonElement>` so the document owns the pooled memory while you inspect the root element.

```csharp
using Corvus.Text.Json;

using ParsedJsonDocument<JsonElement> doc =
    ParsedJsonDocument<JsonElement>.Parse(
        """
        {
          "name": "Anne",
          "age": 42,
          "roles": ["admin", "editor"]
        }
        """);

JsonElement root = doc.RootElement;
string? name = root.GetProperty("name"u8).GetString();
int age = root.GetProperty("age"u8).GetInt32();

if (root.TryGetProperty("roles"u8, out JsonElement roles))
{
    foreach (JsonElement role in roles.EnumerateArray())
    {
        Console.WriteLine(role.GetString());
    }
}
```

### Inspecting value kinds

Use `ValueKind` before reading a value when the shape is not guaranteed.

```csharp
JsonElement maybeName = root.GetProperty("name"u8);

if (maybeName.ValueKind == JsonValueKind.String)
{
    Console.WriteLine(maybeName.GetString());
}
```

### Zero-allocation string comparison

Compare string values directly against UTF-8 or UTF-16 literals without allocating:

```csharp
bool isAnne = root.GetProperty("name"u8).ValueEquals("Anne"u8);
```

### Enumerating object properties

Object enumeration exposes property names and values without converting the entire object to another model:

```csharp
foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
{
    Console.WriteLine($"{property.Name}: {property.Value.ValueKind}");
}
```
