# Code Generation Pattern Discovery Guide

This document explains how the V5 code generation system discovers common JSON Schema patterns from a `TypeDeclaration`, and how developers working with the code generator can query these patterns in their own code.

## Architecture Overview

The code generation pipeline uses a three-layer architecture to discover and act on JSON Schema patterns:

```
┌─────────────────────────────────────┐
│  Keyword Interfaces (src-v4)        │  Define capabilities via marker interfaces
│  e.g. ITupleTypeProviderKeyword     │  (IArrayValidationKeyword, IObjectValidationKeyword, ...)
├─────────────────────────────────────┤
│  TypeDeclaration                    │  Holds the schema, its keywords, properties,
│  + PropertyDeclarations             │  and subschema type declarations
├─────────────────────────────────────┤
│  TypeDeclarationExtensions          │  Query methods that inspect keywords and metadata
│  e.g. IsTuple(), ArrayDimension()   │  to detect patterns and return typed results
├─────────────────────────────────────┤
│  CodeGeneratorExtensions (V5)       │  Emit C# code based on detected patterns
│  e.g. AppendTupleItemProperties()   │  (in src/Corvus.Text.Json.CodeGeneration/)
└─────────────────────────────────────┘
```

**Key principle:** Pattern discovery is driven by *keyword interfaces*, not keyword names. Each JSON Schema keyword class implements one or more marker interfaces. The extension methods on `TypeDeclaration` query these interfaces using `Keywords().OfType<T>()` to detect patterns in a dialect-agnostic way.

### Key files

| File | Location | Purpose |
|------|----------|---------|
| `TypeDeclaration.cs` | `src-v4/.../TypeDeclarations/` | Core type: holds schema, keywords, properties, subschema types |
| `TypeDeclarationExtensions.cs` | `src-v4/.../TypeDeclarations/` | Pattern detection extension methods |
| `TypeDeclarationExtensions.cs` | `src/Corvus.Text.Json.CodeGeneration/` | V5-specific extensions (builds on V4) |
| `CodeGeneratorExtensions.*.cs` | `src/Corvus.Text.Json.CodeGeneration/` | V5 code emission, split by concern |
| `StandaloneEvaluatorGenerator.cs` | `src/Corvus.Text.Json.CodeGeneration/` | Standalone validation code emission |
| `Keywords/I*.cs` | `src-v4/.../Keywords/` | Keyword interface definitions (~90 interfaces) |

---

## CoreTypes — The Type System Foundation

Every `TypeDeclaration` has an implied set of JSON types, represented by the `CoreTypes` flags enum:

```csharp
[Flags]
public enum CoreTypes : byte
{
    None    = 0b0000_0000,
    Object  = 0b0000_0001,
    Array   = 0b0000_0010,
    Boolean = 0b0000_0100,
    Number  = 0b0000_1000,
    Integer = 0b0001_0000,
    String  = 0b0010_0000,
    Null    = 0b0100_0000,
    Any     = Object | Array | Boolean | Number | Integer | String | Null,
}
```

### Querying core types

```csharp
// What types does this schema allow (from the 'type' keyword)?
CoreTypes allowed = typeDeclaration.AllowedCoreTypes();

// What types are implied by keywords (e.g. 'properties' implies Object)?
CoreTypes implied = typeDeclaration.ImpliedCoreTypes();

// Same, but returns Any if nothing is implied (useful for unconstrained schemas)
CoreTypes impliedOrAny = typeDeclaration.ImpliedCoreTypesOrAny();

// Types implied only by local keywords (not composed via allOf, etc.)
CoreTypes local = typeDeclaration.LocallyImpliedCoreTypes();
```

**Driven by:** `ICoreTypeValidationKeyword` (the `type` keyword) and inference from other keyword interfaces (e.g. `IObjectValidationKeyword` implies `Object`, `IArrayValidationKeyword` implies `Array`).

---

## Array Patterns

Arrays in JSON Schema can take several forms. The code generation system distinguishes between them using keyword interfaces.

### Keyword interfaces involved

| Interface | Keywords | Purpose |
|-----------|----------|---------|
| `ITupleTypeProviderKeyword` | `prefixItems` (2020-12), `items` as array (draft-07) | Provides tuple item types |
| `IArrayItemsTypeProviderKeyword` | `items` (2020-12), `additionalItems` (draft-07) | Provides single array item type |
| `INonTupleArrayItemsTypeProviderKeyword` | `items` (when used with prefixItems) | Items after prefix tuple |
| `IUnevaluatedArrayItemsTypeProviderKeyword` | `unevaluatedItems` | Unevaluated items type |
| `IArrayLengthConstantValidationKeyword` | `minItems`, `maxItems` | Array length constraints |
| `IArrayContainsValidationKeyword` | `contains` | Contains item type |
| `IArrayContainsCountConstantValidationKeyword` | `minContains`, `maxContains` | Contains count constraints |
| `IUniqueItemsArrayValidationKeyword` | `uniqueItems` | Uniqueness constraint |

### Pattern 1: Pure Tuple

A pure tuple has `prefixItems` and explicitly denies additional items (`items: false`).

```json
{
    "type": "array",
    "prefixItems": [
        { "type": "string" },
        { "type": "number" },
        { "type": "boolean" }
    ],
    "items": false
}
```

**Detection:**

```csharp
if (typeDeclaration.IsTuple())
{
    TupleTypeDeclaration tuple = typeDeclaration.TupleType()!;

    // Access each tuple item's type
    for (int i = 0; i < tuple.ItemsTypes.Length; i++)
    {
        ReducedTypeDeclaration itemType = tuple.ItemsTypes[i];
        TypeDeclaration resolved = itemType.ReducedType;
        // resolved is the fully-reduced type for position i
    }
}
```

**How it works internally:** `IsTuple()` returns true when `TupleType()` is not null. `TupleType()` finds an `ITupleTypeProviderKeyword`, then checks that non-tuple items are denied — i.e. `items` or `unevaluatedItems` is `false`.

### Pattern 2: Array with Prefix Tuple

Has both `prefixItems` and `items` (items is a schema, not `false`).

```json
{
    "type": "array",
    "prefixItems": [{ "type": "string" }],
    "items": { "type": "number" }
}
```

**Detection:**

```csharp
TupleTypeDeclaration? tuple = typeDeclaration.TupleType();
ArrayItemsTypeDeclaration? nonTupleItems = typeDeclaration.NonTupleItemsType();

if (tuple is not null && nonTupleItems is not null)
{
    // Has both prefix items and trailing items
    // tuple.ItemsTypes[i] — types for fixed prefix positions
    // nonTupleItems.ReducedType — type for items after the prefix
}
```

### Pattern 3: Plain Array

Has only `items` (no `prefixItems`).

```json
{
    "type": "array",
    "items": { "type": "string" }
}
```

**Detection:**

```csharp
ArrayItemsTypeDeclaration? itemsType = typeDeclaration.ArrayItemsType();

if (itemsType is not null && !typeDeclaration.IsTuple())
{
    TypeDeclaration elementType = itemsType.ReducedType;
    // elementType is the type of every array element
}
```

### Explicit vs Implicit

Each pattern has explicit and implicit variants:

```csharp
// Explicit — declared directly on this schema
TupleTypeDeclaration? explicit = typeDeclaration.ExplicitTupleType();

// Implicit — discovered through composition (e.g. via allOf)
TupleTypeDeclaration? implicit = typeDeclaration.ImplicitTupleType();

// Combined — returns explicit if available, otherwise implicit
TupleTypeDeclaration? combined = typeDeclaration.TupleType();
```

The same pattern applies to `ExplicitArrayItemsType()` / `ArrayItemsType()` and `ExplicitNonTupleItemsType()` / `NonTupleItemsType()`.

---

## Tensors and Numeric Arrays

### Fixed-size arrays and dimensions

The `ArrayDimension()` method calculates the fixed size of an array by examining `IArrayLengthConstantValidationKeyword` keywords (`minItems`, `maxItems`). It returns a fixed dimension only when all constraints agree on a single value.

```json
{
    "type": "array",
    "minItems": 3,
    "maxItems": 3,
    "items": { "type": "number" }
}
```

**Detection:**

```csharp
int? dimension = typeDeclaration.ArrayDimension();
// Returns 3 — minItems == maxItems

int? rank = typeDeclaration.ArrayRank();
// Returns 1 for a flat array, 2 for array-of-arrays, etc.
// Computed recursively through nested array items types

bool fixedSize = typeDeclaration.IsFixedSizeArray();
// True if every rank has a fixed dimension

int? bufferSize = typeDeclaration.ArrayValueBufferSize();
// Total element count across all ranks (product of dimensions)
```

**How `ArrayDimension()` works:** Iterates `IArrayLengthConstantValidationKeyword` keywords, examines each keyword's `Operator` (Equals, GreaterThanOrEquals, LessThanOrEquals, etc.) and value, then returns the dimension if minimum == maximum.

The `Operator` enum:

```csharp
public enum Operator
{
    None,
    Equals,
    NotEquals,
    LessThan,
    LessThanOrEquals,
    GreaterThan,
    GreaterThanOrEquals,
    MultipleOf,
}
```

### Numeric arrays

A numeric array is one whose items are all numeric types:

```csharp
bool isNumeric = typeDeclaration.IsNumericArray();
// True if ArrayItemsType().ReducedType.ImpliedCoreTypes() includes Number

bool isFixedNumeric = typeDeclaration.IsFixedSizeNumericArray();
// IsFixedSizeArray() && IsNumericArray()
```

**Used for:** Generating `TryGetNumericValues()` methods, optimised buffer handling, and tensor-like static properties (`Dimension`, `Rank`, `ValueBufferSize`).

---

## Object and Property Patterns

### Keyword interfaces involved

| Interface | Keywords | Purpose |
|-----------|----------|---------|
| `IObjectPropertyValidationKeyword` | `properties` | Named property schemas |
| `IObjectRequiredPropertyValidationKeyword` | `required` | Required property list |
| `IFallbackObjectPropertyTypeProviderKeyword` | `additionalProperties` | Fallback type for unknown properties |
| `IObjectPatternPropertyValidationKeyword` | `patternProperties` | Regex-matched property schemas |
| `IObjectPropertyNameSubschemaValidationKeyword` | `propertyNames` | Schema for property name strings |
| `IObjectDependentRequiredValidationKeyword` | `dependentRequired` | Conditional required properties |
| `IObjectPropertyDependentSchemasValidationKeyword` | `dependentSchemas` | Conditional schemas |
| `IPropertyCountConstantValidationKeyword` | `minProperties`, `maxProperties` | Property count constraints |

### Named properties

```csharp
// All properties (local + composed via allOf, etc.)
IReadOnlyList<PropertyDeclaration> props = typeDeclaration.PropertyDeclarations;

foreach (PropertyDeclaration prop in props)
{
    string jsonName = prop.JsonPropertyName;          // JSON property name
    TypeDeclaration propType = prop.ReducedPropertyType;  // Fully resolved type
    RequiredOrOptional req = prop.RequiredOrOptional;     // Required or Optional
    LocalOrComposed loc = prop.LocalOrComposed;           // Local or Composed
}
```

**`RequiredOrOptional` enum:**

```csharp
public enum RequiredOrOptional
{
    Required,           // Required by the 'required' keyword
    ComposedRequired,   // Required via composition (allOf, etc.)
    Optional,           // Not required
}
```

**`LocalOrComposed` enum:**

```csharp
public enum LocalOrComposed
{
    Local,     // Defined directly on this schema
    Composed,  // Inherited from a subschema via allOf/anyOf/oneOf
}
```

### Filtering properties

```csharp
// Only explicitly declared properties (local or required)
IReadOnlyCollection<PropertyDeclaration>? explicit =
    typeDeclaration.ExplicitProperties();

// Only required properties that are locally declared
IReadOnlyCollection<PropertyDeclaration>? required =
    typeDeclaration.ExplicitRequiredProperties();
```

### Fallback property type and map objects

When a schema defines `additionalProperties` (or `unevaluatedProperties`), it provides a *fallback type* — the schema applied to any property not matched by `properties` or `patternProperties`. This is accessed through `IFallbackObjectPropertyTypeProviderKeyword`.

A **map object** is a schema that has a fallback property type, making it behave like a `Dictionary<string, T>`. The classic example is a schema with no named `properties` but an `additionalProperties` schema that constrains all values:

```json
{
    "type": "object",
    "additionalProperties": { "type": "number" }
}
```

This is equivalent to `Dictionary<string, double>` — any property name is allowed, but all values must be numbers. A schema can also combine named properties with a fallback type (like a dictionary with well-known keys that have more specific types):

```json
{
    "type": "object",
    "properties": {
        "name": { "type": "string" }
    },
    "additionalProperties": { "type": "number" }
}
```

**Detection:**

```csharp
FallbackObjectPropertyType? fallback = typeDeclaration.FallbackObjectPropertyType();

if (fallback is not null)
{
    TypeDeclaration additionalType = fallback.ReducedType;
    bool isExplicit = fallback.IsExplicit;
    // additionalType is the schema for unknown properties
}

// IsMapObject() is true whenever a fallback property type exists —
// the type supports arbitrary string-keyed access beyond its named properties
bool isMap = typeDeclaration.IsMapObject();
```

There are also scoped variants that distinguish where the fallback comes from:

```csharp
// Fallback type from local schema only (additionalProperties on this type)
FallbackObjectPropertyType? local =
    typeDeclaration.LocalEvaluatedPropertyType();

// Fallback type considering both local and applied (composed) scopes
// (includes unevaluatedProperties from parent schemas)
FallbackObjectPropertyType? localAndApplied =
    typeDeclaration.LocalAndAppliedEvaluatedPropertyType();
```

### Pattern properties

```csharp
var patternProps = typeDeclaration.PatternProperties();

if (patternProps is not null)
{
    foreach (var (keyword, declarations) in patternProps)
    {
        foreach (PatternPropertyDeclaration pp in declarations)
        {
            string regex = pp.Pattern;                // Regex pattern
            TypeDeclaration ppType = pp.ReducedType;  // Schema for matching properties
        }
    }
}
```

### Property names, dependent schemas

```csharp
// propertyNames subschema
SingleSubschemaKeywordTypeDeclaration? propNames =
    typeDeclaration.PropertyNamesSubschemaType();

// dependentSchemas
var depSchemas = typeDeclaration.DependentSchemasSubschemaTypes();
```

### Validation requirements

```csharp
// Does this type need to count properties during validation?
bool needsCount = typeDeclaration.RequiresPropertyCount();

// Does this type need to enumerate all properties during validation?
bool needsEnum = typeDeclaration.RequiresObjectEnumeration();

// Does this type need to track which properties have been evaluated
// (for unevaluatedProperties)?
bool needsTracking = typeDeclaration.RequiresPropertyEvaluationTracking();
```

---

## Composition Patterns

### Keyword interfaces involved

| Interface | Keyword | Purpose |
|-----------|---------|---------|
| `IAllOfSubschemaValidationKeyword` | `allOf` | All subschemas must validate |
| `IAnyOfSubschemaValidationKeyword` | `anyOf` | At least one subschema must validate |
| `IOneOfSubschemaValidationKeyword` | `oneOf` | Exactly one subschema must validate |
| `INotValidationKeyword` | `not` | Subschema must NOT validate |
| `ICompositionKeyword` | (marker) | Base marker for composition keywords |

### Accessing composition branches

```csharp
// allOf branches
var allOf = typeDeclaration.AllOfCompositionTypes();
if (allOf is not null)
{
    foreach (var (keyword, branches) in allOf)
    {
        foreach (TypeDeclaration branch in branches)
        {
            // Each branch is a subschema type
            TypeDeclaration reduced = branch.ReducedTypeDeclaration().ReducedType;
        }
    }
}

// anyOf branches
var anyOf = typeDeclaration.AnyOfCompositionTypes();

// oneOf branches
var oneOf = typeDeclaration.OneOfCompositionTypes();

// All composition types combined
IReadOnlyCollection<TypeDeclaration> all =
    typeDeclaration.CompositionTypeDeclarations();
```

### Discriminator detection (fast-path for anyOf/oneOf)

The code generator can detect when anyOf or oneOf branches are distinguishable by a single property with distinct constant values — enabling a hash-based fast path instead of sequential validation.

```csharp
// For oneOf
if (CodeGeneratorExtensions.TryGetOneOfDiscriminator(
    subschemaTypes,
    out string? discriminatorPropertyName,
    out List<(string Value, int BranchIndex)>? discriminatorValues,
    out JsonValueKind discriminatorValueKind,
    requireRequired: true,      // oneOf requires the discriminator to be 'required'
    allowPartial: false))
{
    // discriminatorPropertyName: e.g. "type"
    // discriminatorValues: e.g. [("dog", 0), ("cat", 1), ("bird", 2)]
    // discriminatorValueKind: e.g. JsonValueKind.String
}
```

**Key difference between anyOf and oneOf discriminators:**
- `oneOf` requires `requireRequired: true` — the discriminator property must be in the `required` array
- `anyOf` uses `requireRequired: false` — the discriminator property doesn't need to be required; if absent, the fast path simply falls through to sequential evaluation

**Algorithm:** For each branch, check if any property has a single constant value (`SingleConstantValue()`). If all branches have the same property name with distinct constant values of the same `JsonValueKind`, that property is the discriminator.

### Conditional schemas (if/then/else)

```csharp
SingleSubschemaKeywordTypeDeclaration? ifType =
    typeDeclaration.IfSubschemaType();

SingleSubschemaKeywordTypeDeclaration? thenType =
    typeDeclaration.ThenSubschemaType();

SingleSubschemaKeywordTypeDeclaration? elseType =
    typeDeclaration.ElseSubschemaType();

if (ifType is not null)
{
    TypeDeclaration condition = ifType.ReducedType;
    string pathModifier = ifType.KeywordPathModifier;  // e.g. "if"
}
```

### anyOf constant values (enum-like patterns)

```csharp
// When anyOf contains only constant values (enum-like)
var constants = typeDeclaration.AnyOfConstantValues();

if (constants is not null)
{
    foreach (var (keyword, values) in constants)
    {
        // values is a dictionary of constant value entries
    }
}
```

---

## Type Reduction

Type reduction follows `$ref`, `$dynamicRef`, and similar reference keywords to resolve a type to its ultimate target. This is essential because many schemas are just references to definitions.

```csharp
// Can this type be reduced (does it use $ref, $dynamicRef, etc.)?
bool canReduce = typeDeclaration.CanReduce();

// Get the fully-reduced type
ReducedTypeDeclaration reduced = typeDeclaration.ReducedTypeDeclaration();
TypeDeclaration resolvedType = reduced.ReducedType;
JsonReference pathModifier = reduced.ReducedPathModifier;
```

**`ReducedTypeDeclaration` struct:**

```csharp
public readonly struct ReducedTypeDeclaration
{
    public TypeDeclaration ReducedType { get; }        // The resolved type
    public JsonReference ReducedPathModifier { get; }  // Path from original to reduced
}
```

All the typed result objects (e.g. `PropertyDeclaration`, `ArrayItemsTypeDeclaration`, `TupleTypeDeclaration`, `FallbackObjectPropertyType`) expose both `.ReducedType` (resolved) and `.UnreducedType` (original), so you can work with either.

### Sibling-hiding keywords

Some keywords like `$ref` (in older drafts) hide sibling keywords. This affects pattern detection:

```csharp
bool hidesSiblings = typeDeclaration.HasSiblingHidingKeyword();
// If true, sibling keywords are ignored and the type reduces to the ref target
```

**Driven by:** `IHidesSiblingsKeyword` interface.

---

## String and Numeric Validation Patterns

### String validation

```csharp
// Does this type have any string-specific validation keywords?
bool needsStringValidation = typeDeclaration.RequiresStringValueValidation();

// Does validation need the string length?
bool needsLength = typeDeclaration.RequiresStringLength();

// Get format (e.g. "date-time", "uri", "email")
string? format = typeDeclaration.Format();
string? explicitFormat = typeDeclaration.ExplicitFormat();
bool isFormatAssertion = typeDeclaration.IsFormatAssertion();
```

**Driven by:** `IStringValueValidationKeyword`, `IStringLengthConstantValidationKeyword`, `IFormatValidationKeyword`.

### Numeric validation

```csharp
// Draft-07 style exclusive min/max modifiers
bool hasExclusiveMax = typeDeclaration.HasExclusiveMaximumModifier();
bool hasExclusiveMin = typeDeclaration.HasExclusiveMinimumModifier();
```

**Driven by:** `IExclusiveMaximumBooleanKeyword`, `IExclusiveMinimumBooleanKeyword`, `INumberConstantValidationKeyword`.

---

## Constants, Defaults, and Deprecation

```csharp
// Single constant value (from 'const' keyword)
JsonElement constValue = typeDeclaration.SingleConstantValue();
JsonElement explicitConst = typeDeclaration.ExplicitSingleConstantValue();

// Default value
bool hasDefault = typeDeclaration.HasDefaultValue();
JsonElement defaultValue = typeDeclaration.DefaultValue();

// Deprecation
if (typeDeclaration.IsDeprecated(out string? reason))
{
    // reason contains the deprecation message, if any
}
```

---

## Metadata and the Caching Pattern

`TypeDeclaration` uses a metadata dictionary for caching computed results. All extension methods follow this pattern:

```csharp
public static bool IsNumericArray(this TypeDeclaration that)
{
    if (!that.TryGetMetadata(nameof(IsNumericArray), out bool? isNumericArray))
    {
        // Guard against recursive schemas
        that.SetMetadata(nameof(IsNumericArray), false);

        isNumericArray = GetIsNumericArray(that);

        that.SetMetadata(nameof(IsNumericArray), isNumericArray);
    }

    return isNumericArray ?? false;
}
```

**Key points:**
- Results are computed once and cached via `SetMetadata<T>(string key, T value)`
- Retrieved via `TryGetMetadata<T>(string key, out T? value)`
- Recursive schemas are handled by setting a sentinel value before computation
- After `BuildComplete` is true, all patterns are safe to query

---

## SubschemaInfo — Tracking What Subschemas Need

When the `StandaloneEvaluatorGenerator` collects subschemas for validation, it wraps each in a `SubschemaInfo`:

```csharp
internal sealed class SubschemaInfo
{
    public string MethodName { get; }           // Generated method name (e.g. "EvaluateAllOf0")
    public string SchemaPath { get; }           // Schema path (e.g. "#/allOf/0")
    public TypeDeclaration TypeDeclaration { get; }
    public bool UseEvaluatedItems { get; }      // Needs items evaluation tracking
    public bool UseEvaluatedProperties { get; } // Needs property evaluation tracking
    public string? PathFieldName { get; set; }  // Static field for evaluation path
    public string? SchemaPathFieldName { get; set; } // Static field for schema path
}
```

**`UseEvaluatedItems` / `UseEvaluatedProperties`** are set from:

```csharp
typeDeclaration.RequiresItemsEvaluationTracking()
typeDeclaration.RequiresPropertyEvaluationTracking()
```

These determine whether the generated validation code needs to pass and merge bitmasks tracking which items/properties have been evaluated — required for `unevaluatedItems` and `unevaluatedProperties` support.

---

## Validation Handlers and Priority

Validation handlers inspect `TypeDeclaration` and emit validation code. They are ordered by priority:

| Priority | Name | Value | Handlers |
|----------|------|-------|----------|
| CoreType | 1000 | `type` keyword validation | `TypeValidationHandler` |
| Default | `uint.MaxValue / 2` | Most validation keywords | `ConstValidationHandler`, `StringValidationHandler`, `NumberValidationHandler`, `FormatValidationHandler` |
| Composition | Default + 1000 | Composition keywords | `AllOfSubschemaValidationHandler`, `AnyOfSubschemaValidationHandler`, `OneOfSubschemaValidationHandler`, `NotSubschemaValidationHandler`, `IfThenElseValidationHandler` |
| AfterComposition | Composition + 1000 | Object/Array (need composition results) | `ObjectValidationHandler`, `ArrayValidationHandler` |
| Last | `uint.MaxValue` | Unevaluated keywords (need everything) | `UnevaluatedPropertyValidationHandler`, `UnevaluatedItemsValidationHandler` |

Handlers are sorted ascending by priority. Each handler queries `TypeDeclaration` to decide whether to emit code:

```csharp
// ObjectValidationHandler checks for object-related keywords
// before emitting any validation code
public override CodeGenerator AppendValidationCode(
    CodeGenerator generator,
    TypeDeclaration typeDeclaration,
    bool validateOnly)
{
    // Delegates to child handlers: PropertyCountValidationHandler,
    // PropertiesValidationHandler, PatternPropertiesValidationHandler, etc.
}
```

---

## Keyword Interface Hierarchy (Simplified)

```
IKeyword
├── IValidationKeyword
│   └── IValueKindValidationKeyword
│       ├── ICoreTypeValidationKeyword          ── type
│       ├── IArrayValidationKeyword
│       │   ├── IArrayLengthConstantValidationKeyword  ── minItems, maxItems
│       │   ├── IArrayContainsValidationKeyword        ── contains
│       │   └── IUniqueItemsArrayValidationKeyword     ── uniqueItems
│       ├── IObjectValidationKeyword
│       │   ├── IObjectPropertyValidationKeyword       ── properties
│       │   ├── IObjectRequiredPropertyValidationKeyword ── required
│       │   ├── IObjectPatternPropertyValidationKeyword  ── patternProperties
│       │   └── IObjectDependentRequiredValidationKeyword ── dependentRequired
│       ├── IStringValidationKeyword
│       │   └── IStringLengthConstantValidationKeyword ── minLength, maxLength
│       ├── INumberValidationKeyword                   ── minimum, maximum, etc.
│       └── IFormatValidationKeyword                   ── format
│
├── ISubschemaProviderKeyword
│   ├── IAllOfSubschemaValidationKeyword               ── allOf
│   ├── IAnyOfSubschemaValidationKeyword               ── anyOf
│   ├── IOneOfSubschemaValidationKeyword               ── oneOf
│   └── ISingleSubschemaProviderKeyword                ── if, then, else, not, etc.
│
├── ITupleTypeProviderKeyword                          ── prefixItems
├── IArrayItemsTypeProviderKeyword                     ── items
├── IFallbackObjectPropertyTypeProviderKeyword         ── additionalProperties
├── IPropertyProviderKeyword                           ── properties (type building)
├── IReferenceKeyword                                  ── $ref
├── IHidesSiblingsKeyword                              ── $ref (draft-07 and earlier)
├── IAnnotationProducingKeyword                        ── title, description, etc.
│
├── IDefaultValueProviderKeyword                       ── default
├── IDeprecatedKeyword                                 ── deprecated
├── IFormatProviderKeyword                             ── format (non-validation)
└── INonStructuralKeyword                              ── $comment, etc.
```

---

## Quick Reference: Common Pattern Queries

| Pattern | Query | Returns |
|---------|-------|---------|
| Is it a tuple? | `typeDeclaration.IsTuple()` | `bool` |
| Tuple item types | `typeDeclaration.TupleType()?.ItemsTypes` | `ReducedTypeDeclaration[]` |
| Array element type | `typeDeclaration.ArrayItemsType()?.ReducedType` | `TypeDeclaration` |
| Items after prefix | `typeDeclaration.NonTupleItemsType()?.ReducedType` | `TypeDeclaration` |
| Fixed array size | `typeDeclaration.ArrayDimension()` | `int?` |
| Array nesting depth | `typeDeclaration.ArrayRank()` | `int?` |
| Numeric array? | `typeDeclaration.IsNumericArray()` | `bool` |
| Fixed-size tensor? | `typeDeclaration.IsFixedSizeNumericArray()` | `bool` |
| Named properties | `typeDeclaration.PropertyDeclarations` | `IReadOnlyList<PropertyDeclaration>` |
| Required properties | `typeDeclaration.ExplicitRequiredProperties()` | `IReadOnlyCollection<PropertyDeclaration>?` |
| Additional props type | `typeDeclaration.FallbackObjectPropertyType()?.ReducedType` | `TypeDeclaration` |
| Pattern properties | `typeDeclaration.PatternProperties()` | `IReadOnlyDictionary<..., ...>?` |
| Map object? | `typeDeclaration.IsMapObject()` | `bool` |
| allOf branches | `typeDeclaration.AllOfCompositionTypes()` | `IReadOnlyDictionary<..., ...>?` |
| anyOf branches | `typeDeclaration.AnyOfCompositionTypes()` | `IReadOnlyDictionary<..., ...>?` |
| oneOf branches | `typeDeclaration.OneOfCompositionTypes()` | `IReadOnlyDictionary<..., ...>?` |
| if/then/else | `typeDeclaration.IfSubschemaType()` etc. | `SingleSubschemaKeywordTypeDeclaration?` |
| Allowed JSON types | `typeDeclaration.AllowedCoreTypes()` | `CoreTypes` |
| Implied JSON types | `typeDeclaration.ImpliedCoreTypes()` | `CoreTypes` |
| Format string | `typeDeclaration.Format()` | `string?` |
| Constant value | `typeDeclaration.SingleConstantValue()` | `JsonElement` |
| Can reduce ($ref)? | `typeDeclaration.CanReduce()` | `bool` |
| Reduced type | `typeDeclaration.ReducedTypeDeclaration()` | `ReducedTypeDeclaration` |
| Is deprecated? | `typeDeclaration.IsDeprecated(out reason)` | `bool` |