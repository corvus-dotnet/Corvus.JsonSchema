# Corvus.Json V4 to Corvus.Text.Json V5 Migration Instructions for Copilot

This document provides structured transformation rules for migrating C# code from Corvus.Json (V4) to Corvus.Text.Json (V5). Use these patterns to systematically transform code.

> **Note:** The `Corvus.Text.Json.Migration.Analyzers` NuGet package detects these patterns automatically and provides code fixes for many of them. Each transformation below includes the corresponding diagnostic ID (e.g., CVJ001). See the [Migration Analyzers Reference](../MigrationAnalyzers.md) for the complete list.

---

## NAMESPACE TRANSFORMATIONS (CVJ001)

Replace all occurrences:

```
using Corvus.Json;                    →  using Corvus.Text.Json;
using Corvus.Json.Patch;              →  using Corvus.Text.Json.Patch;
```

## TYPE NAME TRANSFORMATIONS (CVJ007, CVJ009)

| V4 Type | V5 Type |
|---------|---------|
| `Corvus.Json.JsonAny` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonString` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonNumber` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonInteger` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonBoolean` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonObject` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonArray` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonNull` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.ParsedValue<T>` | `Corvus.Text.Json.ParsedJsonDocument<T>` |
| `Corvus.Json.ValidationContext` | (removed - use `bool` or `JsonSchemaResultsCollector`) |
| `Corvus.Json.ValidationLevel` | `Corvus.Text.Json.JsonSchemaResultsLevel` |

---

## PARSING TRANSFORMATIONS (CVJ002, CVJ008)

### Pattern 1: Direct Parse
```csharp
// V4
MyType v4 = MyType.Parse(jsonString);

// V5
using var doc = ParsedJsonDocument<MyType>.Parse(jsonString);
MyType v5 = doc.RootElement;
```

### Pattern 2: ParsedValue
```csharp
// V4
using ParsedValue<MyType> parsed = ParsedValue<MyType>.Parse(jsonString);
MyType v4 = parsed.Instance;

// V5
using ParsedJsonDocument<MyType> doc = ParsedJsonDocument<MyType>.Parse(jsonString);
MyType v5 = doc.RootElement;
```

### Pattern 3: FromJson with JsonDocument
```csharp
// V4
using JsonDocument jsonDoc = JsonDocument.Parse(jsonString);
MyType v4 = MyType.FromJson(jsonDoc.RootElement);

// V5
using var doc = ParsedJsonDocument<MyType>.Parse(jsonString);
MyType v5 = doc.RootElement;
```

### Pattern 4: ParseValue (self-owned)
```csharp
// V4
MyType v4 = MyType.ParseValue(jsonString);

// V5 (identical)
MyType v5 = MyType.ParseValue(jsonString);
```

---

## PROPERTY ACCESS TRANSFORMATIONS (CVJ005)

### Basic property access (UNCHANGED)
```csharp
// V4 and V5 are identical
string name = (string)person.Name;
int age = (int)person.Age;
```

### Property count
```csharp
// V4
int count = v4.Count;

// V5
int count = v5.GetPropertyCount();
```

### TryGetProperty
```csharp
// V4
if (v4.TryGetProperty("name", out JsonAny value)) { }

// V5 (prefer UTF-8 literal for zero-allocation)
if (v5.TryGetProperty("name"u8, out JsonElement value)) { }
```

### Property indexer
```csharp
// V4
JsonAny value = v4["propertyName"];

// V5 (prefer UTF-8 literal)
JsonElement value = v5["propertyName"u8];
```

---

## CORE TYPE ACCESSOR TRANSFORMATIONS (CVJ010)

In V4, when a type composed multiple core types (e.g. a union of string and boolean), V4 would not emit value accessors (casts, `GetString()`, indexers, etc.) directly on that type. You had to use `AsString`, `AsNumber`, etc. to reach a single-core-type that did have those accessors. V5 emits value accessors for all composed core types directly on the type, so the `As*` indirection is no longer needed.

### String access
```csharp
// V4
JsonString asString = v4.AsString;
string value = (string)asString;
// OR
string value = (string)v4.AsString;

// V5
string value = (string)v5;
// OR
v5.TryGetValue(out string? value);
// OR
string? value = v5.GetString();
```

### Number access
```csharp
// V4
JsonNumber asNumber = v4.AsNumber;
int value = (int)asNumber;
// OR
int value = (int)v4.AsNumber;

// V5
int value = (int)v5;
// OR
v5.TryGetValue(out int value);
// OR
int value = v5.GetInt32();
```

### Boolean access
```csharp
// V4
JsonBoolean asBool = v4.AsBoolean;
bool value = (bool)asBool;

// V5
bool value = (bool)v5;
// OR
v5.TryGetValue(out bool value);
```

### AsAny / AsObject / AsArray
```csharp
// V4
JsonAny any = v4.AsAny;
JsonObject obj = v4.AsObject;
JsonArray arr = v4.AsArray;

// V5 - value accessors are emitted directly on the type
JsonElement element = v5;  // implicit
// Objects: v5.EnumerateObject(), v5.TryGetProperty(), typed properties, v5.GetPropertyCount()
// Arrays: v5.EnumerateArray(), v5[index], v5.GetArrayLength()
```

---

## TYPE COERCION TRANSFORMATIONS (CVJ004, CVJ006)

```csharp
// V4
TargetType target = source.As<TargetType>();

// V5
TargetType target = TargetType.From(source);
```

---

## VALIDATION TRANSFORMATIONS (CVJ003)

### Basic validation (bool only)
```csharp
// V4
ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
bool isValid = result.IsValid;

// V5
bool isValid = v5.EvaluateSchema();
```

### Detailed validation with results
```csharp
// V4
ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);
if (!result.IsValid)
{
    foreach (ValidationResult r in result.Results)
    {
        // r.Message, r.Valid, etc.
    }
}

// V5
using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
bool isValid = v5.EvaluateSchema(collector);
if (!isValid)
{
    foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
    {
        if (!r.IsMatch)
        {
            string message = r.GetMessageText();
            string path = r.GetDocumentEvaluationLocationText();
            string schemaLocation = r.GetSchemaEvaluationLocationText();
        }
    }
}
```

### Validation level mapping
| V4 `ValidationLevel` | V5 `JsonSchemaResultsLevel` |
|----------------------|-----------------------------|
| `Flag` | (no collector - just use `EvaluateSchema()`) |
| `Basic` | `JsonSchemaResultsLevel.Basic` |
| `Detailed` | `JsonSchemaResultsLevel.Detailed` |
| `Verbose` | `JsonSchemaResultsLevel.Verbose` |

---

## OBJECT CREATION TRANSFORMATIONS (CVJ013)

### Static Create
```csharp
// V4
MyType v4 = MyType.Create(
    name: "Alice",
    age: 30);

// V5 — convenience overload (preferred)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MyType.CreateBuilder(
    workspace,
    name: "Alice",
    age: 30);
MyType v5 = builder.RootElement;

// V5 — delegate overload (for advanced scenarios like From() conversions)
using var builder2 = MyType.CreateBuilder(
    workspace,
    (ref MyType.Builder b) => b.Create(
        name: "Alice",
        age: 30));
```

### Nested object creation
```csharp
// V4
OuterType v4 = OuterType.Create(
    address: OuterType.AddressType.Create(
        city: "London",
        street: "Baker Street"),
    name: "Sherlock");

// V5 — convenience overload with Build() for nested objects
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = OuterType.CreateBuilder(
    workspace,
    address: OuterType.AddressType.Build(
        static (ref OuterType.AddressType.Builder ab) => ab.Create(
            city: "London",
            street: "Baker Street")),
    name: "Sherlock");
```

### Array creation with FromItems
```csharp
// V4
MyArray v4 = MyArray.FromItems(item1, item2, item3);

// V5
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MyArray.CreateBuilder(
    workspace,
    MyArray.Build(
        static (ref MyArray.Builder b) =>
        {
            b.AddItem(item1);
            b.AddItem(item2);
            b.AddItem(item3);
        }));
```

### Tuple creation
```csharp
// V4
MyTuple v4 = MyTuple.Create("hello", 42, true);

// V5 — CreateBuilder convenience (preferred for closed tuples)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MyTuple.CreateBuilder(workspace, "hello", 42, true);

// V5 — Build + CreateBuilder (two-step)
MyTuple.Source source = MyTuple.Build("hello", 42, true);
using var builder2 = MyTuple.CreateBuilder(workspace, source);

// V5 — delegate (required for open tuples with additional items)
using var builder3 = MyTuple.CreateBuilder(
    workspace,
    MyTuple.Build(
        static (ref MyTuple.Builder b) => b.CreateTuple(
            item1: "hello",
            item2: 42,
            item3: true)));
```

### Numeric array creation from span
```csharp
// V4 (fixed-size tensor)
MyVector v4 = MyVector.FromValues([1, 2, 3]);

// V5 — CreateBuilder convenience (preferred)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MyVector.CreateBuilder(workspace, [1, 2, 3]);

// V5 — Build + CreateBuilder (two-step)
MyVector.Source source = MyVector.Build([1, 2, 3]);
using var builder2 = MyVector.CreateBuilder(workspace, source);

// V5 — delegate route (for combining with other builder operations)
using var builder3 = MyVector.CreateBuilder(
    workspace,
    MyVector.Build(
        static (ref MyVector.Builder b) => b.CreateTensor([1, 2, 3])));
```

> **Note:** `Build(ReadOnlySpan<T>)` and `CreateBuilder(workspace, ReadOnlySpan<T>)` work on **all** numeric array types — both fixed-size tensors and variable-length arrays. For fixed-size tensors, the span must contain exactly `ValueBufferSize` elements. For variable-length arrays, any length is accepted.

---

## MUTATION TRANSFORMATIONS (CVJ011, CVJ021)

### V4 functional (With*) to V5 imperative (Set*)

**CRITICAL**: V4 uses functional `With*()` returning new instances. V5 uses imperative `Set*()` mutating in place.

```csharp
// V4 - functional, returns NEW instance
MyType updated = v4.WithName("Bob");
// IMPORTANT: v4 is UNCHANGED, must use 'updated'

// V5 - requires workspace + builder, mutates in place
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = ParsedJsonDocument<MyType>.Parse(json);
using var builder = doc.RootElement.CreateBuilder(workspace);
MyType.Mutable root = builder.RootElement;
root.SetName("Bob");
// 'root' is now mutated; use root.ToString() or builder.RootElement
```

### Full mutation workflow pattern (V5)
```csharp
// V5 standard mutation pattern
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<MyType> doc = ParsedJsonDocument<MyType>.Parse(json);
using JsonDocumentBuilder<MyType.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

MyType.Mutable root = builder.RootElement;

// Mutations (any order, all in-place)
root.SetName("NewName");
root.SetAge(25);
root.Address.SetCity("London");  // Deep mutation works directly

// Get result
string resultJson = root.ToString();
// OR
MyType immutableResult = builder.RootElement;  // Gets immutable view
```

### Chained With* calls
```csharp
// V4
MyType updated = v4
    .WithName("Bob")
    .WithAge(25)
    .WithEmail("bob@test.com");

// V5
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = ParsedJsonDocument<MyType>.Parse(json);
using var builder = doc.RootElement.CreateBuilder(workspace);
MyType.Mutable root = builder.RootElement;
root.SetName("Bob");
root.SetAge(25);
root.SetEmail("bob@test.com");
```

---

## PROPERTY REMOVAL TRANSFORMATIONS

```csharp
// V4 - set to default to remove optional property
MyType updated = v4.WithEmail(default);

// V5 - explicit Remove method
bool removed = root.RemoveEmail();

// V5 - generic removal by name
root.RemoveProperty("email"u8);
```

---

## ARRAY OPERATION TRANSFORMATIONS (CVJ012, CVJ014, CVJ015)

### Add item
```csharp
// V4
MyArray updated = v4.Add(newItem);

// V5
root.AddItem(newItem);
```

### Insert item
```csharp
// V4
MyArray updated = v4.Insert(1, newItem);

// V5
root.InsertItem(1, newItem);
```

### Set item at index
```csharp
// V4
MyArray updated = v4.SetItem(1, newItem);

// V5
root.SetItem(1, newItem);
```

### Remove at index
```csharp
// V4
MyArray updated = v4.RemoveAt(1);

// V5
root.RemoveAt(1);
```

### Replace by value (V5 only)
```csharp
// V5 only
bool replaced = root.Replace(oldItem, newItem);
```

### Remove by value (V5 only)
```csharp
// V5 only
bool removed = root.Remove(itemToRemove);
```

### Remove by predicate (V5 only)
```csharp
// V5 only
int removedCount = root.RemoveWhere(
    static (in ItemType.Mutable item) => (int)item.Id == 2);
```

---

## STRING ACCESS TRANSFORMATIONS (CVJ018)

### Delegate-based span access
```csharp
// V4 - delegate pattern for UTF-8
bool found = v4.Name.TryGetValue(searchBytes, (term, span) => span.SequenceEqual(term));

// V5 - direct span access
using UnescapedUtf8JsonString utf8 = v5.Name.GetUtf8String();
bool found = utf8.Span.SequenceEqual(searchBytes);
```

### Delegate-based char access
```csharp
// V4 - delegate pattern for UTF-16
int count = v4.Name.TryGetValue(0, (state, span) => span.Count('A'));

// V5 - direct span access
using UnescapedUtf16JsonString utf16 = v5.Name.GetUtf16String();
int count = utf16.Span.Count('A');
```

### ValueEquals (UNCHANGED)
```csharp
// V4 and V5 - identical
bool match = v5.Name.ValueEquals("expectedValue");
bool matchUtf8 = v5.Name.ValueEquals("expectedValue"u8);
```

---

## COMPOSITION TYPE TRANSFORMATIONS (oneOf/anyOf/allOf)

### allOf implicit conversion (UNCHANGED)
```csharp
// V4 and V5 - both support implicit conversion from composite to constituent
CompositeType composite = ...;
DocumentationType doc = composite;  // implicit
CountableType count = composite;    // implicit
```

### allOf property access - naming heuristics
```csharp
// V4 - property names may have "Value" suffix to avoid conflicts
int count = (int)v4Countable.CountValue;  // "count" property → CountValue

// V5 - different name reservations, so "count" may no longer need the suffix
long count = v5Countable.Count;  // "count" property → Count
```

> **Note:** Both V4 and V5 use "Value" and "Entity" suffixes in their naming heuristics to disambiguate generated names from reserved names (interface members, language keywords, etc.). V5 has different name reservations than V4, so some names that were previously disambiguated may now be available without a suffix, and vice versa. This may cause some property or type renaming when migrating.

### TryGetAs* methods

Both V4 and V5 emit `TryGetAs*()` methods for any union variant type. The method name is derived from whatever type the variant resolved to.

V4 reduced unformatted simple types to framework globals from `Corvus.Json.ExtendedTypes` (`Corvus.Json.JsonString`, `Corvus.Json.JsonBoolean`, `Corvus.Json.JsonInteger`, etc.) and `"type": "number"` with a numeric format to globals (`Corvus.Json.JsonInt32`, `Corvus.Json.JsonDouble`, etc.). However, V4 did **not** reduce `"type": "integer"` with a format — those became custom entity types (e.g. `OneOf1Entity`). V4 also had no equivalent of V5's `Json<Format>NotAsserted` global types.

V5 reduces **all** simple and format-typed variants to project-local global types (`JsonString`, `JsonInt32`, `JsonBoolean`, `JsonInt32NotAsserted`, etc.).

For a `oneOf` with `{"type":"string"}`, `{"type":"integer","format":"int32"}`, `{"type":"boolean"}`:

```csharp
// V4 — string and boolean reduce to framework globals; integer+format does NOT
if (v4.TryGetAsJsonString(out JsonString s)) { }         // framework built-in
if (v4.TryGetAsOneOf1Entity(out OneOf1Entity n)) { }     // custom entity (integer + format)
if (v4.TryGetAsJsonBoolean(out JsonBoolean b)) { }       // framework built-in

// V5 — all variants reduce to project-local global types
if (v5.TryGetAsJsonString(out JsonString s)) { }         // project-local global type
if (v5.TryGetAsJsonInt32(out JsonInt32 n)) { }            // project-local global type
if (v5.TryGetAsJsonBoolean(out JsonBoolean b)) { }       // project-local global type
```

### Match without context
```csharp
// V4 — string/boolean are framework globals; integer+format is custom entity
string result = v4.Match(
    static (in JsonString s) => $"string:{(string)s}",
    static (in MigrationUnion.OneOf1Entity n) => $"number:{(int)n}",
    static (in JsonBoolean b) => $"bool:{(bool)b}",
    static (in MyType v) => "fallback");

// V5 — all variants are project-local global types
string result = v5.Match(
    static (in JsonString s) => $"string:{(string)s}",
    static (in JsonInt32 n) => $"number:{(int)n}",
    static (in JsonBoolean b) => $"bool:{(bool)b}",
    static (in MyType v) => "fallback");
```

### Match with context
```csharp
// V4 — string/boolean are framework globals; integer+format is custom entity
string result = v4.Match(
    context,
    static (in JsonString s, in TContext ctx) => ...,
    static (in MigrationUnion.OneOf1Entity n, in TContext ctx) => ...,
    static (in MyType v, in TContext ctx) => ...);

// V5 — all variants are project-local global types
string result = v5.Match(
    context,
    static (in JsonString s, in TContext ctx) => ...,
    static (in JsonInt32 n, in TContext ctx) => ...,
    static (in MyType v, in TContext ctx) => ...);
```

---

## ENUM TRANSFORMATIONS

### Enum values (UNCHANGED)
```csharp
// V4 and V5 - identical
MyEnum active = MyEnum.EnumValues.Active;
```

### Enum Match with named parameters
```csharp
// V4 and V5 - Match uses named parameters based on enum values
string label = v5.Match(
    matchActive: static () => "Active status",
    matchInactive: static () => "Inactive status",
    matchPending: static () => "Pending status",
    defaultMatch: static () => "Unknown");
```

### Enum Match with context
```csharp
// V4 and V5
string label = v5.Match(
    contextValue,
    matchActive: static (ctx) => $"{ctx}: active",
    matchInactive: static (ctx) => $"{ctx}: inactive",
    defaultMatch: static (ctx) => $"{ctx}: unknown");
```

---

## SERIALIZATION TRANSFORMATIONS (CVJ016)

### ToString (UNCHANGED)
```csharp
// V4 and V5 - identical
string json = v5.ToString();
```

### WriteTo - DIFFERENT WRITER TYPE
```csharp
// V4 - uses System.Text.Json.Utf8JsonWriter
v4.WriteTo(systemTextJsonWriter);

// V5 - uses Corvus.Text.Json.Utf8JsonWriter (different type!)
v5.WriteTo(corvusWriter);
```

---

## CSPROJ / PACKAGE REFERENCE TRANSFORMATIONS (CVJ025)

### V4 packages
```xml
<!-- V4 -->
<PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.x.x" />
<PackageReference Include="Corvus.Json.SourceGenerator" Version="4.x.x">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

### V5 packages
```xml
<!-- V5 -->
<PackageReference Include="Corvus.Text.Json" Version="5.x.x" />
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.x.x">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

---

## CODE GENERATION CLI TRANSFORMATIONS

```bash
# V4
generatejsonschematypes --engine V4 --rootNamespace MyApp --outputPath generated/ schema.json

# V5 (--engine V5 is default, can be omitted)
generatejsonschematypes --rootNamespace MyApp --outputPath generated/ schema.json
```

---

## BEST PRACTICES FOR MIGRATION

### 1. Prefer the `CreateBuilder` convenience overload for object creation
When creating objects from scratch with known property values, use the convenience overload:
```csharp
// ✅ Preferred — convenience overload with named parameters
using var builder = MyType.CreateBuilder(workspace, name: "Alice", age: 30);

// ✅ Also good — delegate overload for advanced scenarios (From() conversions, conditional logic)
using var builder2 = MyType.CreateBuilder(workspace, (ref MyType.Builder b) =>
{
    b.Create(
        name: TargetType.NameEntity.From(source.Name),
        age: source.Age);
});
```

### 2. Use `static` lambdas
Always use `static` on builder delegates and Match lambdas that don't capture variables:
```csharp
// ✅ Good
MyType.Build(static (ref MyType.Builder b) => b.Create(...));
v5.Match(matchRed: static () => "Red", ...);

// ❌ Only omit static when capturing variables
var capturedValue = ...;
(ref MyType.Builder b) => b.Create(capturedValue);  // captures, cannot be static
```

### 3. Prefer UTF-8 literals
Use `"name"u8` for zero-allocation property access:
```csharp
// ✅ Zero allocation
v5.TryGetProperty("name"u8, out var value);
v5["name"u8];

// ⚠️ Allocates string
v5.TryGetProperty("name", out var value);
```

### 4. Dispose documents and workspaces
Always use `using` for ParsedJsonDocument, JsonWorkspace, and JsonDocumentBuilder:
```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = ParsedJsonDocument<MyType>.Parse(json);
using var builder = doc.RootElement.CreateBuilder(workspace);
```

### 5. Work with generated types directly
Avoid unnecessary extraction to primitives:
```csharp
// ✅ Good - generated types support formatting
Console.WriteLine($"Name: {person.Name}");

// ⚠️ Unnecessary extraction
Console.WriteLine($"Name: {person.Name.GetString()}");
```

---

## MIGRATION CHECKLIST

When migrating a file:

1. [ ] Update `using` directives: `Corvus.Json` → `Corvus.Text.Json`
2. [ ] Replace `ParsedValue<T>` with `ParsedJsonDocument<T>`
3. [ ] Replace `.Instance` with `.RootElement`
4. [ ] Replace `T.Parse(json)` with `ParsedJsonDocument<T>.Parse(json)`
5. [ ] Replace `.Count` with `.GetPropertyCount()` for object property count
6. [ ] Replace `.AsString`, `.AsNumber`, `.AsBoolean`, `.AsObject`, `.AsArray` — V5 emits value accessors directly on multi-core-type types, so use direct casts, `TryGetValue()`, typed properties, indexers, etc.
7. [ ] Replace `.As<T>()` with `T.From(source)`
8. [ ] Replace `Validate(ctx, level)` with `EvaluateSchema()` or `EvaluateSchema(collector)`
9. [ ] Replace `With*()` chains with `JsonWorkspace` + `CreateBuilder()` + `Set*()` calls
10. [ ] Replace `MyType.Create(...)` with `MyType.CreateBuilder(workspace, prop: value, ...)` (convenience overload) or delegate overload for `From()` conversions
11. [ ] Replace `FromItems(...)` with `CreateBuilder(workspace, Build(b => { b.AddItem(...); }))`
12. [ ] Replace `MyTuple.Create(a,b,c)` with `MyTuple.CreateBuilder(workspace, a, b, c)` (closed tuples) or delegate pattern (open tuples)
13. [ ] Replace `MyVector.FromValues(span)` with `MyVector.CreateBuilder(workspace, span)` or `MyVector.Build(span)`
14. [ ] Replace `.Add()`, `.Insert()`, `.RemoveAt()` with `.AddItem()`, `.InsertItem()`, `.RemoveAt()` on Mutable
15. [ ] Replace `TryGetValue(state, delegate)` span patterns with `GetUtf8String().Span` or `GetUtf16String().Span`
16. [ ] Update Match() and TryGetAs() type names: V4 framework globals for unformatted types (`Corvus.Json.JsonString`, `Corvus.Json.JsonBoolean`) and `"type":"number"` + format (`Corvus.Json.JsonInt32`) become project-local globals (`JsonString`, `JsonBoolean`, `JsonInt32`). V4 custom entity types for `"type":"integer"` + format (e.g. `OneOf1Entity`) also become project-local globals (e.g. `JsonInt32`). Check generated output for actual names.
17. [ ] Add `static` keyword to builder delegates and Match lambdas where possible
18. [ ] Update package references in .csproj
19. [ ] Review generated type/property names — V5 has different name reservations, so some names that previously needed "Value" or "Entity" suffixes may now be available without them (and vice versa)

---

## COMMON MIGRATION ERRORS AND FIXES

### Error: "Cannot implicitly convert type 'Corvus.Json.JsonAny' to 'Corvus.Text.Json.JsonElement'"
**Fix**: Update namespace imports and replace `JsonAny` with `JsonElement`

### Error: "'MyType' does not contain a definition for 'AsString'"
**Fix**: Replace `v4.AsString` with `(string)v5` or `v5.GetString()`

### Error: "'MyType' does not contain a definition for 'With*'"
**Fix**: Use `JsonWorkspace` + `CreateBuilder()` + `Set*()` pattern

### Error: "'MyType' does not contain a definition for 'Validate'"
**Fix**: Replace with `EvaluateSchema()` or `EvaluateSchema(collector)`

### Error: "'MyType' does not contain a definition for 'Count'"
**Fix**: Replace with `GetPropertyCount()` for property count

### Error: "'JsonString' could not be found"
**Fix**: V5 generates global simple types (e.g., `JsonString`, `JsonBoolean`) locally in the project namespace rather than providing them as framework types. Add a `using` directive for the project namespace that contains the generated types.

### Error: "Cannot convert lambda to delegate because it captures variables"
**Fix**: Remove `static` keyword if the lambda genuinely captures outer variables, OR refactor to use the context parameter overload of Match/Build.

### Error: "'MyType' does not contain a definition for 'Create'" (object)
**Fix**: Replace `MyType.Create(name: "Alice", age: 30)` with `MyType.CreateBuilder(workspace, name: "Alice", age: 30)`. The convenience overload takes the same named parameters as V4's `Create()`.

### Error: "'MyType' does not contain a definition for 'Create'" (tuple)
**Fix**: Replace `MyTuple.Create(a, b, c)` with `MyTuple.CreateBuilder(workspace, a, b, c)` for closed tuples, or `CreateBuilder(workspace, Build((ref b) => b.CreateTuple(a, b, c)))` for open tuples.

### Error: "'MyType' does not contain a definition for 'FromValues'" (numeric array)
**Fix**: Replace `MyVector.FromValues(span)` with `MyVector.CreateBuilder(workspace, span)` or `MyVector.Build(span)` + `CreateBuilder(workspace, source)`.
