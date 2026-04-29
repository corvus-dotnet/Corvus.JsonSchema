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
using var bobDoc = ParsedJsonDocument<JsonElement>.Parse("\"Bob\"");
success = root.TryTest("/name"u8, bobDoc.RootElement);
Console.WriteLine($"TryTest /name == \"Bob\": {success}");

using var aliceDoc = ParsedJsonDocument<JsonElement>.Parse("\"Alice\"");
success = root.TryTest("/name"u8, aliceDoc.RootElement);
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

using var patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
    """
    [
        { "op": "replace", "path": "/age", "value": 32 },
        { "op": "add", "path": "/tags/-", "value": "verified" }
    ]
    """);
JsonPatchDocument parsedPatch = patchDoc.RootElement;

success = root.TryValidateAndApplyPatch(parsedPatch);
Console.WriteLine($"Parsed patch applied: {success}");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// JSON Merge Patch (RFC 7396)
// ------------------------------------------------------------------
Console.WriteLine("=== JSON Merge Patch (RFC 7396) ===");
Console.WriteLine();

string targetJson = """{"title": "Goodbye!", "author": {"givenName": "John"}, "tags": ["a"]}""";
string mergePatchJson = """{"title": "Hello!", "author": {"givenName": null}, "tags": ["b", "c"]}""";

using var mergeTargetDoc = ParsedJsonDocument<JsonElement>.Parse(targetJson);
using var mergePatchDoc = ParsedJsonDocument<JsonElement>.Parse(mergePatchJson);
using JsonWorkspace mergeWorkspace = JsonWorkspace.Create();
using var mergeBuilder = mergeTargetDoc.RootElement.CreateBuilder(mergeWorkspace);

JsonElement.Mutable mergeRoot = mergeBuilder.RootElement;
Console.WriteLine($"Before merge patch: {mergeBuilder.RootElement}");
JsonMergePatchExtensions.ApplyMergePatch(ref mergeRoot, mergePatchDoc.RootElement);
Console.WriteLine($"After merge patch:  {mergeBuilder.RootElement}");
Console.WriteLine();

// ------------------------------------------------------------------
// JSON Diff (RFC 6902 Patch Generation)
// ------------------------------------------------------------------
Console.WriteLine("=== JSON Diff ===");
Console.WriteLine();

string diffSourceJson = """{"name": "Alice", "age": 30, "email": "alice@example.com"}""";
string diffTargetJson = """{"name": "Bob", "age": 30, "active": true}""";

using var diffSourceDoc = ParsedJsonDocument<JsonElement>.Parse(diffSourceJson);
using var diffTargetDoc = ParsedJsonDocument<JsonElement>.Parse(diffTargetJson);
using JsonWorkspace diffWorkspace = JsonWorkspace.Create();

JsonPatchDocument diffPatch = JsonDiffExtensions.CreatePatch(
    diffSourceDoc.RootElement,
    diffTargetDoc.RootElement,
    diffWorkspace);

Console.WriteLine($"Diff patch: {diffPatch}");

// Apply the diff patch to verify it produces the target
using var diffBuilder = diffSourceDoc.RootElement.CreateBuilder(diffWorkspace);
JsonElement.Mutable diffRoot = diffBuilder.RootElement;
success = diffRoot.TryApplyPatch(diffPatch);
Console.WriteLine($"Patch applied: {success}");
Console.WriteLine($"Result: {diffBuilder.RootElement}");
