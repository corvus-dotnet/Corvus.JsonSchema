// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Petstore.Server;

// ── Building a minimal API server from an OpenAPI spec ───────────────────────
// The generated code provides:
//   • IApiPetsHandler — an interface with one method per operation
//   • ApiEndpointRegistration.MapApiEndpoints() — registers all routes with ASP.NET
//   • Params structs — strongly-typed, schema-validated parameters
//   • Result structs — typed response builders with status code factory methods
//
// You implement the handler interface. The generated middleware handles:
//   • Deserializing path, query, header, and cookie parameters
//   • Parsing and schema-validating the request body
//   • Returning 400 Problem Details if validation fails
//   • Serializing the typed response body to the HTTP response
//   • Setting Content-Type and response headers

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    string baseUrl = app.Urls.FirstOrDefault(static url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        ?? app.Urls.FirstOrDefault()
        ?? "http://localhost:50515";

    Console.WriteLine();
    Console.WriteLine("OpenAPI Server example is running.");
    Console.WriteLine($"Base URL: {baseUrl}");
    Console.WriteLine();
    Console.WriteLine("Try these requests from another terminal:");
    Console.WriteLine($"  curl \"{baseUrl}/pets?limit=2\"");
    Console.WriteLine($"  curl -X POST \"{baseUrl}/pets\" -H \"Content-Type: application/json\" -d \"{{\\\"name\\\":\\\"Fido\\\",\\\"tag\\\":\\\"dog\\\"}}\"");
    Console.WriteLine($"  curl \"{baseUrl}/pets/1\"");
    Console.WriteLine();
});

app.Use(async (context, next) =>
{
    Console.WriteLine($"--> {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
    await next(context);
    Console.WriteLine($"<-- {context.Response.StatusCode} {context.Response.ContentType ?? "(no content type)"}");
});

// Register a single handler instance — in production, use DI registration
PetsHandler handler = new();
app.MapApiEndpoints(handler);

app.Run();

// ── Implementing the handler ─────────────────────────────────────────────────
// Each handler method receives:
//   • A Params struct with the parsed, validated parameters
//   • A JsonWorkspace for building response objects in pooled memory
//   • A CancellationToken for cooperative cancellation
//
// Return a Result struct using the factory methods that match your OpenAPI
// response definitions (e.g., Ok, Created, Default).

/// <summary>
/// A sample implementation of the Petstore handler.
/// </summary>
internal sealed class PetsHandler : IApiPetsHandler
{
    // In-memory pet store for demonstration
    private readonly List<(long Id, string Name, string? Tag)> pets =
    [
        (1, "Luna", "cat"),
        (2, "Max", "dog"),
        (3, "Coco", "parrot"),
    ];

    private long nextId = 4;

    /// <summary>
    /// Handles GET /pets — returns a filtered list of pets.
    /// </summary>
    /// <remarks>
    /// The generated code has already validated that <c>limit</c> satisfies
    /// its JSON Schema (integer, maximum: 100) before this method is called.
    /// If the client sends limit=200, the middleware returns 400 automatically.
    /// </remarks>
    public ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Read the optional limit parameter — it's already validated
        int limit = parameters.Limit.IsNotUndefined()
            ? (int)parameters.Limit
            : 20;

        var petsToReturn = this.pets.Take(limit).ToList();
        Console.WriteLine($"    [Pets] Returning {petsToReturn.Count} pet(s), limit={limit}");

        // Build the response using the typed Result factory.
        // Ok() accepts a Pets.Source (array builder) and the workspace.
        ListPetsResult result = ListPetsResult.Ok(
            body: new Pets.Source((ref Pets.Builder b) =>
            {
                foreach ((long id, string name, string? tag) in petsToReturn)
                {
                    // Each array item is built with its own typed builder
                    b.AddItem(new Pet.Source((ref Pet.Builder pb) =>
                    {
                        if (tag is string t)
                        {
                            pb.Create(id: id, name: name, tag: t);
                        }
                        else
                        {
                            pb.Create(id: id, name: name);
                        }
                    }));
                }
            }),
            workspace: workspace,
            xNext: limit < this.pets.Count
                ? JsonString.ParseValue("\"/pets?offset=1\""u8)
                : default);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Handles POST /pets — creates a new pet.
    /// </summary>
    /// <remarks>
    /// The generated code has already parsed and validated the request body
    /// against the NewPet schema. If <c>name</c> is missing or the body is
    /// malformed JSON, the middleware returns 400 before this method runs.
    /// </remarks>
    public ValueTask<CreatePetResult> HandleCreatePetAsync(
        CreatePetParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Access the validated body — it's a strongly-typed NewPet
        NewPet body = parameters.Body;
        long id = this.nextId++;
        string name = (string)body.Name;
        string? tag = body.Tag.IsNotUndefined() ? (string)body.Tag : null;

        this.pets.Add((id, name, tag));
        Console.WriteLine($"    [Pets] Created pet {id}: {name} ({tag ?? "no tag"})");

        // Return 201 Created with the full Pet (including the generated ID)
        CreatePetResult result = CreatePetResult.Created(
            body: new Pet.Source((ref Pet.Builder b) =>
            {
                if (tag is string t)
                {
                    b.Create(id: id, name: name, tag: t);
                }
                else
                {
                    b.Create(id: id, name: name);
                }
            }),
            workspace: workspace);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Handles GET /pets/{petId} — looks up a pet by ID.
    /// </summary>
    /// <remarks>
    /// The path parameter has already been URI-decoded and parsed into a
    /// typed JsonString. The generated middleware handles 404-style errors
    /// by returning the Default result with an appropriate status code.
    /// </remarks>
    public ValueTask<ShowPetByIdResult> HandleShowPetByIdAsync(
        ShowPetByIdParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        string petId = (string)parameters.PetId;

        if (long.TryParse(petId, out long id))
        {
            (long Id, string Name, string? Tag) found = this.pets.FirstOrDefault(p => p.Id == id);
            if (found != default)
            {
                Console.WriteLine($"    [Pets] Found pet {found.Id}: {found.Name}");

                return ValueTask.FromResult(ShowPetByIdResult.Ok(
                    body: new Pet.Source((ref Pet.Builder b) =>
                    {
                        if (found.Tag is string t)
                        {
                            b.Create(id: found.Id, name: found.Name, tag: t);
                        }
                        else
                        {
                            b.Create(id: found.Id, name: found.Name);
                        }
                    }),
                    workspace: workspace));
            }
        }

        // Return a default error response for "not found"
        Console.WriteLine($"    [Pets] Pet '{petId}' was not found");
        return ValueTask.FromResult(ShowPetByIdResult.Default(
            statusCode: 404,
            body: new Error.Source((ref Error.Builder b) =>
            {
                b.Create(code: 404, message: $"Pet '{petId}' not found");
            }),
            workspace: workspace));
    }
}