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
JsonPatchDocument patch = root.BeginPatch(workspace)
    .Replace("/name"u8, "Bob")
    .Add("/email"u8, "bob@example.com")
    .Remove("/age"u8)
    .GetPatchAndDispose();

// Apply the patch
bool success = root.TryApplyPatch(patch);

Console.WriteLine(success);             // True
Console.WriteLine(builder.RootElement); // {"name":"Bob","email":"bob@example.com"}
```

## Applying a Patch Document

### Validated application

If you have received a patch document from an external source (e.g. an HTTP request body, a file, or user input) and have not already validated it, use `TryValidateAndApplyPatch`. This validates the patch against its JSON Schema before applying it, returning `false` if the document is structurally invalid:

```csharp
using var patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
    """
    [
        { "op": "replace", "path": "/name", "value": "Charlie" },
        { "op": "add", "path": "/active", "value": true }
    ]
    """);
JsonPatchDocument parsedPatch = patchDoc.RootElement;

bool success = root.TryValidateAndApplyPatch(parsedPatch);
```

### Direct application (skip validation)

If you constructed the patch locally via `PatchBuilder`, or have already validated it as part of request processing, you can call `TryApplyPatch` directly to avoid the cost of redundant schema validation:

```csharp
JsonPatchDocument patch = root.BeginPatch(workspace)
    .Replace("/name"u8, "Charlie")
    .Add("/active"u8, true)
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(patch);
```

> **Note:** `TryApplyPatch` assumes the patch is a valid RFC 6902 document. Behaviour for invalid documents is undefined. A `Debug.Assert` verifies validity in debug builds.

> **Note:** If an operation fails partway through, earlier operations in the patch will already have been applied. The document is left in a partially-patched state. If you need atomic all-or-nothing semantics, take a snapshot before applying (see [JsonDocumentBuilder](JsonDocumentBuilder.md) for snapshot/restore).

## Building Patches with PatchBuilder

The `PatchBuilder` provides a fluent API for constructing patch documents programmatically. Create one by calling `BeginPatch(workspace)` on any mutable element, chain operations, then call `GetPatchAndDispose()` to finalize:

```csharp
JsonPatchDocument patch = root.BeginPatch(workspace)
    .Add("/tags/0"u8, "urgent")
    .Replace("/status"u8, "active")
    .Move("/old_name"u8, "/name"u8)
    .Test("/version"u8, 2)
    .GetPatchAndDispose();
```

The caller provides a `JsonWorkspace` that the builder uses for internal buffer management. Calling `GetPatchAndDispose()` finalizes the array, transfers metadata to the backing document builder, and returns a `JsonPatchDocument` backed by the workspace. The caller must keep the workspace alive for the lifetime of the patch. You must not use the builder after calling `GetPatchAndDispose()`.

If you need to abandon a partially-built patch, call `Dispose()` directly:

```csharp
PatchBuilder builder = root.BeginPatch(workspace);
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
using var replacementDoc = ParsedJsonDocument<JsonElement>.Parse("""{"replaced": true}""");
root.TryAdd(""u8, replacementDoc.RootElement);
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
using var expectedDoc = ParsedJsonDocument<JsonElement>.Parse("""
    "Alice"
    """);
bool matches = root.TryTest("/name"u8, expectedDoc.RootElement);
```

This is useful in patch documents for conditional application — if the test fails, the entire patch fails:

```csharp
JsonPatchDocument guardedPatch = root.BeginPatch(workspace)
    .Test("/version"u8, 1)           // guard: only apply if version is 1
    .Replace("/version"u8, 2)        // update version
    .Add("/migrated"u8, true)        // add new field
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(guardedPatch);
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

---

## JSON Merge Patch (RFC 7396)

### Overview

`Corvus.Text.Json.Patch` implements [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396), a simpler alternative to JSON Patch for describing changes to a JSON document. Instead of specifying individual operations, a merge patch is a JSON document whose structure mirrors the target — properties present in the patch are set or replaced in the target, and properties with `null` values are removed.

### Quick Start

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

string targetJson = """{"title": "Goodbye!", "author": {"givenName": "John"}, "tags": ["a"]}""";
string patchJson = """{"title": "Hello!", "author": {"givenName": null}, "tags": ["b", "c"]}""";

using var targetDoc = ParsedJsonDocument<JsonElement>.Parse(targetJson);
using var patchDoc = ParsedJsonDocument<JsonElement>.Parse(patchJson);
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = targetDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;
JsonMergePatchExtensions.ApplyMergePatch(ref root, patchDoc.RootElement);

Console.WriteLine(builder.RootElement);
// {"title":"Hello!","author":{},"tags":["b","c"]}
```

### Merge semantics

The RFC 7396 algorithm works as follows:

- If the patch is **not an object**, the target is replaced entirely by the patch value.
- If the patch **is an object**, each property is merged into the target:
  - A patch property with a `null` value **removes** the corresponding target property.
  - A patch property with an object value **merges recursively** into the target property (creating it as an empty object if it doesn't exist or isn't an object).
  - Any other patch property **sets or replaces** the corresponding target property.
- **Arrays are replaced wholesale** — they are never merged element-by-element.

### Limitations

Merge Patch cannot express all possible changes that JSON Patch can:

- It cannot set a property to `null` (because `null` means "remove").
- It cannot rearrange array elements — arrays are always replaced entirely.
- It cannot express operations like `move` or `copy`.

For these use cases, use the full [JSON Patch](#applying-a-patch-document) API instead.

---

## JSON Diff (RFC 6902 Patch Generation)

### Overview

The diff feature computes a [JSON Patch (RFC 6902)](https://datatracker.ietf.org/doc/html/rfc6902) that transforms one JSON element into another. This is useful for change tracking, audit logging, or synchronizing JSON documents across systems.

### Quick Start

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

string sourceJson = """{"name": "Alice", "age": 30}""";
string targetJson = """{"name": "Bob", "age": 30, "email": "bob@example.com"}""";

using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(sourceJson);
using var targetDoc = ParsedJsonDocument<JsonElement>.Parse(targetJson);
using JsonWorkspace workspace = JsonWorkspace.Create();

JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(
    sourceDoc.RootElement,
    targetDoc.RootElement,
    workspace);

// Apply the patch to verify it produces the target
using var builder = sourceDoc.RootElement.CreateBuilder(workspace);
JsonElement.Mutable root = builder.RootElement;
bool success = root.TryApplyPatch(patch);

Console.WriteLine(success);             // True
Console.WriteLine(builder.RootElement); // {"name":"Bob","age":30,"email":"bob@example.com"}
```

### Diff strategy

The diff algorithm operates on semantic JSON equality:

- **Objects** are diffed property-by-property. Properties present in both are recursively compared; properties only in the source produce `remove` operations; properties only in the target produce `add` operations. Property order is not significant.
- **Arrays** are compared element-by-element when lengths match. When array lengths differ, the entire array is replaced. A future version may implement LCS-based array diffing for more compact patches.
- **Scalars** (strings, numbers, booleans, null) are compared for value equality. Different values produce a `replace` operation.

### Array handling

When two arrays have the **same length**, the diff compares elements at each index and produces targeted operations for changed elements:

```csharp
// Source: {"items": [1, 2, 3]}  →  Target: {"items": [1, 99, 3]}
// Patch:  [{"op":"replace","path":"/items/1","value":99}]
```

Nested objects within same-length arrays are also diffed recursively:

```csharp
// Source: {"people": [{"name": "Alice"}, {"name": "Bob"}]}
// Target: {"people": [{"name": "Alice"}, {"name": "Charlie"}]}
// Patch:  [{"op":"replace","path":"/people/1/name","value":"Charlie"}]
```

When arrays have **different lengths**, the entire array is replaced:

```csharp
// Source: {"tags": ["a", "b"]}  →  Target: {"tags": ["a", "b", "c"]}
// Patch:  [{"op":"replace","path":"/tags","value":["a","b","c"]}]
```

### Workspace lifetime

The returned `JsonPatchDocument` is backed by the workspace. The caller must keep the workspace alive for the lifetime of the returned document. If you need the patch to outlive the workspace, serialize it to JSON first:

```csharp
JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(source, target, workspace);
string patchJson = patch.ToString();
```
