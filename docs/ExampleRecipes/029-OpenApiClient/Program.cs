// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Petstore.Client;
using Petstore.Client.Models;

// ── Setting up the client ────────────────────────────────────────────────────
// The generated ApiPetsClient wraps the IApiTransport, which handles the actual
// HTTP communication. The generated code handles everything else: path template
// resolution, query string encoding, header serialization, and response parsing.
//
// Production code normally uses HttpClientTransport. This recipe uses an in-memory
// transport so `dotnet run` is self-contained and does not depend on a fictional
// petstore.example.com server.
await using DemoTransport transport = new();
ApiPetsClient client = new(transport);
Console.WriteLine("Using in-memory demo transport. In production, replace it with HttpClientTransport.");
Console.WriteLine();

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
Console.WriteLine("\n2. Creating a pet...");
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
Console.WriteLine("\n3. Showing pet by ID...");
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
Console.WriteLine("\n4. Demonstrating request validation...");
try
{
    // This will throw because limit > 100 violates the schema's "maximum: 100"
    await using ListPetsResponse _ = await client.ListPetsAsync(
        limit: 200,
        validationMode: ValidationMode.Detailed);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"   Validation caught: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Done!");

internal sealed class DemoTransport : IApiTransport
{
    private static readonly DemoHeaders ListPetsHeaders = new(("x-next", "/pets?limit=10&cursor=next"));

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse> =>
        CreateResponseAsync<TResponse>(cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
        in TRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse> =>
        CreateResponseAsync<TResponse>(cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Stream body,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse> =>
        CreateResponseAsync<TResponse>(cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Func<Stream, CancellationToken, ValueTask> bodyWriter,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse> =>
        CreateResponseAsync<TResponse>(cancellationToken);

    public ValueTask DisposeAsync() => default;

    private static ValueTask<TResponse> CreateResponseAsync<TResponse>(CancellationToken cancellationToken)
        where TResponse : struct, IApiResponse<TResponse>
    {
        if (typeof(TResponse) == typeof(ListPetsResponse))
        {
            return TResponse.CreateAsync(
                200,
                CreateStream("""[{"id":1,"name":"Fido","tag":"dog"},{"id":2,"name":"Misty","tag":"cat"}]"""),
                "application/json",
                ListPetsHeaders,
                cancellationToken: cancellationToken);
        }

        if (typeof(TResponse) == typeof(CreatePetResponse))
        {
            return TResponse.CreateAsync(
                201,
                CreateStream("""{"id":42,"name":"Fido","tag":"dog"}"""),
                "application/json",
                cancellationToken: cancellationToken);
        }

        if (typeof(TResponse) == typeof(ShowPetByIdResponse))
        {
            return TResponse.CreateAsync(
                200,
                CreateStream("""{"id":123,"name":"Rex","tag":"dog"}"""),
                "application/json",
                cancellationToken: cancellationToken);
        }

        throw new NotSupportedException($"The demo transport has no canned response for {typeof(TResponse).Name}.");
    }

    private static MemoryStream CreateStream(string content) =>
        new(System.Text.Encoding.UTF8.GetBytes(content), writable: false);
}

internal sealed class DemoHeaders(params (string Name, string Value)[] values) : IResponseHeaders
{
    private readonly Dictionary<string, string> values = values.ToDictionary(
        static item => item.Name,
        static item => item.Value,
        StringComparer.OrdinalIgnoreCase);

    public bool TryGetValue(string headerName, out string? value) =>
        this.values.TryGetValue(headerName, out value);
}