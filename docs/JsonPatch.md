# JSON Patch (RFC 6902)

## Overview

`Corvus.Text.Json.Patch` implements [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) for the Corvus.Text.Json mutable document model. It provides both a fluent `PatchBuilder` for constructing patch documents and extension methods on `JsonElement.Mutable` for applying them.

All six RFC 6902 operations are supported: **add**, **remove**, **replace**, **move**, **copy**, and **test**. Paths follow [RFC 6901 JSON Pointer](https://datatracker.ietf.org/doc/html/rfc6901) syntax.

The library is designed to work with the zero-allocation mutable document infrastructure — patch operations modify the `JsonDocumentBuilder` in-place without creating intermediate copies.

## Installation

```bash
dotnet add package Corvus.Text.Json.Patch
```

You also need the core library and source generator:

```bash
dotnet add package Corvus.Text.Json
```

## Quick Start

Parse a JSON document, build a patch, apply it, and read the result:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

string json = """{"name": "Alice", "age": 30}""";

using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = parsedDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;

// Build a patch document
JsonPatchDocument patch = root.BeginPatch()
    .Replace("/name"u8, "Bob")
    .Add("/email"u8, "bob@example.com")
    .Remove("/age"u8)
    .GetPatchAndDispose();

// Apply the patch
bool success = root.TryApplyPatch(in patch);

Console.WriteLine(success);             // True
Console.WriteLine(builder.RootElement); // {"name":"Bob","email":"bob@example.com"}
```

## Applying a Patch Document

The `TryApplyPatch` extension method applies an entire `JsonPatchDocument` to a mutable element. Operations are applied in order; if any operation fails, processing stops immediately and the method returns `false`.

```csharp
JsonPatchDocument patch = JsonPatchDocument.ParseValue(
    """
    [
        { "op": "replace", "path": "/name", "value": "Charlie" },
        { "op": "add", "path": "/active", "value": true }
    ]
    """u8);

bool success = root.TryApplyPatch(in patch);
```

The patch document is assumed to be a valid RFC 6902 JSON array. No schema validation is performed on the individual operations during application — only the `op` field is inspected to dispatch to the correct handler.

> **Note:** If an operation fails partway through, earlier operations in the patch will already have been applied. The document is left in a partially-patched state. If you need atomic all-or-nothing semantics, take a snapshot before applying (see [JsonDocumentBuilder](JsonDocumentBuilder.md) for snapshot/restore).

## Building Patches with PatchBuilder

The `PatchBuilder` provides a fluent API for constructing patch documents programmatically. Create one by calling `BeginPatch()` on any mutable element, chain operations, then call `GetPatchAndDispose()` to finalize:

```csharp
JsonPatchDocument patch = root.BeginPatch()
    .Add("/tags/0"u8, "urgent")
    .Replace("/status"u8, "active")
    .Move("/old_name"u8, "/name"u8)
    .Test("/version"u8, 2)
    .GetPatchAndDispose();
```

The builder manages its own `JsonWorkspace` internally. Calling `GetPatchAndDispose()` writes the closing array bracket, parses the result into a `JsonPatchDocument`, and disposes the workspace. You must not use the builder after calling `GetPatchAndDispose()`.

If you need to abandon a partially-built patch, call `Dispose()` directly:

```csharp
PatchBuilder builder = root.BeginPatch();
try
{
    builder.Add("/foo"u8, someValue);
    // ... decide not to apply ...
}
finally
{
    builder.Dispose();
}
```

## Individual Operations

Each operation is also available as a standalone extension method on `JsonElement.Mutable`, useful when you need to apply a single operation without constructing a full patch document.

### Add

Adds a value at the target path. If the target is an object property, it is created (or replaced if it already exists). If the target is an array index, the value is inserted at that position. The special index `-` appends to the end of an array.

```csharp
// Add (or replace) an object property
root.TryAdd("/email"u8, "alice@example.com");

// Insert into an array at index 0
root.TryAdd("/tags/0"u8, "important");

// Append to the end of an array
root.TryAdd("/tags/-"u8, "new-tag");
```

Adding to the root path (`""`) replaces the entire document:

```csharp
root.TryAdd(""u8, JsonElement.ParseValue("""{"replaced": true}"""));
```

### Remove

Removes the value at the target path. The target must exist.

```csharp
bool success = root.TryRemove("/email"u8);

// Remove an array element by index
root.TryRemove("/tags/0"u8);
```

Removing the root path is not permitted and returns `false`.

### Replace

Replaces the value at the target path. The target must already exist — unlike `add`, `replace` will not create new properties.

```csharp
root.TryReplace("/name"u8, "Bob");
root.TryReplace("/age"u8, 31);
```

### Move

Moves the value from one path to another. This is equivalent to a `remove` from the source followed by an `add` at the destination.

```csharp
// Move /old_name to /name
root.TryMove("/old_name"u8, "/name"u8);

// Move an array element to a different position
root.TryMove("/items/2"u8, "/items/0"u8);
```

The source path must exist, and the destination must be a valid target for an `add` operation.

### Copy

Copies the value from one path to another without removing the source.

```csharp
// Copy /name to /display_name
root.TryCopy("/name"u8, "/display_name"u8);

// Copy an array element
root.TryCopy("/items/0"u8, "/items/-"u8);
```

### Test

Tests that the value at the target path equals the expected value. Returns `true` if the values match (using deep equality), `false` otherwise. No mutation is performed.

```csharp
bool matches = root.TryTest("/name"u8, JsonElement.ParseValue("""
    "Alice"
    """u8));
```

This is useful in patch documents for conditional application — if the test fails, the entire patch fails:

```csharp
JsonPatchDocument patch = root.BeginPatch()
    .Test("/version"u8, 1)           // guard: only apply if version is 1
    .Replace("/version"u8, 2)        // update version
    .Add("/migrated"u8, true)        // add new field
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(in patch);
// success is false if /version was not 1
```

## JSON Pointer Paths

All paths follow [RFC 6901 JSON Pointer](https://datatracker.ietf.org/doc/html/rfc6901) syntax:

- Paths start with `/` (except the empty string `""` which refers to the root)
- Each `/`-delimited segment is a property name or array index
- The tilde character is escaped as `~0`, the forward slash as `~1`
- Array indices are zero-based decimal integers; `-` refers to the position past the last element

| Path | Meaning |
|------|---------|
| `""` | The root document |
| `"/name"` | The `name` property of the root object |
| `"/address/city"` | The `city` property of the nested `address` object |
| `"/tags/0"` | The first element of the `tags` array |
| `"/tags/-"` | Past-the-end position of the `tags` array (for append) |
| `"/a~1b"` | The property named `a/b` (escaped forward slash) |
| `"/m~0n"` | The property named `m~n` (escaped tilde) |

## Path Overloads

All path-accepting methods provide three overloads:

| Overload | Example | Notes |
|----------|---------|-------|
| `ReadOnlySpan<byte>` | `TryAdd("/name"u8, value)` | UTF-8 bytes — preferred for performance |
| `ReadOnlySpan<char>` | `TryAdd("/name".AsSpan(), value)` | Transcoded to UTF-8 internally |
| `string` | `TryAdd("/name", value)` | Delegates to the `ReadOnlySpan<char>` overload |

For best performance, use the UTF-8 byte literal (`"..."u8`) overloads. The `string` and `char` variants allocate a temporary UTF-8 buffer (stack-allocated for paths up to 256 bytes, pooled for longer paths).

## Error Handling

All `Try*` methods return `bool`:

- `true` — the operation was applied successfully
- `false` — the operation failed (target path not found, index out of range, type mismatch, test value not equal)

When `TryApplyPatch` returns `false`, the document may be in a partially-modified state (operations applied before the failure are not rolled back). If you need transactional semantics, snapshot the builder state before applying the patch.

## See Also

- [JsonDocumentBuilder](JsonDocumentBuilder.md) — Mutable document builder with snapshot/restore
