# Analyzers

The `Corvus.Text.Json` NuGet package ships with built-in Roslyn analyzers and code fixes that help you write correct, high-performance code. These are production analyzers — they run continuously and are not related to the separate V4-to-V5 migration analyzers.

## Installation

The analyzers are included in the main package. No additional installation is required:

```xml
<PackageReference Include="Corvus.Text.Json" Version="5.0.0" />
```

They activate automatically at build time and in Visual Studio's live analysis.

## Diagnostics

| ID | Title | Severity | Code Fix |
|----|-------|----------|----------|
| [CTJ001](#ctj001-prefer-utf-8-string-literal) | Prefer UTF-8 string literal | Warning | ✅ Yes |
| [CTJ002](#ctj002-unnecessary-conversion-to.net-type) | Unnecessary conversion to .NET type | Warning | ✅ Yes |
| [CTJ003](#ctj003-match-lambda-should-be-static) | Match lambda should be static | Info | ✅ Yes |
| [CTJ004](#ctj004-missing-dispose-on-parsedjsondocument) | Missing dispose on ParsedJsonDocument | Warning | ✅ Yes |
| [CTJ005](#ctj005-missing-dispose-on-jsonworkspace) | Missing dispose on JsonWorkspace | Warning | ✅ Yes |
| [CTJ006](#ctj006-missing-dispose-on-jsondocumentbuilder) | Missing dispose on JsonDocumentBuilder | Warning | ✅ Yes |
| [CTJ007](#ctj007-ignored-schema-validation-result) | EvaluateSchema() result is discarded | Warning | — |
| [CTJ008](#ctj008-prefer-non-allocating-property-name-accessors) | Prefer NameEquals over Name for comparisons | Info | ✅ Yes |
| [CTJ009](#ctj009-prefer-renting-utf8jsonwriter-from-workspace) | Prefer renting Utf8JsonWriter from workspace | Info | — |
| [CTJ010](#ctj010-prefer-readonlymemoryspan-based-parse-overload) | Prefer ReadOnlyMemory/Span-based Parse | Info | — |

## Refactorings

| Name | Description |
|------|-------------|
| [CTJ-NAV](#ctj-nav-go-to-schema-definition) | Navigate from a schema-generated type to its JSON Schema source |

---

## CTJ001 — Prefer UTF-8 string literal

**Severity:** Warning · **Code fix:** ✅ Yes · **Category:** Performance

Many `Corvus.Text.Json` APIs offer overloads that accept `ReadOnlySpan<byte>` (a UTF-8 byte span) in addition to `string`. The UTF-8 overload avoids the cost of transcoding from UTF-16 to UTF-8 at runtime and allows the compiler to embed the bytes directly in the assembly.

This analyzer fires when you pass a `string` literal to a method or indexer that also has a `ReadOnlySpan<byte>` overload.

```csharp
// Before — CTJ001 fires
JsonElement name = element["name"];

// After — code fix applied
JsonElement name = element["name"u8];
```

The code fix appends the `u8` suffix to the string literal.

---

## CTJ002 — Unnecessary conversion to .NET type

**Severity:** Warning · **Code fix:** ✅ Yes · **Category:** Performance

Schema-generated types provide implicit conversions to and from common .NET types. When an explicit cast to an intermediate .NET type is used in an argument position where the original type already converts implicitly to the target parameter type, the intermediate cast is redundant and may force an unnecessary allocation or copy.

This analyzer fires when a cast expression like `(int)element` is passed to a parameter that would accept the original type via an implicit conversion.

```csharp
// Before — CTJ002 fires
// Source has an implicit conversion from JsonElement, so the (int) cast is unnecessary.
mutable.SetProperty("age", (int)element);

// After — code fix applied
mutable.SetProperty("age", element);
```

The code fix removes the unnecessary cast, letting the implicit conversion handle the transformation directly.

---

## CTJ003 — Match lambda should be static

**Severity:** Info · **Code fix:** ✅ Yes (non-capturing) · **Category:** Usage

The `Match<TOut>` method on schema-generated union/oneOf types accepts lambda callbacks for each variant. If a lambda does not capture any local variables, it should be marked `static` to avoid allocating a delegate instance on every call.

This analyzer inspects each lambda argument to `Match<TOut>` and reports:

- **Non-capturing lambdas** — suggests adding the `static` modifier.
- **Capturing lambdas** — suggests switching to `Match<TContext, TResult>` to pass captured state as an explicit context parameter, avoiding closure allocation.

```csharp
// Before — CTJ003 fires (non-capturing)
string result = value.Match(
    (JsonString s) => s.ToString(),
    (JsonNumber n) => n.ToString());

// After — code fix applied
string result = value.Match(
    static (JsonString s) => s.ToString(),
    static (JsonNumber n) => n.ToString());
```

For capturing lambdas, consider the `Match<TContext, TResult>` overload:

```csharp
// Before — CTJ003 fires (capturing)
string prefix = GetPrefix();
string result = value.Match(
    (JsonString s) => prefix + s.ToString(),    // captures 'prefix'
    (JsonNumber n) => prefix + n.ToString());

// After — manual refactoring
string prefix = GetPrefix();
string result = value.Match(
    prefix,
    static (string ctx, JsonString s) => ctx + s.ToString(),
    static (string ctx, JsonNumber n) => ctx + n.ToString());
```

> **Note:** The code fix only applies the `static` modifier for non-capturing lambdas. Capturing-lambda refactoring to `Match<TContext, TResult>` requires manual changes.

---

## CTJ004 — Missing dispose on ParsedJsonDocument

**Severity:** Warning · **Code fix:** ✅ Yes · **Category:** Reliability

`ParsedJsonDocument<T>` uses pooled memory from `ArrayPool<byte>`. Failing to dispose it leaks those buffers, increasing GC pressure and potentially exhausting the pool.

This analyzer fires when a `ParsedJsonDocument<T>.Parse(...)` result is assigned to a local variable without `using`, or when the result is discarded entirely.

```csharp
// Before — CTJ004 fires
var doc = ParsedJsonDocument<JsonElement>.Parse(json);

// After — code fix applied
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
```

The code fix adds the `using` keyword to the local declaration.

If you need to control lifetime explicitly (e.g., returning the document from a method), call `Dispose()` in a `finally` block instead.

---

## CTJ005 — Missing dispose on JsonWorkspace

**Severity:** Warning · **Code fix:** ✅ Yes · **Category:** Reliability

`JsonWorkspace` manages pooled arrays of `IJsonDocument` and a writer cache. Failing to dispose it leaks these pool resources and all child document builders.

```csharp
// Before — CTJ005 fires
var workspace = JsonWorkspace.Create();

// After — code fix applied
using var workspace = JsonWorkspace.Create();
```

---

## CTJ006 — Missing dispose on JsonDocumentBuilder

**Severity:** Warning · **Code fix:** ✅ Yes · **Category:** Reliability

`JsonDocumentBuilder<T>` holds mutable document state backed by pooled memory. Failing to dispose it leaks those buffers.

```csharp
// Before — CTJ006 fires
var builder = doc.RootElement.CreateBuilder(workspace);

// After — code fix applied
using var builder = doc.RootElement.CreateBuilder(workspace);
```

---

## CTJ007 — Ignored schema validation result

**Severity:** Warning · **Category:** Usage

`EvaluateSchema()` returns a `bool` indicating whether validation passed. Discarding this return value means the validation call has no effect — the schema is evaluated but no action is taken based on the result.

```csharp
// Before — CTJ007 fires
element.EvaluateSchema();

// After — use the result
if (!element.EvaluateSchema())
{
    // Handle validation failure
}
```

This also detects discarded results on schema-generated types that implement `IJsonElement<T>`.

---

## CTJ008 — Prefer non-allocating property name accessors

**Severity:** Info · **Code fix:** ✅ Yes · **Category:** Performance

`JsonProperty<T>.Name` allocates a new `string` on every access. When comparing the property name against a known literal, use `NameEquals()` instead — it performs the comparison directly on the underlying UTF-8 bytes without allocating.

```csharp
// Before — CTJ008 fires
if (property.Name == "data") { ... }

// After — code fix applied
if (property.NameEquals("data"u8)) { ... }
```

The code fix replaces `==`/`!=` comparisons and `.Equals()` calls with `NameEquals("..."u8)`. For `!=`, the result is negated: `!property.NameEquals("data"u8)`.

### Alternative non-allocating accessors

If you need the property name for something other than comparison, consider:

| Accessor | Returns | Allocates? | Notes |
|----------|---------|------------|-------|
| `property.Name` | `string` | Yes | Use only when you truly need a `string` |
| `property.NameEquals("x"u8)` | `bool` | No | Best for comparison against known values |
| `property.Utf8NameSpan` | `UnescapedUtf8JsonString` | No (may rent) | Unescaped UTF-8 bytes; dispose when done |
| `property.Utf16NameSpan` | `UnescapedUtf16JsonString` | No (may rent) | Unescaped UTF-16 chars; dispose when done |

---

## CTJ009 — Prefer renting Utf8JsonWriter from workspace

**Severity:** Info · **Category:** Performance

When a `JsonWorkspace` is in scope, you should rent a `Utf8JsonWriter` from it rather than allocating a new one. The workspace maintains a writer cache that avoids repeated allocation and provides consistent writer options.

```csharp
// Before — CTJ009 fires
using var workspace = JsonWorkspace.Create();
var writer = new Utf8JsonWriter(stream);  // allocates

// After — use workspace rental
using var workspace = JsonWorkspace.Create();
var writer = workspace.RentWriter(bufferWriter);
// ... use writer ...
workspace.ReturnWriter(writer);
```

The workspace provides two rental methods:
- `RentWriter(IBufferWriter<byte>)` — when you already have a buffer
- `RentWriterAndBuffer(int defaultBufferSize, out IByteBufferWriter bufferWriter)` — rents both

Always return the writer with `ReturnWriter()` or `ReturnWriterAndBuffer()` when done.

---

## CTJ010 — Prefer ReadOnlyMemory/Span-based Parse overload

**Severity:** Info · **Category:** Performance

`ParsedJsonDocument<T>.Parse(string)` and `JsonElement.ParseValue(string)` allocate an internal UTF-8 copy of the input. When you already have the data as bytes, use the `ReadOnlyMemory<byte>` or `ReadOnlySpan<byte>` overload to avoid this copy.

### Encoding roundtrip

```csharp
// Before — CTJ010 fires (encoding roundtrip)
string json = Encoding.UTF8.GetString(bytes);
var doc = ParsedJsonDocument<JsonElement>.Parse(json);

// After — pass bytes directly
var doc = ParsedJsonDocument<JsonElement>.Parse(bytes.AsMemory());
```

### String literal

```csharp
// Before — CTJ010 fires (string literal)
var element = JsonElement.ParseValue("{}");

// After — use UTF-8 literal
var element = JsonElement.ParseValue("{}"u8);
```

### Overload preference

For `ParsedJsonDocument<T>`, prefer overloads in this order:
1. `Parse(ReadOnlyMemory<byte>)` — zero-copy; the document holds a reference directly
2. `Parse(ReadOnlySequence<byte>)` — for pipeline scenarios
3. `Parse(Stream)` / `ParseAsync(Stream)` — for I/O scenarios
4. `Parse(ReadOnlyMemory<char>)` — if you have `char[]` data
5. `Parse(string)` — least preferred; allocates internal UTF-8 copy

---

## CTJ-NAV — Go to Schema Definition

**Type:** Code Refactoring (lightbulb action) · **Category:** Navigation

When you place the cursor on a type, variable, property, parameter, or field that is backed by a JSON Schema, this refactoring offers to open the schema file and navigate to the exact position of the type or property definition within the schema.

### Trigger positions

The refactoring activates on:

| Code construct | Example |
|----------------|---------|
| Type name | `Order order = ...` (cursor on `Order`) |
| Variable declaration | `Order order = ...` (cursor on `order`) |
| Parameter name | `void Process(Order order)` (cursor on `order`) |
| Field usage | `_order.Total` (cursor on `_order` or `Total`) |
| Property access | `order.Customer` (cursor on `Customer`) |
| Generic type argument | `List<Order>` (cursor on `Order`) |
| Method invocation | `GetOrder()` (cursor on `GetOrder`, navigates via return type) |
| Interface wrapper | `IJsonElement<Order>` (cursor on `Order`) |

### Actions offered

Depending on context, one or two lightbulb actions are offered:

| Action | When |
|--------|------|
| **Go to schema type** | Always, when the type has a schema definition |
| **Go to property declaration** | Additionally, when the cursor is on a property whose type is itself schema-generated — navigates to the property declaration on the parent type's schema |

For example, given `order.Customer` where `CustomerEntity` has its own schema, you get both:
1. **Go to schema type** — opens the schema at `CustomerEntity`'s definition
2. **Go to property declaration** — opens the parent schema at `/properties/customer`

### Schema resolution

The refactoring resolves schema files using three strategies (in order):

1. **Attribute-based** — Finds `[JsonSchemaTypeGenerator("path/to/schema.json")]` on the type or its containing types, then matches the path to an `AdditionalFiles` entry.
2. **`$id`-based** — Reads the type's `SchemaLocation` constant (e.g., `"https://example.com/order#/properties/total"`), extracts the base URL, and searches `AdditionalFiles` for a schema whose `$id` matches.
3. **Property fallback** — If a property's type is a project-global type (e.g., `JsonString`) with no schema of its own, falls back to the containing type's schema and appends `/properties/{jsonPropertyName}`.

### Cursor positioning

The refactoring resolves the JSON Pointer from the `SchemaLocation` to a precise line and column within the schema file. In Visual Studio, it uses the DTE automation model to open the file and position the cursor directly on the target property or type definition — including in single-line schema files.

> **Note:** If DTE is not available (e.g., in a non-VS IDE), the action title includes the line number as a fallback hint: `Go to schema: order.json#/properties/total (line 28)`.
