// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Petstore.Client;

// ── Setting up the client ────────────────────────────────────────────────────
// The generated ApiPetsClient wraps the IApiTransport, which handles the actual
// HTTP communication. You provide an HttpClient configured with the server's
// base address. The generated code handles everything else: path template
// resolution, query string encoding, header serialization, and response parsing.
HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v1") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);
ApiPetsClient client = new(transport);

// ── 1. List pets (GET /pets) ─────────────────────────────────────────────────
// The optional `limit` parameter accepts a plain C# int via implicit conversion.
// Behind the scenes, the generated code:
//   • Validates the parameter against its JSON Schema (integer, maximum: 100)
//   • Serializes it as a UTF-8 query string: ?limit=10
//   • Parses the response body into a strongly-typed Pets array
Console.WriteLine("1. Listing pets (limit=10)...");
await using ListPetsResponse listResponse = await client.ListPetsAsync(limit: 10);

// MatchResult ensures you handle every response status code defined in the spec —
// like a discriminated union. The compiler ensures exhaustive handling.
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
// The request body uses an ObjectBuilder delegate to construct the JSON object.
// For scalar properties, pass C# literals directly — the implicit conversions
// handle UTF-8 encoding. The generated code:
//   • Validates the body against the NewPet JSON Schema (name is required)
//   • Serializes it as application/json in the request body
//   • Parses the 201 response into a typed Pet
Console.WriteLine("2. Creating a pet...");
await using CreatePetResponse createResponse = await client.CreatePetAsync(
    body: new NewPet.Source(static (ref NewPet.Builder b) =>
    {
        // The typed Builder.Create() method mirrors the schema's properties.
        // Required properties are parameters; optional ones have defaults.
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
// Path parameters accept strings — the generated code:
//   • URI-encodes the value (percent-encoding special characters)
//   • Substitutes it into the path template: /pets/{petId} → /pets/pet-123
//   • Deserializes the 200 body into a typed Pet
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

// ── 4. Request validation ────────────────────────────────────────────────────
// By default (ValidationMode.Basic), all request parameters and bodies are
// validated against their JSON Schema before sending. You can opt into detailed
// validation for richer error messages, or disable it for performance.
Console.WriteLine("4. Demonstrating request validation...");
try
{
    // This will throw because limit > 100 violates the schema's "maximum: 100"
    await using ListPetsResponse _ = await client.ListPetsAsync(
        limit: 200,
        validationMode: ValidationMode.Detailed);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"   Validation caught: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Done!");