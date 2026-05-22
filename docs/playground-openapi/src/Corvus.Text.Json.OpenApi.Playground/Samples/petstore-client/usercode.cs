// Petstore Client — from ExampleRecipes/029-OpenApiClient
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Playground;

// ── Setting up the client ────────────────────────────────────────────────────
HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v1") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);
ApiPetsClient client = new(transport);

// ── 1. List pets (GET /pets) ─────────────────────────────────────────────────
Console.WriteLine("1. Listing pets (limit=10)...");
await using ListPetsResponse listResponse = await client.ListPetsAsync(limit: 10);

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

// Response headers are also typed — x-next provides the pagination link
JsonString nextPage = listResponse.XNextHeader;
if (nextPage.IsNotUndefined())
{
    Console.WriteLine($"   Next page: {nextPage}");
}

Console.WriteLine();

// ── 2. Create a pet (POST /pets) ─────────────────────────────────────────────
Console.WriteLine("2. Creating a pet...");
await using CreatePetResponse createResponse = await client.CreatePetAsync(
    body: new NewPet.Source(static (ref NewPet.Builder b) =>
    {
        b.Create(name: "Fido"u8, tag: "dog"u8);
    }));

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

// ── 3. Show a specific pet (GET /pets/{petId}) ───────────────────────────────
Console.WriteLine("3. Showing pet by ID...");
await using ShowPetByIdResponse showResponse = await client.ShowPetByIdAsync(petId: "pet-123"u8);

showResponse.MatchResult(
    matchOk: foundPet =>
    {
        Console.WriteLine($"   Found: [{foundPet.Id}] {foundPet.Name} (tag: {foundPet.Tag})");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error {error.Code}: {error.Message}");
        return 0;
    });

Console.WriteLine();
Console.WriteLine("Done!");