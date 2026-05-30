# Corvus.Toon.SystemTextJson

High-performance TOON (Token-Oriented Object Notation) conversion support using only System.Text.Json.

TOON is a compact text format for JSON-shaped data and LLM prompts. It keeps JSON's data model while using indentation and table-style rows to reduce prompt tokens and make uniform arrays easier for AI models to follow. This package converts TOON to `System.Text.Json.JsonDocument` and converts JSON back to TOON without depending on Corvus.Text.Json.

## Installation

```bash
dotnet add package Corvus.Toon.SystemTextJson
```

## Quick start

```csharp
using Corvus.Toon;

using System.Text.Json.JsonDocument doc =
    ToonDocument.Parse("name: Alice\nage: 30");

string json = ToonDocument.ConvertToJsonString("name: Alice\nage: 30");
string toon = ToonDocument.ConvertToToonString("""{"name":"Alice","age":30}""");
```

## Related packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json.Toon** | TOON converter for Corvus.Text.Json |

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