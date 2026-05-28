# Corvus.Text.Json.OpenApi31

Strongly-typed V5 model types for OpenAPI 3.1 specifications, generated from the official
[OpenAPI 3.1 JSON Schema metaschema](https://spec.openapis.org/oas/3.1/schema/2022-10-07)
using the Corvus.Text.Json source generator.

These types parse and validate OpenAPI 3.1 documents with full JSON Schema fidelity, operating
directly on pooled memory with zero-allocation property access.

## Usage

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi31;

using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(specJson);
OpenApiDocument api = doc.RootElement;

// Access strongly-typed properties
var info = api.Info;
var title = info.Title;
```

## Related Packages

- `Corvus.Text.Json` — Core library
- `Corvus.Text.Json.OpenApi30` — OpenAPI 3.0 types
