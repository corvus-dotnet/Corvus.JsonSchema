# JSON Schema Patterns in .NET - JSON Patch (RFC 6902)

This recipe demonstrates how to apply [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) operations to mutable documents using the `Corvus.Text.Json.Patch` library. It covers building patches fluently, applying individual operations, conditional patches with `test`, and parsing patches from raw JSON.

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

The `PatchBuilder` provides a fluent API for constructing patch documents. Call `BeginPatch()` to start, chain operations, and call `GetPatchAndDispose()` to finalize:

```csharp
JsonPatchDocument patch = root.BeginPatch()
    .Replace("/name"u8, "Bob")
    .Add("/tags/-"u8, "admin")
    .Remove("/email"u8)
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(in patch);
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
bool matches = root.TryTest("/name"u8, JsonElement.ParseValue("""
    "Bob"
    """u8));   // true
bool fails   = root.TryTest("/name"u8, JsonElement.ParseValue("""
    "Alice"
    """u8));   // false
```

### Conditional patches with Test guards

When `test` is used in a patch document, a failing test aborts the entire patch. This enables optimistic concurrency patterns:

```csharp
JsonPatchDocument guardedPatch = root.BeginPatch()
    .Test("/name"u8, "Bob")           // guard: only apply if name is Bob
    .Replace("/name"u8, "Charlie")    // update name
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(in guardedPatch);
// success is true only if /name was "Bob"
```

### Parsing patches from JSON

You can also parse a patch document directly from raw JSON:

```csharp
JsonPatchDocument parsedPatch = JsonPatchDocument.Parse(
    """
    [
        { "op": "replace", "path": "/age", "value": 32 },
        { "op": "add", "path": "/tags/-", "value": "verified" }
    ]
    """u8);

root.TryApplyPatch(in parsedPatch);
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
