A `JsonPatchDocument` represents an RFC 6902 JSON Patch — an array of operations that describe mutations to apply to a JSON document.

### Parsing from JSON

```csharp
using Corvus.Text.Json.Patch;

JsonPatchDocument patch = JsonPatchDocument.ParseValue(
    """
    [
        { "op": "replace", "path": "/name", "value": "Bob" },
        { "op": "add", "path": "/email", "value": "bob@example.com" },
        { "op": "remove", "path": "/temporary" }
    ]
    """u8);
```

### Applying to a mutable document

Use `TryValidateAndApplyPatch` when the patch comes from an untrusted source — it validates the patch against its JSON Schema before applying:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = parsedDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;

bool applied = root.TryValidateAndApplyPatch(patch);
```

### Building a patch with PatchBuilder

For locally-constructed patches that don't need validation, use `PatchBuilder`:

```csharp
JsonPatchDocument patch = root.BeginPatch(workspace)
    .Replace("/name"u8, "Charlie")
    .Add("/tags/-"u8, "admin")
    .Remove("/temp"u8)
    .GetPatchAndDispose();

root.TryApplyPatch(patch);
```
