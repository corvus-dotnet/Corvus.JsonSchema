# Dynamic Schema Validation

## Overview

`Corvus.Text.Json.Validator` is a library for dynamically loading, compiling, and validating JSON documents against JSON Schema at runtime. Unlike the build-time source generator, the Validator compiles schemas on the fly using Roslyn, making it ideal for scenarios where schemas are not known at compile time — such as schema registries, configuration validation, or user-supplied schemas.

It supports all major JSON Schema drafts (Draft 4, 6, 7, 2019-09, and 2020-12), plus OpenAPI 3.0 and Corvus custom vocabulary extensions.

## Key Features

- **Runtime Schema Compilation**: Dynamically generates and compiles strongly-typed validators from JSON Schema using Roslyn
- **Multiple Schema Drafts**: Supports Draft 4, 6, 7, 2019-09, and 2020-12 with automatic draft detection
- **Schema Caching**: Compiled schemas are cached so repeated validations against the same schema are fast
- **Multiple Input Formats**: Validate from strings, byte arrays, streams, `ReadOnlyMemory`, `ReadOnlySequence`, or pre-parsed `JsonElement`
- **Detailed Diagnostics**: Optional results collector provides hierarchical validation failures with schema locations and error messages
- **External Schema Resolution**: Resolve `$ref` references via file system, HTTP, or pre-loaded additional schema files

## Installation

```bash
dotnet add package Corvus.Text.Json.Validator
```

## Quick Start

The main entry point is the `JsonSchema` struct. Load a schema, then validate JSON documents against it:

```csharp
using Corvus.Text.Json.Validator;

// Load a schema from a file
JsonSchema schema = JsonSchema.FromFile("Schemas/person.json");

// Validate a JSON string
bool isValid = schema.Validate("""{"name": "Alice", "age": 30}""");
```

## Loading Schemas

`JsonSchema` provides several factory methods for loading schemas from different sources:

### From a File

```csharp
JsonSchema schema = JsonSchema.FromFile("path/to/schema.json");
```

The file's directory is used as the base for resolving relative `$ref` references. The canonical URI is extracted from the `$id` property in the schema.

### From a String

```csharp
string schemaText = """
    {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/person",
        "type": "object",
        "required": ["name"],
        "properties": {
            "name": { "type": "string" },
            "age": { "type": "integer", "minimum": 0 }
        }
    }
    """;

JsonSchema schema = JsonSchema.FromText(schemaText);
```

If the schema does not contain an `$id` property, you must provide a canonical URI explicitly:

```csharp
JsonSchema schema = JsonSchema.FromText(schemaText, canonicalUri: "https://example.com/person");
```

### From a Stream

```csharp
using FileStream stream = File.OpenRead("schema.json");
JsonSchema schema = JsonSchema.FromStream(stream);
```

### From a URI

Resolve a schema by its canonical URI. The schema is fetched from the file system or via HTTP, depending on the URI scheme:

```csharp
JsonSchema schema = JsonSchema.FromUri("https://example.com/schemas/person.json");

// Or use the shorthand alias
JsonSchema schema = JsonSchema.From("file:///C:/schemas/person.json");
```

## Validating Documents

Once you have a `JsonSchema`, call `Validate()` with the JSON document in any supported format:

```csharp
// From a string
bool valid = schema.Validate("""{"name": "Alice"}""");

// From UTF-8 bytes
ReadOnlyMemory<byte> utf8 = Encoding.UTF8.GetBytes("""{"name": "Alice"}""");
bool valid = schema.Validate(utf8);

// From a stream (e.g., HTTP request body)
bool valid = schema.Validate(requestStream);

// From a pre-parsed JsonElement
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
bool valid = schema.Validate(in doc.RootElement);
```

All overloads return `true` if the document is valid, `false` otherwise.

## Detailed Validation Results

For diagnostic output, pass an `IJsonSchemaResultsCollector` to `Validate()`. The collector records every validation step, including the schema location and evaluation path for failures:

```csharp
using JsonSchemaResultsCollector collector =
    JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

bool valid = schema.Validate(json, collector);

if (!valid)
{
    foreach (var result in collector.EnumerateResults())
    {
        if (!result.IsMatch)
        {
            Console.WriteLine($"  Failed: {result}");
        }
    }
}
```

### Results Levels

| Level | What It Collects |
|-------|-----------------|
| `Flag` | Pass/fail only — no diagnostic details |
| `Basic` | Failure messages without location information |
| `Detailed` | Failure messages with schema location and evaluation path |
| `Verbose` | All evaluation steps, including successful validations |

Use `Flag` for maximum performance when you only need a boolean result. Use `Detailed` or `Verbose` when diagnosing schema violations.

## Configuration Options

Pass a `JsonSchema.Options` instance to any factory method to control schema compilation behaviour:

```csharp
var options = new JsonSchema.Options(
    alwaysAssertFormat: true,
    allowFileSystemAndHttpResolution: true,
    fallbackVocabulary: null,
    additionalSchemaFiles: new[]
    {
        new AdditionalSchemaFile(
            canonicalUri: "https://example.com/shared/address.json",
            filePath: "Schemas/address.json")
    });

JsonSchema schema = JsonSchema.FromFile("person.json", options: options);
```

### Available Options

| Option | Default | Description |
|--------|---------|-------------|
| `alwaysAssertFormat` | `true` | When `true`, the `format` keyword is enforced as a validation assertion. When `false`, it is treated as an annotation only (per the JSON Schema specification). |
| `allowFileSystemAndHttpResolution` | `true` | Enable resolution of `$ref` references via `file://` and `http://`/`https://` URIs. Set to `false` to restrict resolution to pre-loaded schemas only. |
| `fallbackVocabulary` | Draft 2020-12 | The JSON Schema vocabulary to use when the schema does not include a `$schema` keyword. |
| `additionalSchemaFiles` | `null` | Pre-load external schema files for `$ref` resolution. Each entry maps a canonical URI to a local file path. |
| `hostAssembly` | Entry assembly | The assembly context used for resolving metadata references during dynamic compilation. |

## Pre-loading Referenced Schemas

When your schema uses `$ref` to reference other schemas, you can pre-load them with `AdditionalSchemaFile`:

```csharp
var options = new JsonSchema.Options(
    additionalSchemaFiles: new[]
    {
        new AdditionalSchemaFile(
            "https://example.com/schemas/address.json",
            "Schemas/address.json"),
        new AdditionalSchemaFile(
            "https://example.com/schemas/phone.json",
            "Schemas/phone.json")
    });

JsonSchema schema = JsonSchema.FromFile("Schemas/person.json", options: options);
```

This avoids network calls for referenced schemas and ensures deterministic builds. It is particularly useful in CI/CD environments where external resolution may be unreliable or disallowed.

## Schema Caching

Compiled schemas are cached automatically by their canonical URI and `alwaysAssertFormat` flag. Subsequent calls to any `From*` method with the same URI return the cached validator without recompilation:

```csharp
// First call: compiles the schema (~100ms)
JsonSchema schema1 = JsonSchema.FromFile("person.json");

// Second call: returns cached validator (sub-millisecond)
JsonSchema schema2 = JsonSchema.FromFile("person.json");
```

To force recompilation (for example, after updating a schema file), pass `refreshCache: true`:

```csharp
JsonSchema schema = JsonSchema.FromFile("person.json", refreshCache: true);
```

## How It Works

Under the hood, the Validator uses the same code generation engine as the source generator and CLI tool:

1. **Parse** the JSON Schema document
2. **Resolve** all `$ref` references using registered document resolvers
3. **Generate** C# source code for strongly-typed validators (identical output to `generatejsonschematypes`)
4. **Compile** the generated code using Roslyn (`Microsoft.CodeAnalysis.CSharp`) into an in-memory assembly
5. **Load** the compiled assembly and create a validation pipeline
6. **Cache** the pipeline for subsequent validations against the same schema

This means the Validator produces the exact same validation logic as build-time source generation — the only difference is that compilation happens at runtime.

## Supported JSON Schema Drafts

| Draft | `$schema` URI |
|-------|--------------|
| Draft 4 | `http://json-schema.org/draft-04/schema` |
| Draft 6 | `http://json-schema.org/draft-06/schema` |
| Draft 7 | `http://json-schema.org/draft-07/schema` |
| Draft 2019-09 | `https://json-schema.org/draft/2019-09/schema` |
| Draft 2020-12 | `https://json-schema.org/draft/2020-12/schema` |

The Validator also supports OpenAPI 3.0 schema vocabulary and Corvus custom extensions.

## Use Cases

- **Schema Registry Validation**: Validate messages against schemas fetched from a central registry at runtime
- **Configuration Validation**: Validate user-supplied configuration files against application-defined schemas
- **API Gateway Validation**: Validate HTTP request/response bodies against OpenAPI schemas
- **Testing**: Validate test fixtures against schemas without pre-generating types
- **Dynamic Schema Selection**: Choose schemas based on runtime conditions (e.g., document version, content type)
