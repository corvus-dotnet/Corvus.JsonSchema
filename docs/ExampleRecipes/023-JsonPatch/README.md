# JSON Schema Patterns in .NET - JSON Patch, Merge Patch, and Diff

This recipe demonstrates [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902), [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396), and JSON Diff using the `Corvus.Text.Json.Patch` library. It covers building patches fluently, applying individual operations, conditional patches with `test`, parsing patches from raw JSON, applying merge patches, and computing diffs between documents.

## The Pattern

JSON Patch is a standard format for describing modifications to a JSON document. A patch is a JSON array of operation objects, each specifying an `op` (add, remove, replace, move, copy, or test), a `path` (a JSON Pointer per RFC 6901), and optionally a `value` or `from` field.

In Corvus.Text.Json, patches are applied to mutable elements (`JsonElement.Mutable`) obtained from a `JsonDocumentBuilder`. You can construct patches programmatically with the fluent `PatchBuilder`, parse them from JSON, or apply individual operations directly.

## The Schema

File: `person.json`

```json
{
    "title": "Person",
    "type": "object",
    "required": ["name", "age"],
    "properties": {
        "name": { "type": "string" },
        "age": { "type": "integer", "format": "int32" },
        "email": { "type": "string", "format": "email" },
        "tags": {
            "type": "array",
            "items": { "type": "string" }
        }
    }
}
```

## Generated Code Usage

[Example code](./Program.cs)

### Setup: parse and create a mutable builder

```csharp
string personJson =
    """
    {
        "name": "Alice",
        "age": 30,
        "email": "alice@example.com",
        "tags": ["user"]
    }
    """;

using var parsedDoc = ParsedJsonDocument<Person>.Parse(personJson);
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = parsedDoc.RootElement.CreateBuilder(workspace);
Person.Mutable root = builder.RootElement;
```

### Building and applying a patch with PatchBuilder

The `PatchBuilder` provides a fluent API for constructing patch documents. Call `BeginPatch(workspace)` to start, chain operations, and call `GetPatchAndDispose()` to finalize:

```csharp
JsonPatchDocument patch = root.BeginPatch(workspace)
    .Replace("/name"u8, "Bob")
    .Add("/tags/-"u8, "admin")
    .Remove("/email"u8)
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(patch);
// Result: {"name":"Bob","age":30,"tags":["user","admin"]}
```

### Applying individual operations

Each RFC 6902 operation is available as a standalone extension method:

```csharp
root.TryAdd("/email"u8, "bob@example.com");       // add a property
root.TryReplace("/age"u8, 31);                     // replace a value
root.TryRemove("/email"u8);                        // remove a property
root.TryCopy("/name"u8, "/display_name"u8);        // copy a value
root.TryMove("/display_name"u8, "/nickname"u8);     // move a value
```

### Testing values

The `test` operation checks that a value at a path equals an expected value without mutating the document:

```csharp
using var bobDoc = ParsedJsonDocument<JsonElement>.Parse("\"Bob\"");
bool matches = root.TryTest("/name"u8, bobDoc.RootElement);   // true
using var aliceDoc = ParsedJsonDocument<JsonElement>.Parse("\"Alice\"");
bool fails   = root.TryTest("/name"u8, aliceDoc.RootElement); // false
```

### Conditional patches with Test guards

When `test` is used in a patch document, a failing test aborts the entire patch. This enables optimistic concurrency patterns:

```csharp
JsonPatchDocument guardedPatch = root.BeginPatch(workspace)
    .Test("/name"u8, "Bob")           // guard: only apply if name is Bob
    .Replace("/name"u8, "Charlie")    // update name
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(guardedPatch);
// success is true only if /name was "Bob"
```

### Parsing patches from JSON

You can also parse a patch document directly from raw JSON:

```csharp
using var patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
    """
    [
        { "op": "replace", "path": "/age", "value": 32 },
        { "op": "add", "path": "/tags/-", "value": "verified" }
    ]
    """);
JsonPatchDocument parsedPatch = patchDoc.RootElement;

root.TryApplyPatch(parsedPatch);
```

## FAQ

### What happens if a patch operation fails?

All `Try*` methods return `false` on failure. When `TryApplyPatch` processes a multi-operation patch, operations that were applied before the failure are **not** rolled back — the document is left in a partially-patched state. Use `test` guards or snapshot/restore if you need atomic semantics.

### What path format do the operations use?

Paths follow [RFC 6901 JSON Pointer](https://datatracker.ietf.org/doc/html/rfc6901) syntax. Segments are separated by `/`, with `~0` escaping `~` and `~1` escaping `/`. The empty string `""` refers to the root document.

### Can I use string paths instead of UTF-8?

Yes. All path-accepting methods have `ReadOnlySpan<byte>`, `ReadOnlySpan<char>`, and `string` overloads. UTF-8 byte literals (`"..."u8`) are preferred for performance.

## Running the Example

```bash
cd docs/ExampleRecipes/023-JsonPatch
dotnet run
```

## Related Patterns

- [018-CreatingAndMutatingObjects](../018-CreatingAndMutatingObjects/) — Mutable document lifecycle
- [019-CloneAndFreeze](../019-CloneAndFreeze/) — Cloning mutable elements into independent immutable documents

### JSON Merge Patch (RFC 7396)

Merge Patch is a simpler alternative to JSON Patch. Instead of individual operations, the patch itself is a JSON document that mirrors the target structure. Properties in the patch are merged into the target; `null` values remove properties; arrays are replaced wholesale.

```csharp
string targetJson = """{"title": "Goodbye!", "author": {"givenName": "John"}, "tags": ["a"]}""";
string mergePatchJson = """{"title": "Hello!", "author": {"givenName": null}, "tags": ["b", "c"]}""";

using var mergeTargetDoc = ParsedJsonDocument<JsonElement>.Parse(targetJson);
using var mergePatchDoc = ParsedJsonDocument<JsonElement>.Parse(mergePatchJson);
using JsonWorkspace mergeWorkspace = JsonWorkspace.Create();
using var mergeBuilder = mergeTargetDoc.RootElement.CreateBuilder(mergeWorkspace);

JsonElement.Mutable mergeRoot = mergeBuilder.RootElement;
JsonMergePatchExtensions.ApplyMergePatch(ref mergeRoot, mergePatchDoc.RootElement);
// Result: {"title":"Hello!","author":{},"tags":["b","c"]}
```

The `givenName` property is removed (patch value is `null`), `title` is replaced, and `tags` is replaced wholesale.

### JSON Diff (RFC 6902 Patch Generation)

The diff feature computes an RFC 6902 patch that transforms one JSON element into another:

```csharp
string diffSourceJson = """{"name": "Alice", "age": 30, "email": "alice@example.com"}""";
string diffTargetJson = """{"name": "Bob", "age": 30, "active": true}""";

using var diffSourceDoc = ParsedJsonDocument<JsonElement>.Parse(diffSourceJson);
using var diffTargetDoc = ParsedJsonDocument<JsonElement>.Parse(diffTargetJson);
using JsonWorkspace diffWorkspace = JsonWorkspace.Create();

JsonPatchDocument diffPatch = JsonDiffExtensions.CreatePatch(
    diffSourceDoc.RootElement,
    diffTargetDoc.RootElement,
    diffWorkspace);

// Apply the diff patch to verify round-trip correctness
using var diffBuilder = diffSourceDoc.RootElement.CreateBuilder(diffWorkspace);
JsonElement.Mutable diffRoot = diffBuilder.RootElement;
bool success = diffRoot.TryApplyPatch(diffPatch);
// Result: {"name":"Bob","age":30,"active":true}
```

The diff produces `replace` for changed values, `remove` for deleted properties, and `add` for new properties.

Same-length arrays are diffed element-by-element, producing targeted operations:

```csharp
// Source: {"items": [1, 2, 3]}  →  Target: {"items": [1, 99, 3]}
// Patch:  [{"op":"replace","path":"/items/1","value":99}]
```

Different-length arrays are replaced wholesale:

```csharp
// Source: {"tags": ["a", "b"]}  →  Target: {"tags": ["a", "b", "c"]}
// Patch:  [{"op":"replace","path":"/tags","value":["a","b","c"]}]
```
