# Corvus.Text.Json.Arazzo10

Strongly-typed V5 model types for Arazzo 1.0 workflow documents, generated from the official
[Arazzo 1.0 JSON Schema](https://spec.openapis.org/arazzo/1.0/schema/2024-08-01)
using the Corvus.Text.Json source generator.

These types parse and validate [Arazzo](https://github.com/OAI/Arazzo-Specification) workflow
documents with full JSON Schema fidelity, operating directly on pooled memory with
zero-allocation property access.

## Usage

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo10;

using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(arazzoJson);
ArazzoDocument arazzo = doc.RootElement;

// Access strongly-typed properties
var info = arazzo.Info;
var title = info.Title;
```

## Related Packages

- `Corvus.Text.Json` — Core library
- `Corvus.Text.Json.OpenApi30` / `Corvus.Text.Json.OpenApi31` — OpenAPI source-description types
