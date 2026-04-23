Build an RFC 6902 JSON Patch document using the fluent builder API. Create a builder by calling `BeginPatch()` on a mutable element, chain operations, then call `GetPatchAndDispose()` to produce the final `JsonPatchDocument`.

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"name": "Alice", "age": 30, "tags": ["user"]}"""u8);
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = parsedDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;

JsonPatchDocument patch = root.BeginPatch()
    .Replace("/name"u8, "Bob")
    .Add("/email"u8, "bob@example.com")
    .Add("/tags/-"u8, "admin")
    .Remove("/age"u8)
    .GetPatchAndDispose();

root.TryApplyPatch(in patch);
// root is now: {"name":"Bob","tags":["user","admin"],"email":"bob@example.com"}
```

### Conditional patches with test

The `Test` operation verifies a value before proceeding. If the test fails, the entire patch is rejected:

```csharp
JsonPatchDocument guardedPatch = root.BeginPatch()
    .Test("/version"u8, 1)
    .Replace("/version"u8, 2)
    .GetPatchAndDispose();

bool applied = root.TryApplyPatch(in guardedPatch);
// applied is false if /version was not 1
```

### Move and copy operations

```csharp
JsonPatchDocument reorgPatch = root.BeginPatch()
    .Move("/tags"u8, "/labels"u8)
    .Copy("/name"u8, "/display_name"u8)
    .GetPatchAndDispose();

root.TryApplyPatch(in reorgPatch);
```
