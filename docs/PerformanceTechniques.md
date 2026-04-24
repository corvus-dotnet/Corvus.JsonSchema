# Performance Techniques

This document describes the low-allocation and high-performance patterns used throughout the Corvus.JsonSchema codebase. It serves as a reference for contributors who need to understand *why* the code is structured the way it is, and *when* to apply each technique.

For prescriptive guidance on applying these patterns, see the Copilot skills:
- `.github/skills/corvus-buffer-and-pooling/` — buffer management and pooling
- `.github/skills/corvus-low-alloc-data-structures/` — ref-struct collections and SIMD

## Design Goal

Zero allocation on the happy path. When parsing, validating, or querying JSON, the library should produce no GC pressure unless the user explicitly requests a heap object (e.g., calling `GetString()` or `ToString()`). Every allocation in a hot path is a performance regression.

---

## 1. Flat-Array Document Model

Traditional JSON DOM implementations allocate a heap object for every token. Corvus stores parsed JSON as two flat buffers — a metadata array and a UTF-8 value buffer — with zero per-token heap allocations.

### DbRow — 12 bytes per token

Every JSON token is described by a `DbRow`: a 12-byte struct with `[StructLayout(LayoutKind.Sequential)]`. Three `uint/int` fields pack token type, byte offset, size, child count, and flags using bit manipulation:

- **Bits 0–27** of field 1: byte offset into the value buffer
- **Bit 31** of field 1: external document flag
- **Bit 31** of field 2: HasComplexChildren flag; remaining bits hold size/length
- **Top nibble** of field 3: `JsonTokenType` enum; remaining 28 bits hold row skip count

This encoding supports documents with over 268 million tokens.

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/Document/Internal/DbRow.cs`

### MetadataDb

The flat `DbRow[]` array, rented from `ArrayPool<byte>.Shared`. Returned on `Dispose()`.

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/Document/Internal/MetadataDb.cs`

### JsonElement — 2-field accessor

`JsonElement` is a `readonly partial struct` with exactly two fields: `IJsonDocument _parent` and `int _idx`. Every property access, array indexer, or enumeration returns a new struct on the stack by indexing into the parent document's flat arrays. No heap allocation at any point.

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/Document/JsonElement.cs`

### PropertyMap — Hash-based lookup without Dictionary

JSON object property lookup uses a compact binary hash table stored inline in the MetadataDb buffer. The `PropertyMap` struct (20 bytes) stores bucket/entry offsets. Lookup hashes UTF-8 property name bytes directly via `Utf8Hash.GetHashCode()` and resolves collisions with `SequenceEqual()` on byte spans — no string allocation.

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/Document/Internal/JsonDocument.PropertyMap.cs`

---

## 2. UTF-8-Native Processing

The entire parsing and validation pipeline operates on `ReadOnlySpan<byte>`. Strings are only created when the user explicitly requests them.

### Principles

- **URI handling**: `Utf8Uri` is a `readonly ref struct` that stores URIs as raw UTF-8 bytes with integer offsets for each component. Component accessors are `Slice()` calls, not substring allocations.
- **Format validation**: date parsing goes directly from `ReadOnlySpan<byte>` to `DateTimeOffset`. Email validation checks byte values against `u8` character sets. Regex matching transcodes only the minimum needed into a rented `char[]` buffer.
- **UTF-8 string literals** (`"..."u8`): used throughout for compile-time static byte arrays. `SearchValues<byte>` (on .NET 8+) provides vectorised matching over these.
- **Property lookup**: hashes UTF-8 bytes directly — no intermediate `string`.

### Transcoding

When a `string` is genuinely needed, `JsonReaderHelper.TranscodeHelper` provides four overloads:

| Signature | Allocation |
|-----------|-----------|
| `string TranscodeHelper(ReadOnlySpan<byte>)` | One string (unavoidable) |
| `int TranscodeHelper(ReadOnlySpan<byte>, Span<char>)` | Zero — writes to caller's buffer |
| `bool TryTranscode(ReadOnlySpan<byte>, Span<char>, out int)` | Zero — non-throwing variant |
| `int TranscodeHelper(ReadOnlySpan<char>, Span<byte>)` | Zero — reverse direction |

On `netstandard2.0` these use `unsafe fixed` pointer overloads; on modern .NET they use span-based `Encoding` APIs.

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/Reader/JsonReaderHelper.Unescaping.cs`

---

## 3. Three-Tier Pooling Hierarchy

### Tier 1: stackalloc with threshold constants

All temporary buffers follow a single consistent pattern: stack-allocate for small sizes, rent from `ArrayPool` for larger. Four threshold constants (defined in `Common/src/System/Text/Json/JsonConstants.cs`) control the crossover:

| Constant | Value | Use for |
|----------|-------|---------|
| `StackallocByteThreshold` | 256 | UTF-8 byte buffers |
| `StackallocCharThreshold` | 128 | char buffers |
| `StackallocNonRecursiveByteThreshold` | 4096 | Non-recursive top-level contexts |
| `StackallocNonRecursiveCharThreshold` | 2048 | Non-recursive char contexts |

The standard pattern:

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
        ArrayPool<byte>.Shared.Return(rentedArray);
}
```

Rules: always declare the nullable array before the ternary, always use the named constant, always slice to `length`, always use `try/finally`.

### Tier 2: ArrayPool\<T\>.Shared

Used for larger or longer-lived buffers:

- **MetadataDb** backing storage
- **ParsedJsonDocument** value buffers (returned via `Interlocked.Exchange` in `Dispose()`)
- **JsonWorkspace** document index array (2× growth)
- **Utf8KeyHashSet** buckets, entries, and key data (three separate pools)
- **PooledByteBufferWriter** write buffers

### Tier 3: Thread-local caches

For expensive-to-create objects, `[ThreadStatic]` caches with a depth counter handle the common case of one outstanding use per thread, while cleanly supporting recursive re-entry:

| Cache | Pools | File |
|-------|-------|------|
| `Utf8JsonWriterCache` | `Utf8JsonWriter` + `PooledByteBufferWriter` | `Writer/Utf8JsonWriterCache.cs` |
| `JsonWorkspaceCache` | `JsonWorkspace` instances | `DocumentBuilder/Internal/JsonWorkspaceCache.cs` |
| `JsonSchemaResultsCollectorCache` | Validation result collectors | `JsonSchema/Internal/JsonSchemaResultsCollectorCache.cs` |

The depth counter pattern: `RentedCount++` on rent; if 0→1, return cached instance (after reset); if already >0, allocate fresh. On return, decrement; if back to 0, reset for reuse.

---

## 4. Stack-Allocated Collections

The codebase contains several custom `ref struct` (and value type) collections that avoid heap allocation for typical workloads.

### Utf8KeyHashSet

A separate-chaining hash set for UTF-8 byte-string keys. Entries are 20-byte packed binary records written with `MemoryMarshal.Write`. Growth uses a small prime table. Falls back to `ArrayPool` only when stack buffers overflow.

> Source: `src/Common/Utf8KeyHashSet.cs`

### ValueListBuilder\<T\>

A `ref struct` list (ported from `dotnet/runtime`) that starts on a caller-provided `stackalloc` span and overflows to `ArrayPool<T>` with 2× doubling.

> Source: `System.Private.CoreLib/src/System/Collections/Generic/ValueListBuilder.cs`

### ValueStringBuilder / Utf8ValueStringBuilder

Parallel `ref struct` string builders for `char` and `byte`. Both follow stackalloc → ArrayPool growth. `Utf8ValueStringBuilder` sets `_pos = -1` on dispose to detect double-dispose.

> Sources: `Common/src/System/Text/ValueStringBuilder.cs`, `Common/src/System/Text/Utf8ValueStringBuilder.cs`

### BitStack

Tracks JSON nesting using a `ulong` for the first 64 levels (zero allocation). Falls back to `int[]` only for pathologically deep documents.

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/BitStack.cs`

### Sequence (JSONata)

A 32-byte inline tagged union. Singleton JSON element evaluations produce zero heap allocation — the element is stored inline in the struct. Only multi-value results rent from `ArrayPool<JsonElement>`.

> Source: `src/Corvus.Text.Json.Jsonata/Sequence.cs`

### ArrayBuffer

A sliding-window buffer wrapping a pooled `byte[]`. `Discard(n)` advances read position (no copy), `Commit(n)` extends write position, growth doubles capacity.

> Source: `Common/src/System/Net/ArrayBuffer.cs`

---

## 5. Source-Generated Strongly-Typed Structs

### The CRTP pattern

`IJsonElement<T> where T : struct, IJsonElement<T>` — the Curiously Recurring Template Pattern — ensures the JIT sees the concrete type at every call site:

- `where T : struct` eliminates boxing
- The JIT devirtualises all interface calls (the concrete type is known statically)
- `static abstract CreateInstance()` (on .NET 8+) provides zero-cost factory abstraction
- Pre-.NET 8 falls back to a reflection helper

### AggressiveInlining on generated methods

The code generator emits `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on thin wrapper methods: array mutators (`SetItem`, `InsertItem`, `AddItem`, `AddRange`), property setters, and type conversion operators. These delegate to document operations — inlining eliminates call overhead.

### Conditional compilation

`#if NET9_0_OR_GREATER` unlocks:
- **`allows ref struct`** on delegate type parameters — passing ref struct contexts through lambdas without boxing
- **`SearchValues<byte>`** — vectorised character class matching
- **Span-based `Encoding` APIs** — avoiding `unsafe fixed` fallbacks

---

## 6. SIMD and Bit Manipulation

### Vectorised scanning

On platforms without `SearchValues<byte>` (.NET 7 and earlier, netstandard), explicit `Vector<byte>` operations scan 16–32 bytes simultaneously for special characters (quotes, backslashes, control characters).

> Source: `src/Corvus.Text.Json/Corvus/Text/Json/Reader/JsonReaderHelper.netstandard.cs`

### 8× unrolled SIMD count

`SpanHelper.Count` in the source generator processes `8 × Vector<T>.Count` elements per iteration for batch counting.

> Source: `src/Corvus.Text.Json.SourceGenerator/Community.HighPerformance/Helpers/Internals/SpanHelper.Count.cs`

### De Bruijn bit scanning

Finding the first set bit uses a De Bruijn multiplier for O(1) branchless lookup — standard technique for converting an isolated lowest set bit to an index.

### Branchless classification

`IsTokenTypePrimitive` uses unsigned subtraction + comparison to replace a multi-branch switch: `(tokenType - String) <= (Null - String)`.

---

## 7. Happy-Path Optimisations

### Lazy error messages

Schema validation uses `static readonly` delegate fields (`JsonSchemaMessageProvider`) that format error messages only when called. On the success path, `EvaluatedKeyword(true, ...)` records success without touching the message provider.

### Lazy string resources

The `SR` class's `ResourceManager` is only created on first exception throw. `UsingResourceKeys()` provides a fast-path for trimmed apps.

### Deferred exception messages

`CodeGenThrowHelper` sets `Exception.Source` to a resource key rather than formatting a message string at the throw site. The actual message is only resolved if the exception is caught and displayed.

---

## 8. Unsafe Code

`AllowUnsafeBlocks=true` is set for targeted operations in compatibility paths:

| Usage | Why | Where |
|-------|-----|-------|
| `fixed (char* ptr = ...)` | `Encoding.UTF8.GetByteCount` on netstandard2.0 | `JsonPatchExtensions.cs` |
| `Unsafe.ReadUnaligned<Vector<byte>>()` | SIMD vector loading | `JsonReaderHelper.netstandard.cs` |
| `MemoryMarshal.Write/Read` | Binary serialisation of hash set entries | `Utf8KeyHashSet.cs` |

On modern .NET (8+), most `unsafe` blocks are replaced with safe span-based APIs via conditional compilation.
