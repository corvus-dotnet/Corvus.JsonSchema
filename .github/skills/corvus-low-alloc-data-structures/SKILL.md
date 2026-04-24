---
name: corvus-low-alloc-data-structures
description: >
  Use and extend the custom low-allocation data structures in Corvus.JsonSchema.
  Covers ref-struct collections (Utf8KeyHashSet, UniqueItemsHashSet, ValueListBuilder,
  ValueStringBuilder, Utf8ValueStringBuilder, BitStack, Sequence), SIMD scanning patterns,
  bit manipulation tricks, and happy-path optimisations like lazy error messages. USE FOR:
  choosing which stack-allocated collection to use, writing new ref-struct types, adding
  SIMD-accelerated code paths, understanding branchless techniques, writing validation code
  that avoids allocation on success. DO NOT USE FOR: basic buffer allocation patterns
  (use corvus-buffer-and-pooling), document model (use corvus-parsed-documents-and-memory).
---

# Low-Allocation Data Structures

## Collection Selection Guide

Choose the right collection based on your need:

| Need | Type | Location | Heap allocation? |
|------|------|----------|-----------------|
| Deduplicate UTF-8 keys | `Utf8KeyHashSet` | `Common/Utf8KeyHashSet.cs` | No (stack + ArrayPool fallback) |
| Check JSON array uniqueness | `UniqueItemsHashSet` | `JsonSchema/Internal/UniqueItemsHashSet.cs` | No (stack + ArrayPool fallback) |
| Growable list (small) | `ValueListBuilder<T>` | `System.Private.CoreLib/.../ValueListBuilder.cs` | No (stack + ArrayPool fallback) |
| Build a UTF-16 string | `ValueStringBuilder` | `Common/src/System/Text/ValueStringBuilder.cs` | No (stack + ArrayPool fallback) |
| Build a UTF-8 string | `Utf8ValueStringBuilder` | `Common/src/System/Text/Utf8ValueStringBuilder.cs` | No (stack + ArrayPool fallback) |
| Track nesting depth | `BitStack` | `BitStack.cs` | No (ulong for ≤64 levels) |
| Return singleton/multi value | `Sequence` | `Corvus.Text.Json.Jsonata/Sequence.cs` | No for singletons; ArrayPool for multi |
| Sliding-window byte buffer | `ArrayBuffer` | `Common/src/System/Net/ArrayBuffer.cs` | ArrayPool backed |

All `ref struct` types must be disposed (they implement `IDisposable` to return pooled memory).

## Utf8KeyHashSet — Stack-Allocated UTF-8 Hash Set

A `ref struct` separate-chaining hash set for UTF-8 byte-string keys. Used for property deduplication and JSONata merge operations.

```csharp
// Typical usage — stack-allocate initial buffers, dispose to return any rented overflow
Span<int> buckets = stackalloc int[Utf8KeyHashSet.StackAllocBucketSize];
Span<byte> entries = stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize];
Span<byte> keyBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];

using Utf8KeyHashSet hashSet = new(buckets, entries, keyBuffer);
bool added = hashSet.Add(utf8Key);
```

### Internal design

- Entries are packed as 20-byte binary records: `Next(4) + BufOffset(4) + Length(4) + HashCode(8)`
- Packed with `MemoryMarshal.Write/Read` and `[MethodImpl(AggressiveInlining)]`
- Growth uses a small prime table (`3, 7, 11, 17, ... 521`) — avoids `HashHelpers` dependency
- Falls back to `ArrayPool` only when stack buffers overflow
- Stack alloc sizes: buckets = 64 ints (256 bytes), entries = 512 bytes, key buffer = 512 bytes

### When to use

- Property name deduplication in JSON object processing
- Merge operations in JSONata where duplicate keys must be detected
- Any scenario needing set membership testing on UTF-8 byte sequences without allocation

## UniqueItemsHashSet — Schema Validation Hash Set

Same architectural pattern as `Utf8KeyHashSet`, but specifically for JSON Schema `uniqueItems` keyword validation. Compares full JSON element values (not just strings).

## ValueListBuilder<T> — Stack-Backed Generic List

Ported from `dotnet/runtime`'s internal type. A `ref struct` list that starts on the stack and overflows to `ArrayPool`.

```csharp
Span<int> initialBuffer = stackalloc int[16];
ValueListBuilder<int> list = new(initialBuffer);

try
{
    list.Append(42);
    list.Append(99);
    ReadOnlySpan<int> items = list.AsSpan();
}
finally
{
    list.Dispose();  // returns any rented array
}
```

### Key behaviours

- Initial buffer is the caller's `stackalloc` span — zero heap allocation for small lists
- Grows with 2× doubling via `ArrayPool<T>.Shared.Rent()`
- `Dispose()` returns the rented array and clears references if `T` is a reference type
- Used in YAML writing (`Utf8YamlWriter._contextStack`), JSON parsing, and path navigation

## ValueStringBuilder / Utf8ValueStringBuilder

Two parallel `ref struct` string builders — one for `char` (UTF-16), one for `byte` (UTF-8). Both follow the stackalloc → ArrayPool growth pattern.

```csharp
// UTF-16 string building
Span<char> initialChars = stackalloc char[JsonConstants.StackallocCharThreshold];
ValueStringBuilder sb = new(initialChars);
try
{
    sb.Append("hello");
    sb.Append(' ');
    sb.Append("world");
    return sb.ToString();
}
finally
{
    sb.Dispose();
}

// UTF-8 string building
Span<byte> initialBytes = stackalloc byte[JsonConstants.StackallocByteThreshold];
Utf8ValueStringBuilder usb = new(initialBytes);
try
{
    usb.Append("hello"u8);
    ReadOnlySpan<byte> result = usb.AsSpan();
}
finally
{
    usb.Dispose();
}
```

### Differences

| Feature | `ValueStringBuilder` | `Utf8ValueStringBuilder` |
|---------|----------------------|--------------------------|
| Element type | `char` | `byte` |
| Double-dispose detection | No | Yes (`_pos = -1` on dispose) |
| Interpolation support | `AppendSpanFormattable` | No |

## BitStack — Allocation-Free Nesting Tracker

Tracks JSON object/array nesting using bit manipulation on a `ulong`. No allocation for the first 64 levels.

```csharp
BitStack stack = default;
stack.PushTrue();   // entering an object
stack.PushFalse();  // entering an array
bool wasObject = stack.Pop();  // leaving — returns true if it was an object
```

### Internal design

- First 64 levels: single `ulong _allocationFreeContainer` with shift/mask operations
- Beyond 64 levels: falls back to heap-allocated `int[]` (extremely rare in practice)
- `Div32Rem` uses `& 31` instead of `% 32` for the array path

## Sequence — 32-Byte Inline Tagged Union (JSONata)

The JSONata evaluator's primary result type. Stores singleton values inline without allocation.

```
Layout (32 bytes):
  object? payload     (8 bytes)  — reference-type backing for multi-value
  double rawValue     (8 bytes)  — inline double storage
  JsonElement single  (12 bytes) — inline singleton element
  int countAndTag     (4 bytes)  — bits 0-23: count, bits 24-31: tag
```

Tags: `Undefined=0, Singleton=1, Multi=2, Lambda=3, Regex=4, RawDouble=7, Tuple=9`.

- **Singleton**: zero heap allocation — the JSON element is stored inline in the struct
- **Multi**: rents from `ArrayPool<JsonElement>` — must be disposed
- A single JSON element evaluation produces a `Singleton` sequence with zero allocation

## SIMD Scanning Patterns

### Vector<byte> parallel scanning (netstandard path)

On platforms without `SearchValues<byte>`, the codebase uses explicit SIMD:

```csharp
if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
{
    Vector<byte> vQuote = new((byte)'"');
    Vector<byte> vBackslash = new((byte)'\\');
    Vector<byte> vControlMax = new(0x1F);

    Vector<byte> vData = Unsafe.ReadUnaligned<Vector<byte>>(
        ref Unsafe.AddByteOffset(ref searchSpace, index));

    var vMatches = Vector.BitwiseOr(
        Vector.BitwiseOr(
            Vector.Equals(vData, vQuote),
            Vector.Equals(vData, vBackslash)),
        Vector.LessThan(vData, vControlMax));
}
```

Scans 16–32 bytes simultaneously for quotes, backslashes, or control characters.

### De Bruijn bit scanning

Finding the first set bit in a SIMD match result:

```csharp
ulong powerOfTwoFlag = match ^ (match - 1);    // isolate lowest set bit
return (int)((powerOfTwoFlag * XorPowerOfTwoToHighByte) >> 57);
```

O(1) branchless lookup using a precomputed De Bruijn constant.

### 8× unrolled SIMD count

`SpanHelper.Count` in the source generator processes `8 × Vector<T>.Count` elements per iteration for counting matching items in spans.

## Bit Manipulation Techniques

### Branchless token type classification

```csharp
public static bool IsTokenTypePrimitive(JsonTokenType tokenType) =>
    (tokenType - JsonTokenType.String) <= (JsonTokenType.Null - JsonTokenType.String);
```

Single unsigned subtraction + comparison replaces a multi-branch switch.

### Bit-packed struct fields

`DbRow` (12 bytes) packs token type, location, size, child count, and flags into 3 `uint/int` fields using bit shifts and masks. The top nibble of the third field encodes `JsonTokenType`.

## Happy-Path Optimisations

### Lazy error messages in schema validation

```csharp
private static readonly JsonSchemaMessageProvider ExpectedDate =
    static (buffer, out written) => { /* format error message only when called */ };
```

On the success path, `EvaluatedKeyword(true, ...)` records success without touching the message provider. Error strings are only materialised on failure.

### Lazy SR (string resources)

`ResourceManager` is only created on first exception throw — `UsingResourceKeys()` returns early with the key itself when trimming is active.

### CodeGenThrowHelper

Throw helpers set `Exception.Source` to a key instead of formatting a message string. The actual message is only looked up if the exception is caught and displayed.

## Creating New ref struct Collections

When adding a new `ref struct` collection, follow these patterns:

1. **Accept initial storage as constructor parameters** — let the caller stackalloc the initial buffers
2. **Define stack-alloc size constants** on the type (e.g., `StackAllocBucketSize`)
3. **Implement `IDisposable`** to return any `ArrayPool`-rented overflow buffers
4. **Use `MemoryMarshal.Write/Read`** with `[MethodImpl(AggressiveInlining)]` for packed entries
5. **Growth via `ArrayPool<T>.Shared.Rent()`** with geometric (2×) sizing
6. **Return old rented buffer** before taking a new one during growth

## Cross-References

- For the basic stackalloc/rent pattern and pooling tiers, see `corvus-buffer-and-pooling`
- For document memory model, see `corvus-parsed-documents-and-memory`
- For full conventions, see `.github/copilot-instructions.md`
