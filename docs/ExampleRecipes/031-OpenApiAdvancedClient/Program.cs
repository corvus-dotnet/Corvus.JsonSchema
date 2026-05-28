// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Petstore.Extended;

// ══════════════════════════════════════════════════════════════════════════════
// Petstore Extended — Advanced OpenAPI Client Features
// ══════════════════════════════════════════════════════════════════════════════
//
// This example extends the basic Petstore to demonstrate:
//   1. Deep-object query filters and array query parameters
//   2. Path array parameters (batch operations)
//   3. Cookie-based authentication
//   4. Header parameters (correlation IDs, array headers)
//   5. SSE streaming responses (vet chat)
//   6. NDJSON streaming (activity feed)
//   7. Binary file upload and download
//   8. Multipart form-data with file attachments
//   9. URL-encoded form submissions

HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v2") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);

// The generator creates separate client classes per tag group.
// Each client shares the same transport.
ApiPetsClient petsClient = new(transport);
ApiPhotosClient photosClient = new(transport);
ApiChatClient chatClient = new(transport);
ApiAdoptionClient adoptionClient = new(transport);

// ── 1. Advanced filtering with deep-object and array query params ────────────
// The spec declares:
//   - tags: query array, style=form, explode=true → ?tags=dog&tags=friendly
//   - filter: query object, style=deepObject → ?filter[status]=available&filter[breed]=labrador
//   - x-request-id: required header for distributed tracing
//
// The generated code serializes the deep-object filter into bracket notation,
// explodes the tags array into repeated keys, and writes the header — all from
// a single method call with typed builders.
Console.WriteLine("1. Searching pets with filters...");
await using ListPetsResponse listResponse = await petsClient.ListPetsAsync(
    xRequestId: "req-abc-123"u8,
    limit: 10,
    tags: new GetPetsTags.Source((ref GetPetsTags.Builder b) =>
    {
        // Array params use AddItem — one per value
        b.AddItem("dog"u8);
        b.AddItem("friendly"u8);
    }),
    filter: new GetPetsFilter.Source((ref GetPetsFilter.Builder b) =>
    {
        // Deep-object params mirror the schema properties
        b.Create(status: "available"u8, breed: "labrador"u8, minAge: 1, maxAge: 5);
    }));

// Wire format: GET /pets?tags=dog&tags=friendly&filter[status]=available&filter[breed]=labrador&filter[minAge]=1&filter[maxAge]=5&limit=10
// Header: x-request-id: req-abc-123

listResponse.MatchResult(
    matchOk: pets =>
    {
        Console.WriteLine($"   Found {pets.GetArrayLength()} pets");
        foreach (Pet pet in pets.EnumerateArray())
        {
            Console.WriteLine($"   - [{pet.Id}] {pet.Name} ({pet.Breed}) — {pet.Status}");
        }

        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error {error.Code}: {error.Message}");
        return 0;
    });

// Response headers are typed — x-total-count gives the full result size
JsonInteger totalCount = listResponse.XTotalCountHeader;
if (totalCount.IsNotUndefined())
{
    Console.WriteLine($"   Total matching: {totalCount}");
}

Console.WriteLine();

// ── 2. Path array parameter — batch fetch ────────────────────────────────────
// The path parameter is declared as style=simple, schema=array[int64].
// The generated code serializes the array into the path: /pets/batch/1,2,3
// using comma-separated (simple style) encoding.
Console.WriteLine("2. Batch fetching pets by IDs...");
await using GetPetsBatchResponse batchResponse = await petsClient.GetPetsBatchAsync(
    ids: new GetPetsBatchByIdsIds.Source((ref GetPetsBatchByIdsIds.Builder b) =>
    {
        b.AddItem(1L);
        b.AddItem(2L);
        b.AddItem(3L);
    }));

// Wire format: GET /pets/batch/1,2,3

batchResponse.MatchResult(
    matchOk: pets =>
    {
        Console.WriteLine($"   Fetched {pets.GetArrayLength()} pets in one call");
        return 0;
    },
    matchDefault: statusCode =>
    {
        Console.WriteLine($"   Unexpected status: {statusCode}");
        return 0;
    });

Console.WriteLine();

// ── 3. Cookie authentication ─────────────────────────────────────────────────
// The session_token cookie is declared as a required cookie parameter.
// The generated code writes it into the Cookie header automatically.
// The consumer just passes the token as a typed value.
Console.WriteLine("3. Creating a pet (with session cookie)...");
await using CreatePetResponse createResponse = await petsClient.CreatePetAsync(
    session_token: "sess_k7j2m9x4"u8,
    body: new NewPet.Source((ref NewPet.Builder b) =>
    {
        b.Create(
            name: "Bella"u8,
            status: "available"u8,
            breed: "golden retriever"u8,
            age: 2,
            tags: new NewPet.JsonStringArray.Source((ref NewPet.JsonStringArray.Builder tb) =>
            {
                tb.AddItem("dog"u8);
                tb.AddItem("friendly"u8);
                tb.AddItem("trained"u8);
            }));
    }));

// Wire format: POST /pets, Cookie: session_token=sess_k7j2m9x4
// Body: {"name":"Bella","status":"available","breed":"golden retriever",...}

createResponse.MatchResult(
    matchCreated: pet =>
    {
        Console.WriteLine($"   Created: [{pet.Id}] {pet.Name}");
        return 0;
    },
    matchUnauthorized: error =>
    {
        Console.WriteLine($"   Auth failed: {error.Message}");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error {error.Code}: {error.Message}");
        return 0;
    });

Console.WriteLine();

// ── 4. Multipart file upload (photo with metadata) ───────────────────────────
// The spec declares multipart/form-data with a binary "file" part plus metadata.
// The generated code produces two parameters:
//   - body: typed metadata Source (caption, isPrimary)
//   - file: BinaryPartData record struct (WriteContentAsync func + content type + filename)
//
// Behind the scenes, the generated code builds the multipart boundary, writes
// each field as a form part, and streams the binary content without buffering.
Console.WriteLine("4. Uploading a pet photo...");
byte[] photoBytes = [0x89, 0x50, 0x4E, 0x47]; // PNG header stub

await using UploadPetPhotoResponse uploadResponse = await photosClient.UploadPetPhotoAsync(
    petId: "pet-42"u8,
    session_token: "sess_k7j2m9x4"u8,
    body: new PostPetsByPetIdPhotosBody.Source((ref PostPetsByPetIdPhotosBody.Builder b) =>
    {
        b.Create(
            file: default, // binary part handled separately
            caption: "Bella at the park"u8,
            isPrimary: true);
    }),
    file: new BinaryPartData(
        WriteContentAsync: (stream, ct) => { stream.Write(photoBytes); return default; },
        ContentType: "image/png",
        FileName: "bella-park.png"));

// Wire format: POST /pets/pet-42/photos
// Content-Type: multipart/form-data; boundary=...
// --boundary
// Content-Disposition: form-data; name="caption"
// Bella at the park
// --boundary
// Content-Disposition: form-data; name="isPrimary"
// true
// --boundary
// Content-Disposition: form-data; name="file"; filename="bella-park.png"
// Content-Type: image/png
// <binary data>
// --boundary--

uploadResponse.MatchResult(
    matchCreated: meta =>
    {
        Console.WriteLine($"   Uploaded: {meta.PhotoId} (primary={meta.IsPrimary})");
        return 0;
    },
    matchUnauthorized: error =>
    {
        Console.WriteLine($"   Auth failed: {error.Message}");
        return 0;
    },
    matchDefault: statusCode =>
    {
        Console.WriteLine($"   Unexpected status: {statusCode}");
        return 0;
    });

Console.WriteLine();

// ── 5. Binary file download ──────────────────────────────────────────────────
// When the response content type is application/octet-stream, the generated
// response exposes the raw Stream directly — no JSON parsing overhead.
// MatchResult<ValueTask> distinguishes between a successful stream and error
// responses while allowing async I/O on the stream.
Console.WriteLine("5. Downloading a pet photo...");
await using DownloadPhotoResponse downloadResponse = await photosClient.DownloadPhotoAsync(
    photoId: "photo-001"u8);

await downloadResponse.MatchResult<ValueTask>(
    matchOkStream: async stream =>
    {
        if (stream is not null)
        {
            Console.WriteLine($"   Got photo stream (readable={stream.CanRead})");

            // In real code you'd use CopyToAsync:
            // await stream.CopyToAsync(fileStream);
        }
    },
    matchNotFound: error =>
    {
        Console.WriteLine($"   Not found: {error.Message}");
        return default;
    },
    matchDefault: statusCode =>
    {
        Console.WriteLine($"   Unexpected status: {statusCode}");
        return default;
    });

Console.WriteLine();

// ── 6. SSE streaming — vet chat ──────────────────────────────────────────────
// The spec declares text/event-stream with an itemSchema. The generated code
// exposes IAsyncEnumerable<ParsedJsonDocument<ChatChunk>> for streaming
// consumption. Each chunk arrives as a strongly-typed, pooled document — you
// process it and dispose automatically via await foreach.
Console.WriteLine("6. Starting vet chat (SSE streaming)...");
await using StartVetChatResponse chatResponse = await chatClient.StartVetChatAsync(
    petId: "pet-42"u8,
    session_token: "sess_k7j2m9x4"u8,
    body: new PostPetsByPetIdChatBody.Source((ref PostPetsByPetIdChatBody.Builder b) =>
    {
        b.Create(message: "My dog hasn't been eating well for two days. Should I be worried?"u8);
    }));

// EnumerateOkItems returns IAsyncEnumerable<ParsedJsonDocument<ChatChunk>>
// Each document is individually pooled — process and dispose as you go.
if (chatResponse.StatusCode == 200)
{
    Console.Write("   Vet: ");
    await foreach (ParsedJsonDocument<ChatChunk> chunk in chatResponse.EnumerateOkItems())
    {
        using (chunk)
        {
            ChatChunk c = chunk.RootElement;
            if (c.Delta.IsNotUndefined())
            {
                Console.Write(c.Delta);
            }

            if (c.Done.IsNotUndefined() && (bool)c.Done)
            {
                break;
            }
        }
    }

    Console.WriteLine();
}

Console.WriteLine();

// ── 7. NDJSON streaming — activity feed ──────────────────────────────────────
// application/x-ndjson streams newline-delimited JSON objects. The generated
// code provides the same IAsyncEnumerable interface as SSE, but each item is
// parsed from a single line. No SSE envelope — just raw typed objects.
Console.WriteLine("7. Streaming pet activity (NDJSON)...");
await using StreamPetActivityResponse activityResponse = await chatClient.StreamPetActivityAsync(
    petId: "pet-42"u8);

int eventCount = 0;
await foreach (ParsedJsonDocument<ActivityEvent> eventDoc in activityResponse.EnumerateOkItems())
{
    using (eventDoc)
    {
        ActivityEvent evt = eventDoc.RootElement;
        Console.WriteLine($"   [{evt.Type}] {evt.Description} @ {evt.Timestamp}");

        if (++eventCount >= 3)
        {
            break; // Stop after 3 events for demo purposes
        }
    }
}

Console.WriteLine();

// ── 8. URL-encoded form submission (adoption application) ────────────────────
// The spec declares application/x-www-form-urlencoded. The generated code
// serializes the typed body as key=value&key=value with proper URL-encoding.
// No manual string concatenation — the builder mirrors the schema properties.
Console.WriteLine("8. Submitting adoption application (form-encoded)...");
await using SubmitAdoptionApplicationResponse adoptionResponse =
    await adoptionClient.SubmitAdoptionApplicationAsync(
        body: new PostAdoptionApplyBody.Source((ref PostAdoptionApplyBody.Builder b) =>
        {
            b.Create(
                applicantName: "Jane Smith"u8,
                email: "jane@example.com"u8,
                housingType: "house"u8,
                petId: "pet-42"u8,
                hasGarden: true,
                experience: "Had dogs for 10 years"u8,
                phone: "+44 7700 900000"u8);
        }));

// Wire format: POST /adoption/apply
// Content-Type: application/x-www-form-urlencoded
// applicantName=Jane+Smith&email=jane%40example.com&housingType=house&petId=pet-42&hasGarden=true&experience=Had+dogs+for+10+years&phone=%2B44+7700+900000

adoptionResponse.MatchResult(
    matchAccepted: result =>
    {
        Console.WriteLine($"   Application {result.ApplicationId}: {result.Status}");
        if (result.EstimatedReviewDays.IsNotUndefined())
        {
            Console.WriteLine($"   Estimated review: {result.EstimatedReviewDays} days");
        }

        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error: {error.Message}");
        return 0;
    });

Console.WriteLine();
Console.WriteLine("Done!");