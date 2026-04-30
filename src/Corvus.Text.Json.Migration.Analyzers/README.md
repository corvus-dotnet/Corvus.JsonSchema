# Corvus.Text.Json.Migration.Analyzers

Roslyn analyzers and code fixes to help migrate from Corvus.Json (V4) to Corvus.Text.Json (V5).

## Installation

```bash
dotnet add package Corvus.Text.Json.Migration.Analyzers
```

## Related Packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json** | Core library — pooled-memory parsing, mutable documents, schema validation |
| **Corvus.Text.Json.SourceGenerator** | Roslyn source generator — generates C# from JSON Schema at build time |
| **Corvus.Json.CodeGenerator** | CLI tool for ahead-of-time code generation |
| **Corvus.Text.Json.Validator** | Runtime schema validation using dynamically compiled schemas |
| **Corvus.Text.Json.Compatibility** | Bridge types for interoperating with System.Text.Json |
| **Corvus.Text.Json.CodeGeneration** | Shared code generation engine |
| **Corvus.Text.Json.Patch** | RFC 6902 JSON Patch support |

## Documentation

See the [full documentation](https://github.com/corvus-dotnet/Corvus.JsonSchema).

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).