---
name: corvus-buffer-and-pooling
description: >
  Write allocation-efficient buffer code in Corvus.JsonSchema using the codebase's
  established three-tier pooling pattern: stackalloc → ArrayPool → ThreadStatic caches.
  Covers threshold constants, the rent/return pattern, UTF-8-first processing, thread-local
  writer and workspace caches, and PooledByteBufferWriter. USE FOR: writing any code that
  needs temporary byte/char buffers, adding new pooled caches, working with UTF-8 data,
  avoiding heap allocation on hot paths. DO NOT USE FOR: choosing which ref-struct collection
  to use (use corvus-low-alloc-data-structures), document model internals (use
  corvus-parsed-documents-and-memory).
---

# Buffer Management and Pooling Patterns

## The Three-Tier Pooling Hierarchy

All temporary buffers in this codebase follow a strict three-tier allocation strategy. Use the cheapest tier that fits.

```
Tier 1: stackalloc           — ≤ threshold bytes, zero cost, no cleanup needed
Tier 2: ArrayPool<T>.Shared  — larger buffers, must return in finally block
Tier 3: [ThreadStatic] cache — expensive-to-create objects (writers, workspaces), reused per thread
```

## Tier 1 + 2: The stackalloc / ArrayPool Rent Pattern

This is the single most common pattern in the codebase. Every temporary buffer follows it exactly.

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

### Threshold Constants

Defined in `Common/src/System/Text/Json/JsonConstants.cs`:

| Constant | Value | Use for |
|----------|-------|---------|
| `JsonConstants.StackallocByteThreshold` | 256 | `byte` / UTF-8 buffers |
| `JsonConstants.StackallocCharThreshold` | 128 | `char` buffers |
| `JsonConstants.StackallocNonRecursiveByteThreshold` | 4096 | Non-recursive contexts (e.g., top-level parse) |
| `JsonConstants.StackallocNonRecursiveCharThreshold` | 2048 | Non-recursive char contexts |

Use `NonRecursive` variants only when you can prove the call site is not recursive — otherwise stack overflow is possible.

### Rules (non-negotiable)

1. **Declare `rentedArray` before the ternary** — it must be in scope for the `finally` block
2. **Use the named constant**, not a magic number, for both the threshold check and the `stackalloc` size
3. **Always slice to `length`** — `ArrayPool.Rent()` returns an array that may be larger than requested
4. **Always use `try/finally`** to guarantee the rented array is returned
5. **For fixed-size buffers always ≤ threshold** (e.g., a 128-byte scratch buffer), plain `stackalloc` without pool fallback is acceptable

### char buffer variant

```csharp
char[]? rentedChars = null;

Span<char> charBuffer = length <= JsonConstants.StackallocCharThreshold
    ? stackalloc char[JsonConstants.StackallocCharThreshold]
    : (rentedChars = ArrayPool<char>.Shared.Rent(length));

try
{
    DoWork(charBuffer.Slice(0, length));
}
finally
{
    if (rentedChars != null)
    {
        ArrayPool<char>.Shared.Return(rentedChars);
    }
}
```

## Tier 2: ArrayPool Usage in Core Types

Several core types rent directly from `ArrayPool<T>.Shared`:

| Type | What it rents | Pool type | Return trigger |
|------|--------------|-----------|----------------|
| `MetadataDb` | Token metadata array | `ArrayPool<byte>` | `Dispose()` |
| `ParsedJsonDocument` | UTF-8 value buffer | `ArrayPool<byte>` | `Dispose()` via `Interlocked.Exchange` |
| `JsonWorkspace` | Document index array | `ArrayPool<IJsonDocument>` | `Dispose()` with 2× growth on expand |
| `Utf8KeyHashSet` | Buckets, entries, key buffer | `ArrayPool<int>` + `ArrayPool<byte>` | `Dispose()` |
| `PooledByteBufferWriter` | Write buffer | `ArrayPool<byte>` | `ClearAndReturnBuffers()` |

When writing Dispose logic for pooled types, use `Interlocked.Exchange(ref _array, null)` to prevent double-return.

## Tier 3: Thread-Local Caches

For objects that are expensive to construct but frequently needed, the codebase uses `[ThreadStatic]` caches with a depth-counter pattern:

```csharp
[ThreadStatic]
private static ThreadLocalState? t_threadLocalState;

public static ExpensiveObject Rent(...)
{
    ThreadLocalState state = t_threadLocalState ??= new();
    if (state.RentedCount++ == 0)
    {
        // First (non-recursive) call — return the cached instance
        state.CachedObject.Reset(...);
        return state.CachedObject;
    }
    // Recursive call — allocate a fresh instance
    return new ExpensiveObject(...);
}

public static void Return(ExpensiveObject obj)
{
    ThreadLocalState state = t_threadLocalState!;
    if (--state.RentedCount == 0)
    {
        state.CachedObject.ResetAllStateForCacheReuse();
        // Cached object stays in thread-local for next Rent
    }
    else
    {
        obj.Dispose(); // Recursive instance — dispose immediately
    }
}
```

Three caches follow this pattern:

| Cache class | What it pools | Key file |
|-------------|--------------|----------|
| `Utf8JsonWriterCache` | `Utf8JsonWriter` + `PooledByteBufferWriter` pair | `Writer/Utf8JsonWriterCache.cs` |
| `JsonWorkspaceCache` | `JsonWorkspace` instances | `DocumentBuilder/Internal/JsonWorkspaceCache.cs` |
| `JsonSchemaResultsCollectorCache` | Schema validation result collectors | `JsonSchema/Internal/JsonSchemaResultsCollectorCache.cs` |

### Rules for thread-local caches

- The **depth counter** is critical — it handles recursive re-entry (e.g., schema validation validating a sub-schema)
- `ResetAllStateForCacheReuse()` must clear all mutable state without reallocating internal buffers
- Only cache objects where construction cost justifies the pattern (writers with internal buffers, workspaces with document arrays)

## UTF-8-First Processing

The entire parsing and validation pipeline operates on `ReadOnlySpan<byte>` (UTF-8). Strings are only created when the user explicitly requests them.

### Key principle: never create `System.String` on hot paths

- URI components: `Utf8Uri` is a `readonly ref struct` that stores integer offsets into the original UTF-8 byte span — component accessors return `Slice()` calls, not substrings
- Format validation (date, email, URI, regex): operates directly on `ReadOnlySpan<byte>`
- Property lookup: `PropertyMap` hashes UTF-8 bytes directly via `Utf8Hash.GetHashCode()` — no string at any point
- Use UTF-8 string literals (`"..."u8`) for static byte arrays (character sets, fixed tokens)

### When you must transcode

Use `JsonReaderHelper.TranscodeHelper` — four overloads with zero allocation except when a `string` is genuinely needed:

| Signature | Allocation |
|-----------|-----------|
| `string TranscodeHelper(ReadOnlySpan<byte>)` | One string (unavoidable) |
| `int TranscodeHelper(ReadOnlySpan<byte>, Span<char>)` | Zero — writes to caller's buffer |
| `bool TryTranscode(ReadOnlySpan<byte>, Span<char>, out int)` | Zero — non-throwing variant |
| `int TranscodeHelper(ReadOnlySpan<char>, Span<byte>)` | Zero — reverse direction |

### SearchValues on .NET 8+

Use `SearchValues<byte>` for character class matching:

```csharp
private static readonly SearchValues<byte> s_controlQuoteBackslash =
    SearchValues.Create("\u0000\u0001...\u001F\"\\"u8);
```

This is vectorised by the JIT. On older TFMs, fall back to manual `Span.IndexOfAny()`.

## PooledByteBufferWriter

The bridge between `ArrayPool` and `IBufferWriter<byte>`. Wraps an `ArrayBuffer` that rents from the shared pool.

- Used by `Utf8JsonWriterCache` to provide pre-allocated write buffers
- `ClearAndReturnBuffers()` returns memory to the pool without disposing the writer itself
- `ArrayBuffer` uses a sliding-window pattern: `Discard(n)` advances read position, `Commit(n)` extends write position, growth doubles capacity

## Common Pitfalls

| Mistake | Consequence | Fix |
|---------|-------------|-----|
| Missing `try/finally` on rented array | Memory leak from ArrayPool | Always wrap in try/finally |
| Using magic number `256` instead of `JsonConstants.StackallocByteThreshold` | Inconsistency, harder to change | Use the named constant |
| Forgetting to slice rented buffer | Processing garbage bytes beyond `length` | Always `buffer.Slice(0, length)` |
| Returning rented array twice | Pool corruption | Use `Interlocked.Exchange(ref arr, null)` |
| Creating `string` from UTF-8 on a hot path | Unnecessary GC pressure | Use `ReadOnlySpan<byte>` throughout, transcode only at the boundary |
| Using `NonRecursive` threshold in recursive code | Stack overflow | Only use when call site is provably non-recursive |

## Cross-References

- For ref-struct collections (hash sets, list builders, string builders), see `corvus-low-alloc-data-structures`
- For document memory model and disposal, see `corvus-parsed-documents-and-memory`
- For full conventions, see `.github/copilot-instructions.md`
