The following example applies an RFC 6902 JSON Patch to a mutable document using the fluent `PatchBuilder`.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

string json = """{"name": "Alice", "age": 30, "tags": ["user"]}""";

using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = parsedDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;

JsonPatchDocument patch = root.BeginPatch()
    .Replace("/name"u8, "Bob")
    .Add("/email"u8, "bob@example.com")
    .Add("/tags/-"u8, "admin")
    .Remove("/age"u8)
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(in patch);
// root is now: {"name":"Bob","tags":["user","admin"],"email":"bob@example.com"}
```

### Individual operations

You can also apply operations one at a time without constructing a patch document:

```csharp
root.TryAdd("/active"u8, true);
root.TryReplace("/name"u8, "Charlie");
root.TryRemove("/email"u8);
root.TryMove("/tags"u8, "/labels"u8);
root.TryCopy("/name"u8, "/display_name"u8);
```

### Conditional patches with test

The `test` operation checks a value without mutating the document. When used in a patch document, a failing test aborts the entire patch:

```csharp
JsonPatchDocument guardedPatch = root.BeginPatch()
    .Test("/version"u8, 1)
    .Replace("/version"u8, 2)
    .GetPatchAndDispose();

bool applied = root.TryApplyPatch(in guardedPatch);
// applied is false if /version was not 1
```

### Parsing a patch from JSON

```csharp
JsonPatchDocument patch = JsonPatchDocument.ParseValue(
    """
    [
        { "op": "add", "path": "/foo", "value": "bar" },
        { "op": "remove", "path": "/baz" }
    ]
    """u8);

root.TryApplyPatch(in patch);
```
