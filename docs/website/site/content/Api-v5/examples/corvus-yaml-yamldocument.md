Parse YAML into a `JsonDocument`, or convert between YAML and JSON string representations using the standalone System.Text.Json package (no Corvus.Text.Json dependency required).

```csharp
using System.Text.Json;
using Corvus.Yaml;

string yaml = """
    name: Alice
    age: 30
    hobbies:
      - reading
      - cycling
    """;

using JsonDocument doc = YamlDocument.Parse(yaml);
string name = doc.RootElement.GetProperty("name").GetString()!;
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

```csharp
using JsonDocument docs = YamlDocument.Parse(multiDocYaml,
    new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray });
```
