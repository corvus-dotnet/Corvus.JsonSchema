# Standalone Schema Evaluator

## Overview

The standalone schema evaluator is an alternative code generation mode that produces a lightweight static evaluator class for JSON Schema validation and annotation collection, without generating the full strongly-typed C# models.

This is ideal for scenarios where you need:

- **Full annotation collection** — conformant JSON Schema annotation gathering for tooling that consumes annotations (e.g., form generators, documentation tools, schema-driven UIs)
- **Smaller footprint** — the evaluator generates a single class per schema instead of a type hierarchy, reducing binary size and compilation time
- **Validation-only workflows** — when you need schema validation but don't require serialization, property accessors, or builder support

The evaluator supports all the same JSON Schema drafts as the type-based generator (Draft 4, 6, 7, 2019-09, 2020-12, and OpenAPI 3.0).

## Using the Source Generator

Add the source generator and runtime packages to your project:

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Corvus.Text.Json" Version="5.0.0" />
```

Annotate a partial struct with `EmitEvaluator = true`:

```csharp
[JsonSchemaTypeGenerator("Schemas/person.json", EmitEvaluator = true)]
public readonly partial struct Person;
```

This generates both the strongly-typed `Person` type **and** a standalone evaluator class. The evaluator class is emitted as a nested static class that performs validation and annotation collection independently of the typed model.

If you only want the evaluator (no types), use the CLI tool with `--codeGenerationMode SchemaEvaluationOnly`.

## Using the CLI Tool

```bash
# Generate only the standalone evaluator (no types)
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Evaluators \
    --outputPath Generated/ \
    --codeGenerationMode SchemaEvaluationOnly

# Generate both types and evaluator
generatejsonschematypes Schemas/person.json \
    --rootNamespace MyApp.Models \
    --outputPath Generated/ \
    --codeGenerationMode Both
```

### Code generation modes

| Mode | Description |
|------|-------------|
| `TypeGeneration` | Generate strongly-typed C# models (default) |
| `SchemaEvaluationOnly` | Generate only the standalone evaluator class |
| `Both` | Generate both types and the standalone evaluator |

## What Gets Generated

The standalone evaluator produces a single static class with:

- **Per-subschema validation methods** — one `static void` method per schema and subschema, performing full validation including type checks, constraints, composition (allOf/anyOf/oneOf/not), and conditional logic (if/then/else)
- **Property matchers** — hash-based property dispatch for efficient object validation
- **Discriminator fast paths** — for oneOf and anyOf schemas with discriminator properties, the evaluator uses direct property lookup (`TryGetNamedPropertyValue`) for O(1) branch dispatch
- **Optimized regex handling** — common patterns like `.*`, `.+`, `^prefix`, and `^.{n,m}$` are classified at code generation time and replaced with inline checks (no `Regex` object allocation)
- **Schema path tracking** — evaluation paths and schema locations are tracked throughout validation for standards-compliant error and annotation reporting

## Annotation Collection

The standalone evaluator provides **fully compliant** annotation collection conforming to the JSON Schema specification. By contrast, the type-based generator only collects annotations for validation keywords.

To collect annotations, run the evaluator with a `JsonSchemaResultsCollector` in `Verbose` mode, then use `JsonSchemaAnnotationProducer` to extract the annotations.

### Basic enumeration with `foreach`

The `EnumerateAnnotations` method returns a zero-allocation `ref struct` enumerator that you can use in a `foreach` loop:

```csharp
using Corvus.Text.Json;

// Parse the instance
using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonText);
JsonElement instance = doc.RootElement;

// Validate in Verbose mode
using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
instance.EvaluateSchema(collector);

// Enumerate annotations
foreach (JsonSchemaAnnotationProducer.Annotation annotation
    in JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
{
    // Each annotation is a ref struct with UTF-8 span properties:
    //   annotation.InstanceLocation  — e.g. "", "/foo", "/items/0"
    //   annotation.Keyword           — e.g. "title", "description", "default"
    //   annotation.SchemaLocation    — e.g. "", "/$defs/foo"
    //   annotation.Value             — raw JSON value, e.g. "\"My Title\"", "42", "true"

    // String accessors are also available:
    Console.WriteLine(
        $"  {annotation.GetInstanceLocationText()} " +
        $"[{annotation.GetKeywordText()}] " +
        $"@ {annotation.GetSchemaLocationText()} " +
        $"= {annotation.GetValueText()}");
}
```

> **Note:** The `Annotation` type is a `ref struct` whose spans reference the internal buffers of the `JsonSchemaResultsCollector`. It is only valid during enumeration and must not be stored beyond the current iteration. Use the string accessors (`GetKeywordText()`, etc.) if you need to capture values.

### Writing annotations as JSON

`WriteAnnotationsTo` writes all annotations as a structured JSON object to a `Utf8JsonWriter`. The output is grouped by instance location, then by keyword, then by schema location:

```csharp
using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
instance.EvaluateSchema(collector);

using var buffer = new MemoryStream();
using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
{
    JsonSchemaAnnotationProducer.WriteAnnotationsTo(collector, writer);
}

// Output structure:
// {
//   "": {                          // instance location (root)
//     "title": {
//       "#": "\"Person\""          // schema location → annotation value
//     },
//     "description": {
//       "#": "\"A person object\""
//     }
//   },
//   "/name": {
//     "title": {
//       "#/properties/name": "\"Full name\""
//     }
//   }
// }
```

### Callback-based enumeration

For scenarios where you want to process annotations without a `foreach` loop, use the callback overload. Return `true` to continue, `false` to stop early:

```csharp
JsonSchemaAnnotationProducer.EnumerateAnnotations(
    collector,
    (instanceLocation, keyword, schemaLocation, annotationValue) =>
    {
        Console.WriteLine($"{instanceLocation}/{keyword} @ {schemaLocation} = {annotationValue}");
        return true; // continue enumeration
    });
```

### Collecting annotations into a dictionary (testing)

The `CollectAnnotations` method returns a `Dictionary` keyed by `(instanceLocation, keyword)`, useful for testing assertions:

```csharp
var annotations = JsonSchemaAnnotationProducer.CollectAnnotations(collector);

// Check a specific annotation exists
Assert.True(annotations.TryGetValue(("", "title"), out var titleMap));
Assert.Equal("\"Person\"", titleMap["#"]);
```

> **Note:** `CollectAnnotations` allocates dictionaries. For production use, prefer `EnumerateAnnotations` or `WriteAnnotationsTo`.

## Performance Optimizations

The standalone evaluator includes the same performance optimizations as the type-based generator:

- **Regex pattern classification** — patterns like `.*` (noop), `.+` (non-empty), `^prefix` (starts-with), and `^.{n,m}$` (range) are detected at code generation time and replaced with inline checks, avoiding `Regex` allocation entirely
- **Discriminator fast paths** — both oneOf and anyOf schemas with discriminator properties use `TryGetNamedPropertyValue` for direct property lookup instead of object enumeration
- **Numeric discriminators** — discriminator values can be numbers (not just strings), using normalized number comparison
- **Named-property else clause** — when named properties don't overlap with pattern properties, the evaluator wraps pattern/additional property checks in an else clause, skipping them for already-matched properties
- **Hash-based property dispatch** — schemas with 4+ named properties use a hash map for O(1) property routing

## Comparison with Type-Based Generation

| Feature | Type Generation | Evaluator Only |
|---------|----------------|----------------|
| Strongly-typed accessors | ✅ | ❌ |
| JSON serialization/deserialization | ✅ | ❌ |
| Mutable builder support | ✅ | ❌ |
| Implicit/explicit conversions | ✅ | ❌ |
| Schema validation | ✅ | ✅ |
| Annotation collection | Validation keywords only | Fully compliant |
| Binary size | Larger | Smaller |
| Compilation time | Longer | Shorter |

## See Also

- [Source Generator](SourceGenerator.md) — build-time type generation
- [CLI Code Generator](CodeGenerator.md) — command-line type generation
- [Validator](Validator.md) — runtime validation API