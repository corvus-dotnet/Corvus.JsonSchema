# Corvus.Json.CodeGenerator

CLI tool for generating strongly-typed C# from JSON Schema files. Supports both **Corvus.Text.Json (V5)** and **Corvus.Json (V4)** output, making it the single tool for all Corvus JSON Schema code generation.

Supports JSON Schema draft 4, 6, 7, 2019-09, 2020-12, and OpenAPI 3.0 (including YAML input).

## Installation

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

Or as a local tool:

```bash
dotnet new tool-manifest
dotnet tool install Corvus.Json.CodeGenerator
```

## Commands

### `generate` (default)

Generate C# types from a single schema file. This is the default command — you can omit the command name.

```bash
generatejsonschematypes <schemaFile> --rootNamespace <namespace> [OPTIONS]
```

### `config`

Generate types from a configuration file that specifies multiple schemas and shared settings:

```bash
generatejsonschematypes config myconfig.json [--engine V5]
```

### `validateDocument`

Validate a JSON or YAML document against a schema:

```bash
generatejsonschematypes validateDocument Schemas/person.json data.json
```

### `listNameHeuristics`

List available naming heuristics (some can be disabled):

```bash
generatejsonschematypes listNameHeuristics
```

## Quick Start

### V5 Types (Corvus.Text.Json) — default

```bash
generatejsonschematypes person.json \
    --rootNamespace MyApp.Models \
    --outputPath ./Generated
```

Generated types are lightweight `readonly struct` wrappers with pooled-memory parsing, schema validation, and builder-pattern mutation. They require the `Corvus.Text.Json` NuGet package at runtime.

### V4 Types (Corvus.Json)

Use the `--engine V4` option to generate V4 types targeting the `Corvus.Json` namespace:

```bash
generatejsonschematypes person.json \
    --rootNamespace MyApp.Models \
    --outputPath ./Generated \
    --engine V4
```

V4 generated types use the functional immutable API with `Corvus.Json.ExtendedTypes` at runtime.

## Common Options

| Option | Default | Description |
|---|---|---|
| `<schemaFile>` | — | Path to the JSON Schema file (required) |
| `--rootNamespace` | — | Root namespace for generated types (required) |
| `--outputPath` | — | Directory for generated `.cs` files |
| `--outputRootTypeName` | Derived | Override the .NET type name for the root type |
| `--rootPath` | — | JSON Pointer to the root element (e.g., `#/definitions/Person`) |
| `--rebaseToRootPath` | `false` | Rebase the document as if rooted at `--rootPath` |
| `--useSchema` | Auto-detect | Fallback schema draft: `Draft4`, `Draft6`, `Draft7`, `Draft201909`, `Draft202012`, `OpenApi30` |
| `--assertFormat` | `true` | Enforce `format` keyword as a validation assertion |
| `--optionalAsNullable` | `None` | How to handle optional properties: `None` or `NullOrUndefined` |
| `--useImplicitOperatorString` | `false` | Use implicit (vs explicit) conversion to `string` |
| `--yaml` | `false` | Enable YAML schema support |
| `--addExplicitUsings` | `false` | Include explicit `using` statements for standard implicit usings |
| `--engine` | `V5` | Code generation engine: `V5` (Corvus.Text.Json) or `V4` (legacy Corvus.Json.ExtendedTypes) |
| `--outputMapFile` | — | Write a JSON map of all generated files |

## Example

Given a schema `person.json`:

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "required": ["name"],
    "properties": {
        "name": { "type": "string" },
        "age": { "type": "integer", "format": "int32" }
    }
}
```

Generate V5 types:

```bash
generatejsonschematypes person.json \
    --rootNamespace MyApp.Models \
    --outputPath ./Models \
    --outputRootTypeName Person
```

Then use in your project:

```csharp
// V5 (Corvus.Text.Json) — requires Corvus.Text.Json package
using var doc = ParsedJsonDocument<Person>.Parse(json);
Person person = doc.RootElement;
string name = (string)person.Name;
```

## Related Packages

| Package | Purpose |
|---|---|
| **Corvus.Text.Json** | V5 core runtime library (required by V5 generated code) |
| **Corvus.Text.Json.SourceGenerator** | Build-time alternative using Roslyn (V5 only) |
| **Corvus.Json.ExtendedTypes** | V4 core runtime library (required by V4 generated code) |

## Documentation

See the [CLI Code Generation guide](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/CodeGenerator.md) for the full reference including config file format, naming heuristics, and advanced usage.

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
