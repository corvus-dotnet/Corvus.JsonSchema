# Standalone Evaluator Internals

This document describes the architecture and implementation of `StandaloneEvaluatorGenerator`, which produces a standalone static evaluator class for JSON Schema validation and annotation collection, independent of generated types.

## Overview

The standalone evaluator is an alternative to the type-based code generation. While type-based code gen produces strongly-typed C# structs with built-in validation, the standalone evaluator generates a single static class that validates any `IJsonElement<T>` against a schema. It is the engine behind `SchemaEvaluator.Evaluate()`.

## When to use each mode

| Mode | Generated output | Use case |
|------|-----------------|----------|
| `TypeGeneration` | Strongly-typed structs | Application code that deserialises JSON into domain types |
| `SchemaEvaluationOnly` | Static evaluator class | Validation-only scenarios, annotation collection, tooling |
| `Both` | Both | Full-featured: typed access + standalone evaluation |

The mode is selected via `CodeGenerationMode`:

```csharp
public enum CodeGenerationMode
{
    TypeGeneration,        // Default
    SchemaEvaluationOnly,
    Both,
}
```

In `CSharpLanguageProvider`, the decision is straightforward:

```csharp
bool generateTypes = options.CodeGenerationMode is TypeGeneration or Both;
bool generateEvaluator = options.CodeGenerationMode is SchemaEvaluationOnly or Both;
```

## Architecture

### Two-pass generation

`StandaloneEvaluatorGenerator.Generate()` uses a two-pass approach:

**Pass 1 — Collect subschemas:** Walk the type declaration tree and build a dictionary mapping schema locations to `SubschemaInfo` records. Each record holds the method name, schema path, type declaration, and flags for whether it needs evaluated-items or evaluated-properties tracking.

**Pass 2 — Emit evaluation methods:** For each subschema, emit a static method that validates a JSON element against that subschema's constraints.

```
Generate()
├── CollectSubschemas()          → Pass 1: walk tree, build SubschemaInfo map
├── EmitFileHeader()
├── EmitPathProviderFields()     → UTF-8 path constants for schema/evaluation paths
├── EmitPatternRegexFields()     → Regex fields for pattern keywords
├── CollectPropertyMatcherInfo() → Build PropertyMatcherInfo for object schemas
├── EmitPropertyMatcherInfrastructure()
├── EmitPublicEvaluateMethod()   → The public Evaluate<TElement>() entry point
├── foreach subschema:
│   └── EmitSchemaEvaluationMethod()  → The core per-schema validation method
└── EmitDeferredConstantFields()
```

### SubschemaInfo

Each subschema in the schema tree is represented by a `SubschemaInfo` record:

```csharp
internal sealed class SubschemaInfo
{
    public string MethodName { get; }            // e.g. "EvaluateRoot", "EvaluateAllOf0"
    public string SchemaPath { get; }            // e.g. "#", "#/allOf/0"
    public TypeDeclaration TypeDeclaration { get; }
    public bool UseEvaluatedItems { get; }       // needs unevaluatedItems tracking
    public bool UseEvaluatedProperties { get; }  // needs unevaluatedProperties tracking
    public string? PathFieldName { get; set; }   // evaluation path UTF-8 field
    public string? SchemaPathFieldName { get; set; } // schema path UTF-8 field
}
```

Method names are derived from the schema path: `EvaluateRoot`, `EvaluateProperties_Name`, `EvaluateAllOf0`, `EvaluateAnyOf1_Properties_Age`, etc.

## Key infrastructure

### Property matchers

For schemas with `properties`, the evaluator generates a hash-map-based property dispatch system (the same O(1) lookup pattern used by the type-based `PropertiesValidationHandler`):

1. **UTF-8 property name constants** — `private static ReadOnlySpan<byte> PropName_... => "name"u8;`
2. **Delegate type** — per-schema delegate with signature `(IJsonDocument, int, int, ref JsonSchemaContext, Span<uint>)`
3. **Per-property methods** — each property gets its own validation method that:
   - Marks the property as locally evaluated
   - Pushes a child `JsonSchemaContext`
   - Calls the subschema evaluation method
   - Commits the child context
   - Sets the required bit (if this property is required)
4. **Hash map** — `PropertySchemaMatchers` for O(1) lookup by property name
5. **`TryGetNamedMatcher` wrapper** — bridge method used by the property enumeration loop

The decision to use a map vs linear scan depends on property count (`PropertyMatcherInfo.UseMap`).

### Bitmask tracking

Required properties and dependent-required properties are tracked with `Span<uint>` bitmasks, stack-allocated for efficiency:

```csharp
// Required: one bit per required property
int requiredIntCount = (int)Math.Ceiling(totalRequiredCount / 32.0);
Span<uint> requiredBits = stackalloc uint[requiredIntCount];

// DependentSchemas: one bit per property that triggers a dependent schema
Span<uint> depSchemaBits = stackalloc uint[depSchemaIntCount];

// DependentRequired: one bit per property referenced in dependentRequired
Span<uint> depReqBits = stackalloc uint[depReqIntCount];
```

During property enumeration, bits are set:
```csharp
requiredBits[offset] |= 0x00000001;  // property at bit index 0 was found
```

After enumeration, unset bits indicate missing required properties:
```csharp
if ((requiredBits[0] & 0x00000001) == 0)
{
    context.EvaluatedKeywordForProperty(false, null, "name"u8, "required"u8);
}
```

### Path provider fields

The evaluator emits `ReadOnlySpan<byte>` fields for every unique schema path and evaluation path segment. These are used by the `JsonSchemaContext` to build up the full evaluation and schema-evaluation paths without runtime string concatenation:

```csharp
private static ReadOnlySpan<byte> Path_Properties => "/properties"u8;
private static ReadOnlySpan<byte> SchemaPath_AllOf_0 => "/allOf/0"u8;
```

### Pattern regex fields

For schemas with `pattern` keywords, the evaluator classifies regex patterns into optimisation categories:

| Category | Optimisation |
|----------|-------------|
| `Noop` | Always matches — skip entirely |
| `NonEmpty` | `^.+$` equivalent — just check non-empty |
| `Prefix` | `^prefix` — use `StartsWith` |
| `Range` | `^[a-z]$` — use range check |
| `FullRegex` | Requires a `Regex` field and factory |

Only `FullRegex` patterns emit a `Regex` field; the others use inline runtime helper methods.

## Composition handling

### allOf

Evaluates each branch sequentially. All must pass for the overall allOf to pass:

```
for each allOf branch:
    push child context
    evaluate branch
    commit child (AND semantics)
    merge evaluated items/properties from child
```

### anyOf

Evaluates all branches, tracking how many pass. At least one must pass:

```
for each anyOf branch:
    push child context
    evaluate branch
    commit child
    if passed: merge evaluated items/properties
at end: fail if none passed
```

The evaluator supports **discriminator optimisation**: if a property value can distinguish between branches, it checks that property first and only evaluates the matching branch.

### oneOf

Similar to anyOf but exactly one branch must pass. Uses `oneOfMatchCount` to track:

```
for each oneOf branch:
    push child context
    evaluate branch
    if passed: increment matchCount, save evaluated state
at end: fail if matchCount != 1
```

### not

Evaluates the child schema and inverts the result:

```
push child context (with validation-only flag)
evaluate child
fail if child passed
```

## Annotation emission

When the evaluator encounters annotation-producing keywords (title, description, default, examples, etc.), it emits `IgnoredKeyword` calls:

```csharp
context.IgnoredKeyword(
    Corvus.Text.Json.JsonSchema.Internal.JsonSchemaMessageProviders.AnnotationMessageProvider("\"Person\""),
    "title"u8);
```

The key distinction between `IgnoredKeyword` and `EvaluatedKeyword`:
- **`IgnoredKeyword`** — appends the keyword to the *evaluation path* only (not the schema evaluation path)
- **`EvaluatedKeyword`** — appends to *both* paths

This path divergence is how `JsonSchemaAnnotationProducer.IsAnnotation()` distinguishes annotations from validation results when filtering the verbose results.

## Difference from type-based code generation

| Aspect | Type-based | Standalone evaluator |
|--------|-----------|---------------------|
| Output | Many partial struct files | Single static class |
| Entry point | `element.EvaluateSchema()` | `Evaluator.Evaluate(element, collector)` |
| Type system | Strongly-typed, CRTP `IJsonElement<T>` | Works with any `IJsonElement<T>` |
| Annotations | Limited to validation keywords | Full annotation collection |
| Uses | Application domain types | Validation tooling, schema evaluation |
| Type tree | Uses reduced types | Uses **unreduced** original types |
| Property access | Typed property accessors | Dynamic property enumeration |

The evaluator deliberately uses *unreduced* type declarations (via `SetEvaluatorRootTypes()`) to preserve the full schema structure, including intermediate subschema applicators that would be collapsed during type reduction.