# Adding Keywords

This document explains how JSON Schema keywords are implemented in the Corvus.JsonSchema code generation system, covering the keyword interfaces, vocabulary registration, draft-specific variations, and custom keyword extension points.

## Overview

Keywords are the building blocks of JSON Schema. Each keyword (e.g., `properties`, `type`, `minLength`) is represented by a class that implements `IKeyword` plus one or more marker interfaces that describe its behaviour. Keywords are grouped into vocabularies, and vocabularies are registered per draft.

## IKeyword interface

Every keyword implements the base `IKeyword` interface:

```csharp
public interface IKeyword
{
    string Keyword { get; }                      // e.g. "properties"
    ReadOnlySpan<byte> KeywordUtf8 { get; }      // UTF-8 representation
    CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration);
    bool CanReduce(in JsonElement schemaValue);
}
```

- **`Keyword`** — the JSON property name in the schema
- **`KeywordUtf8`** — UTF-8 bytes for high-performance matching
- **`ImpliesCoreTypes`** — what JSON types this keyword implies (e.g., `properties` implies `CoreTypes.Object`)
- **`CanReduce`** — whether this keyword allows type reduction (collapsing subschemas)

## Behavioural marker interfaces

Keywords declare their behaviour through marker interfaces. The code generation system queries these interfaces to decide what code to emit.

### Schema structure

| Interface | Purpose | Example keywords |
|-----------|---------|-----------------|
| `ISubschemaTypeBuilderKeyword` | Builds child type declarations from subschemas | `properties`, `items`, `allOf` |
| `ILocalSubschemaRegistrationKeyword` | Registers subschemas with the schema registry | `properties`, `$defs` |
| `IPropertySubchemaProviderKeyword` | Provides property declarations for object schemas | `properties`, `patternProperties` |

### Validation

| Interface | Purpose | Example keywords |
|-----------|---------|-----------------|
| `IObjectPropertyValidationKeyword` | Validates individual object properties | `properties` |
| `IValueKindValidationKeyword` | Validates the JSON value kind/type | `type`, `format` |
| `INumericValidationKeyword` | Validates numeric constraints | `minimum`, `multipleOf` |
| `IStringValidationKeyword` | Validates string constraints | `minLength`, `pattern` |
| `IArrayValidationKeyword` | Validates array constraints | `minItems`, `uniqueItems` |

### Annotation and documentation

| Interface | Purpose | Example keywords |
|-----------|---------|-----------------|
| `IAnnotationProducingKeyword` | Produces JSON Schema annotations | `title`, `description`, `default` |
| `IShortDocumentationProviderKeyword` | Provides short documentation (title) | `title` |
| `ILongDocumentationProviderKeyword` | Provides long documentation (description) | `description` |
| `IDefaultValueProviderKeyword` | Provides a default value | `default` |
| `IExamplesProviderKeyword` | Provides example values | `examples` |
| `INonStructuralKeyword` | Does not affect the generated type structure | `title`, `description`, `readOnly` |
| `IDeprecatedKeyword` | Marks the schema as deprecated | `deprecated` |

### References and identity

| Interface | Purpose | Example keywords |
|-----------|---------|-----------------|
| `IRefKeyword` | Resolves `$ref` references | `$ref` |
| `IDynamicRefKeyword` | Resolves `$dynamicRef` references | `$dynamicRef` |
| `IIdKeyword` | Provides schema identity | `$id` |
| `IAnchorKeyword` | Provides anchor points | `$anchor`, `$dynamicAnchor` |
| `ISchemaKeyword` | Identifies the schema dialect | `$schema` |

## Concrete keyword example

Here's how `PropertiesKeyword` is implemented:

```csharp
public sealed class PropertiesKeyword
    : ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        IPropertySubchemaProviderKeyword,
        IObjectPropertyValidationKeyword
{
    public static PropertiesKeyword Instance { get; } = new();

    public string Keyword => "properties";
    public ReadOnlySpan<byte> KeywordUtf8 => "properties"u8;
    public uint PropertyProviderPriority => PropertyProviderPriorities.Last;
    public uint ValidationPriority => ValidationPriorities.AfterComposition;

    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration)
        => CoreTypes.Object;

    public bool CanReduce(in JsonElement schemaValue)
        => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    public void RegisterLocalSubschema(
        JsonSchemaRegistry registry, JsonElement schema,
        JsonReference currentLocation, IVocabulary vocabulary,
        CancellationToken cancellationToken) { ... }

    public async ValueTask BuildSubschemaTypes(
        TypeBuilderContext typeBuilderContext,
        TypeDeclaration typeDeclaration,
        CancellationToken cancellationToken) { ... }

    public void CollectProperties(
        TypeDeclaration source, TypeDeclaration target,
        HashSet<TypeDeclaration> visitedTypeDeclarations,
        bool treatRequiredAsOptional,
        CancellationToken cancellationToken) { ... }
}
```

Keywords follow the singleton pattern (`Instance` property) and are stateless.

## Vocabularies

Keywords are grouped into vocabularies. A vocabulary is a named collection of keywords with a URI identifier.

### Draft-specific vocabularies

| Draft | Keywords | URI |
|-------|----------|-----|
| Draft 4 | 31 keywords | `http://json-schema.org/draft-04/schema#` |
| Draft 6 | 33 keywords | `http://json-schema.org/draft-06/schema#` |
| Draft 7 | 37 keywords | `http://json-schema.org/draft-07/schema#` |
| Draft 2019-09 | 6 sub-vocabularies | `https://json-schema.org/draft/2019-09/schema` |
| Draft 2020-12 | 7 sub-vocabularies | `https://json-schema.org/draft/2020-12/schema` |

### Vocabulary modularisation (2019-09 and 2020-12)

Starting with Draft 2019-09, keywords are split into named sub-vocabularies:

| Vocabulary | Keywords |
|-----------|----------|
| **Core** | `$id`, `$schema`, `$anchor`, `$ref`, `$defs`, `$vocabulary`, `$comment` |
| **Applicator** | `allOf`, `anyOf`, `oneOf`, `not`, `if`/`then`/`else`, `properties`, `patternProperties`, `additionalProperties`, `items`, `prefixItems`, `contains` |
| **Validation** | `type`, `const`, `enum`, `multipleOf`, `minimum`, `maximum`, `minLength`, `maxLength`, `pattern`, `minItems`, `maxItems`, `uniqueItems`, `minProperties`, `maxProperties`, `required`, `dependentRequired` |
| **MetaData** | `title`, `description`, `default`, `examples`, `deprecated`, `readOnly`, `writeOnly` |
| **FormatAnnotation** | `format` (annotation-only in 2020-12) |
| **Content** | `contentMediaType`, `contentEncoding`, `contentSchema` |
| **Unevaluated** | `unevaluatedProperties`, `unevaluatedItems` (2020-12) |

### Vocabulary registration

The `VocabularyRegistry` manages vocabulary and analyser registration:

```csharp
VocabularyRegistry registry = new();
registry.RegisterVocabularies(Draft202012.SchemaVocabulary.DefaultInstance);
registry.RegisterAnalyser(new Draft202012Analyser());
```

The registry supports:
- `RegisterVocabularies(params IVocabulary[] vocabularies)` — register keyword sets
- `RegisterAnalyser(IVocabularyAnalyser analyser)` — register schema analysers
- `TryGetVocabulary(string uri, out IVocabulary vocabulary)` — look up by URI
- `TryGetSchemaDialect(string uri, out IVocabulary vocabulary)` — look up standard dialect

## Keyword evolution across drafts

Some keywords change behaviour between drafts:

| Keyword | Draft 4 | Draft 6 | Draft 7 | 2019-09 | 2020-12 |
|---------|---------|---------|---------|---------|---------|
| `exclusiveMaximum` | Boolean | Number | Number | Number | Number |
| `items` | Schema or array | Schema or array | Schema or array | Schema or array | Schema only |
| `additionalItems` | ✅ | ✅ | ✅ | ✅ | Replaced by `prefixItems` |
| `$ref` | Replaces all siblings | Replaces all siblings | Replaces all siblings | Coexists with siblings | Coexists |
| `format` | Assertion | Assertion | Assertion | Assertion (optional) | Annotation only |
| `$recursiveRef` | — | — | — | ✅ | Replaced by `$dynamicRef` |

Each draft has its own keyword implementation where behaviour differs (e.g., `ExclusiveMaximumBooleanKeyword` for Draft 4 vs `ExclusiveMaximumKeyword` for later drafts).

## Custom keyword extension points

The code generation system provides extension points for custom keywords that need to inject logic at specific phases:

| Interface | Phase | Use case |
|-----------|-------|----------|
| `ISchemaRegistrationCustomKeyword` | Schema registration | Register additional schemas |
| `ITypeBuilderCustomKeyword` | Type building | Modify type declarations |
| `IApplyBeforeScopeCustomKeyword` | Before scope entry | Pre-scope setup |
| `IApplyBeforeEnteringScopeCustomKeyword` | Before entering scope | Scope preparation |
| `IApplyAfterEnteringScopeCustomKeyword` | After entering scope | Post-scope setup |
| `IApplyBeforeSubschemasCustomKeyword` | Before subschemas | Combines registration + type building |
| `IApplyAfterSubschemasCustomKeyword` | After subschemas | Post-subschema processing |
| `IApplyBeforeAnchorsCustomKeyword` | Before anchors | Anchor preparation |

Custom keywords are applied through the `CustomKeywords` static helper class, which orchestrates execution at the appropriate phases during schema walking.

## Adding a new keyword

To add a keyword:

1. **Create the keyword class** in the appropriate draft directory under `src-v4/Corvus.Json.CodeGeneration.<Draft>/`
2. **Implement `IKeyword`** plus the behavioural interfaces that describe its semantics
3. **Add it to the vocabulary** in `SchemaVocabulary.cs` for the relevant draft(s)
4. **If it produces annotations**, implement `IAnnotationProducingKeyword`
5. **If it needs validation code gen**, implement the appropriate validation interface and add handling in the relevant `ValidationHandler`
6. **Add tests** using the JSON Schema Test Suite or custom test cases