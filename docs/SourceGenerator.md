# Source Generator Code Generation

## Overview

`Corvus.Text.Json.SourceGenerator` is a Roslyn incremental source generator that produces strongly-typed C# models from JSON Schema at build time. Simply annotate a `partial struct` with `[JsonSchemaTypeGenerator]` pointing at a schema file, and the generator produces a complete implementation — type-safe property accessors, validation, serialization, implicit conversions, and mutable builder support — all without leaving your IDE.

The source generator produces identical output to the [`generatejsonschematypes` CLI tool](/docs/code-generator.html), but runs automatically during the build with full IntelliSense support as you type.

> **Tip:** If you only need validation and annotation collection without the full type system, you can also generate a [standalone schema evaluator](SchemaEvaluator.md) by setting `EmitEvaluator = true` on the attribute.

## Installation

Add both the source generator and the core runtime package to your project:

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Corvus.Text.Json" Version="5.0.0" />
```

## Quick Start

### 1. Create a JSON Schema

Create a schema file in your project, for example `Schemas/person.json`:

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "required": ["name"],
    "properties": {
        "name": { "type": "string", "minLength": 1 },
        "age": { "type": "integer", "format": "int32", "minimum": 0 }
    }
}
```

### 2. Register the schema as an AdditionalFile

In your `.csproj`, register the schema file so the source generator can find it:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas/person.json" />
</ItemGroup>
```

### 3. Declare a partial struct

Annotate a `partial struct` with `[JsonSchemaTypeGenerator]`, pointing at the schema file relative to the `.cs` file:

```csharp
using Corvus.Text.Json;

namespace MyApp.Models;

[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;
```

### 4. Build and use

The generator fills in the struct at build time. You get full IntelliSense immediately:

```csharp
using var doc = ParsedJsonDocument<Person>.Parse(
    """{"name":"Alice","age":30}""");
Person person = doc.RootElement;

string name = (string)person.Name;      // "Alice"
int age = (int)person.Age;              // 30
bool valid = person.EvaluateSchema();   // true
```

## The JsonSchemaTypeGenerator Attribute

The attribute accepts a schema location and an optional rebase flag:

```csharp
[JsonSchemaTypeGenerator(string location, bool rebaseToRootPath = false)]
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `location` | `string` | **Yes** | — | Path to the JSON Schema file, relative to the `.cs` file declaring the struct. Can include a JSON Pointer fragment to target a specific definition (e.g., `"schema.json#/$defs/Address"`). |
| `rebaseToRootPath` | `bool` | No | `false` | When `true` (and `location` includes a JSON Pointer), the document is rebased as if the element at the pointer is the root. This affects how `$ref` references within the extracted subtree are resolved. |

### Targeting a specific definition

If your schema file contains multiple definitions, use a JSON Pointer fragment to select one:

```csharp
[JsonSchemaTypeGenerator("Schemas/api.json#/$defs/Address")]
public readonly partial struct Address;

[JsonSchemaTypeGenerator("Schemas/api.json#/$defs/PhoneNumber")]
public readonly partial struct PhoneNumber;
```

### Rules for the target struct

- Must be a **`partial struct`** (not a class, record, or interface)
- Must be declared inside a **namespace** (file-scoped or block-scoped)
- The struct name becomes the .NET type name for the root schema type
- The struct's accessibility (`public`, `internal`) is preserved in the generated code

## Registering Schema Files

The source generator discovers schemas through the MSBuild `AdditionalFiles` mechanism. Every JSON Schema file your types reference — including files referenced via `$ref` — must be registered:

```xml
<ItemGroup>
  <!-- The schema your type uses directly -->
  <AdditionalFiles Include="Schemas/person.json" />

  <!-- Schemas referenced via $ref -->
  <AdditionalFiles Include="Schemas/address.json" />
  <AdditionalFiles Include="Schemas/phone.json" />
</ItemGroup>
```

You can use glob patterns to include all schemas in a directory:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas/**/*.json" />
</ItemGroup>
```

All registered JSON files are loaded into a pre-populated document resolver at build time. When the generator encounters a `$ref`, it resolves it against this resolver — no file system or network access occurs during generation.

## MSBuild Configuration Properties

Control the generator's behaviour with MSBuild properties in your `.csproj`:

```xml
<PropertyGroup>
  <CorvusTextJsonFallbackVocabulary>Draft202012</CorvusTextJsonFallbackVocabulary>
  <CorvusTextJsonOptionalAsNullable>NullOrUndefined</CorvusTextJsonOptionalAsNullable>
  <CorvusTextJsonAlwaysAssertFormat>true</CorvusTextJsonAlwaysAssertFormat>
</PropertyGroup>
```

| Property | Default | Description |
|----------|---------|-------------|
| `CorvusTextJsonFallbackVocabulary` | `Draft202012` | Fallback schema vocabulary when the `$schema` keyword is omitted. Values: `Draft4`, `Draft6`, `Draft7`, `Draft201909`, `Draft202012`, `OpenApi30`. |
| `CorvusTextJsonOptionalAsNullable` | — | When set to `NullOrUndefined`, optional properties generate as .NET nullable types (`T?`). JSON `null` or missing values map to C# `null`. When omitted, optional properties use the full type and you check for `Undefined` explicitly. |
| `CorvusTextJsonAlwaysAssertFormat` | `true` | When `true`, the `format` keyword is enforced as a validation assertion. When `false`, it is treated as an annotation only. |
| `CorvusTextJsonUseImplicitOperatorString` | `true` | When `true`, conversion operators to `string` are implicit. When `false`, explicit casting is required. Explicit casts make string allocations more visible. |
| `CorvusTextJsonAddExplicitUsings` | `true` | When `true`, generated files include `using` statements for standard implicit usings. Disable if your project already uses implicit usings and you want cleaner output. |
| `CorvusTextJsonUseOptionalNameHeuristics` | `true` | When `true`, applies naming heuristics to infer idiomatic C# names from JSON Schema property names and definitions. |
| `CorvusTextJsonDisabledNamingHeuristics` | `DocumentationNameHeuristic` | Semicolon-separated list of specific naming heuristics to disable (e.g., `DocumentationNameHeuristic;PathNameHeuristic`). |

## What Gets Generated

For each `[JsonSchemaTypeGenerator]` attribute, the generator produces a complete implementation of the partial struct:

- **Type-safe property accessors** for every property defined in the schema
- **Validation** via `EvaluateSchema()` with full draft 2020-12 (or earlier draft) support
- **Parsing** from strings, byte arrays, streams, and sequences via `ParsedJsonDocument<T>`
- **Serialization** via `WriteTo(Utf8JsonWriter)` and `ToString()`
- **Implicit conversions** to and from .NET primitive types
- **Mutable builder** via `CreateBuilder(JsonWorkspace)` for in-place modification
- **Pattern matching** via `Match()` for `oneOf`/`anyOf` discriminated unions
- **Equality** operators and `GetHashCode()`

All generated types are `readonly struct` values — they are lightweight indexes into pooled JSON data, not heap-allocated objects.

## Inspecting Generated Code

By default, generated code exists only in memory. To write it to disk for inspection:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

Generated files appear under:

```
obj/Debug/{TargetFramework}/generated/Corvus.Text.Json.SourceGenerator/
  Person.g.cs
  Person.Mutable.g.cs
  ...
```

This is useful for debugging, code review, or understanding how the schema maps to C#.

## Incremental Generation

The source generator is fully incremental — it only regenerates code when something relevant changes:

- Modifying a `.json` schema file triggers regeneration for types using that schema
- Changing a C# file that declares a `[JsonSchemaTypeGenerator]` attribute triggers regeneration for that type only
- Changes to unrelated C# files (comments, other classes) do not trigger regeneration

This keeps build times fast even in large projects with many generated types.

## Source Generator vs. CLI Tool

Both the source generator and the [`generatejsonschematypes` CLI tool](/docs/code-generator.html) use the same code generation engine and produce identical output:

| Aspect | Source Generator | CLI Tool |
|--------|-----------------|----------|
| **When it runs** | At build time, automatically | On demand, from the command line |
| **Triggered by** | `[JsonSchemaTypeGenerator]` attribute | Explicit `generatejsonschematypes` command |
| **Output location** | In-memory (or `obj/` with `EmitCompilerGeneratedFiles`) | Any directory you specify |
| **IDE integration** | Full IntelliSense as you type | IntelliSense after generation + build |
| **Configuration** | MSBuild properties in `.csproj` | CLI arguments or JSON config file |
| **Schema discovery** | `<AdditionalFiles>` in `.csproj` | Command-line arguments or config file |
| **Best for** | Day-to-day development | CI pipelines, inspecting output, multi-schema configs |

### When to use the source generator

- You want **zero-configuration** code generation that happens automatically during the build
- You prefer generated code to stay **out of source control**
- You want **instant IntelliSense** as you edit schema files
- This is the recommended approach for most projects

### When to use the CLI tool

- You want to **inspect or version-control** the generated code
- You have a **multi-schema configuration file** with shared settings
- You need to **pre-generate** types in a CI/CD pipeline before the build
- You want to **compare** V4 vs V5 output during migration

## Troubleshooting

| Symptom | Cause | Solution |
|---------|-------|----------|
| Generated types not appearing | Schema file not registered | Add `<AdditionalFiles Include="..." />` to your `.csproj` |
| `$ref` resolution errors | Referenced schema not in `AdditionalFiles` | Register all files in the `$ref` chain as `AdditionalFiles` |
| Type name collisions | Multiple schemas define types with the same name | Use `[JsonSchemaTypeGenerator("schema.json#/$defs/Specific")]` to target a specific definition |
| Unexpected property names | Naming heuristic choosing a poor name | Disable the heuristic with `CorvusTextJsonDisabledNamingHeuristics` |
| Stale generated code | Incremental cache not invalidated | Clean and rebuild (`dotnet clean && dotnet build`) |
