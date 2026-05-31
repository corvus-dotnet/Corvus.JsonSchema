// End-to-End: Generated Client → Generated Server
// ================================================
// This recipe demonstrates the full round-trip of a Corvus.Text.Json
// OpenAPI generated client calling a generated server over real HTTP.
//
// The server handles parameter deserialization, schema validation,
// and response serialization — all generated from the spec.
// The client handles request building, query/header/cookie serialization,
// and response parsing — again, all generated.
//
// You just write the business logic (server) and consume typed results (client).

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Petstore.EndToEnd.Client;
using Petstore.EndToEnd.Server;

// ════════════════════════════════════════════════════════════════════
// SERVER SETUP
// ════════════════════════════════════════════════════════════════════
// The generated server code gives us:
//   - Handler interfaces (IApiPetsHandler, IApiAdoptionHandler, etc.)
//   - A MapApiEndpoints extension that wires all routes with full
//     parameter parsing, schema validation, and response serialization
//
// We just implement the handler interfaces with our business logic.

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0"); // Random available port
builder.Logging.ClearProviders(); // Keep console output clean for this demo

WebApplication app = builder.Build();

// Wire up our handler implementations — the generated endpoint registration
// handles all the HTTP plumbing (parsing query params, validating schemas,
// serializing JSON responses, returning proper status codes).
PetstoreHandler handler = new();
app.MapApiEndpoints(handler, handler, handler, handler);

await app.StartAsync();

string serverUrl = app.Urls.First();
Console.WriteLine($"╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Petstore Server running at {serverUrl,-31} ║");
Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// CLIENT SETUP
// ════════════════════════════════════════════════════════════════════
// The generated client takes an IApiTransport — we use HttpClientTransport
// backed by a standard HttpClient pointing at our server.

using HttpClient httpClient = new() { BaseAddress = new Uri(serverUrl) };
await using HttpClientTransport transport = new(httpClient);
await using ApiPetsClient petsClient = new(transport);
await using ApiAdoptionClient adoptionClient = new(transport);
await using ApiChatClient chatClient = new(transport);

// ════════════════════════════════════════════════════════════════════
// SCENARIO 1: Create a pet (with cookie authentication)
// ════════════════════════════════════════════════════════════════════
// The client serializes the NewPet body as JSON and sends the
// session_token cookie. The server validates both the cookie
// presence and the request body schema before calling our handler.

Console.WriteLine("━━━ Scenario 1: Create a pet ━━━");
Console.WriteLine("  → POST /pets  (cookie: session_token=admin-token-123)");
Console.WriteLine("  → Body: {\"name\":\"Luna\",\"status\":\"available\",\"tags\":[\"friendly\",\"vaccinated\"]}");

await using (CreatePetResponse createResponse = await petsClient.CreatePetAsync(
    session_token: "admin-token-123"u8,
    body: new Petstore.EndToEnd.Client.Models.NewPet.Source((ref Petstore.EndToEnd.Client.Models.NewPet.Builder b) =>
        b.Create(
            name: "Luna"u8,
            status: "available"u8,
            tags: new Petstore.EndToEnd.Client.Models.NewPet.JsonStringArray.Source(
                static (ref Petstore.EndToEnd.Client.Models.NewPet.JsonStringArray.Builder ab) =>
                {
                    ab.AddItem("friendly"u8);
                    ab.AddItem("vaccinated"u8);
                })))))
{
    string createdPet = createResponse.MatchResult<string>(
        matchCreated: static (pet) =>
        {
            Console.WriteLine($"  ← 201 Created");
            Console.WriteLine($"     Pet: {pet}");
            return pet.ToString();
        },
        matchUnauthorized: static (error) =>
        {
            Console.WriteLine($"  ← 401 Unauthorized: {error}");
            return string.Empty;
        },
        matchDefault: static (error) =>
        {
            Console.WriteLine($"  ← Error: {error}");
            return string.Empty;
        });
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 2: List pets with filters and required header
// ════════════════════════════════════════════════════════════════════
// The client serializes:
//   - Deep-object query: filter[status]=available
//   - Array query: tags=friendly&tags=vaccinated
//   - Required header: x-request-id
// The server validates all parameters against their schemas and
// returns the x-total-count response header.

Console.WriteLine("━━━ Scenario 2: List pets with filters ━━━");
Console.WriteLine("  → GET /pets?limit=10&tags=friendly&tags=vaccinated&filter[status]=available");
Console.WriteLine("  → Header: x-request-id: req-001");

await using (ListPetsResponse listResponse = await petsClient.ListPetsAsync(
    xRequestId: "req-001"u8,
    limit: 10,
    tags: new Petstore.EndToEnd.Client.Models.GetPetsTags.Source(
        static (ref Petstore.EndToEnd.Client.Models.GetPetsTags.Builder ab) =>
        {
            ab.AddItem("friendly"u8);
            ab.AddItem("vaccinated"u8);
        }),
    filter: new Petstore.EndToEnd.Client.Models.GetPetsFilter.Source(
        static (ref Petstore.EndToEnd.Client.Models.GetPetsFilter.Builder ob) =>
        {
            ob.AddProperty("status"u8, "available"u8);
        })))
{
    listResponse.MatchResult<bool>(
        matchOk: (pets) =>
        {
            Console.WriteLine($"  ← 200 OK");
            Console.WriteLine($"     x-total-count: {listResponse.XTotalCountHeader}");
            Console.WriteLine($"     Pets: {pets}");
            return true;
        },
        matchDefault: static (error) =>
        {
            Console.WriteLine($"  ← Error: {error}");
            return false;
        });
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 3: Get a specific pet by ID
// ════════════════════════════════════════════════════════════════════
// The client substitutes the path parameter. The server validates
// the path param and returns the typed Pet or 404 Error.

Console.WriteLine("━━━ Scenario 3: Show pet by ID ━━━");
Console.WriteLine("  → GET /pets/pet-1");

await using (ShowPetByIdResponse showResponse = await petsClient.ShowPetByIdAsync(
    petId: "pet-1"u8))
{
    showResponse.MatchResult<bool>(
        matchOk: static (pet) =>
        {
            Console.WriteLine($"  ← 200 OK");
            Console.WriteLine($"     Pet: {pet}");
            return true;
        },
        matchNotFound: static (error) =>
        {
            Console.WriteLine($"  ← 404 Not Found: {error}");
            return false;
        },
        matchDefault: static (statusCode) =>
        {
            Console.WriteLine($"  ← Unexpected status: {statusCode}");
            return false;
        });
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 4: Get a pet that doesn't exist (404)
// ════════════════════════════════════════════════════════════════════
// Demonstrates the typed error path through the generated code.

Console.WriteLine("━━━ Scenario 4: Show non-existent pet ━━━");
Console.WriteLine("  → GET /pets/does-not-exist");

await using (ShowPetByIdResponse notFoundResponse = await petsClient.ShowPetByIdAsync(
    petId: "does-not-exist"u8))
{
    notFoundResponse.MatchResult<bool>(
        matchOk: static (pet) =>
        {
            Console.WriteLine($"  ← 200 OK: {pet}");
            return true;
        },
        matchNotFound: static (error) =>
        {
            Console.WriteLine($"  ← 404 Not Found");
            Console.WriteLine($"     Error: {error}");
            return false;
        },
        matchDefault: static (statusCode) =>
        {
            Console.WriteLine($"  ← Unexpected status: {statusCode}");
            return false;
        });
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 5: Submit an adoption application (URL-encoded form)
// ════════════════════════════════════════════════════════════════════
// The client serializes the body as application/x-www-form-urlencoded.
// The server parses form fields back into the typed model and responds
// with a JSON body (202 Accepted).

Console.WriteLine("━━━ Scenario 5: Submit adoption application ━━━");
Console.WriteLine("  → POST /adoption/apply  (application/x-www-form-urlencoded)");
Console.WriteLine("  → Form: applicantName=Jane+Doe&email=jane@example.com&petId=pet-1&housingType=house");

await using (SubmitAdoptionApplicationResponse adoptionResponse = await adoptionClient.SubmitAdoptionApplicationAsync(
    body: new Petstore.EndToEnd.Client.Models.PostAdoptionApplyBody.Source(
        (ref Petstore.EndToEnd.Client.Models.PostAdoptionApplyBody.Builder b) =>
            b.Create(
                applicantName: "Jane Doe"u8,
                email: "jane@example.com"u8,
                housingType: "house"u8,
                petId: "pet-1"u8))))
{
    adoptionResponse.MatchResult<bool>(
        matchAccepted: static (body) =>
        {
            Console.WriteLine($"  ← 202 Accepted");
            Console.WriteLine($"     Response: {body}");
            return true;
        },
        matchDefault: static (error) =>
        {
            Console.WriteLine($"  ← Error: {error}");
            return false;
        });
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 6: Streaming responses — SSE and NDJSON
// ════════════════════════════════════════════════════════════════════
// Demonstrates both streaming media types generated from OpenAPI
// itemSchema responses. The SSE response is read item-by-item by the
// generated client, and the NDJSON response is read line-by-line.

Console.WriteLine("━━━ Scenario 6: Streaming responses (SSE + NDJSON) ━━━");
Console.WriteLine("  → POST /pets/pet-1/chat  (Accept: text/event-stream)");

await using (StartVetChatResponse chatResponse = await chatClient.StartVetChatAsync(
    petId: "pet-1"u8,
    session_token: "admin-token-123"u8,
    body: new Petstore.EndToEnd.Client.Models.PostPetsByPetIdChatBody.Source(
        static (ref Petstore.EndToEnd.Client.Models.PostPetsByPetIdChatBody.Builder b) =>
            b.Create(message: "Bella has been coughing since this morning. What should I do?"u8))))
{
    Console.WriteLine($"  ← {chatResponse.StatusCode} text/event-stream");

    if (chatResponse.StatusCode == 200)
    {
        int chunkNumber = 0;
        await foreach (ParsedJsonDocument<Petstore.EndToEnd.Client.Models.ChatChunk> chunkDocument in chatResponse.EnumerateOkItems())
        {
            using (chunkDocument)
            {
                Petstore.EndToEnd.Client.Models.ChatChunk chunk = chunkDocument.RootElement;
                Console.WriteLine($"     SSE chunk {++chunkNumber}: id={chunk.Id}, delta={chunk.Delta}, done={chunk.Done}");
            }
        }
    }
    else
    {
        chatResponse.MatchResult<bool>(
            matchUnauthorized: static (error) =>
            {
                Console.WriteLine($"     Unauthorized: {error}");
                return false;
            },
            matchDefault: static (statusCode) =>
            {
                Console.WriteLine($"     Unexpected status: {statusCode}");
                return false;
            });
    }
}

Console.WriteLine("  → GET /pets/pet-1/activity  (Accept: application/x-ndjson)");

await using (StreamPetActivityResponse activityResponse = await chatClient.StreamPetActivityAsync(
    petId: "pet-1"u8))
{
    Console.WriteLine($"  ← {activityResponse.StatusCode} application/x-ndjson");

    if (activityResponse.StatusCode == 200)
    {
        int eventNumber = 0;
        await foreach (ParsedJsonDocument<Petstore.EndToEnd.Client.Models.ActivityEvent> activityDocument in activityResponse.EnumerateOkItems())
        {
            using (activityDocument)
            {
                Petstore.EndToEnd.Client.Models.ActivityEvent activity = activityDocument.RootElement;
                Console.WriteLine($"     NDJSON event {++eventNumber}: {activity.EventId} [{activity.Type}] {activity.Description} at {activity.Timestamp}");
            }
        }
    }
    else
    {
        Console.WriteLine($"     Unexpected status: {activityResponse.StatusCode}");
    }
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 7: Validation failure — missing required header
// ════════════════════════════════════════════════════════════════════
// The generated server validates that x-request-id is required.
// Calling without it triggers a 400 with a ProblemDetails body.
// We bypass the client validation to demonstrate server-side enforcement.

Console.WriteLine("━━━ Scenario 7: Server-side validation (missing required header) ━━━");
Console.WriteLine("  → GET /pets  (no x-request-id header)");

// Use raw HttpClient to bypass client-side validation
using (HttpResponseMessage rawResponse = await httpClient.GetAsync("/pets"))
{
    string body = await rawResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"  ← {(int)rawResponse.StatusCode} {rawResponse.ReasonPhrase}");
    Console.WriteLine($"     {body}");
}

Console.WriteLine();

// ════════════════════════════════════════════════════════════════════
// SCENARIO 8: Create without cookie (server rejects)
// ════════════════════════════════════════════════════════════════════
// The server validates that session_token cookie is required.

Console.WriteLine("━━━ Scenario 8: Server-side validation (missing cookie) ━━━");
Console.WriteLine("  → POST /pets  (no session_token cookie)");

using (HttpRequestMessage postRequest = new(HttpMethod.Post, "/pets"))
{
    postRequest.Content = new StringContent(
        """{"name":"Ghost","status":"available"}""",
        System.Text.Encoding.UTF8,
        "application/json");

    using HttpResponseMessage rawPost = await httpClient.SendAsync(postRequest);
    string postBody = await rawPost.Content.ReadAsStringAsync();
    Console.WriteLine($"  ← {(int)rawPost.StatusCode} {rawPost.ReasonPhrase}");
    Console.WriteLine($"     {postBody}");
}

Console.WriteLine();
Console.WriteLine("════════════════════════════════════════════════════════════════");
Console.WriteLine("  All scenarios complete — generated client ↔ server round-trip");
Console.WriteLine("════════════════════════════════════════════════════════════════");

await app.StopAsync();

// ════════════════════════════════════════════════════════════════════
// SERVER HANDLER IMPLEMENTATION
// ════════════════════════════════════════════════════════════════════
// This is the only code you write on the server side. Everything else
// (routing, parameter parsing, validation, serialization) is generated.

/// <summary>
/// A single handler class implementing all four tag interfaces.
/// In a real application these would typically be separate classes
/// registered via DI.
/// </summary>
internal sealed class PetstoreHandler
    : IApiPetsHandler, IApiPhotosHandler, IApiChatHandler, IApiAdoptionHandler
{
    private readonly List<(string Id, string Name, string Status, string[] Tags)> pets =
    [
        ("pet-1", "Buddy", "available", ["friendly", "trained"]),
        ("pet-2", "Max", "adopted", ["energetic"]),
        ("pet-3", "Luna", "available", ["friendly", "vaccinated"]),
    ];

    private int nextId = 4;

    // ─── Pets ───────────────────────────────────────────────────────

    public ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        Console.WriteLine("       [Server] HandleListPetsAsync called");
        Console.WriteLine($"       [Server]   x-request-id = {parameters.XRequestId}");

        // Apply filtering logic
        IEnumerable<(string Id, string Name, string Status, string[] Tags)> filtered = pets;

        // The generated code parses filter[status]=X into a typed GetPetsFilter object
        if (!parameters.Filter.IsUndefined())
        {
            Console.WriteLine($"       [Server]   filter = {parameters.Filter}");
        }

        if (!parameters.Tags.IsUndefined())
        {
            Console.WriteLine($"       [Server]   tags = {parameters.Tags}");
        }

        int total = filtered.Count();

        ListPetsResult result = ListPetsResult.Ok(
            body: new Petstore.EndToEnd.Server.Models.PetList.Source(
                (ref Petstore.EndToEnd.Server.Models.PetList.Builder ab) =>
                {
                    foreach (var p in filtered)
                    {
                        ab.AddItem(new Petstore.EndToEnd.Server.Models.Pet.Source(
                            (ref Petstore.EndToEnd.Server.Models.Pet.Builder pb) =>
                            {
                                pb.Create(
                                    id: long.Parse(p.Id.Replace("pet-", string.Empty)),
                                    name: p.Name.AsSpan(),
                                    status: p.Status.AsSpan(),
                                    tags: new Petstore.EndToEnd.Server.Models.Pet.TagsJsonStArray.Source(
                                        (ref Petstore.EndToEnd.Server.Models.Pet.TagsJsonStArray.Builder tb) =>
                                        {
                                            foreach (string t in p.Tags)
                                            {
                                                tb.AddItem(t.AsSpan());
                                            }
                                        }));
                            }));
                    }
                }),
            workspace: workspace,
            xTotalCount: Petstore.EndToEnd.Server.Models.JsonInteger.ParseValue(total.ToString()));

        Console.WriteLine($"       [Server]   → returning {total} pets");
        return new(result);
    }

    public ValueTask<CreatePetResult> HandleCreatePetAsync(
        CreatePetParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        Console.WriteLine("       [Server] HandleCreatePetAsync called");
        Console.WriteLine($"       [Server]   session_token = {parameters.SessionToken}");
        Console.WriteLine($"       [Server]   body = {parameters.Body}");

        // Extract values from the typed body
        string name = parameters.Body.Name.ToString();
        string status = parameters.Body.Status.ToString();

        string id = $"pet-{nextId++}";
        pets.Add((id, name, status, []));

        CreatePetResult result = CreatePetResult.Created(
            body: new Petstore.EndToEnd.Server.Models.Pet.Source(
                (ref Petstore.EndToEnd.Server.Models.Pet.Builder pb) =>
                {
                    pb.Create(
                        id: long.Parse(id.Replace("pet-", string.Empty)),
                        name: name.AsSpan(),
                        status: status.AsSpan());
                }),
            workspace: workspace);

        Console.WriteLine($"       [Server]   → Created pet {id}");
        return new(result);
    }

    public ValueTask<GetPetsBatchResult> HandleGetPetsBatchAsync(
        GetPetsBatchParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        Console.WriteLine("       [Server] HandleGetPetsBatchAsync called");
        return new(GetPetsBatchResult.Ok(
            body: new Petstore.EndToEnd.Server.Models.PetList.Source(
                static (ref Petstore.EndToEnd.Server.Models.PetList.Builder ab) => { }),
            workspace: workspace));
    }

    public ValueTask<ShowPetByIdResult> HandleShowPetByIdAsync(
        ShowPetByIdParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        string petId = parameters.PetId.ToString().Trim('"');
        Console.WriteLine($"       [Server] HandleShowPetByIdAsync called (petId={petId})");

        var found = pets.FirstOrDefault(p => p.Id == petId);
        if (found == default)
        {
            Console.WriteLine("       [Server]   → Not found");
            return new(ShowPetByIdResult.NotFound(
                body: new Petstore.EndToEnd.Server.Models.Error.Source(
                    static (ref Petstore.EndToEnd.Server.Models.Error.Builder eb) =>
                        eb.Create(code: 404, message: "Pet not found"u8)),
                workspace: workspace));
        }

        Console.WriteLine($"       [Server]   → Found: {found.Name}");
        return new(ShowPetByIdResult.Ok(
            body: new Petstore.EndToEnd.Server.Models.Pet.Source(
                (ref Petstore.EndToEnd.Server.Models.Pet.Builder pb) =>
                {
                    pb.Create(
                        id: long.Parse(found.Id.Replace("pet-", string.Empty)),
                        name: found.Name.AsSpan(),
                        status: found.Status.AsSpan(),
                        tags: new Petstore.EndToEnd.Server.Models.Pet.TagsJsonStArray.Source(
                            (ref Petstore.EndToEnd.Server.Models.Pet.TagsJsonStArray.Builder tb) =>
                            {
                                foreach (string t in found.Tags)
                                {
                                    tb.AddItem(t.AsSpan());
                                }
                            }));
                }),
            workspace: workspace));
    }

    // ─── Photos (stub — streaming not demonstrated in E2E) ──────────

    public ValueTask<UploadPetPhotoResult> HandleUploadPetPhotoAsync(
        UploadPetPhotoParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        return new(UploadPetPhotoResult.Created(
            body: new Petstore.EndToEnd.Server.Models.PhotoMetadata.Source(
                static (ref Petstore.EndToEnd.Server.Models.PhotoMetadata.Builder pb) =>
                    pb.Create(petId: "pet-1"u8, photoId: "photo-001"u8, uploadedAt: "2024-01-15T10:30:00Z"u8)),
            workspace: workspace));
    }

    public ValueTask<DownloadPhotoResult> HandleDownloadPhotoAsync(
        DownloadPhotoParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        return new(DownloadPhotoResult.NotFound(
            body: new Petstore.EndToEnd.Server.Models.Error.Source(
                static (ref Petstore.EndToEnd.Server.Models.Error.Builder eb) =>
                    eb.Create(code: 404, message: "Photo not found"u8)),
            workspace: workspace));
    }

    // ─── Chat (stub) ────────────────────────────────────────────────

    public ValueTask<StartVetChatResult> HandleStartVetChatAsync(
        StartVetChatParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        Console.WriteLine("       [Server] HandleStartVetChatAsync called");
        Console.WriteLine($"       [Server]   petId = {parameters.PetId}");
        Console.WriteLine($"       [Server]   session_token = {parameters.SessionToken}");
        Console.WriteLine($"       [Server]   body = {parameters.Body}");

        return new(StartVetChatResult.Ok(static async (stream, cancellationToken) =>
        {
            Console.WriteLine("       [Server]   → streaming 2 SSE chat chunks");

            Petstore.EndToEnd.Server.Models.ChatChunk greeting = Petstore.EndToEnd.Server.Models.ChatChunk.ParseValue("""{"id":"chunk-1","delta":"Thanks for the update about Bella. ","done":false}"""u8);
            await stream.AppendChatChunk(greeting, cancellationToken).ConfigureAwait(false);

            Petstore.EndToEnd.Server.Models.ChatChunk advice = Petstore.EndToEnd.Server.Models.ChatChunk.ParseValue("""{"id":"chunk-2","delta":"Please keep her calm and call the clinic if breathing becomes laboured.","done":true}"""u8);
            await stream.AppendChatChunk(advice, cancellationToken).ConfigureAwait(false);
        }));
    }

    public ValueTask<StreamPetActivityResult> HandleStreamPetActivityAsync(
        StreamPetActivityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        Console.WriteLine("       [Server] HandleStreamPetActivityAsync called");
        Console.WriteLine($"       [Server]   petId = {parameters.PetId}");

        return new(StreamPetActivityResult.Ok(static async (stream, cancellationToken) =>
        {
            Console.WriteLine("       [Server]   → streaming 2 NDJSON activity events");

            Petstore.EndToEnd.Server.Models.ActivityEvent checkIn = Petstore.EndToEnd.Server.Models.ActivityEvent.ParseValue("""{"eventId":"evt-1","timestamp":"2026-05-30T18:00:00Z","type":"check-in","description":"Bella checked in at reception"}"""u8);
            await stream.AppendActivityEvent(checkIn, cancellationToken).ConfigureAwait(false);

            Petstore.EndToEnd.Server.Models.ActivityEvent exam = Petstore.EndToEnd.Server.Models.ActivityEvent.ParseValue("""{"eventId":"evt-2","timestamp":"2026-05-30T18:05:00Z","type":"exam","description":"Initial examination started"}"""u8);
            await stream.AppendActivityEvent(exam, cancellationToken).ConfigureAwait(false);
        }));
    }

    // ─── Adoption ───────────────────────────────────────────────────

    public ValueTask<SubmitAdoptionApplicationResult> HandleSubmitAdoptionApplicationAsync(
        SubmitAdoptionApplicationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken)
    {
        Console.WriteLine("       [Server] HandleSubmitAdoptionApplicationAsync called");
        Console.WriteLine($"       [Server]   body = {parameters.Body}");

        SubmitAdoptionApplicationResult result = SubmitAdoptionApplicationResult.Accepted(
            body: new Petstore.EndToEnd.Server.Models.PostAdoptionApplyAccepted.Source(
                static (ref Petstore.EndToEnd.Server.Models.PostAdoptionApplyAccepted.Builder rb) =>
                    rb.Create(
                        applicationId: "app-42"u8,
                        status: "received"u8,
                        estimatedReviewDays: 5)),
            workspace: workspace);

        Console.WriteLine("       [Server]   → Accepted (app-42)");
        return new(result);
    }
}