// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Petstore.Extended.Server;
using Petstore.Extended.Server.Models;

// ══════════════════════════════════════════════════════════════════════════════
// Petstore Extended — Advanced OpenAPI Server Implementation
// ══════════════════════════════════════════════════════════════════════════════
//
// This example implements the server side of the extended Petstore API, showing
// how to handle:
//   - Deep-object/array query parameters (already deserialized into typed params)
//   - Cookie-based authentication (session token validation)
//   - Streaming responses (SSE and NDJSON via generated push writers)
//   - File upload/download (multipart form body, binary streams)
//   - Form-encoded request bodies (typed property access)
//   - Response header emission (pagination headers)

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    string baseUrl = app.Urls.FirstOrDefault(static url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        ?? app.Urls.FirstOrDefault()
        ?? "http://localhost:50516";

    Console.WriteLine();
    Console.WriteLine("OpenAPI Advanced Server example is running.");
    Console.WriteLine($"Base URL: {baseUrl}");
    Console.WriteLine();
    Console.WriteLine("Try these requests from another terminal:");
    Console.WriteLine("  # --globoff stops curl from treating filter[status] as URL range/glob syntax.");
    Console.WriteLine($"  curl --globoff \"{baseUrl}/pets?limit=2&filter[status]=available&tags=dog\" -H \"x-request-id: demo-request-1\"");
    Console.WriteLine($"  curl -N \"{baseUrl}/pets/1/activity\" -H \"Accept: application/x-ndjson\"");
    Console.WriteLine($"  curl -N -X POST \"{baseUrl}/pets/1/chat\" -H \"Accept: text/event-stream\" -H \"Content-Type: application/json\" -H \"Cookie: session_token=sess_admin-token-123\" -d '{{\"message\":\"Bella is coughing. What should I do?\"}}'");
    Console.WriteLine();
});

app.Use(async (context, next) =>
{
    Console.WriteLine($"--> {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
    await next(context);
    Console.WriteLine($"<-- {context.Response.StatusCode} {context.Response.ContentType ?? "(no content type)"}");
});

// The generated ApiEndpointRegistration wires all routes to your handler implementations.
// Each handler interface covers one tag group from the spec.
PetsHandler petsHandler = new();
PhotosHandler photosHandler = new();
ChatHandler chatHandler = new();
AdoptionHandler adoptionHandler = new();

app.MapApiEndpoints(petsHandler, photosHandler, chatHandler, adoptionHandler);

app.Run();

// ═══════════════════════════════════════════════════════════════════════════════
// Handler implementations
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Implements pet listing, creation, and batch fetch operations.
/// The generated infrastructure deserializes all parameters (deep-object filters,
/// array queries, cookies, headers) before calling your handler — you just read
/// typed properties from the params struct.
/// </summary>
internal sealed class PetsHandler : IApiPetsHandler
{
    // In-memory store for demonstration
    private readonly List<PetRecord> pets =
    [
        new(1, "Luna", "available", "siamese", 3, ["cat", "indoor"]),
        new(2, "Max", "available", "labrador", 2, ["dog", "friendly"]),
        new(3, "Bella", "adopted", "golden retriever", 4, ["dog", "trained"]),
        new(4, "Charlie", "available", "tabby", 1, ["cat", "playful"]),
        new(5, "Rocky", "available", "bulldog", 5, ["dog", "guard"]),
    ];

    private long nextId = 6;

    public ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // The generated code has already deserialized deep-object filter params,
        // exploded array query params, and header values into typed properties.
        // You just read them — no manual parsing needed.
        IEnumerable<PetRecord> query = this.pets;

        // Apply deep-object filter (filter[status], filter[breed], filter[minAge], filter[maxAge])
        if (parameters.Filter.IsNotUndefined())
        {
            GetPetsFilter filter = parameters.Filter;

            if (filter.Status.IsNotUndefined())
            {
                string status = (string)filter.Status;
                query = query.Where(p => p.Status == status);
            }

            if (filter.Breed.IsNotUndefined())
            {
                string breed = (string)filter.Breed;
                query = query.Where(p => string.Equals(p.Breed, breed, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.MinAge.IsNotUndefined())
            {
                int min = (int)filter.MinAge;
                query = query.Where(p => p.Age >= min);
            }

            if (filter.MaxAge.IsNotUndefined())
            {
                int max = (int)filter.MaxAge;
                query = query.Where(p => p.Age <= max);
            }
        }

        // Apply tags array filter (tags=dog&tags=friendly)
        if (parameters.Tags.IsNotUndefined())
        {
            List<string> requestedTags = [];
            foreach (JsonString tag in parameters.Tags.EnumerateArray())
            {
                requestedTags.Add((string)tag);
            }

            query = query.Where(p => requestedTags.All(t => p.Tags.Contains(t)));
        }

        List<PetRecord> results = query.ToList();
        int totalCount = results.Count;

        // Apply limit
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 20;
        results = results.Take(limit).ToList();

        // Build the typed response with response headers.
        // The generated Result factory handles serialization and header emission.
        return ValueTask.FromResult(ListPetsResult.Ok(
            body: new PetList.Source((ref PetList.Builder b) =>
            {
                foreach (PetRecord p in results)
                {
                    b.AddItem(Pet.Build(
                        id: p.Id,
                        name: p.Name,
                        status: p.Status,
                        breed: p.Breed,
                        age: p.Age));
                }
            }),
            workspace: workspace,
            xTotalCount: totalCount,
            xNext: results.Count < totalCount
                ? (JsonString.Source)$"\"/pets?offset={limit}\""
                : default));
    }

    public ValueTask<CreatePetResult> HandleCreatePetAsync(
        CreatePetParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // The cookie parameter is already extracted and validated.
        // Check it for authentication.
        if (parameters.SessionToken.IsUndefined() || !IsValidSession((string)parameters.SessionToken))
        {
            return ValueTask.FromResult(CreatePetResult.Unauthorized(
                Error.Build(code: 401, message: "Invalid or expired session token"u8),
                workspace));
        }

        // The request body is already deserialized into a typed NewPet value.
        // Read properties directly — no JsonDocument parsing needed.
        NewPet body = parameters.Body;
        long id = Interlocked.Increment(ref this.nextId);

        string name = (string)body.Name;
        string status = (string)body.Status;
        string breed = body.Breed.IsNotUndefined() ? (string)body.Breed : "unknown";
        int age = body.Age.IsNotUndefined() ? (int)body.Age : 0;

        this.pets.Add(new PetRecord(id, name, status, breed, age, []));

        return ValueTask.FromResult(CreatePetResult.Created(
            Pet.Build(
                id: id,
                name: name,
                status: status,
                breed: breed,
                age: age),
            workspace));
    }

    public ValueTask<GetPetsBatchResult> HandleGetPetsBatchAsync(
        GetPetsBatchParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Path array parameter — already deserialized from /pets/batch/1,2,3
        // into a typed array. Enumerate to get individual IDs.
        List<long> requestedIds = [];
        foreach (JsonInt64 id in parameters.Ids.EnumerateArray())
        {
            requestedIds.Add((long)id);
        }

        List<PetRecord> found = this.pets.Where(p => requestedIds.Contains(p.Id)).ToList();

        return ValueTask.FromResult(GetPetsBatchResult.Ok(
            body: new PetList.Source((ref PetList.Builder b) =>
            {
                foreach (PetRecord p in found)
                {
                    b.AddItem(Pet.Build(id: p.Id, name: p.Name, status: p.Status));
                }
            }),
            workspace: workspace));
    }

    public ValueTask<ShowPetByIdResult> HandleShowPetByIdAsync(
        ShowPetByIdParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        string petId = (string)parameters.PetId;

        if (long.TryParse(petId, out long id))
        {
            PetRecord? found = this.pets.FirstOrDefault(p => p.Id == id);
            if (found is not null)
            {
                return ValueTask.FromResult(ShowPetByIdResult.Ok(
                    Pet.Build(
                        id: found.Id,
                        name: found.Name,
                        status: found.Status,
                        breed: found.Breed,
                        age: found.Age),
                    workspace));
            }
        }

        return ValueTask.FromResult(ShowPetByIdResult.NotFound(
            Error.Build(code: 404, message: "Pet not found"u8),
            workspace));
    }

    private static bool IsValidSession(string token) => token.StartsWith("sess_");
}

/// <summary>
/// Implements photo upload and download.
/// Demonstrates multipart form handling (typed metadata + binary file) and
/// binary stream responses.
/// </summary>
internal sealed class PhotosHandler : IApiPhotosHandler
{
    private readonly Dictionary<string, PhotoRecord> photos = new()
    {
        ["photo-001"] = new("photo-001", "pet-42", "image/png", [0x89, 0x50, 0x4E, 0x47]),
    };

    private int photoCounter = 1;

    public ValueTask<UploadPetPhotoResult> HandleUploadPetPhotoAsync(
        UploadPetPhotoParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Cookie auth check
        if (parameters.SessionToken.IsUndefined() || !((string)parameters.SessionToken).StartsWith("sess_"))
        {
            return ValueTask.FromResult(UploadPetPhotoResult.Unauthorized(
                Error.Build(code: 401, message: "Authentication required"u8),
                workspace));
        }

        // The multipart body is parsed: metadata fields are typed properties,
        // the binary file content was consumed by the infrastructure.
        string petId = (string)parameters.PetId;
        string photoId = $"photo-{Interlocked.Increment(ref this.photoCounter):D3}";
        string caption = parameters.Body.Caption.IsNotUndefined()
            ? (string)parameters.Body.Caption
            : "No caption";
        bool isPrimary = parameters.Body.IsPrimary.IsNotUndefined()
            && (bool)parameters.Body.IsPrimary;

        this.photos[photoId] = new(photoId, petId, "image/png", []);

        return ValueTask.FromResult(UploadPetPhotoResult.Created(
            PhotoMetadata.Build(
                petId: petId,
                photoId: photoId,
                uploadedAt: DateTime.UtcNow.ToString("O"),
                caption: caption,
                isPrimary: isPrimary),
            workspace));
    }

    public ValueTask<DownloadPhotoResult> HandleDownloadPhotoAsync(
        DownloadPhotoParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        string photoId = (string)parameters.PhotoId;

        if (!this.photos.TryGetValue(photoId, out PhotoRecord? photo))
        {
            return ValueTask.FromResult(DownloadPhotoResult.NotFound(
                Error.Build(code: 404, message: "Photo not found"u8),
                workspace));
        }

        // For binary responses, the generated infrastructure streams the data
        // to the client. Here we return Ok() — the actual streaming is handled
        // by the endpoint registration middleware.
        _ = photo; // In production, you'd stream photo.Data
        return ValueTask.FromResult(DownloadPhotoResult.Ok());
    }
}

/// <summary>
/// Implements SSE streaming (vet chat) and NDJSON streaming (activity feed).
/// Streaming operations return a result with a writer callback. The generated
/// endpoint registration invokes the callback while the response body is open.
/// </summary>
internal sealed class ChatHandler : IApiChatHandler
{
    public ValueTask<StartVetChatResult> HandleStartVetChatAsync(
        StartVetChatParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Validate session cookie
        if (parameters.SessionToken.IsUndefined() || !((string)parameters.SessionToken).StartsWith("sess_"))
        {
            return ValueTask.FromResult(StartVetChatResult.Unauthorized(
                Error.Build(code: 401, message: "Authentication required for chat"u8),
                workspace));
        }

        // Read the typed chat body — message and optional history are properties.
        string message = (string)parameters.Body.Message;
        Console.WriteLine($"[Chat] Pet {parameters.PetId}: {message}");

        return new(StartVetChatResult.Ok(static async (stream, cancellationToken) =>
        {
            ChatChunk greeting = ChatChunk.ParseValue("""{"id":"chunk-1","delta":"Hello! ","done":false}"""u8);
            await stream.AppendChatChunk(greeting, cancellationToken).ConfigureAwait(false);

            ChatChunk advice = ChatChunk.ParseValue("""{"id":"chunk-2","delta":"A vet will review Bella's symptoms and respond shortly.","done":true}"""u8);
            await stream.AppendChatChunk(advice, cancellationToken).ConfigureAwait(false);
        }));
    }

    public ValueTask<StreamPetActivityResult> HandleStreamPetActivityAsync(
        StreamPetActivityParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Activity] Starting stream for pet {parameters.PetId}");

        return new(StreamPetActivityResult.Ok(static async (stream, cancellationToken) =>
        {
            ActivityEvent checkIn = ActivityEvent.ParseValue("""{"eventId":"evt-1","timestamp":"2026-05-30T18:00:00Z","type":"check-in","description":"Bella checked in at reception"}"""u8);
            await stream.AppendActivityEvent(checkIn, cancellationToken).ConfigureAwait(false);

            ActivityEvent exam = ActivityEvent.ParseValue("""{"eventId":"evt-2","timestamp":"2026-05-30T18:05:00Z","type":"exam","description":"Initial examination started"}"""u8);
            await stream.AppendActivityEvent(exam, cancellationToken).ConfigureAwait(false);

            ActivityEvent complete = ActivityEvent.ParseValue("""{"eventId":"evt-3","timestamp":"2026-05-30T18:20:00Z","type":"complete","description":"Examination completed"}"""u8);
            await stream.AppendActivityEvent(complete, cancellationToken).ConfigureAwait(false);
        }));
    }
}

/// <summary>
/// Implements URL-encoded form handling for the adoption application.
/// The generated infrastructure parses application/x-www-form-urlencoded bodies
/// into typed structs — you read properties, not raw form strings.
/// </summary>
internal sealed class AdoptionHandler : IApiAdoptionHandler
{
    private int applicationCounter;

    public ValueTask<SubmitAdoptionApplicationResult> HandleSubmitAdoptionApplicationAsync(
        SubmitAdoptionApplicationParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // The form body is already deserialized into typed properties.
        // No manual form parsing or URL decoding needed.
        PostAdoptionApplyBody body = parameters.Body;

        string applicantName = (string)body.ApplicantName;
        string email = (string)body.Email;
        string petId = (string)body.PetId;
        string housingType = (string)body.HousingType;

        Console.WriteLine($"[Adoption] {applicantName} ({email}) applying for {petId} — housing: {housingType}");

        string appId = $"APP-{Interlocked.Increment(ref this.applicationCounter):D4}";

        return ValueTask.FromResult(SubmitAdoptionApplicationResult.Accepted(
            PostAdoptionApplyAccepted.Build(
                applicationId: appId,
                status: "pending_review"u8,
                estimatedReviewDays: 5),
            workspace));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Simple in-memory records for demonstration
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record PetRecord(long Id, string Name, string Status, string Breed, int Age, List<string> Tags);

internal sealed record PhotoRecord(string PhotoId, string PetId, string ContentType, byte[] Data);