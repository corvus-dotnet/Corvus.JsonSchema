# CLI Code Generation

## Overview

`generatejsonschematypes` is a .NET global tool that generates strongly-typed C# models from JSON Schema files. It produces the same output as the Roslyn incremental source generator, but runs ahead of time from the command line — making it suitable for CI/CD pipelines, pre-generation workflows, and scenarios where you need to inspect or version-control the generated code.

The tool supports all major JSON Schema drafts (Draft 4, 6, 7, 2019-09, and 2020-12), OpenAPI 3.0, and YAML input.

> **Tip:** If you only need validation and annotation collection without the full type system, use `--codeGenerationMode SchemaEvaluationOnly` to generate a [standalone schema evaluator](SchemaEvaluator.md).

## Installation

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

Or as a local tool:

```bash
dotnet new tool-manifest
dotnet tool install Corvus.Json.CodeGenerator
```

## Quick Start

Generate types from a schema file:

```bash
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/
```

This reads `person.json`, generates one or more C# files into `Generated/`, and uses `MyApp.Models` as the root namespace.

## Commands

### `generate` (Default)

Generate C# types from a single schema file. This is the default command — you can omit the command name.

```bash
generatejsonschematypes <schemaFile> [OPTIONS]
```

**Required arguments:**
- `<schemaFile>` — Path to the JSON Schema file
- `--rootNamespace` — Root namespace for generated types

**Common options:**

| Option | Default | Description |
|--------|---------|-------------|
| `--outputPath` | — | Directory for generated `.cs` files |
| `--outputRootTypeName` | Derived | Override the .NET type name for the root type. If omitted, the name is derived automatically using naming heuristics (see [Naming Heuristics](#naming-heuristics) below). |
| `--rootPath` | — | JSON Pointer to the root element (e.g., `#/definitions/Person`) |
| `--rebaseToRootPath` | `false` | Rebase the document as if rooted at `--rootPath` |
| `--useSchema` | Auto-detect | Fallback schema draft: `Draft4`, `Draft6`, `Draft7`, `Draft201909`, `Draft202012`, `OpenApi30` |
| `--assertFormat` | `true` | Enforce `format` keyword as a validation assertion |
| `--optionalAsNullable` | `None` | How to handle optional properties: `None` or `NullOrUndefined` |
| `--useImplicitOperatorString` | `false` | Use implicit (vs explicit) conversion to `string` |
| `--yaml` | `false` | Enable YAML schema support |
| `--addExplicitUsings` | `false` | Include explicit `using` statements for standard implicit usings |
| `--engine` | `V5` | Code generation engine: `V5` (Corvus.Text.Json) or `V4` (legacy Corvus.Json.ExtendedTypes) |
| `--codeGenerationMode` | `TypeGeneration` | `TypeGeneration` (types only), `SchemaEvaluationOnly` ([standalone evaluator](SchemaEvaluator.md) only), or `Both` |
| `--outputMapFile` | — | Write a JSON map of all generated files |

**Examples:**

```bash
# Generate with a custom root type name
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/ \
    --outputRootTypeName Person

# Generate from a specific definition within a schema
generatejsonschematypes Schemas/api.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/ \
    --rootPath "#/definitions/Address" \
    --rebaseToRootPath

# Treat optional properties as nullable
generatejsonschematypes Schemas/config.json \
    --rootNamespace MyApp.Config \
    --outputPath Generated/ \
    --optionalAsNullable NullOrUndefined
```

### `config`

Generate types from a configuration file that specifies multiple schemas and shared settings:

```bash
generatejsonschematypes config myconfig.json [--engine V5]
```

#### Example configuration file

```json
{
    "rootNamespace": "MyApp.Models",
    "outputPath": "Generated/",
    "useSchema": "Draft202012",
    "assertFormat": true,
    "optionalAsNullable": "None",
    "typesToGenerate": [
        {
            "schemaFile": "Schemas/person.json",
            "outputRootTypeName": "Person"
        },
        {
            "schemaFile": "Schemas/order.json",
            "outputRootTypeName": "Order",
            "outputRootNamespace": "MyApp.Models.Orders"
        }
    ],
    "additionalFiles": [
        {
            "canonicalUri": "https://example.com/schemas/address.json",
            "contentPath": "Schemas/address.json"
        }
    ],
    "namedTypes": [
        {
            "reference": "https://example.com/schemas/person.json#/$defs/Name",
            "dotnetTypeName": "PersonName",
            "dotnetNamespace": "MyApp.Models"
        }
    ],
    "namespaces": {
        "https://example.com/schemas/": "MyApp.ExternalModels"
    }
}
```

The `config` command is ideal when you have multiple schemas to generate from, shared `$ref` dependencies, or you want to explicitly name generated types.

#### Root-level properties

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `rootNamespace` | **Yes** | — | The default .NET namespace for all generated types. Individual schemas can override this with `outputRootNamespace`. |
| `typesToGenerate` | **Yes** | — | Array of schemas to process. Each entry specifies a schema file and optional overrides (see below). |
| `outputPath` | No | — | Directory where generated `.cs` files are written. |
| `outputMapFile` | No | — | Path for a JSON manifest listing every generated file, useful for build systems that need to track outputs. |
| `useSchema` | No | `Draft202012` | Fallback schema draft when vocabulary analysis cannot determine the draft from the `$schema` keyword. Values: `Draft4`, `Draft6`, `Draft7`, `Draft201909`, `Draft202012`, `OpenApi30`. |
| `assertFormat` | No | `true` | When `true`, the `format` keyword is enforced as a validation assertion. When `false`, `format` is treated as an annotation only (per the JSON Schema specification). |
| `optionalAsNullable` | No | `None` | Controls how optional properties are represented in generated types. `None`: optional properties use the same type as required properties — you check for `Undefined` explicitly. `NullOrUndefined`: optional properties generate as .NET nullable types (`T?`), and both JSON `null` and missing properties map to C# `null`. |
| `additionalFiles` | No | — | Pre-load external schema files so `$ref` references can resolve without network access (see below). |
| `namedTypes` | No | — | Explicitly assign .NET names to specific schema definitions that would otherwise get auto-generated names (see below). |
| `namespaces` | No | — | A dictionary mapping schema base URIs to .NET namespaces. Any schema whose canonical URI starts with a given key is generated into the corresponding namespace. For example, `"https://example.com/schemas/": "MyApp.External"` places all schemas under that URI prefix into `MyApp.External`. |
| `disableOptionalNameHeuristics` | No | `false` | When `true`, disables all optional naming heuristics at once. Cannot be combined with `disabledNamingHeuristics`. |
| `disabledNamingHeuristics` | No | — | An array of specific naming heuristic names to disable. Use `generatejsonschematypes listNameHeuristics` to see available names. Cannot be combined with `disableOptionalNameHeuristics`. |
| `useImplicitOperatorString` | No | `false` | When `true`, conversion operators to `string` are implicit rather than explicit. Use with care — implicit conversions can cause unintended string allocations. |
| `useUnixLineEndings` | No | `false` | When `true`, generated files use Unix line endings (`\n`) instead of Windows (`\r\n`). |
| `supportYaml` | No | `false` | When `true`, enables YAML support. Schema and document files can be YAML, JSON, or a mixture. |
| `addExplicitUsings` | No | `false` | When `true`, generated files include `using` statements for standard implicit usings. Enable this if your project does not use implicit usings. |

#### `typesToGenerate` entries

Each entry in `typesToGenerate` specifies one schema to process:

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `schemaFile` | **Yes** | — | Path to the JSON Schema file to process, relative to the config file. |
| `outputRootTypeName` | No | Derived | The .NET type name for the root type generated from this schema. If omitted, the name is derived automatically using the standard naming heuristics — typically from the schema's `title` keyword, `$id` URI fragment, or the schema file name (see [Naming Heuristics](#naming-heuristics)). |
| `outputRootNamespace` | No | `rootNamespace` | Override the .NET namespace for this schema's root type. Useful when different schemas should go into different namespaces. |
| `rootPath` | No | — | A JSON Pointer within the schema document to use as the root type. For example, `#/$defs/Address` generates types starting from the `Address` definition rather than the document root. |
| `rebaseToRootPath` | No | `false` | When `true` (and `rootPath` is set), the document is rebased as if the element at `rootPath` were the document root. This affects how `$ref` references within the extracted subtree are resolved. |

#### `additionalFiles` entries

Pre-load schema files that other schemas reference via `$ref`. This avoids network calls during generation and ensures deterministic, reproducible builds — particularly important in CI/CD environments:

| Property | Required | Description |
|----------|----------|-------------|
| `canonicalUri` | **Yes** | The canonical URI that other schemas use in their `$ref` to reference this file. Must match the URI used in `$ref` exactly. |
| `contentPath` | **Yes** | Local file path to the schema, relative to the config file. |

For example, if `person.json` contains `"$ref": "https://example.com/schemas/address.json"`, add an entry with that URI pointing to your local copy of the address schema.

#### `namedTypes` entries

By default, the generator derives .NET type names from the schema structure. Use `namedTypes` to override specific types with explicit names — useful when the auto-generated name is awkward or when you need a type to live in a specific namespace:

| Property | Required | Description |
|----------|----------|-------------|
| `reference` | **Yes** | The fully-qualified schema reference (base URI + JSON Pointer fragment), e.g. `https://example.com/schemas/person.json#/$defs/Name`. |
| `dotnetTypeName` | **Yes** | The .NET type name to use, e.g. `PersonName`. |
| `dotnetNamespace` | No | When set, the type is generated directly in this namespace rather than as a nested type of its containing schema type. |

### `validateDocument`

Validate a JSON or YAML document against a schema:

```bash
generatejsonschematypes validateDocument Schemas/person.json data.json
```

Output includes pass/fail status for each validation rule, with file path, line, column, and the schema evaluation location for failures.

### `listNameHeuristics`

List all available naming heuristics. Some are optional and can be disabled with `--disableNamingHeuristic`:

```bash
generatejsonschematypes listNameHeuristics
```

### `version`

Display the tool version and build information:

```bash
generatejsonschematypes version
```

## Source Generator vs. CLI Tool

Both produce identical output — the same `readonly struct` types with the same property accessors, validation, serialization, and implicit conversions. The difference is when and how they run:

| Aspect | Source Generator | CLI Tool |
|--------|-----------------|----------|
| **When** | At build time, automatically | On demand, from the command line |
| **Triggered by** | `[JsonSchemaTypeGenerator]` attribute | Explicit `generatejsonschematypes` invocation |
| **Output location** | `obj/` (not checked in) | Any directory (can be checked in) |
| **IDE integration** | Full IntelliSense as you type | IntelliSense after generation + build |
| **Best for** | Most projects | CI pipelines, inspecting output, multi-schema configs |

### When to Use the CLI Tool

- You want to **inspect or version-control** the generated code
- You have a **multi-schema configuration** file with shared settings
- You need to **pre-generate** types in a CI/CD pipeline before the build
- You want to **validate** documents against schemas from the command line
- You are migrating from V4 and want to compare V4 vs V5 output

### When to Use the Source Generator

- You want **zero-configuration** code generation at build time
- You prefer generated code to stay out of source control
- You want **instant IntelliSense** as you edit schema files

## Generated Output

The tool generates `readonly struct` types that are thin wrappers over pooled JSON data. For a schema like:

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

The tool generates:

- **Type-safe property accessors**: `person.Name` returns a strongly-typed string value
- **Validation**: `person.EvaluateSchema()` validates against the full schema
- **Implicit conversions**: `(string)person.Name` extracts the .NET value
- **Mutable builder**: `person.CreateBuilder(workspace)` creates a mutable copy
- **Pattern matching**: `Match()` methods for `oneOf`/`anyOf` discriminated unions
- **Serialization**: `WriteTo(Utf8JsonWriter)` for zero-allocation output

## Schema Draft Support

| Draft | `$schema` URI | `--useSchema` Value |
|-------|--------------|-------------------|
| Draft 4 | `http://json-schema.org/draft-04/schema` | `Draft4` |
| Draft 6 | `http://json-schema.org/draft-06/schema` | `Draft6` |
| Draft 7 | `http://json-schema.org/draft-07/schema` | `Draft7` |
| Draft 2019-09 | `https://json-schema.org/draft/2019-09/schema` | `Draft201909` |
| Draft 2020-12 | `https://json-schema.org/draft/2020-12/schema` | `Draft202012` |
| OpenAPI 3.0 | (custom vocabulary) | `OpenApi30` |

The tool auto-detects the schema draft from the `$schema` keyword. Use `--useSchema` only as a fallback when the keyword is missing.

## Output Map File

Use `--outputMapFile` to generate a JSON manifest of all generated files:

```bash
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/ \
    --outputMapFile generated.map.json
```

This is useful for build systems that need to track which files were generated.

## Naming Heuristics

When `--outputRootTypeName` is not specified, the tool applies a chain of **naming heuristics** to derive an idiomatic C# type name from the schema. Heuristics are tried in priority order (lowest number first) and the first match wins. The same heuristics are also applied to nested types, sub-schemas, and property types throughout the generated code.

### How the root type name is determined

The tool tries these sources in order:

1. **Explicit name** (`--outputRootTypeName` or `outputRootTypeName` in config) — used as-is, no heuristics applied
2. **Custom keywords** (priority 100) — the `title` keyword or the last segment of the `$id` URI
3. **Schema reference** (priority 1000) — the last segment of the JSON Pointer fragment (e.g., `#/$defs/Address` → `Address`) or the schema file name without extension (e.g., `person.json` → `Person`)
4. **Documentation** (priority 1500, optional) — the schema `description` if it is short (under 64 characters) and unique within its parent
5. **Fallback** — a name derived from the file path

For example, given this schema in `Schemas/person.json`:

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "Person",
    "type": "object",
    "properties": {
        "name": { "type": "string" }
    }
}
```

The root type name is `Person` (from the `title` keyword). If `title` were absent, it would be `Person` (from the file name `person.json`).

### Full heuristic list

Heuristics marked **optional** can be disabled with `--disableOptionalNamingHeuristics` (disables all optional) or `--disableNamingHeuristic <name>` (disables one at a time). Non-optional heuristics are always active.

| Priority | Heuristic | Optional | What it does |
|----------|-----------|----------|-------------|
| 1 | `WellKnownTypeNameHeuristic` | No | Maps well-known schema patterns to built-in types (e.g., the "any" schema maps to `JsonElement`). |
| 2 | `SimpleCoreTypeNameHeuristic` | No | Maps simple schemas with a single core type and optional `format` to global types: `JsonString`, `JsonInt32`, `JsonUuid`, `JsonBoolean`, etc. These types are shared — no per-schema code is generated. |
| 2 | `BuiltIn*TypeNameHeuristic` | No | A family of heuristics (`BuiltInStringTypeNameHeuristic`, `BuiltInNumberTypeNameHeuristic`, etc.) that map bare `"type": "string"`, `"type": "number"`, `"type": "integer"`, `"type": "boolean"`, `"type": "null"`, `"type": "object"`, and `"type": "array"` schemas to their respective global types. |
| 100 | `CustomKeywordNameHeuristic` | No | Extracts the name from the `title` keyword or the last segment of the `$id` URI. This is the most common source for root type names. |
| 1000 | `BaseSchemaNameHeuristic` | No | For root or `$defs`-level schemas: extracts the name from the JSON reference fragment (e.g., `#/$defs/Address` → `Address`) or the schema file name (e.g., `address.json` → `Address`). |
| 1500 | `DocumentationNameHeuristic` | **Yes** | Uses the schema `description` as the type name if it is short enough (under 64 characters) and unique within its parent. |
| 1500 | `DefaultValueNameHeuristic` | **Yes** | For schemas with only a `default` keyword: generates names like `DefaultValueTrue` or `DefaultValueActive`. |
| 1550 | `RequiredPropertyNameHeuristic` | **Yes** | For nested schemas: uses `Required` + property names, e.g., a schema requiring `id` and `name` becomes `RequiredIdAndName`. |
| 1600 | `ConstPropertyNameHeuristic` | **Yes** | For nested schemas with `const` properties: uses `With` + const values, e.g., `WithStatusActive`. |
| 1600 | `SingleTypeArrayNameHeuristic` | **Yes** | For array schemas: appends `Array` to the item type name, e.g., an array of `Item` becomes `ItemArray`. |
| 10000 | `SubschemaNameHeuristic` | No | For inline nested schemas: extracts the name from the JSON reference path segment. |
| 11000 | `PathNameHeuristic` | No | Final fallback for nested schemas: extracts the name from the last path segment of the JSON reference. |

### Collision resolution

When a derived name collides with its parent type or a sibling, the tool automatically appends a suffix based on the schema kind (`Object`, `Array`, `String`, etc.). For example, if both a parent and child would be named `User`, the child becomes `UserObject`.

### Disabling heuristics

```bash
# Disable a specific heuristic by name
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/ \
    --disableNamingHeuristic DocumentationNameHeuristic

# Disable all optional heuristics at once
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/ \
    --disableOptionalNamingHeuristics
```

Run `generatejsonschematypes listNameHeuristics` to see all available heuristics and which ones are optional.
