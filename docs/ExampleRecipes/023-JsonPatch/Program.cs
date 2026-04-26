using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

// ------------------------------------------------------------------
// Parse a JSON string into an immutable document
// ------------------------------------------------------------------
string personJson =
    """
    {
        "name": "Alice",
        "age": 30,
        "email": "alice@example.com",
        "tags": ["user"]
    }
    """;

using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(personJson);

Console.WriteLine("Original document:");
Console.WriteLine(parsedDoc.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Create a mutable builder
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = parsedDoc.RootElement.CreateBuilder(workspace);
JsonElement.Mutable root = builder.RootElement;

// ------------------------------------------------------------------
// Build and apply a patch document using PatchBuilder
// ------------------------------------------------------------------
Console.WriteLine("=== PatchBuilder: fluent patch construction ===");
Console.WriteLine();

JsonPatchDocument patch = root.BeginPatch(workspace)
    .Replace("/name"u8, "Bob")
    .Add("/tags/-"u8, "admin")
    .Remove("/email"u8)
    .GetPatchAndDispose();

bool success = root.TryApplyPatch(patch);
Console.WriteLine($"Patch applied: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Individual operations: TryAdd
// ------------------------------------------------------------------
Console.WriteLine("=== Individual operations ===");
Console.WriteLine();

success = root.TryAdd("/email"u8, "bob@example.com");
Console.WriteLine($"TryAdd /email: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Individual operations: TryReplace
// ------------------------------------------------------------------
success = root.TryReplace("/age"u8, 31);
Console.WriteLine($"TryReplace /age: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Individual operations: TryRemove
// ------------------------------------------------------------------
success = root.TryRemove("/email"u8);
Console.WriteLine($"TryRemove /email: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Individual operations: TryCopy
// ------------------------------------------------------------------
success = root.TryCopy("/name"u8, "/display_name"u8);
Console.WriteLine($"TryCopy /name -> /display_name: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Individual operations: TryMove
// ------------------------------------------------------------------
success = root.TryMove("/display_name"u8, "/nickname"u8);
Console.WriteLine($"TryMove /display_name -> /nickname: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Individual operations: TryTest
// ------------------------------------------------------------------
success = root.TryTest("/name"u8, JsonElement.ParseValue("""
    "Bob"
    """u8));
Console.WriteLine($"TryTest /name == \"Bob\": {success}");

success = root.TryTest("/name"u8, JsonElement.ParseValue("""
    "Alice"
    """u8));
Console.WriteLine($"TryTest /name == \"Alice\": {success}");
Console.WriteLine();

// ------------------------------------------------------------------
// Conditional patch using Test as a guard
// ------------------------------------------------------------------
Console.WriteLine("=== Conditional patch with Test guard ===");
Console.WriteLine();

JsonPatchDocument guardedPatch = root.BeginPatch(workspace)
    .Test("/name"u8, "Bob")
    .Replace("/name"u8, "Charlie")
    .GetPatchAndDispose();

success = root.TryApplyPatch(guardedPatch);
Console.WriteLine($"Guarded patch (name == Bob): {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// Now try a guarded patch that should fail
JsonPatchDocument failingPatch = root.BeginPatch(workspace)
    .Test("/name"u8, "Bob")
    .Replace("/name"u8, "Dave")
    .GetPatchAndDispose();

success = root.TryApplyPatch(failingPatch);
Console.WriteLine($"Guarded patch (name == Bob, but it's Charlie): {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Parsing a patch from raw JSON
// ------------------------------------------------------------------
Console.WriteLine("=== Parsing a patch from JSON ===");
Console.WriteLine();

JsonPatchDocument parsedPatch = JsonPatchDocument.ParseValue(
    """
    [
        { "op": "replace", "path": "/age", "value": 32 },
        { "op": "add", "path": "/tags/-", "value": "verified" }
    ]
    """u8);

success = root.TryValidateAndApplyPatch(parsedPatch);
Console.WriteLine($"Parsed patch applied: {success}");
Console.WriteLine(builder.RootElement);
