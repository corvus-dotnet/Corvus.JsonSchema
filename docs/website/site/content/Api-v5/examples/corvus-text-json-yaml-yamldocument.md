Parse YAML into a `ParsedJsonDocument<TElement>`, or convert between YAML and JSON string representations.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;

string yaml = """
    name: Alice
    age: 30
    hobbies:
      - reading
      - cycling
    """;

using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
string name = (string)doc.RootElement.GetProperty("name"u8);
// name: "Alice"
```

### Converting YAML to a JSON string

```csharp
string json = YamlDocument.ConvertToJsonString(yaml);
// json: {"name":"Alice","age":30,"hobbies":["reading","cycling"]}
```

### Converting JSON to YAML

```csharp
string yamlOutput = YamlDocument.ConvertToYamlString(
    """{"name":"Alice","age":30,"hobbies":["reading","cycling"]}""");
// yamlOutput:
// name: Alice
// age: 30
// hobbies:
// - reading
// - cycling
```

### Multi-document streams

Parse a YAML stream containing multiple documents as a JSON array:

```csharp
string multiDoc = """
    ---
    name: Alice
    ---
    name: Bob
    """;

using ParsedJsonDocument<JsonElement> docs = YamlDocument.Parse<JsonElement>(multiDoc,
    new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray });
int count = docs.RootElement.GetArrayLength(); // 2
```

### Schema selection

```csharp
// Use the JSON schema (all scalars except null, true, false, and numbers are strings)
string json = YamlDocument.ConvertToJsonString(yaml,
    new YamlReaderOptions { Schema = YamlSchema.Json });

// Use the Failsafe schema (everything is a string)
string failsafe = YamlDocument.ConvertToJsonString(yaml,
    new YamlReaderOptions { Schema = YamlSchema.Failsafe });
```
