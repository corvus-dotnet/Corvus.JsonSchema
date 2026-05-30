# Corvus.Text.Json.Toon

High-performance TOON (Token-Oriented Object Notation) conversion support for Corvus.Text.Json.

TOON is a compact text format for JSON-shaped data and LLM prompts. It keeps JSON's data model while using indentation and table-style rows to reduce prompt tokens and make uniform arrays easier for AI models to follow. This package converts TOON to the Corvus pooled document model and converts JSON back to TOON while staying UTF-8-first internally.

## Installation

```bash
dotnet add package Corvus.Text.Json.Toon
```

## Quick start

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Toon;

using ParsedJsonDocument<JsonElement> doc =
    ToonDocument.Parse<JsonElement>("name: Alice\nage: 30");

string json = ToonDocument.ConvertToJsonString("name: Alice\nage: 30");
string toon = ToonDocument.ConvertToToonString(doc.RootElement);
```

## Related packages

| Package | Purpose |
|---|---|
| **Corvus.Toon.SystemTextJson** | TOON converter using System.Text.Json only |

## Documentation

See the [TOON documentation](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/Toon.md).

TOON project resources:

| Resource | Link |
|---|---|
| Project site | [toonformat.dev](https://toonformat.dev/) |
| Reference implementation | [github.com/toon-format/toon](https://github.com/toon-format/toon) |
| Specification | [github.com/toon-format/spec](https://github.com/toon-format/spec) |

## License

Apache 2.0 - see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).