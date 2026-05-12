# Annotation System

This document describes the JSON Schema annotation collection architecture in Corvus.JsonSchema V5.

## Overview

JSON Schema defines certain keywords as *annotation-producing* — they carry metadata about the schema rather than validation constraints. The V5 standalone evaluator provides fully compliant annotation collection conforming to the JSON Schema specification.

Annotations are collected by running the evaluator with a `JsonSchemaResultsCollector` in `Verbose` mode, then using `JsonSchemaAnnotationProducer` to extract annotations from the verbose results.

## Annotation-producing keywords

The following keywords implement `IAnnotationProducingKeyword`:

| Keyword | Interface(s) | Applicable types | Notes |
|---------|-------------|-----------------|-------|
| `title` | `IShortDocumentationProviderKeyword`, `INonStructuralKeyword` | All | Schema title |
| `description` | `ILongDocumentationProviderKeyword`, `INonStructuralKeyword` | All | Schema description |
| `default` | `IDefaultValueProviderKeyword` | All | Default value |
| `examples` | `IExamplesProviderKeyword`, `INonStructuralKeyword` | All | Example values (array) |
| `deprecated` | `IDeprecatedKeyword` | All | Deprecation flag |
| `readOnly` | `INonStructuralKeyword` | All | Read-only hint |
| `writeOnly` | `INonStructuralKeyword` | All | Write-only hint |
| `format` | `IFormatProviderKeyword`, `IValueKindValidationKeyword` | All | Format annotation (2020-12) |
| `contentMediaType` | — | Strings | Content media type |
| `contentEncoding` | — | Strings | Content encoding |
| `contentSchema` | — | Strings | Requires `contentMediaType` |

## IAnnotationProducingKeyword interface

```csharp
public interface IAnnotationProducingKeyword : IKeyword
{
    /// <summary>
    /// Gets the raw JSON annotation value from the type declaration.
    /// </summary>
    bool TryGetAnnotationJsonValue(TypeDeclaration typeDeclaration, out string rawJsonValue);

    /// <summary>
    /// Returns the core types this annotation applies to.
    /// Return CoreTypes.None to apply to all instance types.
    /// </summary>
    CoreTypes AnnotationAppliesToCoreTypes(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Whether preconditions are met (e.g. contentSchema requires contentMediaType).
    /// </summary>
    bool AnnotationPreconditionsMet(TypeDeclaration typeDeclaration);
}
```

Most keywords return `CoreTypes.None` (applies to all types) and `true` for preconditions. The exception is `contentSchema`, which requires `contentMediaType` to also be present.

## How annotations flow through validation

### Step 1: Evaluator emission

At code generation time, `StandaloneEvaluatorGenerator.EmitAnnotations()` processes each `IAnnotationProducingKeyword` on a type declaration and emits `IgnoredKeyword` calls:

```csharp
// Generated code (simplified):
context.IgnoredKeyword(
    JsonSchemaMessageProviders.AnnotationMessageProvider("\"Person\""),
    "title"u8);
```

### Step 2: Results collection

At runtime, when the evaluator runs with a `JsonSchemaResultsCollector` in `Verbose` mode, `IgnoredKeyword` records a result with:

- `IsMatch = true` (annotations are always "matching")
- `Message` = the raw JSON annotation value (e.g., `"Person"`, `["example1"]`)
- `EvaluationLocation` = path with keyword appended (e.g., `/title`)
- `SchemaEvaluationLocation` = path **without** keyword appended

The critical difference: `IgnoredKeyword` only appends the keyword name to the *evaluation path*, not the *schema evaluation path*. `EvaluatedKeyword` (used for validation keywords) appends to both. This path divergence is how annotations are distinguished from validation results.

### Step 3: Annotation extraction

`JsonSchemaAnnotationProducer` filters the verbose results to extract annotations:

```csharp
internal static bool IsAnnotation(in Result result, out ReadOnlySpan<byte> keyword)
{
    // Must be a match
    if (!result.IsMatch) return false;

    // Must have a non-empty message (raw JSON)
    if (result.Message.Length == 0) return false;

    // Key: EvaluationLocation != SchemaEvaluationLocation
    if (evaluationLocation.SequenceEqual(schemaEvaluationLocation))
        return false;  // This is a validation result

    // Extract keyword from the last path segment
    keyword = evaluationLocation[(lastSlash + 1)..];

    // Message must start with a valid JSON value byte (not a diagnostic)
    return IsJsonValueStart(message[0]);
}
```

## Using annotations

### Enumeration with foreach

```csharp
using var collector = new JsonSchemaResultsCollector(JsonSchemaResultsLevel.Verbose);
schema.EvaluateSchema(collector);

foreach (JsonSchemaAnnotationProducer.Annotation annotation
    in JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
{
    Console.WriteLine(
        $"{annotation.GetInstanceLocationText()} " +
        $"[{annotation.GetKeywordText()}] " +
        $"@ {annotation.GetSchemaLocationText()} " +
        $"= {annotation.GetValueText()}");
}
```

### Writing as JSON

```csharp
JsonSchemaAnnotationProducer.WriteAnnotationsTo(collector, writer);
```

Output structure:
```json
{
  "": {
    "title": {
      "#": "\"Person\""
    },
    "description": {
      "#": "\"A person object\""
    }
  },
  "/name": {
    "title": {
      "#/properties/name": "\"Full name\""
    }
  }
}
```

The nesting is: `instanceLocation → keyword → schemaLocation → annotationValue`.

### Callback-based enumeration

```csharp
JsonSchemaAnnotationProducer.EnumerateAnnotations(
    collector,
    (instanceLocation, keyword, schemaLocation, value) =>
    {
        // Process annotation; return false to stop early
        return true;
    });
```

### Collecting to a dictionary

```csharp
var annotations = JsonSchemaAnnotationProducer.CollectAnnotations(collector);
// Dictionary<string, Dictionary<string, Dictionary<string, string>>>
// instanceLocation → keyword → schemaLocation → value
```

## The Annotation ref struct

`JsonSchemaAnnotationProducer.Annotation` is a `ref struct` for zero-allocation enumeration:

```csharp
public readonly ref struct Annotation
{
    public ReadOnlySpan<byte> InstanceLocation { get; }
    public ReadOnlySpan<byte> Keyword { get; }
    public ReadOnlySpan<byte> SchemaLocation { get; }
    public ReadOnlySpan<byte> Value { get; }

    // Convenience methods to get string representations:
    public string GetInstanceLocationText();
    public string GetKeywordText();
    public string GetSchemaLocationText();
    public string GetValueText();
}
```

Because it is a `ref struct`, it cannot be stored in collections or captured by lambdas. Use the callback or dictionary APIs for those use cases.

## Verbosity levels

| Level | Annotations collected? | Use case |
|-------|----------------------|----------|
| `Flag` | No | Quick pass/fail check |
| `Basic` | No | Validation with error messages |
| `Detailed` | No | Validation with full error context |
| `Verbose` | **Yes** | Full annotation collection |

Annotations are only collected in `Verbose` mode. The other levels skip the `IgnoredKeyword` calls entirely for performance.

## Type-based vs standalone evaluator

| Feature | Type-based | Standalone evaluator |
|---------|-----------|---------------------|
| Validation keywords | ✅ Full support | ✅ Full support |
| Annotation keywords | ❌ Not emitted | ✅ Full collection |
| Annotation compliance | N/A | Conforms to JSON Schema test suite |
| Performance | Optimised (skips annotations) | Slightly slower (records annotations) |

The type-based code generator (`element.EvaluateSchema()`) does not emit annotation collection code. To collect annotations, use the standalone evaluator via `SchemaEvaluator.Evaluate()` with a `Verbose` results collector.