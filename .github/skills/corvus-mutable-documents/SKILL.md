---
name: corvus-mutable-documents
description: >
  Create and manipulate mutable JSON documents using JsonWorkspace,
  JsonDocumentBuilder, and the builder pattern. Covers workspace creation
  (rented vs unrented), the canonical parse-build-mutate-serialize pattern,
  deep property mutation, array operations, cloning, and RFC 6902 JSON Patch
  via PatchBuilder. USE FOR: writing code that creates or modifies JSON,
  understanding the V5 mutation model, implementing JSON Patch operations,
  working with JsonWorkspace. DO NOT USE FOR: read-only parsing
  (use corvus-parsed-documents-and-memory), V4 mutation patterns
  (use corvus-v4-migration).
---

# Mutable Documents

## JsonWorkspace

A scoped container for pooled memory used during mutable JSON operations.

```csharp
// Preferred — rents from thread-local cache
using JsonWorkspace workspace = JsonWorkspace.Create();

// When you need explicit lifetime control
JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
```

Always use a `using` block. `Dispose()` returns the workspace to the thread-local cache (rented) or disposes all child documents and returns backing arrays to `ArrayPool` (unrented).

## Canonical Mutation Pattern

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);

// Convert immutable → mutable
using JsonDocumentBuilder<JsonElement.Mutable> builder =
    sourceDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;

// Mutate
root.SetProperty("name"u8, "new value"u8);
root.RemoveProperty("oldProp"u8);

// Serialize
string result = root.ToString();
```

## Multiple Builders Per Workspace

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder1 = doc1.RootElement.CreateBuilder(workspace);
using var builder2 = doc2.RootElement.CreateBuilder(workspace);
// Both share the same workspace memory pool
```

## Empty Builder

```csharp
using JsonDocumentBuilder<JsonElement.Mutable> builder =
    workspace.CreateBuilder<JsonElement.Mutable>(initialCapacity: 30, initialValueBufferSize: 8192);
```

## Cloning

`Clone()` produces an immutable `ParsedJsonDocument`-backed element that outlives the builder:

```csharp
JsonElement clone;
using (JsonWorkspace workspace = JsonWorkspace.Create())
using (ParsedJsonDocument<JsonElement> parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[[]]]"))
using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
{
    clone = doc.RootElement[0].Clone();
}
// clone is still valid after the workspace is disposed
Assert.Equal("[[]]", clone.GetRawText());
```

## Version Tracking

`JsonDocumentBuilder<T>` tracks a `ulong _version`. When the builder is mutated, the version increments. Stale element references (from before the mutation) throw `InvalidOperationException`.

## JSON Patch (RFC 6902)

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
using var builder = doc.RootElement.CreateBuilder(workspace);

// Build a patch
JsonPatchDocument patch = builder.RootElement.BeginPatch(workspace)
    .Add("/name"u8, JsonElement.ParseValue("\"Alice\""u8))
    .Remove("/obsolete"u8)
    .Replace("/version"u8, JsonElement.ParseValue("2"u8))
    .GetPatchAndDispose();

// Apply the patch (returns bool — true if all operations succeed)
bool success = builder.RootElement.TryApplyPatch(patch);
```

All six RFC 6902 operations: `Add`, `Remove`, `Replace`, `Move`, `Copy`, `Test`.

**Performance tip:** Use UTF-8 byte literal paths (`"/name"u8`) for zero-allocation path handling.

**Atomicity:** `TryApplyPatch` returns `false` on the first failing operation, leaving partial state. For atomic semantics, snapshot the builder state before applying.

## Common Pitfalls

- **Forgetting to dispose workspace/builder**: Both rent from pools. Missing `using` leaks memory.
- **Using stale element references**: After mutating a builder, any previously-obtained element references are invalidated.
- **Not using `using` with `BeginPatch()`**: The patch builder must be disposed via `GetPatchAndDispose()`. The returned `JsonPatchDocument` is backed by the workspace — keep the workspace alive for the lifetime of the patch.

## Cross-References
- For read-only parsing, see `corvus-parsed-documents-and-memory`
- For dispose analyzers (CTJ004-006), see `corvus-analyzers`
- For V4→V5 mutation model changes, see `corvus-v4-migration`
