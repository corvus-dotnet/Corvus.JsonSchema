---
name: corvus-codegen
description: >
  Generate strongly-typed C# from JSON Schema using the Roslyn source generator or the
  corvusjson CLI tool. Covers the JsonSchemaTypeGenerator attribute, CLI options, naming
  heuristics, AdditionalFiles registration, config file format, MSBuild properties,
  and troubleshooting generated output. USE FOR: generating types from schemas, configuring
  the source generator or CLI tool, understanding naming heuristics, inspecting generated
  output, troubleshooting generation issues. DO NOT USE FOR: modifying the generator
  internals (use corvus-keywords-and-validation), running benchmarks
  (use corvus-benchmarks).
---

# Code Generation from JSON Schema

## Two Code Generation Mechanisms

### 1. Roslyn Source Generator (build-time)

Add the source generator NuGet package and annotate a **partial struct**:

```csharp
[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;
```

**Attribute** (defined in `IncrementalSourceGenerator.cs`):
- Constructor: `JsonSchemaTypeGeneratorAttribute(string location, bool rebaseToRootPath = false)`
- Property: `EmitEvaluator` (bool) — also emit a standalone schema evaluator
- **Must be applied to a `partial struct`** (not class)

All referenced schema files must be registered as `<AdditionalFiles>`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas\**\*.json" />
</ItemGroup>
```

### 2. CLI Tool (corvusjson)

```powershell
# Install the CLI tool
dotnet tool install --global Corvus.Json.Cli

# Generate types
corvusjson jsonschema <schema-path> `
  --rootNamespace MyApp.Models `
  --outputRootTypeName PersonSchema `
  --outputPath Generated/ `
  --engine V5
```

The command is `corvusjson jsonschema` (registered in `CliAppFactory.cs`).

**Key CLI options** (defined in `src/Corvus.Json.Cli.Core/GenerateCommand.cs`):
- `--rootNamespace` — C# namespace for generated types
- `--outputRootTypeName` — name of the root generated type
- `--outputPath` — output directory
- `--engine` — `V4` or `V5` (default: V5 for `corvusjson`, V4 for legacy `generatejsonschematypes`)
- `--assertFormat` — whether format validation asserts (default: true)
- `--codeGenerationMode` — `TypeGeneration`, `SchemaEvaluationOnly`, or `Both`

> **IMPORTANT:** Never invent option names. Verify against `GenerateCommand.cs`.

### Legacy CLI

The `generatejsonschematypes` command (package: `Corvus.Json.CodeGenerator`) still works as a shim but defaults to the V4 engine.

## MSBuild Properties

All read from the source generator (`IncrementalSourceGenerator.cs` lines 111-196):

| Property | Effect | Default |
|----------|--------|---------|
| `CorvusTextJsonFallbackVocabulary` | Default vocabulary for schemas without `$schema` | Draft202012 |
| `CorvusTextJsonOptionalAsNullable` | Set to `NullOrUndefined` to treat optional as nullable | off |
| `CorvusTextJsonAddExplicitUsings` | Add explicit using directives | true |
| `CorvusTextJsonUseImplicitOperatorString` | Generate implicit string conversion operators | true |
| `CorvusTextJsonUseOptionalNameHeuristics` | Enable optional naming heuristics | true |
| `CorvusTextJsonAlwaysAssertFormat` | Assert format validation | true |
| `CorvusTextJsonDefaultAccessibility` | `Public` or `Internal` | Public |
| `CorvusTextJsonDisabledNamingHeuristics` | Semicolon-separated list of heuristics to disable | — |
| `EmitCompilerGeneratedFiles` | Write generated `.g.cs` to `obj/` for inspection | false |

## Naming Heuristics

The generator uses 15 heuristics (priorities 1–11000) to derive C# type names from JSON Schema. Key ones:

1. **`title`** — uses the schema's `title` keyword
2. **`$anchor`** / **`$id`** — uses the fragment or filename
3. **Property name** — derives from the parent property name
4. **`$ref` path** — derives from the `$ref` URI path/fragment

Collisions are resolved by appending a type-kind suffix: `Object`, `Array`, `String`, `Number`, `Boolean`.

Optional heuristics can be disabled via `CorvusTextJsonDisabledNamingHeuristics`.

## Config File

For complex generation scenarios, use a config file (`corvusjson config`):

```json
{
  "typesToGenerate": [
    {
      "schemaFile": "schemas/main.json",
      "rootTypeName": "MainType"
    }
  ],
  "additionalFiles": [
    { "canonicalUri": "https://example.com/shared.json", "contentPath": "schemas/shared.json" }
  ],
  "namedTypes": [
    { "reference": "#/definitions/Foo", "dotnetTypeName": "FooType", "dotnetNamespace": "MyApp.Sub" }
  ],
  "namespaces": {
    "https://example.com/schemas/": "MyApp.ExternalModels"
  }
}
```

## Inspecting Generated Output

```powershell
# Enable generated file emission in the .csproj
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>

# After building, find generated files at:
# obj\{Config}\{TFM}\generated\Corvus.Text.Json.SourceGenerator\
```

## Common Pitfalls

- **Missing `<AdditionalFiles>`**: Referenced schemas not in `<AdditionalFiles>` are invisible to the source generator.
- **Stale generated files**: After schema changes, clean and rebuild. Old generated files may cause compilation errors.
- **No glob `<Compile>` items**: This repo requires every `.cs` file to be explicitly listed in the `.csproj`. CLI-generated output directories need corresponding `<Compile Include="..." />` entries.

## Cross-References
- For the keyword/vocabulary system that drives generation, see `corvus-keywords-and-validation`
- For evaluator-only generation, see `corvus-standalone-evaluator`
- For building and testing generated code, see `corvus-build-and-test`
