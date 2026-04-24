---
name: corvus-parsed-documents-and-memory
description: >
  Parse JSON into pooled-memory documents and manage memory correctly in the
  Corvus.Text.Json library. Covers ParsedJsonDocument, IJsonDocument, the IJsonElement
  CRTP pattern, the stackalloc/ArrayPool rent pattern with JsonConstants thresholds,
  UTF-8 transcoding helpers, and disposal requirements. USE FOR: parsing JSON, understanding
  the memory model, writing allocation-efficient code, handling UTF-8/UTF-16 transcoding,
  understanding the IJsonElement type system. DO NOT USE FOR: mutable documents
  (use corvus-mutable-documents), code generation (use corvus-codegen).
---

# Parsed Documents and Memory Management

## Core Abstractions

### ParsedJsonDocument<T>

The primary read-only JSON document type. Backed by `ArrayPool<byte>` — **always dispose**.

```csharp
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;
// Use root...
// doc is disposed at end of using block, returning memory to the pool
```

### IJsonElement<T> (CRTP Pattern)

```csharp
public interface IJsonElement<T> where T : struct, IJsonElement<T>
```

Every JSON type implements this curiously recurring template pattern, enabling static dispatch without virtual calls. The generated types from JSON Schema all implement this interface.

### IJsonDocument

Base interface for all pooled-memory documents. Always call `Dispose()` to return memory.

## Parsing Overloads

Prefer these overloads in order (most efficient first):

| Overload | Use when |
|----------|----------|
| `Parse(ReadOnlyMemory<byte>)` | You have UTF-8 bytes in memory |
| `Parse(ReadOnlySequence<byte>)` | You have a `PipeReader` or multi-segment buffer |
| `Parse(Stream)` | Reading from a file or network stream |
| `Parse(ReadOnlyMemory<char>)` | You have a `char` buffer |
| `Parse(string)` | Convenience; least efficient |

`ParseValue()` creates a self-owned copy (the element owns its backing memory).
`Parse()` returns a disposable document that owns the backing memory.

## stackalloc / ArrayPool Rent Pattern

The codebase uses a single consistent pattern for temporary buffers:

```csharp
byte[]? rentedArray = null;

Span<byte> buffer = length <= JsonConstants.StackallocByteThreshold
    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
    : (rentedArray = ArrayPool<byte>.Shared.Rent(length));

try
{
    DoWork(buffer.Slice(0, length));
}
finally
{
    if (rentedArray != null)
    {
        ArrayPool<byte>.Shared.Return(rentedArray);
    }
}
```

### Thresholds

| Constant | Value | Use for |
|----------|-------|---------|
| `JsonConstants.StackallocByteThreshold` | 256 | `byte` / UTF-8 buffers |
| `JsonConstants.StackallocCharThreshold` | 128 | `char` buffers |

**Rules:**
- Declare `rentedArray` **before** the ternary (must be in scope for `finally`)
- Use the named constant, not a magic number
- **Always** slice to `length` — `ArrayPool.Rent` may return a larger array
- For small fixed-size buffers (always ≤ threshold), plain `stackalloc` without pool is acceptable

## UTF-8 Transcoding Helpers

Use `JsonReaderHelper.TranscodeHelper` to convert between UTF-8 and strings:

```csharp
// UTF-8 bytes → string (most common in tests)
string result = JsonReaderHelper.TranscodeHelper(utf8Span);

// UTF-8 bytes → char buffer
int charsWritten = JsonReaderHelper.TranscodeHelper(utf8Span, charBuffer);

// char buffer → UTF-8 bytes (reverse)
int bytesWritten = JsonReaderHelper.TranscodeHelper(charSpan, utf8Buffer);

// Non-throwing variant
bool success = JsonReaderHelper.TryTranscode(utf8Span, charBuffer, out int written);
```

Invalid UTF-8 always throws `InvalidOperationException` (wrapping `DecoderFallbackException`).

## Configuration

Default max JSON depth: **64** for `ParsedJsonDocument` (reader/parser), **1000** for `Utf8JsonWriter`.

## Partial-Class Organization

The `JsonElement` type is split across files by concern:
- `JsonElement.cs` — core struct
- `JsonElement.Parse.cs` — parsing
- `JsonElement.JsonSchema.cs` — schema validation
- `JsonElement.Mutable.cs` — mutable operations
- `JsonElementHelpers.*.cs` — DateTime, Uri, numeric, NodaTime helpers

When adding functionality, create a new file `JsonElement.<Concern>.cs`.

## Common Pitfalls

- **Forgetting to dispose**: `ParsedJsonDocument` rents from `ArrayPool<byte>`. Not disposing leaks pooled memory.
- **Using elements after disposal**: Elements reference the document's memory. Using them after `Dispose()` is undefined behavior.
- **Magic numbers in stackalloc**: Always use `JsonConstants.StackallocByteThreshold` / `StackallocCharThreshold`.

## Cross-References
- For mutable documents, see `corvus-mutable-documents`
- For production analyzers that catch missing dispose (CTJ004-006), see `corvus-analyzers`
- For full conventions, see `.github/copilot-instructions.md`
