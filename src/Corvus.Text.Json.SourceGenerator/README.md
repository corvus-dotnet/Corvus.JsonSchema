# Corvus.Text.Json.SourceGenerator

Roslyn incremental source generator that produces strongly-typed C# from JSON Schema at build time.

Supports JSON Schema draft 4, 6, 7, 2019-09, and 2020-12. Generated types are lightweight `readonly struct` wrappers over pooled JSON data with type-safe property accessors, validation, serialization, and implicit conversions.

## Installation

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

You also need the runtime library:

```bash
dotnet add package Corvus.Text.Json
```

## Usage

1. Add a JSON Schema file to your project (e.g. `Schemas/person.json`)
2. Annotate a partial struct:

```csharp
using Corvus.Text.Json;

[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;
```

3. Use the generated type:

```csharp
using var doc = ParsedJsonDocument<Person>.Parse(
    """{"name":"Alice","age":30}""");
Person person = doc.RootElement;

string name = (string)person.Name;
int age = person.Age;
bool valid = person.EvaluateSchema();
```

## What Gets Generated

- Property accessors for each schema property
- `EvaluateSchema()` for full JSON Schema validation
- `Match()` methods for `oneOf`/`anyOf` discriminated unions
- `From()` conversions for `allOf` composition
- Implicit conversions to/from .NET primitives
- `Builder` type for creating instances with the builder pattern
- `Mutable` type for in-place modification

## Related Packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json** | Core runtime library (required) |
| **Corvus.Json.CodeGenerator** | CLI alternative for ahead-of-time generation |

## Documentation

See the [Source Generator guide](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/SourceGenerator.md).

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
