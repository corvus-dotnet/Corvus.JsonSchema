# Corvus.Text.Json.CodeGeneration

Shared code generation engine for producing strongly-typed C# from JSON Schema. This package is used by both the Roslyn source generator and the CLI code generator tool.

Supports JSON Schema draft 4, 6, 7, 2019-09, and 2020-12 with full vocabulary support including `format`, `$ref`, `oneOf`/`anyOf`/`allOf` composition, and pattern properties.

## Installation

```bash
dotnet add package Corvus.Text.Json.CodeGeneration
```

> **Note:** Most users should use either the [source generator](https://www.nuget.org/packages/Corvus.Text.Json.SourceGenerator) or the [CLI tool](https://www.nuget.org/packages/Corvus.Json.CodeGenerator) instead of this package directly. This package is for building custom code generation tooling.

## Target Frameworks

- .NET 10.0, 9.0, 8.0
- .NET Standard 2.0

## Related Packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json** | Core runtime library |
| **Corvus.Text.Json.SourceGenerator** | Roslyn source generator (uses this package internally) |
| **Corvus.Json.CodeGenerator** | CLI tool (uses this package internally) |

## Documentation

See the [full documentation](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/docs).

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
