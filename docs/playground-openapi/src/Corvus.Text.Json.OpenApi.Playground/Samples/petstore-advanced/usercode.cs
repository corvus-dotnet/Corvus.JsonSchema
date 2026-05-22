// Petstore Extended — from ExampleRecipes/031-OpenApiAdvancedClient
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Playground;

HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v2") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);

ApiPetsClient petsClient = new(transport);
ApiPhotosClient photosClient = new(transport);
ApiAdoptionClient adoptionClient = new(transport);

// ── 1. Advanced filtering with deep-object and array query params ────────────
Console.WriteLine("1. Searching pets with filters...");
await using ListPetsResponse listResponse = await petsClient.ListPetsAsync(
    xRequestId: "req-abc-123"u8,
    limit: 10,
    tags: new GetPetsTags.Source((ref GetPetsTags.Builder b) =>
    {
        b.AddItem("dog"u8);
        b.AddItem("friendly"u8);
    }),
    filter: new GetPetsFilter.Source((ref GetPetsFilter.Builder b) =>
    {
        b.Create(status: "available"u8, breed: "labrador"u8, minAge: 1, maxAge: 5);
    }));

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

Console.WriteLine();

// ── 2. Path array parameter — batch fetch ────────────────────────────────────
Console.WriteLine("2. Batch fetching pets by IDs...");
await using GetPetsBatchResponse batchResponse = await petsClient.GetPetsBatchAsync(
    ids: new GetPetsBatchByIdsIds.Source((ref GetPetsBatchByIdsIds.Builder b) =>
    {
        b.AddItem(1L);
        b.AddItem(2L);
        b.AddItem(3L);
    }));

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

// ── 3. Cookie authentication + pet creation ──────────────────────────────────
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

// ── 4. Multipart file upload ─────────────────────────────────────────────────
Console.WriteLine("4. Uploading a pet photo...");
byte[] photoBytes = [0x89, 0x50, 0x4E, 0x47]; // PNG header stub

await using UploadPetPhotoResponse uploadResponse = await photosClient.UploadPetPhotoAsync(
    petId: "pet-42"u8,
    session_token: "sess_k7j2m9x4"u8,
    body: new PostPetsByPetIdPhotosBody.Source((ref PostPetsByPetIdPhotosBody.Builder b) =>
    {
        b.Create(
            file: default,
            caption: "Bella at the park"u8,
            isPrimary: true);
    }),
    file: new BinaryPartData(
        WriteContentAsync: (stream, ct) => { stream.Write(photoBytes); return default; },
        ContentType: "image/png",
        FileName: "bella-park.png"));

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

// ── 5. URL-encoded form submission (adoption application) ────────────────────
Console.WriteLine("5. Submitting adoption application (form-encoded)...");
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

adoptionResponse.MatchResult(
    matchAccepted: result =>
    {
        Console.WriteLine($"   Application {result.ApplicationId}: {result.Status}");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"   Error: {error.Message}");
        return 0;
    });

Console.WriteLine();
Console.WriteLine("Done!");