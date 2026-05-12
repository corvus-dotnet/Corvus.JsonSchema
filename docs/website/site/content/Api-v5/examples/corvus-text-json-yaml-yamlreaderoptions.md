Configure YAML parsing behaviour with `YamlReaderOptions`. The default options use the Core schema, single-document mode, and error on duplicate keys.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;

// Default options — Core schema, single document, duplicate keys error
using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
```

### Custom options

```csharp
var options = new YamlReaderOptions
{
    Schema = YamlSchema.Json,
    DocumentMode = YamlDocumentMode.MultiAsArray,
    DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins,
};

using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml, options);
```

### Schema modes

| Schema | Behaviour |
|--------|-----------|
| `YamlSchema.Core` | Default. Resolves `true`/`false`/`null` and numeric scalars. |
| `YamlSchema.Json` | Like Core, but untagged scalars that aren't null/bool/number become strings. |
| `YamlSchema.Failsafe` | All scalars are strings — no type resolution. |
| `YamlSchema.Yaml11` | YAML 1.1 rules — `yes`/`no`/`on`/`off` are booleans, octal `0777`, etc. |

### Alias expansion limits

Control anchor/alias expansion depth and total size to prevent denial-of-service from deeply nested or exponentially expanding aliases:

```csharp
var options = new YamlReaderOptions
{
    MaxAliasExpansionDepth = 10,
    MaxAliasExpansionSize = 1_000_000,
};
```
