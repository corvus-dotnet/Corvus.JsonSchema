# Validation Handler Guide

This document explains how validation handlers work in the Corvus.JsonSchema code generation system — the components that emit C# validation code from JSON Schema keywords.

## Overview

Validation handlers are responsible for translating JSON Schema keywords into C# validation code. Each handler targets a specific category of validation (type checking, string constraints, object properties, composition, etc.) and emits the corresponding code into the generated types.

## Handler hierarchy and priorities

Handlers execute in a strict priority order defined by `ValidationPriorities`:

| Priority | Name | Value | Handlers |
|----------|------|-------|----------|
| 1 | `CoreType` | 1000 | `TypeValidationHandler` |
| 2 | `Default` | `uint.MaxValue / 2` | `ConstValidationHandler`, `StringValidationHandler`, `NumberValidationHandler`, `FormatValidationHandler` |
| 3 | `Composition` | `Default + 1000` | `AllOfSubschemaValidationHandler`, `AnyOfSubschemaValidationHandler`, `OneOfSubschemaValidationHandler`, `NotSubschemaValidationHandler`, `IfThenElseValidationHandler` |
| 4 | `AfterComposition` | `Composition + 1000` | `ObjectValidationHandler`, `ArrayValidationHandler`, `PropertiesValidationHandler` |
| 5 | `Last` | `uint.MaxValue` | `UnevaluatedPropertyValidationHandler`, `UnevaluatedItemValidationHandler` |

Handlers are sorted by `ValidationPriority` (ascending) and executed in that order. This ensures:

1. **Type checking first** — reject invalid types before doing detailed validation
2. **Value-level validation** — const, string, number constraints
3. **Composition** — allOf/anyOf/oneOf/not/if-then-else
4. **Structural validation** — object properties and array items (after composition, so evaluated tracking is complete)
5. **Unevaluated keywords last** — can only run after all other validation has marked items/properties as evaluated

## Type validation handler

`TypeValidationHandler` (priority: `CoreType`) runs first and emits the `type` keyword check:

```csharp
// Generated code pattern:
if (!MatchTypeObject(tokenType))
{
    // Emit IgnoredKeyword for type-specific keywords that don't apply
    collector.IgnoredKeyword(null, "properties"u8);
    collector.IgnoredKeyword(null, "required"u8);
}
```

When the type check fails, downstream keywords that only apply to that type are reported as `IgnoredKeyword` rather than evaluated. This prevents false negatives when a schema has (e.g.) both string and object keywords.

## Object and property validation

### ObjectValidationHandler

Handles structural object constraints:
- `minProperties` / `maxProperties`
- `required` (bitmask tracking)
- `dependentRequired` (bitmask tracking)
- `dependentSchemas`
- `propertyNames`

### PropertiesValidationHandler

Handles property-level validation:
- `properties` — named property schemas
- `patternProperties` — regex-matched property schemas
- `additionalProperties` — fallback schema for unmatched properties

Uses a `PropertySchemaMatchers` hash map for O(1) named property dispatch (same pattern as the standalone evaluator).

## Array validation

`ArrayValidationHandler` handles:
- `items` / `prefixItems` — item schemas
- `contains` / `minContains` / `maxContains`
- `minItems` / `maxItems`
- `uniqueItems`
- `additionalItems`

It has its own token type guard:
```csharp
if (tokenType == JsonTokenType.StartArray)
{
    // Array-specific validation
}
```

## Composition handlers

### AllOfSubschemaValidationHandler

Evaluates all branches; all must pass. Generated code pattern:

```csharp
// allOf[0]
{
    var childContext = context.PushChildContext(...);
    AllOf0.Validate(element, ref childContext);
    context.CommitChildContext(childContext.IsMatch, ref childContext);
}
```

### AnyOfSubschemaValidationHandler

Evaluates all branches; at least one must pass. Supports discriminator optimisation for branches distinguishable by a property value.

### OneOfSubschemaValidationHandler

Evaluates all branches; exactly one must pass. Also supports discriminator optimisation.

### NotSubschemaValidationHandler

Evaluates child schema and inverts result.

### IfThenElseValidationHandler

Evaluates `if` schema; on success evaluates `then`, on failure evaluates `else`.

## How handlers query TypeDeclaration

Handlers inspect the `TypeDeclaration` to decide what code to emit. Key queries:

```csharp
// What JSON types does this schema allow?
CoreTypes coreTypes = typeDeclaration.ImpliedCoreTypesOrAny();

// Does it have specific keywords?
bool hasProperties = typeDeclaration.HasKeyword<PropertiesKeyword>();
bool hasItems = typeDeclaration.HasKeyword<ItemsKeyword>();

// Get keyword values
if (typeDeclaration.TryGetKeyword<MinLengthKeyword>(out JsonElement value))
{
    int minLength = value.GetInt32();
}

// Get property declarations
foreach (PropertyDeclaration prop in typeDeclaration.PropertyDeclarations)
{
    string name = prop.JsonPropertyName;
    bool required = prop.RequiredOrOptional == RequiredOrOptional.Required;
    TypeDeclaration propertyType = prop.UnreducedPropertyType;
}

// Get composition subschemas
foreach (TypeDeclaration allOfType in typeDeclaration.AllOfTypes())
{
    // Process each allOf branch
}
```

## Child handler registration

Some handlers register child handlers for nested validation. For example, `ObjectValidationHandler` registers `PropertiesValidationHandler` to handle the per-property validation within the object enumeration loop.

The registration pattern:

```csharp
public class ObjectValidationHandler : IValidationHandler
{
    public IEnumerable<IValidationHandler> GetChildHandlers(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.HasKeyword<PropertiesKeyword>())
        {
            yield return new PropertiesValidationHandler(typeDeclaration);
        }
    }
}
```

## Emitting validation code

Each handler implements `EmitValidation` which writes C# code to a `CodeGenerator` context:

```csharp
public void EmitValidation(
    CodeGenerator generator,
    TypeDeclaration typeDeclaration,
    string elementVariable,
    string contextVariable)
{
    // Emit C# validation code
    generator.AppendLine($"if ({elementVariable}.GetArrayLength() < {minItems})");
    generator.AppendLine("{");
    generator.PushIndent();
    generator.AppendLine($"{contextVariable}.EvaluatedKeyword(false, null, \"minItems\"u8);");
    generator.PopIndent();
    generator.AppendLine("}");
}
```

## Adding a new validation handler

1. Create a handler class implementing the validation handler interface
2. Set the appropriate `ValidationPriority`
3. Implement `EmitValidation` to generate the C# validation code
4. Register the handler in the handler pipeline
5. Add tests to verify the generated code validates correctly