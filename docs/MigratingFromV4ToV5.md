# Migrating from V4

This guide helps you migrate code written against the V4 code generator (`Corvus.Json.CodeGeneration`, known informally as "Corvus.Json") to the V5 code generator (`Corvus.Text.Json.CodeGeneration`, known informally as "Corvus.Text.Json").

V5 is a ground-up rewrite. The generated types are still readonly structs backed by JSON Schema, but the underlying architecture — memory management, mutation, and code generation — is fundamentally different. Most V4 patterns have a direct V5 equivalent, but some require rethinking.

> **Tip:** Install the [`Corvus.Text.Json.Migration.Analyzers`](MigrationAnalyzers.md) NuGet package to get Roslyn diagnostics that detect V4 patterns in your code and offer automatic code fixes. See the [Migration Analyzers Reference](MigrationAnalyzers.md) for the complete list of diagnostics.

## Table of Contents

- [Overview of Changes](#overview-of-changes)
- [Package and Namespace Changes](#package-and-namespace-changes)
- [Strongly-Typed Value Types](#strongly-typed-value-types)
- [Parsing](#parsing)
- [Property Access](#property-access)
- [JSON Property Names](#json-property-names)
- [Serialization (ToString / WriteTo)](#serialization)
- [Formatting (IFormattable, ISpanFormattable, IUtf8SpanFormattable)](#formatting-iformattable-ispanformattable-iutf8spanformattable)
- [Equality and Comparison](#equality-and-comparison)
- [Validation](#validation)
- [Creating Objects from Scratch](#creating-objects-from-scratch)
- [Building Nested Objects and Arrays](#building-nested-objects-and-arrays)
- [Mutating Object Properties](#mutating-object-properties)
- [Removing Properties](#removing-properties)
- [Default Property Values](#default-property-values)
- [Arrays](#arrays)
- [Numeric Arrays (Vectors)](#numeric-arrays-vectors)
- [Tuples](#tuples)
- [Core Type Accessors](#core-type-accessors)
- [Composition Types (`oneOf`, `anyOf`, `allOf`)](#composition-types-oneof-anyof-allof)
- [Enum Types](#enum-types)
- [Memory Management](#memory-management)
- [Code Generation](#code-generation)
- [Quick Reference Table](#quick-reference-table)

---

## Overview of Changes

| Aspect | V4 (Corvus.Json) | V5 (Corvus.Text.Json) |
|---|---|---|
| **Architecture** | Each value stores its own `JsonElement` or `ImmutableList<JsonObjectProperty>` backing | Values are lightweight indexes into a pooled `IJsonDocument` |
| **Mutation model** | Functional — `With*()` returns a new instance | Imperative — `Set*()` mutates in-place via `JsonDocumentBuilder` |
| **Memory** | `JsonElement` backed by `JsonDocument` | `ParsedJsonDocument<T>` backed by `ArrayPool<byte>` |
| **Parsing** | `MyType.Parse(json)` or `ParsedValue<MyType>.Parse(json)` | `ParsedJsonDocument<MyType>.Parse(json)` returning a disposable document, **or** `MyType.ParseValue(json)` for a self-owned copy |
| **Validation** | `entity.Validate(ValidationContext, ValidationLevel)` | `entity.EvaluateSchema()` returning `bool`, or `entity.EvaluateSchema(collector)` for detailed results |
| **Code generation** | `Corvus.Json.CodeGeneration` / `generatejsonschematypes` | `Corvus.Text.Json.CodeGeneration` / `generatejsonschematypes --engine V5` |
| **Target frameworks** | net8.0, netstandard2.0 | net8.0, net9.0, net10.0, netstandard2.0 |

---

## Package and Namespace Changes

| V4 | V5 |
|---|---|
| `Corvus.Json` namespace | `Corvus.Text.Json` namespace |
| `Corvus.Json.JsonAny` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonString` | `Corvus.Text.Json.JsonElement` (use `GetString()`, `TryGetValue()`) |
| `Corvus.Json.JsonNumber` | `Corvus.Text.Json.JsonElement` (use `GetInt32()`, `GetDouble()`, etc.) |
| `Corvus.Json.JsonBoolean` | `Corvus.Text.Json.JsonElement` (use `GetBoolean()`, `TryGetValue()`) |
| `Corvus.Json.JsonObject` | `Corvus.Text.Json.JsonElement` |
| `Corvus.Json.JsonArray` | `Corvus.Text.Json.JsonElement` |
| `System.Text.Json.JsonElement` | `Corvus.Text.Json.JsonElement` (different type!) |
| `System.Text.Json.JsonValueKind` | `Corvus.Text.Json.JsonValueKind` |
| `Corvus.Json.ParsedValue<T>` | `Corvus.Text.Json.ParsedJsonDocument<T>` |

> **Important**: `Corvus.Text.Json.JsonElement` is _not_ `System.Text.Json.JsonElement`. If you need to interop with `System.Text.Json`, you'll need explicit conversions.

---

## Strongly-Typed Value Types

V4 provided a fixed set of well-known types for common JSON values. V5 replaces most of these with direct access via operators and `TryGetValue()`, and adds support for arbitrary-precision numerics and UTF-8 URI/IRI types.

### Numeric types

| V4 (Corvus.Json) | V5 (Corvus.Text.Json) |
|---|---|
| `JsonNumber` | `GetInt32()`, `GetDouble()`, `TryGetValue(out int)`, `TryGetValue(out double)`, etc. |
| `JsonInteger` | `GetInt64()`, `TryGetValue(out long)` |
| `JsonInt32` | `GetInt32()`, `TryGetValue(out int)` |
| `JsonInt64` | `GetInt64()`, `TryGetValue(out long)` |
| `JsonInt128` | `TryGetValue(out Int128)` |
| `JsonDouble` | `GetDouble()`, `TryGetValue(out double)` |
| `JsonSingle` | `TryGetValue(out float)` |
| `JsonHalf` | `TryGetValue(out Half)` |
| `JsonDecimal` | `TryGetValue(out decimal)` |
| `JsonByte` | `TryGetValue(out byte)` |
| `JsonSByte` | `TryGetValue(out sbyte)` |
| `JsonInt16` | `TryGetValue(out short)` |
| `JsonUInt16` | `TryGetValue(out ushort)` |
| `JsonUInt32` | `TryGetValue(out uint)` |
| `JsonUInt64` | `TryGetValue(out ulong)` |
| `JsonUInt128` | `TryGetValue(out UInt128)` |
| N/A | `TryGetValue(out BigNumber)` — arbitrary-precision decimal (`Corvus.Numerics.BigNumber`) |
| N/A | `TryGetValue(out BigInteger)` — arbitrary-precision integer (`System.Numerics.BigInteger`) |

### String types

| V4 (Corvus.Json) | V5 (Corvus.Text.Json) |
|---|---|
| `JsonString` | `GetString()`, `GetUtf8String()`, `GetUtf16String()`, `TryGetValue(out string?)` |
| `v4.TryGetValue<TState>(state, callback)` (delegate with `ReadOnlySpan<byte>` or `ReadOnlySpan<char>`) | `GetUtf8String()` → `.Span` or `GetUtf16String()` → `.Span` |
| `JsonUri` | `TryGetValue(out Utf8UriValue)` |
| `JsonUriReference` | `TryGetValue(out Utf8UriReferenceValue)` |
| `JsonIri` | `TryGetValue(out Utf8IriValue)` |
| `JsonIriReference` | `TryGetValue(out Utf8IriReferenceValue)` |
| `JsonDate` | `TryGetValue(out LocalDate)` (NodaTime) |
| `JsonDateTime` | `TryGetValue(out OffsetDateTime)` (NodaTime), or `TryGetDateTimeOffset(out DateTimeOffset)` |
| `JsonTime` | `TryGetValue(out OffsetTime)` (NodaTime) |
| `JsonDuration` | `TryGetValue(out Period)` (NodaTime) |
| `JsonUuid` | `TryGetGuid(out Guid)` |

> **Note**: V5's `Utf8UriValue`, `Utf8UriReferenceValue`, `Utf8IriValue`, and `Utf8IriReferenceValue` are `readonly struct` types that hold the parsed URI/IRI as UTF-8 bytes, avoiding UTF-16 string allocations. V5's `BigNumber` provides arbitrary-precision decimal arithmetic beyond `decimal`'s 28-digit limit, and `BigInteger` support allows lossless handling of very large integers.

### Type coercion

Because V5 no longer has a set of common well-known types (`JsonString`, `JsonNumber`, etc.) that all schema types share, you will always need to explicitly coerce values to the specific target type. In V4 this was `sourceInstance.As<TargetType>()`; in V5 it is `TargetType.From(sourceInstance)`.

Although slightly more verbose, this makes the intent clearer.

In reality, this was often necessary anyway, because your strings and numbers had additional constraints (such as length or range) and no longer resolved to the built-in types.

---

## Parsing

### V4: Parse directly to a value

```csharp
// V4 — simple, but leaks the underlying JsonDocument
MigrationPerson v4 = MigrationPerson.Parse("""{"name":"Jo","age":30}""");

// V4 (preferred) — ParsedValue manages the JsonDocument lifetime
using ParsedValue<MigrationPerson> parsed = ParsedValue<MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
MigrationPerson v4 = parsed.Instance;
```

### V5: Parse into a ParsedJsonDocument

```csharp
// V5 — ParsedJsonDocument owns the pooled memory; always dispose it
using ParsedJsonDocument<MigrationPerson> doc =
    ParsedJsonDocument<MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
MigrationPerson v5 = doc.RootElement;
```

Alternatively, `ParseValue` creates a self-contained document from a span, string, or reader:

```csharp
// V5 — ParseValue creates a self-owned document (disposable)
MigrationPerson v5 = MigrationPerson.ParseValue("""{"name":"Jo","age":30}""");
```

> **Key similarity**: In both V4 and V5, the returned struct is a lightweight reference into a parent document. In V4 it references the `JsonDocument` inside `ParsedValue<T>`; in V5 it references the `ParsedJsonDocument<T>`. In both cases, the value is only valid while the parent document is alive — always keep the `using` in scope.

---

## Property Access

Property access is largely unchanged — both V4 and V5 generate typed properties on the struct.

```csharp
// V4
string name = (string)v4.Name;
int age = (int)v4.Age;

// V5 — identical
string name = (string)v5.Name;
int age = (int)v5.Age;
```

### Property count

```csharp
// V4
int count = v4.Count;

// V5
int count = v5.GetPropertyCount();
```

### Property indexers

V4 used `JsonPropertyName`-based indexing. `JsonPropertyName` was implicitly convertible from `string`, so in practice you were usually just passing strings. V5 replaces this with direct `ReadOnlySpan<byte>`, `ReadOnlySpan<char>`, and `string` overloads, giving you more flexibility. In particular, the `"name"u8` UTF-8 literal syntax lets you use `ReadOnlySpan<byte>` for zero-allocation property lookups:

```csharp
// V4 — JsonPropertyName was implicitly convertible from string
JsonAny value = v4["name"];

// V5 — multiple overloads
JsonElement value = v5["name"u8];      // UTF-8 span (preferred, zero-allocation)
JsonElement value = v5["name"];         // string
```

### TryGetProperty

```csharp
// V4
if (v4.TryGetProperty("name", out JsonAny value)) { ... }

// V5
if (v5.TryGetProperty("name"u8, out JsonElement value)) { ... }
```

### Getting the unescaped UTF-8 string

V4 provided `TryGetValue` delegate overloads that gave you access to the underlying bytes or characters without allocating a `string` — one for `ReadOnlySpan<byte>` (UTF-8) and one for `ReadOnlySpan<char>` (UTF-16). V5 replaces the UTF-8 delegate with `GetUtf8String()`, which returns an `UnescapedUtf8JsonString` — a disposable `ref struct` giving you direct access to the unescaped UTF-8 bytes:

```csharp
// V4 — delegate-based access to the UTF-8 bytes
bool found = v4.Name.TryGetValue(searchTerm, (term, span) => span.SequenceEqual(term));

// V5 — direct span access (replaces the delegate pattern)
using UnescapedUtf8JsonString utf8 = v5.Name.GetUtf8String();
ReadOnlySpan<byte> bytes = utf8.Span;
bool found = bytes.SequenceEqual(searchTerm);
```

V4 string properties also have `GetString()` and `TryGetString()`, which return a `string?`. V5 has these too, but `GetUtf8String()` avoids the UTF-8 → UTF-16 transcoding cost:

```csharp
// Both V4 and V5 — returns string? (allocates)
string? name = v4.Name.GetString();
string? name = v5.Name.GetString();

// V5 only — returns unescaped UTF-8 bytes (no string allocation)
using UnescapedUtf8JsonString utf8 = v5.Name.GetUtf8String();
```

> **Note**: `UnescapedUtf8JsonString` is a `ref struct` that may hold a rented buffer, so always use a `using` declaration to ensure the buffer is returned to the pool.

### Getting the unescaped UTF-16 string

Similarly, V5 provides `GetUtf16String()`, which returns an `UnescapedUtf16JsonString` — a disposable `ref struct` giving you access to the unescaped UTF-16 characters without allocating a managed `string`. This replaces the V4 `TryGetValue` delegate overload that provided `ReadOnlySpan<char>`:

```csharp
// V4 — delegate-based access to the UTF-16 chars
bool found = v4.Name.TryGetValue(searchTerm, (term, span) => span.SequenceEqual(term));

// V5 — direct UTF-16 span access
using UnescapedUtf16JsonString utf16 = v5.Name.GetUtf16String();
ReadOnlySpan<char> chars = utf16.Span;
bool found = chars.SequenceEqual(searchTerm);
```

You can also access the data as `ReadOnlyMemory<char>` via `utf16.Memory`, or take ownership of the underlying rented array with `utf16.TakeOwnership(out char[]? rentedChars)` for scenarios where you need the data to outlive the `using` scope.

> **Note**: Like `UnescapedUtf8JsonString`, `UnescapedUtf16JsonString` is a `ref struct` that may hold a rented buffer. Always use a `using` declaration to ensure cleanup.

---

## JSON Property Names

Both V4 and V5 generate a `JsonPropertyNames` nested class on each object type, providing both `string` constants and UTF-8 `ReadOnlySpan<byte>` accessors for each declared property:

```csharp
// V4 and V5 — identical public API
string name = MigrationPerson.JsonPropertyNames.Name;              // "name"
ReadOnlySpan<byte> nameUtf8 = MigrationPerson.JsonPropertyNames.NameUtf8; // "name"u8
```

These are useful for generic property access, serialization, and working with the low-level `JsonElement` API.

---

## Serialization

Both engines produce equivalent JSON output via `ToString()` and `WriteTo()`.

```csharp
// V4
string json = v4.ToString();
v4.WriteTo(systemTextJsonWriter); // System.Text.Json.Utf8JsonWriter

// V5
string json = v5.ToString();
v5.WriteTo(corvusWriter);         // Corvus.Text.Json.Utf8JsonWriter (not System.Text.Json!)
```

> **Note**: V5 uses `Corvus.Text.Json.Utf8JsonWriter`, a drop-in replacement for `System.Text.Json.Utf8JsonWriter`. The `JsonWorkspace` offers the ability to rent writers for zero-allocation writing to in-memory buffers, streams, and ASP.NET Core HTTP Response Bodies (see [Renting Writers from the Workspace](./JsonDocumentBuilder.md#renting-writers-from-the-workspace)).

---

## Formatting (`IFormattable`, `ISpanFormattable`, `IUtf8SpanFormattable`)

V5 generated types implement `IFormattable`, `ISpanFormattable` (.NET 8+), and `IUtf8SpanFormattable` (.NET 8+). V4 types do not implement any of these interfaces.

This means V5 types can be used directly with `string.Format`, interpolated strings with custom format specifiers, and zero-allocation `TryFormat` to `Span<char>` or `Span<byte>` buffers:

```csharp
// V5 — IFormattable: ToString with format string and culture
string formatted = v5.DateOfBirth.ToString("d", CultureInfo.InvariantCulture); // "03/15/2024"
string iso = v5.DateOfBirth.ToString("o", null);                               // "2024-03-15"

// V5 — IFormattable: number formatting
string currency = v5.Age.ToString("C0", CultureInfo.InvariantCulture);         // "¤30"
string grouped = v5.Age.ToString("N0", CultureInfo.InvariantCulture);          // "30"
```

```csharp
// V5 — ISpanFormattable: zero-allocation formatting to char buffer (.NET 8+)
Span<char> charBuffer = stackalloc char[64];
if (v5.Age.TryFormat(charBuffer, out int charsWritten, "N0", CultureInfo.InvariantCulture))
{
    ReadOnlySpan<char> result = charBuffer[..charsWritten];
}

// V5 — IUtf8SpanFormattable: zero-allocation formatting to UTF-8 byte buffer (.NET 8+)
Span<byte> utf8Buffer = stackalloc byte[64];
if (v5.Age.TryFormat(utf8Buffer, out int bytesWritten, "N0", CultureInfo.InvariantCulture))
{
    ReadOnlySpan<byte> result = utf8Buffer[..bytesWritten];
}
```

When no format string is provided (or it is `null`/empty), the canonical JSON representation is returned:

```csharp
// V5 — null/empty format returns the canonical JSON value
string canonical = v5.DateOfBirth.ToString(null, null); // "2024-03-15"
string name = v5.Name.ToString(null, null);             // "Jo"
```

---

## Equality and Comparison

Both engines support `Equals()`, `==`, and `!=` with the same semantics.

```csharp
// V4
bool equal = v4a.Equals(v4b);
bool same = v4a == v4b;

// V5 — identical signatures
bool equal = v5a.Equals(v5b);
bool same = v5a == v5b;
```

V5 additionally supports `==` and `!=` against `JsonElement`:

```csharp
// V5 only
bool same = v5 == someJsonElement;
```

---

## Validation

### Basic validation

V4 uses a `Validate()` method returning a `ValidationContext`. V5 uses `EvaluateSchema()` returning a `bool`.

```csharp
// V4
ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
bool isValid = result.IsValid;

// V5
bool isValid = v5.EvaluateSchema();
```

### Collecting detailed results

To enumerate validation errors with messages and locations, both V4 and V5 support a `ValidationLevel` higher than `Flag`.

**V4** — pass `ValidationLevel.Detailed` (or `Basic`/`Verbose`) and enumerate `result.Results`:

```csharp
using ParsedValue<MigrationPerson> parsed = ParsedValue<MigrationPerson>.Parse(
    """{"age":200}""");
MigrationPerson v4 = parsed.Instance;

ValidationContext result = v4.Validate(
    ValidationContext.ValidContext,
    ValidationLevel.Detailed);

Assert.False(result.IsValid);

foreach (ValidationResult r in result.Results)
{
    // r.Valid — whether this individual result passed
    // r.Message — the error/success message
}
```

**V5** — create a `JsonSchemaResultsCollector` and pass it to `EvaluateSchema()`:

```csharp
using ParsedJsonDocument<MigrationPerson> parsed =
    ParsedJsonDocument<MigrationPerson>.Parse("""{"age":200}""");
MigrationPerson v5 = parsed.RootElement;

using JsonSchemaResultsCollector collector =
    JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

bool isValid = v5.EvaluateSchema(collector);

Assert.False(isValid);

foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
{
    // r.IsMatch — whether this individual result passed
    // r.GetMessageText() — the error/success message as a string
    // r.GetDocumentEvaluationLocationText() — JSON pointer to the failing location
    // r.GetSchemaEvaluationLocationText() — JSON pointer within the schema
}
```

### Validation levels

| V4 `ValidationLevel` | V5 `JsonSchemaResultsLevel` | Description |
|---|---|---|
| `Flag` | *(no collector)* | Fastest — returns only `bool`, no result details |
| `Basic` | `Basic` | Failure messages only |
| `Detailed` | `Detailed` | Failure messages with schema and document locations |
| `Verbose` | `Verbose` | All events including successful validations |

---

## Creating Objects from Scratch

### V4: Static `Create()` method with typed parameters

```csharp
// V4 — functional construction with implicit conversions from primitives
MigrationPerson v4 = MigrationPerson.Create(
    name: "Alice",
    age: 30,
    email: "alice@test.com");
```

### V5: Builder pattern with `CreateBuilder()`

```csharp
// V5 — convenience overload with named parameters (preferred)
using JsonWorkspace workspace = JsonWorkspace.Create();
using JsonDocumentBuilder<MigrationPerson.Mutable> builder =
    MigrationPerson.CreateBuilder(
        workspace,
        name: "Alice",
        age: 30,
        email: "alice@test.com");

MigrationPerson.Mutable root = builder.RootElement;
// Read values from the mutable instance
string name = (string)root.Name; // "Alice"
```

For scenarios that require logic inside the builder (e.g., `From()` conversions, conditional properties), use the delegate overload:

```csharp
// V5 — delegate overload (for advanced scenarios)
using var builder = MigrationPerson.CreateBuilder(
    workspace,
    (ref b) => b.Create(
        name: "Alice",
        age: 30,
        email: "alice@test.com"));
```

If all of an object's properties are optional, you can create an empty builder with no initializer:

```csharp
// V5 — empty object (all properties optional)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MigrationPerson.CreateBuilder(workspace);
MigrationPerson.Mutable root = builder.RootElement;
// Set properties as needed
root.Name = "Alice";
```

> **Key differences**:
> - V5 requires a `JsonWorkspace` for mutable operations
> - The convenience `CreateBuilder()` overload takes named property parameters directly — similar ergonomics to V4's `Create()`
> - The delegate overload is available for advanced scenarios (e.g., `From()` conversions)
> - The builder returns a `Mutable` nested type, not the immutable type itself
>
> Both V4 and V5 support implicit conversions from primitives (e.g., `"Alice"` rather than `(JsonString)"Alice"`).

> **Performance**: The V5 builder pattern offers considerable performance and memory allocation benefits over V4's `ImmutableList`-based construction:
>
> | Method | Mean | Allocated |
> |---|---:|---:|
> | V4 `Create()` | 831.9 ns | 2,080 B |
> | V5 `CreateBuilder()` | 631.0 ns | 120 B |

---

## Building Nested Objects and Arrays

### V4: Compose nested values with `Create()`

V4 uses `Create()` for each nested type, composing from the inside out:

```csharp
// V4 — build a nested object from scratch
MigrationNested v4 = MigrationNested.Create(
    address: MigrationNested.RequiredCityAndStreet.Create(
        city: "London",
        street: "221B Baker Street",
        zipCode: "12345"),
    name: "Sherlock");
```

For arrays of objects, use `FromItems()`:

```csharp
// V4 — build an array of objects
MigrationItemArray v4 = MigrationItemArray.FromItems(
    MigrationItemArray.RequiredId.Create(id: 1, label: "First"),
    MigrationItemArray.RequiredId.Create(id: 2, label: "Second"),
    MigrationItemArray.RequiredId.Create(id: 3));
```

### V5: Compose nested values with `CreateBuilder()`

V5 uses the convenience `CreateBuilder()` overload with named parameters. For nested object properties, use `Build()` to compose the value as a parameter:

```csharp
// V5 — build a nested object from scratch
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MigrationNested.CreateBuilder(
    workspace,
    address: MigrationNested.RequiredCityAndStreet.Build(
        (ref ab) => ab.Create(
            city: "London",
            street: "221B Baker Street",
            zipCode: "12345")),
    name: "Sherlock");

MigrationNested.Mutable root = builder.RootElement;
string city = (string)root.Address.City; // "London"
```

For arrays of objects, use `AddItem()` inside the build callback:

```csharp
// V5 — build an array of objects
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MigrationItemArray.CreateBuilder(
    workspace,
    MigrationItemArray.Build(
        (ref b) =>
        {
            b.AddItem(MigrationItemArray.RequiredId.Build(
                (ref ib) => ib.Create(id: 1, label: "First")));
            b.AddItem(MigrationItemArray.RequiredId.Build(
                (ref ib) => ib.Create(id: 2, label: "Second")));
            b.AddItem(MigrationItemArray.RequiredId.Build(
                (ref ib) => ib.Create(id: 3)));
        }));

MigrationItemArray.Mutable root = builder.RootElement;
int count = root.GetArrayLength(); // 3
```

---

## Mutating Object Properties

The mutation model is one of the most significant changes between V4 and V5.

**V4** uses a _functional_ (copy-on-write) model: every `With*()` call returns a new immutable instance. The original is unchanged, which is beneficial for a stateless, functional approach to processing. However, if you hang onto a stale reference instead of using the returned value, you silently work with outdated data — a common source of bugs.

**V5** uses an _imperative_ (mutate-in-place) model: `Set*()` methods modify the underlying `JsonDocumentBuilder` directly. The builder tracks a version number, and every `Mutable` element reference records the version at which it was obtained. If the document is mutated after you captured a reference, attempting to use that stale reference throws an `InvalidOperationException` — protecting you from the class of silent-staleness bugs that V4 allowed. The versioning system also allows you to make multiple modifications to the same entity without having to refresh it from the root each time.

Because mutation is in-place, you can set values deep into a nested object hierarchy without needing to rebuild every ancestor:

```csharp
// V5 — set a deeply nested property directly
root.Address.SetCity("London"); // mutates the builder in-place; no need to rebuild root
```

### V4: Functional `With*()` methods

V4 generates `With*()` methods that return a _new_ immutable instance:

```csharp
// V4 — returns new instance; original is unchanged
MigrationPerson updated = v4.WithName("Bob");
```

### V5: Imperative `Set*()` methods on Mutable

V5 generates `Set*()` methods on the `Mutable` nested class that mutate in-place:

```csharp
// V5 — parse, create mutable builder, then mutate
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<MigrationPerson> doc =
    ParsedJsonDocument<MigrationPerson>.Parse(json);
using JsonDocumentBuilder<MigrationPerson.Mutable> builder =
    doc.RootElement.CreateBuilder(workspace);

MigrationPerson.Mutable root = builder.RootElement;
root.SetName("Bob");
```

The standard mutation workflow is:

1. Parse JSON into a `ParsedJsonDocument<T>`
2. Create a `JsonDocumentBuilder<T.Mutable>` via `.CreateBuilder(workspace)`
3. Get the `Mutable` root element from the builder
4. Call `Set*()` methods on the mutable element
5. Serialize via `root.WriteTo(writer)` or convert to immutable via `.Clone()`

> **Tip — Root element caching:** The root element (`builder.RootElement`) is always live because it is at index 0 and is never relocated by mutations. You can cache it once and use it as your navigation hub for multiple mutations:
>
> ```csharp
> MigrationPerson.Mutable root = builder.RootElement;
> root.SetName("Alice");
> root.SetAge(25);
> root.SetEmail("alice@example.com");
> // All three mutations work because root is always live
> ```
>
> See [Version Tracking and Element Invalidation](JsonDocumentBuilder.md#version-tracking-and-element-invalidation) for the full version tracking rules, including limitations on intermediate child references.

---

## Removing Properties

### V4: Set to `default` via `With*()`

```csharp
// V4 — WithEmail(default) removes the optional property
MigrationPerson updated = v4.WithEmail(default);
```

### V5: Named `Remove*()` methods

```csharp
// V5 — explicit remove methods for each optional property
bool removed = root.RemoveEmail(); // returns true if the property was present
```

V5 also supports generic property removal via `RemoveProperty()` on `JsonElement.Mutable`:

```csharp
// V5 — generic removal on untyped elements
root.RemoveProperty("email"u8);
```

---

## Default Property Values

Both engines support schema-defined defaults. The access pattern is the same:

```csharp
// V4
using ParsedValue<MigrationWithDefaults> parsed = ParsedValue<MigrationWithDefaults>.Parse("""{"name":"Jo"}""");
MigrationWithDefaults v4 = parsed.Instance;
string status = (string)v4.Status; // "active" (schema default)

// V5
using var doc = ParsedJsonDocument<MigrationWithDefaults>.Parse("""{"name":"Jo"}""");
MigrationWithDefaults v5 = doc.RootElement;
string status = (string)v5.Status; // "active" (schema default)
```

Entity-level defaults are accessible via `DefaultInstance`:

```csharp
// V5
MigrationWithDefaults.StatusEntity defaultStatus =
    MigrationWithDefaults.StatusEntity.DefaultInstance;
defaultStatus.TryGetValue(out string? status); // "active"
```

---

## Arrays

### Indexing and enumeration

```csharp
// V4
int id = (int)v4[0].Id;
foreach (MigrationItemArray.RequiredId item in v4.EnumerateArray()) { ... }
int length = v4.GetArrayLength();

// V5 — identical pattern
int id = (int)v5[0].Id;
foreach (MigrationItemArray.RequiredId item in v5.EnumerateArray()) { ... }
int length = v5.GetArrayLength();
```

### Adding items

```csharp
// V4 — functional: returns new array
MigrationItemArray updated = v4.Add(newItem);

// V5 — imperative: mutates in place
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = ParsedJsonDocument<MigrationItemArray>.Parse(arrayJson);
using var builder = doc.RootElement.CreateBuilder(workspace);
MigrationItemArray.Mutable root = builder.RootElement;
root.AddItem(newItem);
```

### Inserting items

```csharp
// V4 — functional
MigrationItemArray updated = v4.Insert(1, newItem);

// V5 — imperative
root.InsertItem(1, newItem);
```

### Adding multiple items (range)

```csharp
// V4 — functional: returns new array with all items appended
MigrationItemArray updated = v4.AddRange(newItems);

// V5 — imperative: mutates in place, uses a builder delegate
root.AddRange(static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem(item1);
    b.AddItem(item2);
});
```

### Inserting multiple items (range)

```csharp
// V4 — functional
MigrationItemArray updated = v4.InsertRange(1, newItems);

// V5 — imperative: inserts items at the specified index
root.InsertRange(1, static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem(item1);
    b.AddItem(item2);
});
```

> **Tip:** If the items to insert depend on external state, use the `TContext` overload to avoid closure allocations:
>
> ```csharp
> root.AddRange(myItems, static (in MyItems ctx, ref JsonElement.ArrayBuilder b) =>
> {
>     foreach (var item in ctx)
>     {
>         b.AddItem(item);
>     }
> });
> ```

### Replacing items

```csharp
// V4 — functional, by index
MigrationItemArray updated = v4.SetItem(1, newItem);

// V5 — imperative, by index
root.SetItem(1, newItem);

// V5 — imperative, by value (replaces the first matching item)
bool replaced = root.Replace(oldItem, newItem);
```

> **Note**: `Replace()` uses deep equality to find the first matching element and replaces it in a single pass. If the replacement `Source` is undefined, the matched item is removed instead. Returns `false` if no match is found.

### Removing items

```csharp
// V4 — by index
MigrationItemArray updated = v4.RemoveAt(1);

// V5 — by index
root.RemoveAt(1);

// V5 — by value (removes the first matching item)
bool removed = root.Remove(oldItem);

// V5 — by predicate (removes all matching items)
int removedCount = root.RemoveWhere(
    (in MigrationItemArray.RequiredId.Mutable item) => (int)item.Id == 2);
```

### Creating empty arrays

V5 provides an empty `CreateBuilder()` for array types:

```csharp
// V5
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MigrationItemArray.CreateBuilder(workspace);
MigrationItemArray.Mutable root = builder.RootElement; // empty array
root.AddItem(newItem);
```

---

## Numeric Arrays (Vectors)

Both engines support `TryGetNumericValues()` for efficient extraction:

```csharp
// V4
Span<int> values = stackalloc int[3];
v4.TryGetNumericValues(values, out int written);

// V5 — identical
Span<int> values = stackalloc int[3];
v5.TryGetNumericValues(values, out int written);
```

Static metadata is also the same:

```csharp
// Both V4 and V5
int rank = MigrationIntVector.Rank;       // 1
int dim = MigrationIntVector.Dimension;   // 3
int bufSize = MigrationIntVector.ValueBufferSize; // 3
```

### Constructing a tensor from a flat span

V4 provided `FromValues(span)` to construct a tensor directly from numeric data. V5 provides a `CreateBuilder(workspace, ReadOnlySpan<T>)` convenience overload as the direct replacement:

```csharp
// V4
MigrationIntVector v4 = MigrationIntVector.FromValues([1, 2, 3]);

// V5 — CreateBuilder convenience (preferred: span → mutable document in one call)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = MigrationIntVector.CreateBuilder(workspace, [1, 2, 3]);
MigrationIntVector v5 = builder.RootElement;
```

If you need to separate construction from materialisation, use `Build` + `CreateBuilder`:

```csharp
// V5 — two-step: Build + CreateBuilder
MigrationIntVector.Source source = MigrationIntVector.Build([1, 2, 3]);
using var builder = MigrationIntVector.CreateBuilder(workspace, source);
```

The delegate-based `Build` + `CreateTensor` pattern also works:

```csharp
// V5 — Build + CreateTensor (delegate route)
using var builder = MigrationIntVector.CreateBuilder(
    workspace,
    MigrationIntVector.Build(
        static (ref MigrationIntVector.Builder b) => b.CreateTensor([1, 2, 3])));
```

Use the convenience overload when you already have the data in a span. Use the delegate pattern when you need to combine tensor creation with other builder operations.

### Variable-length numeric arrays

The `Build(ReadOnlySpan<T>)` and `CreateBuilder(workspace, ReadOnlySpan<T>)` overloads are also available on **variable-length** numeric arrays (those without `minItems`/`maxItems` constraints, or with different min and max). The span can contain any number of elements:

```csharp
// Given a schema: { "type": "array", "items": { "type": "number", "format": "double" } }
// The generated ScoresArray type gets Build and CreateBuilder overloads:

using JsonWorkspace workspace = JsonWorkspace.Create();

// CreateBuilder convenience — any length span
using var builder = ScoresArray.CreateBuilder(workspace, [1.5, 2.5, 3.5, 4.5, 5.5]);
ScoresArray scores = builder.RootElement;

// Or two-step: Build + CreateBuilder
ScoresArray.Source source = ScoresArray.Build([1.5, 2.5]);
using var builder2 = ScoresArray.CreateBuilder(workspace, source);
```

> **Note:** For fixed-size tensors, the span must contain exactly `ValueBufferSize` elements. For variable-length arrays, any span length is accepted — including empty spans which produce `[]`.

---

## Tuples

### Accessing tuple elements

Both engines generate typed `Item1`, `Item2`, `Item3` (etc.) properties:

```csharp
// V4
string first = (string)v4.Item1;
int second = (int)v4.Item2;
bool third = (bool)v4.Item3;

// V5
string first = (string)v5.Item1;
int second = (int)v5.Item2;
bool third = (bool)v5.Item3;
```

V5 also supports index access:

```csharp
// V5 only — int indexer returning JsonElement
JsonElement first = v5[0];
```

### Creating tuples

```csharp
// V4 — static Create
MigrationTuple v4 = MigrationTuple.Create(
    "hello",
    42,
    true);

// V5 — CreateBuilder convenience (preferred for fixed-size tuples)
using var builder = MigrationTuple.CreateBuilder(workspace, "hello", 42, true);
MigrationTuple result = builder.RootElement;

// V5 — Build + CreateBuilder two-step
MigrationTuple.Source source = MigrationTuple.Build("hello", 42, true);
using var builder2 = MigrationTuple.CreateBuilder(workspace, source);

// V5 — Build delegate + CreateTuple (required for open tuples)
using var builder3 = MigrationTuple.CreateBuilder(
    workspace,
    MigrationTuple.Build(
        static (ref b) => b.CreateTuple(
            item1: "hello",
            item2: 42,
            item3: true)));
MigrationTuple result2 = builder2.RootElement;
```

The `Build` overload taking positional sources is the direct V5 replacement for `Create` — it takes positional `Source` parameters (with implicit conversions from the underlying .NET types) and returns a `Source` without delegate indirection. It is only available on **pure tuple** types (closed with `items: false` or `unevaluatedItems: false`).

### ValueTuple decomposition

```csharp
// V4 — explicit cast to ValueTuple
(JsonString first, MigrationTuple.PrefixItems1Entity second, JsonBoolean third) =
    ((JsonString, MigrationTuple.PrefixItems1Entity, JsonBoolean))v4;

// V5 — no ValueTuple operator; use typed properties instead
string first = (string)v5.Item1;
int second = (int)v5.Item2;
bool third = (bool)v5.Item3;
```

---

## Core Type Accessors

### V4: `AsString`, `AsNumber`, `AsBoolean`, `AsObject`, `AsArray`

In V4, when a type composed multiple core types (e.g. a union of string and boolean), V4 would *not* emit value accessors (casts, `GetString()`, indexers, etc.) directly on that type. Instead, you used `AsString`, `AsNumber`, `AsBoolean`, etc. to reach a single-core-type that did have those accessors:

```csharp
// V4 — two-step: go via the core type accessor, then cast to the primitive
JsonString asString = v4.AsString;
string value = (string)asString;

JsonNumber asNumber = v4.AsNumber;
int number = (int)asNumber;

JsonBoolean asBool = v4.AsBoolean;
bool flag = (bool)asBool;

JsonObject asObject = v4.AsObject;
JsonArray asArray = v4.AsArray;
```

### V5: Direct value access

V5 emits value accessors for all composed core types directly on the type, so the `As*` indirection is no longer needed:

```csharp
// V5 — direct access: value accessors are on the type itself
string value = (string)v5;                   // explicit operator string
int number = (int)v5;                        // explicit operator int
bool flag = (bool)v5;                        // explicit operator bool

// Or with the TryGetValue/GetString pattern:
if (v5.TryGetValue(out string? s)) { ... }
if (v5.TryGetValue(out long n)) { ... }
if (v5.TryGetValue(out bool b)) { ... }
string? str = v5.GetString();
```

---

## Composition Types (`oneOf`, `anyOf`, `allOf`)

### `allOf` — Composition (Multiple Interface Pattern)

`allOf` represents composition — a type that satisfies *all* of the constituent schemas simultaneously (like implementing multiple interfaces in C#).

Both V4 and V5 support **implicit conversion** from the composite type to any of its `allOf` constituents:

```csharp
// Schema: CompositeType = allOf [Documentation, Countable] + additional properties
// V4
V4.CompositeType v4Composite = V4.CompositeType.Parse(json);
V4.Documentation v4Documentation = v4Composite;  // implicit
V4.Countable v4Count = v4Composite;    // implicit

// Access properties - note V4's "Value" suffix for "count"
int count = (int)v4Count.CountValue;   // V4: disambiguated with "Value" suffix

// V5
using var v5Doc = ParsedJsonDocument<V5.CompositeType>.Parse(json);
V5.CompositeType v5Composite = v5Doc.RootElement;
V5.Documentation v5Documentation = v5Composite;  // implicit
V5.Countable v5Countable = v5Composite;          // implicit

// Access properties - V5 may not need the suffix here
long count = v5Countable.Count;  // V5: "count" no longer conflicts
```

**Property naming note:**
- Both V4 and V5 use **"Value" and "Entity" suffixes** in their naming heuristics to disambiguate generated property and type names from reserved names (interface members, language keywords, etc.)
- V5 has **different name reservations** than V4, so some names that were previously disambiguated may now be available without a suffix (e.g. `Count` instead of `CountValue`), and vice versa
- This may cause some property or type renaming when migrating from V4 to V5

Both also support **explicit conversion** in the reverse direction (from constituent to composite):

```csharp
// V4
V4.CompositeType v4Composite = (V4.CompositeType)v4Documentation;  // explicit

// V5
V5.CompositeType v5Composite = (V5.CompositeType)v5Documentation;  // explicit
```

### `oneOf` / `anyOf` — Discriminated Unions

For `oneOf` and `anyOf`, the conversion operators are **opposite**: implicit *from* constituent types, explicit *to* constituent types.

### Pattern matching with `TryGetAs*()`

Both V4 and V5 emit `TryGetAs*()` methods for any union variant type — whether local or global. The method name is derived from whatever type the variant resolved to. V4 reduced unformatted simple types to framework globals from `Corvus.Json.ExtendedTypes` (`Corvus.Json.JsonString`, `Corvus.Json.JsonBoolean`, `Corvus.Json.JsonInteger`, etc.) and `"type": "number"` with a numeric format to globals (`Corvus.Json.JsonInt32`, `Corvus.Json.JsonDouble`, etc.). However, V4 did **not** reduce `"type": "integer"` with a format — those became custom entity types (e.g. `OneOf1Entity`). V4 also had no equivalent of V5's `Json<Format>NotAsserted` global types. V5 reduces **all** simple and format-typed variants to project-local global types:

```csharp
// V4 — string and boolean reduce to framework globals; integer+format does NOT
if (v4.TryGetAsJsonString(out JsonString stringEntity)) { ... }
if (v4.TryGetAsOneOf1Entity(out MigrationUnion.OneOf1Entity numberEntity)) { ... }
if (v4.TryGetAsJsonBoolean(out JsonBoolean boolEntity)) { ... }

// V5 — all variants reduce to project-local global types
if (v5.TryGetAsJsonString(out JsonString stringEntity)) { ... }
if (v5.TryGetAsJsonInt32(out JsonInt32 numberEntity)) { ... }
if (v5.TryGetAsJsonBoolean(out JsonBoolean boolEntity)) { ... }
```

> **Note**: `allOf` types typically don't generate `TryGetAsAllOf*Entity()` methods because the composite type inherently *is* all constituents simultaneously. Use implicit conversion instead.

### Exhaustive matching with `Match`

Both V4 and V5 support `Match` without a context parameter:

```csharp
// V4 — string/boolean are framework globals; integer+format is custom entity
string result = v4.Match(
    static (in JsonString s) => $"string:{(string)s}",
    static (in MigrationUnion.OneOf1Entity n) => $"number:{(int)n}",
    static (in JsonBoolean b) => $"bool:{(bool)b}",
    static (in MigrationUnion v) => "none");

// V5 — all variants are project-local global types
string result = v5.Match(
    static (in JsonString s) => $"string:{(string)s}",
    static (in JsonInt32 n) => $"number:{(int)n}",
    static (in JsonBoolean b) => $"bool:{(bool)b}",
    static (in MigrationUnion v) => "none");
```

Both also support `Match` with a context parameter to avoid closures:

```csharp
// V4 — string/boolean are framework globals; integer+format is custom entity
string result = v4.Match(
    "prefix",
    static (in JsonString s, in string ctx) => $"{ctx}:string:{(string)s}",
    static (in MigrationUnion.OneOf1Entity n, in string ctx) => $"{ctx}:number:{(int)n}",
    static (in JsonBoolean b, in string ctx) => $"{ctx}:bool:{(bool)b}",
    static (in MigrationUnion v, in string ctx) => $"{ctx}:none");

// V5 — all variants are project-local global types
string result = v5.Match(
    "prefix",
    static (in JsonString s, in string ctx) => $"{ctx}:string:{(string)s}",
    static (in JsonInt32 n, in string ctx) => $"{ctx}:number:{(int)n}",
    static (in JsonBoolean b, in string ctx) => $"{ctx}:bool:{(bool)b}",
    static (in MigrationUnion v, in string ctx) => $"{ctx}:none");
```

---

## Enum Types

Both V4 and V5 handle string enums identically for reading:

```csharp
// V4
using ParsedValue<MigrationStatusEnum> parsed = ParsedValue<MigrationStatusEnum>.Parse("\"active\"");
MigrationStatusEnum v4 = parsed.Instance;
string status = (string)v4; // "active"

// V5
using var doc = ParsedJsonDocument<MigrationStatusEnum>.Parse("\"active\"");
MigrationStatusEnum v5 = doc.RootElement;
string status = (string)v5; // "active"
```

Both V4 and V5 generate a public `EnumValues` class with named constants:

```csharp
// V4 and V5 — compile-time enum constants
MigrationStatusEnum active = MigrationStatusEnum.EnumValues.Active;
MigrationStatusEnum inactive = MigrationStatusEnum.EnumValues.Inactive;
```

Both V4 and V5 also generate `Match` methods for exhaustive matching over enum values. Each enum value gets a named delegate parameter:

```csharp
// V4 and V5 — Match without context
string label = v5.Match(
    static () => "Status is active",
    static () => "Status is inactive",
    static () => "Status is pending",
    static () => "Unknown status");

// V4 and V5 — Match with context (avoids closures)
string label = v5.Match(
    "prefix",
    static (string ctx) => $"{ctx}: active",
    static (string ctx) => $"{ctx}: inactive",
    static (string ctx) => $"{ctx}: pending",
    static (string ctx) => $"{ctx}: unknown");
```

The delegate parameters are named after the enum values (`matchActive`, `matchInactive`, `matchPending`), plus a `defaultMatch` for values that don't match any declared entry.

---

## Memory Management

This is the most significant architectural change.

### V4: JsonDocument-backed values

In V4, each parsed value either wraps a `System.Text.Json.JsonElement` (and its underlying `JsonDocument`) or an `ImmutableList<JsonObjectProperty>` for dotnet-backed values. The `JsonDocument` must be kept alive while values are in use.

Using `ParsedValue<T>` was recommended:

```csharp
// V4
using ParsedValue<MigrationPerson> parsed = ParsedValue<MigrationPerson>.Parse(json);
MigrationPerson v4 = parsed.Instance;
// Use v4... then parsed disposes the JsonDocument
```

### V5: ParsedJsonDocument-backed values

In V5, values are indexes into a `ParsedJsonDocument<T>`. This document uses `ArrayPool<byte>` for efficient memory management. **Always dispose the document.**

```csharp
// V5
using ParsedJsonDocument<MigrationPerson> doc =
    ParsedJsonDocument<MigrationPerson>.Parse(json);
MigrationPerson v5 = doc.RootElement;
// Use v5... then doc returns memory to the pool
```

### V5: JsonWorkspace for mutation

Any mutation requires a `JsonWorkspace` (see [Understanding the Workspace](./JsonDocumentBuilder.md#understanding-the-workspace) in the JsonDocumentBuilder guide):

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = doc.RootElement.CreateBuilder(workspace);
// workspace must outlive all builders created from it
```

### Disposal hierarchy

```
JsonWorkspace (outermost — create first, dispose last)
  └─ JsonDocumentBuilder<T.Mutable>  (one or more per workspace)
       └─ T.Mutable                  (lightweight, no disposal needed)

ParsedJsonDocument<T>                (independent — for read-only parsing)
  └─ T                               (lightweight, no disposal needed)
```

---

## Code Generation

### CLI tool: `generatejsonschematypes`

Both V4 and V5 types are generated by the same CLI tool, `generatejsonschematypes`. V5 adds the `--engine` option to select the generation engine:

```bash
# V4 generation (legacy)
generatejsonschematypes --engine V4 \
  --rootNamespace MyApp.Models \
  --outputPath generated/ \
  schema.json

# V5 generation (default)
generatejsonschematypes \
  --rootNamespace MyApp.Models \
  --outputPath generated/ \
  schema.json
```

The `--engine` option accepts `V4` (generates `Corvus.Json.ExtendedTypes`-based code) or `V5` (generates `Corvus.Text.Json`-based code). The default is `V5`.

All other options (`--rootNamespace`, `--outputPath`, `--outputRootTypeName`, `--optionalAsNullable`, etc.) work identically with both engines.

### Source generator

Both V4 and V5 provide a Roslyn incremental source generator that generates types at build time. The approach is the same in both versions: you declare a `partial struct` annotated with `JsonSchemaTypeGeneratorAttribute`, and the generator produces the implementation from the referenced JSON Schema file.

```csharp
// V4 and V5 — same attribute-driven approach
[JsonSchemaTypeGenerator("person-schema.json")]
public readonly partial struct Person;
```

The generator derives:
- **Namespace** — from the containing namespace of the declared struct
- **Accessibility** — from the declared accessibility of the struct (`public`, `internal`, or `private`)
- **Type name** — from the struct name

To set up the source generator, add the generator NuGet package and include your schema files as `AdditionalFiles`.

**V4:**

```xml
<ItemGroup>
  <PackageReference Include="Corvus.Json.SourceGenerator" Version="4.6.3">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.6.3" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="person-schema.json" />
</ItemGroup>
```

**V5:**

```xml
<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
      <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Corvus.Text.Json" Version="5.0.0" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="person-schema.json" />
</ItemGroup>
```

Additional generation options are controlled via MSBuild properties (e.g., `CorvusTextJsonOptionalAsNullable`, `CorvusTextJsonFallbackVocabulary`).

---

## Best Practices and Patterns

### Prefer convenience overloads for tuples and numeric arrays

For **closed tuples** and **numeric arrays**, prefer the convenience `CreateBuilder` overloads that accept values directly, avoiding the delegate indirection:

```csharp
// ✅ Preferred — convenience overload
using var builder = MyTuple.CreateBuilder(workspace, "hello", 42, true);
using var vecBuilder = MyVector.CreateBuilder(workspace, [1, 2, 3]);

// ✅ Also good — two-step Build + CreateBuilder (when you need the Source separately)
MyTuple.Source source = MyTuple.Build("hello", 42, true);
using var builder = MyTuple.CreateBuilder(workspace, source);

// ⚠️ Verbose — use the delegate pattern only when required (open tuples, combining operations)
using var builder = MyTuple.CreateBuilder(workspace,
    MyTuple.Build(static (ref MyTuple.Builder b) => b.CreateTuple("hello", 42, true)));
```

### Use `static` lambdas where possible

When creating builder delegates or pattern matching lambdas that don't capture variables, mark them `static` for better performance:

```csharp
// Builder delegates with literals — use static
using var doc = Person.CreateBuilder(
    workspace,
    Person.Build(
        static (ref Person.Builder b) => b.Create(
            familyName: "Brontë",
            givenName: "Anne",
            height: 1.57)));

// Pattern matching without captured variables — use static
string result = color.Match(
    matchRed: static () => "The color of fire",
    matchGreen: static () => "The color of grass",
    matchBlue: static () => "The color of sky",
    defaultMatch: static () => "Unknown color");

// Pattern matching WITH context parameter — use static (context is a parameter, not captured)
string result = status.Match(
    requestCount,  // context parameter
    matchActive: static (count) => $"Processing {count} requests",
    matchInactive: static (count) => $"Cannot process {count} requests",
    defaultMatch: static (count) => throw new InvalidOperationException());
```

Only omit `static` when the lambda genuinely captures variables from the enclosing scope:

```csharp
// Captures 'source' variable from outer scope — cannot be static
using var targetBuilder = TargetType.CreateBuilder(workspace, (ref TargetType.Builder b) =>
{
    if (source.Id.TryGetValue(out long idValue))
    {
        b.WithIdentifier(idValue);
    }
});
```

### Work with generated types directly

Prefer working with generated entity types throughout your code, only extracting to .NET primitives when necessary for operations:

```csharp
// ✅ Good — work with generated types
Person person = parsedDoc.RootElement;
string familyName = person.FamilyName;  // Generated type, supports formatting
double height = person.Height;  // Implicit conversion where supported

// ❌ Avoid unnecessary extraction
string familyName = (string)person.FamilyName;  // Unnecessary when not doing string operations
```

Extract to primitives only when you need to:
- Perform arithmetic: `id.TryGetValue(out long value); value + 1000`
- Manipulate strings: `name.TryGetValue(out string? str); str.ToUpperInvariant()`
- Map between different schemas (see below)

### Cross-schema mapping does not requires value extraction for compatible types

When mapping between generated types from **different schemas**, entity types don't necessarily implicitly convert, but they can still b be compatible. Use `TargetType.From(sourceInstance)` and pass them to the builder:

```csharp
// SourceType and TargetType are from different schemas
using var sourceDoc = ParsedJsonDocument<SourceType>.Parse(json);
SourceType source = sourceDoc.RootElement;

using JsonWorkspace workspace = JsonWorkspace.Create();
using var targetBuilder = TargetType.CreateBuilder(workspace, (ref TargetType.Builder b) =>
{
    b.Create(nameValue, TargetType.IdentifierEntity.From(idValue));
});
```

While `SourceType.IdEntity` and `TargetType.IdentifierEntity` are different types they both represent integers with compatible constraints, so you can safely convert between them, without having to convert to an intermediate .NET value type.

### Pattern matching uses named parameters

V5's `Match()` methods use named parameters for each variant, making the code more self-documenting:

```csharp
// Enum matching
string desc = color.Match(
    matchRed: static () => "Fire",
    matchGreen: static () => "Grass",
    matchBlue: static () => "Sky",
    defaultMatch: static () => "Unknown");

// Discriminated union matching (named by required properties)
string result = shape.Match(
    matchRequiredRadiusAndType: static (in circle) =>
        $"Circle r={circle.Radius}",
    matchRequiredHeightAndTypeAndWidth: static (in rect) =>
        $"Rectangle {rect.Width}x{rect.Height}",
    defaultMatch: static (in unknown) => "Unknown shape");
```

The parameter names (`matchRed`, `matchRequiredRadiusAndType`) are generated from the schema and can be found in IntelliSense.

### Use context parameters to avoid closures

When pattern matching needs external state, pass it as a context parameter rather than capturing it:

```csharp
// ✅ Good — context parameter (no closure, can be static)
double brightness = 0.8;
string rgb = color.Match(
    brightness,
    matchRed: static (ctx) => $"RGB({(int)(255 * ctx)}, 0, 0)",
    matchGreen: static (ctx) => $"RGB(0, {(int)(255 * ctx)}, 0)",
    matchBlue: static (ctx) => $"RGB(0, 0, {(int)(255 * ctx)})",
    defaultMatch: static (ctx) => "RGB(0, 0, 0)");

// ❌ Avoid — capturing closure (cannot be static, allocates)
double brightness = 0.8;
string rgb = color.Match(
    matchRed: () => $"RGB({(int)(255 * brightness)}, 0, 0)",  // captures 'brightness'
    matchGreen: () => $"RGB(0, {(int)(255 * brightness)}, 0)",
    matchBlue: () => $"RGB(0, 0, {(int)(255 * brightness)})",
    defaultMatch: () => "RGB(0, 0, 0)");
```

Note that on .NET 9.0 and later, you can pass a `ref struct` as your context.

---

## Quick Reference Table

| V4 Operation | V5 Equivalent |
|---|---|
| `MyType.Parse(json)` | `ParsedJsonDocument<MyType>.Parse(json)` + `.RootElement` |
| `ParsedValue<T>.Parse(json)` | `ParsedJsonDocument<T>.Parse(json)` |
| `parsed.Instance` | `doc.RootElement` |
| `MyType.ParseValue(json)` | `MyType.ParseValue(json)` (same) |
| `v4.ValueKind` | `v5.ValueKind` (same, but `Corvus.Text.Json.JsonValueKind`) |
| `v4.Count` | `v5.GetPropertyCount()` |
| `v4.Name` (typed property) | `v5.Name` (same) |
| `v4.GetString()` | `v5.GetString()` (same) |
| `v4.TryGetString(out string?)` | `v5.TryGetValue(out string?)` |
| N/A | `v5.GetUtf8String()` (V5 only — returns `UnescapedUtf8JsonString`) |
| N/A | `v5.GetUtf16String()` (V5 only — returns `UnescapedUtf16JsonString`) |
| `v4.TryGetValue(state, (s, span) => ...)` | `v5.GetUtf8String().Span` or `v5.GetUtf16String().Span` |
| `v4.AsJsonElement` | `(JsonElement)v5` (implicit operator) |
| `v4.AsAny` | N/A — implicitly cast to `JsonElement` |
| `v4.AsObject` | N/A — V5 emits object accessors directly: typed properties, `TryGetProperty()`, `EnumerateObject()` |
| `v4.AsString` | N/A — V5 emits value accessors directly: `(string)v5`, `v5.GetString()`, `v5.TryGetValue(out string?)` |
| `v4.AsNumber` | N/A — V5 emits value accessors directly: `(int)v5`, `(long)v5`, `v5.TryGetValue(out int)` |
| `v4.AsBoolean` | N/A — V5 emits value accessors directly: `(bool)v5`, `v5.TryGetValue(out bool)` |
| `v4.AsArray` | N/A — V5 emits array accessors directly: `v5.GetArrayLength()`, `v5[0]`, `v5.EnumerateArray()` |
| `v4.As<TargetType>()` | `TargetType.From(v5)` |
| `v4.Equals(v4b)` | `v5.Equals(v5b)` (same) |
| `v4 == v4b` | `v5 == v5b` (same) |
| `v4.Validate(ctx, level)` | `v5.EvaluateSchema()` or `v5.EvaluateSchema(collector)` |
| `v4.ToString()` | `v5.ToString()` (same) |
| `v4.WriteTo(writer)` | `v5.WriteTo(writer)` (different writer type!) |
| N/A | `v5.ToString(format, provider)` (V5 only — `IFormattable`) |
| N/A | `v5.TryFormat(charSpan, ...)` (V5 only — `ISpanFormattable`) |
| N/A | `v5.TryFormat(byteSpan, ...)` (V5 only — `IUtf8SpanFormattable`) |
| `MyType.JsonPropertyNames.Name` | `MyType.JsonPropertyNames.Name` (same) |
| `MyType.JsonPropertyNames.NameUtf8` | `MyType.JsonPropertyNames.NameUtf8` (same) |
| `MyType.Create(...)` | `MyType.CreateBuilder(workspace, (ref b) => b.Create(...))` |
| `MyType.FromItems(item1, item2)` | `MyType.CreateBuilder(workspace, MyType.Build((ref b) => { b.AddItem(...); }))` |
| `v4.WithName("Bob")` (typed property) | `mutable.SetName("Bob")` (typed property, imperative) |
| `v4.WithEmail(default)` (typed property) | `mutable.RemoveEmail()` (typed property) |
| `v4.Add(item)` | `mutable.AddItem(item)` |
| `v4.AddRange(items)` | `mutable.AddRange((ref b) => { b.AddItem(...); })` |
| `v4.Insert(idx, item)` | `mutable.InsertItem(idx, item)` |
| `v4.InsertRange(idx, items)` | `mutable.InsertRange(idx, (ref b) => { b.AddItem(...); })` |
| `v4.SetItem(idx, item)` | `mutable.SetItem(idx, item)` |
| N/A | `mutable.Replace(oldItem, newItem)` (V5 only — by value, first match) |
| `v4.RemoveAt(idx)` | `mutable.RemoveAt(idx)` |
| N/A | `mutable.Remove(item)` (V5 only — by value, first match) |
| N/A | `mutable.RemoveWhere(predicate)` (V5 only — by predicate, all matches) |
| `v4.GetArrayLength()` | `v5.GetArrayLength()` (same) |
| `v4[0]` (array index) | `v5[0]` (same) |
| `v4.EnumerateArray()` | `v5.EnumerateArray()` (same) |
| `v4.EnumerateObject()` | `v5.EnumerateObject()` (same) |
| `v4.TryGetNumericValues(span, out written)` | `v5.TryGetNumericValues(span, out written)` (same) |
| `MyTensor.FromValues(span)` | `MyTensor.CreateBuilder(ws, span)`, `Build(span)`, or `Build((ref b) => b.CreateTensor(span))` |
| N/A | `MyNumericArray.CreateBuilder(ws, span)` or `Build(span)` (V5 only — variable-length numeric arrays) |
| `v4.Item1` (tuple) | `v5.Item1` (same) |
| `MigrationTuple.Create(a,b,c)` | `MigrationTuple.CreateBuilder(ws,a,b,c)`, `Build(a,b,c)`, or `Build((ref b) => b.CreateTuple(a,b,c))` |
| `v4.Match(...)` (composition) | `v5.Match(...)` (same for unformatted types; `"type":"integer"` + format entity types become global names like `JsonInt32`) |
| `v4.Match(...)` (enum) | `v5.Match(...)` (same) |
| `MyType.EnumValues.Active` | `MyType.EnumValues.Active` (same) |
| `MyType.DefaultInstance` | `MyType.DefaultInstance` (same) |
| `MyType.FromJson(element)` | `MyType.From(element)` |
| `MyType.FromAny(any)` | `MyType.From(element)` |

---

## Migration Checklist

1. **Update package references** — replace `Corvus.Json` packages with `Corvus.Text.Json` packages
2. **Update namespaces** — replace `using Corvus.Json;` with `using Corvus.Text.Json;`
3. **Update parsing** — replace `ParsedValue<T>.Parse()` and `JsonDocument`/`T.FromJson(doc.RootElement)` patterns with `ParsedJsonDocument<T>.Parse()`
4. **Convert `With*()` to `Set*()`** — create a `JsonWorkspace` and `JsonDocumentBuilder`, then use imperative `Set*()` methods
5. **Update validation calls** — replace `Validate(ctx, level)` with `EvaluateSchema()`
6. **Update union access** — V4's `AsString`, `AsNumber`, `AsBoolean` existed because multi-core-type types didn't emit value accessors directly. V5 emits all value accessors on the type itself: `(string)v5`, `v5.TryGetValue(out int)`, `(bool)v5`, etc. The `TryGetAs*()` pattern is the same in both V4 and V5; for unformatted types the names match (e.g. `TryGetAsJsonString()`). For `"type":"integer"` + format variants, V4 produced custom entity types (e.g. `OneOf1Entity`) while V5 reduces them to globals (e.g. `TryGetAsJsonInt32()`). Use `v5.Match()` for exhaustive union matching.
7. **Update writer types** — use `Corvus.Text.Json.Utf8JsonWriter` instead of `System.Text.Json.Utf8JsonWriter`
8. **Update tuple creation** — replace `MyTuple.Create(a, b, c)` with `MyTuple.CreateBuilder(workspace, a, b, c)` for closed tuples, or the delegate pattern for open tuples
9. **Update numeric array construction** — replace `MyVector.FromValues(span)` with `MyVector.CreateBuilder(workspace, span)`; for variable-length arrays, use `MyArray.CreateBuilder(workspace, span)` or `MyArray.Build(span)`
10. **Regenerate types** — run the V5 code generator against your JSON Schema files
11. **Review generated names** — V5 has different name reservations than V4, so some property and type names may change (e.g. `CountValue` → `Count`). Check for renamed properties and types after regenerating.
