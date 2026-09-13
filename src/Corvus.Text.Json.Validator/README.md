# Corvus.Text.Json.Validator

Load and validate JSON documents against JSON Schema at runtime. Schemas are compiled into an in-memory evaluator by `Corvus.Text.Json.RuntimeEvaluator`: no code generation, no Roslyn, and a cold start measured in milliseconds.

Ideal for schema registries, configuration validation, and user-supplied schemas where the schema is not known at build time.

## Installation

```bash
dotnet add package Corvus.Text.Json.Validator
```

## Usage

```csharp
using Corvus.Text.Json.Validator;

// Load a schema
string schema = """
    {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "required": ["name"],
        "properties": {
            "name": { "type": "string", "minLength": 1 }
        }
    }
    """;

// Compile and cache the schema
JsonSchema validator = JsonSchema.FromText(schema);

// Validate JSON documents
string json = """{"name": "Alice"}""";
bool isValid = validator.Validate(json);

Console.WriteLine(isValid);  // true
```

## Features

- **Runtime compilation** — schemas are compiled into an in-memory evaluator in milliseconds, with zero-allocation validation once warm
- **Full schema support** — draft 4, 6, 7, 2019-09, and 2020-12
- **Detailed diagnostics** — validation results include schema location, evaluation path, and error messages
- **Caching** — compiled schemas are cached by canonical URI and reused across documents

## Related Packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json** | Core runtime library |
| **Corvus.Text.Json.SourceGenerator** | Build-time alternative for known schemas |

## Documentation

See the [Runtime Schema Validation guide](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/Validator.md).

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
