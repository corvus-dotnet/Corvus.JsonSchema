# Corvus.Text.Json

High-performance JSON library for .NET that builds on the patterns of `System.Text.Json` with:

- **Source-generated types** from JSON Schema (draft 4, 6, 7, 2019-09, 2020-12)
- **Pooled-memory parsing** via `ParsedJsonDocument<T>` — just 136B per-document allocation (91% less than `JsonNode`)
- **Mutable document building** via `JsonDocumentBuilder<T>` and `JsonWorkspace`
- **Full schema validation** with `EvaluateSchema()` — 10× faster than other .NET validators
- **Extended numeric types** — `BigNumber` (arbitrary precision), `BigInteger`, `Int128`, `Half`
- **NodaTime integration** — `LocalDate`, `OffsetDateTime`, `OffsetTime`, `Period` from JSON Schema formats
- **Pattern matching** — type-safe `Match()` for `oneOf`/`anyOf` discriminated unions

## Installation

```bash
dotnet add package Corvus.Text.Json
```

## Quick Start

```csharp
using Corvus.Text.Json;

// Parse JSON with pooled memory
using var doc = ParsedJsonDocument<JsonElement>.Parse(
    """{"name":"Alice","age":30}""");
JsonElement root = doc.RootElement;

string name = root.GetProperty("name").GetString();  // "Alice"
int age = root.GetProperty("age").GetInt32();         // 30
```

### With Source-Generated Types

```csharp
// Generate strongly-typed C# from JSON Schema
[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;

// Parse, access, and validate
using var doc = ParsedJsonDocument<Person>.Parse(json);
Person person = doc.RootElement;
string name = (string)person.Name;
bool valid = person.EvaluateSchema();

// Mutate with the builder pattern
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = person.CreateBuilder(workspace);
Person.Mutable root = builder.RootElement;
root.SetAge(31);
```

## Target Frameworks

- .NET 10.0, 9.0
- .NET Standard 2.0, 2.1

## Related Packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json.SourceGenerator** | Roslyn source generator — generates C# from JSON Schema at build time |
| **Corvus.Json.CodeGenerator** | CLI tool for ahead-of-time code generation |
| **Corvus.Text.Json.Validator** | Runtime schema validation using dynamically compiled schemas |

## Documentation

See the [full documentation](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs).

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
