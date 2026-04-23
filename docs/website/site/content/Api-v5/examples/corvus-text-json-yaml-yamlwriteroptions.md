Configure YAML output formatting with `YamlWriterOptions`.

```csharp
using Corvus.Text.Json.Yaml;

var options = new YamlWriterOptions
{
    IndentSize = 4,   // Default is 2
};

string yaml = YamlDocument.ConvertToYamlString(jsonElement, options);
```

### Default options

The default options use 2-space indentation with validation enabled:

```csharp
// These are equivalent:
string yaml1 = YamlDocument.ConvertToYamlString(json);
string yaml2 = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Default);
```

### Skip validation for performance

When you are certain the write calls are well-formed (balanced start/end, correct nesting), disable validation for a small performance gain:

```csharp
var buffer = new ArrayBufferWriter<byte>();
using var writer = new Utf8YamlWriter(buffer,
    new YamlWriterOptions { SkipValidation = true });
```
