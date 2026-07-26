// Petstore (Swagger 2.0) — from ExampleRecipes/042-OpenApi20Client
// The spec is OpenAPI 2.0: the generator auto-detects `swagger: "2.0"` and
// lowers body/formData parameters, collectionFormat, and host/basePath/schemes
// onto the same generated-client shape as the 3.x generators.
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Playground;
using Playground.Models;

// ── Setting up the client ────────────────────────────────────────────────────
// The server URI comes from the 2.0 host + basePath + schemes fields.
HttpClient httpClient = new(new PlaygroundHttpMessageHandler()) { BaseAddress = new Uri("https://petstore.example.com/v1") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);
ApiPetsClient client = new(transport);

// ── 1. List pets (GET /pets) ─────────────────────────────────────────────────
// `tags` declares collectionFormat: csv, so the values are comma-joined into a
// single query key: ?limit=10&tags=dog,friendly
Console.WriteLine("1. Listing pets (limit=10, tags=dog,friendly)...");
await using ListPetsResponse listResponse = await client.ListPetsAsync(
    limit: 10,
    tags: new GetPetsTags.Source(static (ref GetPetsTags.Builder b) =>
    {
        b.AddItem("dog"u8);
        b.AddItem("friendly"u8);
    }));

listResponse.MatchResult(
    matchOk: pets =>
    {
        Console.WriteLine($"   Got {pets.GetArrayLength()} pets");
        foreach (Pet pet in pets.EnumerateArray())
        {
            Console.WriteLine($"   - [{pet.Id}] {pet.Name} (tag: {pet.Tag})");
        }

        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error {error.Code}: {error.Message}");
        return 0;
    });

// Response headers are also typed — x-next comes from the 2.0 Response
// Object's `headers` map.
JsonString nextPage = listResponse.XNextHeader;
if (nextPage.IsNotUndefined())
{
    Console.WriteLine($"   Next page: {nextPage}");
}

Console.WriteLine();

// ── 2. Create a pet (POST /pets) ─────────────────────────────────────────────
// In 2.0 the request body is a parameter with `in: body`; it behaves exactly
// like a 3.x requestBody — a typed model with a builder.
Console.WriteLine("2. Creating a pet...");
await using CreatePetResponse createResponse = await client.CreatePetAsync(
    body: NewPet.Build(name: "Fido"u8, tag: "dog"u8));

createResponse.MatchResult(
    matchCreated: createdPet =>
    {
        Console.WriteLine($"   Created: [{createdPet.Id}] {createdPet.Name} (tag: {createdPet.Tag})");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error {error.Code}: {error.Message}");
        return 0;
    });

Console.WriteLine();

// ── 3. Update a pet with form data (POST /pets/{petId}) ──────────────────────
// The 2.0-specific shape: `in: formData` parameters are aggregated into a
// synthesized UpdatePetWithFormFormBody type and serialized as
// application/x-www-form-urlencoded: name=Fido+II&status=sold
Console.WriteLine("3. Updating a pet with form data...");
await using UpdatePetWithFormResponse updateResponse = await client.UpdatePetWithFormAsync(
    petId: "pet-123"u8,
    body: UpdatePetWithFormFormBody.Build(name: "Fido II"u8, status: "sold"u8));

updateResponse.MatchResult(
    matchOk: updatedPet =>
    {
        Console.WriteLine($"   Updated: [{updatedPet.Id}] {updatedPet.Name} (tag: {updatedPet.Tag})");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error {error.Code}: {error.Message}");
        return 0;
    });

Console.WriteLine();
Console.WriteLine("Done!");