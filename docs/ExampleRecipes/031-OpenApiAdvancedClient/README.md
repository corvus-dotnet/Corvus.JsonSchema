# 031 — OpenAPI Advanced Client

Demonstrates advanced OpenAPI 3.2 client features using a **realistic extended Petstore** API. This recipe builds on [029-OpenApiClient](../029-OpenApiClient/) by adding features that real-world APIs use: streaming, file transfers, forms, cookies, and complex parameter serialization.

## What This Demonstrates

| Feature | API Method | OpenAPI Mechanism |
|---------|-----------|-------------------|
| Deep-object filters | `GET /pets` | `style: deepObject, explode: true` query |
| Array query params | `GET /pets` | `style: form, explode: true` array query |
| Path array params | `GET /pets/batch/{ids}` | `style: simple` path array |
| Cookie authentication | `POST /pets` | `in: cookie` parameter |
| Header correlation IDs | `GET /pets` | `in: header, required: true` |
| SSE streaming | `POST /pets/{petId}/chat` | `text/event-stream` with `itemSchema` |
| NDJSON streaming | `GET /pets/{petId}/activity` | `application/x-ndjson` with `itemSchema` |
| Binary download | `GET /photos/{photoId}` | `application/octet-stream` response |
| Multipart upload | `POST /pets/{petId}/photos` | `multipart/form-data` with binary part |
| Form-encoded bodies | `POST /adoption/apply` | `application/x-www-form-urlencoded` |

## Prerequisites

```bash
dotnet tool install --global Corvus.Json.Cli
```

## Generating the Client

```bash
corvusjson openapi-client petstore-extended.json \
    --rootNamespace Petstore.Extended \
    --outputPath Generated \
    --force
```

This produces:
- **4 client classes** (`ApiPetsClient`, `ApiPhotosClient`, `ApiChatClient`, `ApiAdoptionClient`) — one per tag
- **Request/response structs** for each operation with typed `MatchResult()` handlers
- **106 model types** including deep-object filters, form bodies, streaming chunks, and enum types

## How the Generated Code Helps

### Parameter Serialization (You Don't Have To)

The generated client handles all the serialization styles defined in OpenAPI 3.2:

```csharp
// You write typed builders...
await petsClient.ListPetsAsync(
    xRequestId: "req-abc-123"u8,
    filter: new GetPetsFilter.Source((ref GetPetsFilter.Builder b) =>
    {
        b.Create(status: "available"u8, breed: "labrador"u8, minAge: 1);
    }),
    tags: new GetPetsTags.Source((ref GetPetsTags.Builder b) =>
    {
        b.AddItem("dog"u8);
        b.AddItem("friendly"u8);
    }));

// ...the generated code produces:
// GET /pets?filter[status]=available&filter[breed]=labrador&filter[minAge]=1&tags=dog&tags=friendly
// Header: x-request-id: req-abc-123
```

### Streaming Responses

For SSE and NDJSON streams, the generated response exposes `IAsyncEnumerable<ParsedJsonDocument<T>>`. Each item arrives as a strongly-typed, pooled document:

```csharp
await foreach (ParsedJsonDocument<ChatChunk> chunk in chatResponse.EnumerateOkItems())
{
    using (chunk)
    {
        Console.Write(chunk.RootElement.Delta);
    }
}
```

### Binary Transfers

File uploads use `BinaryPartData` — a lightweight record struct that streams content without buffering.
The `WriteContentAsync` callback supports both synchronous and fully async sources:

```csharp
// In-memory data (synchronous write, zero-allocation ValueTask)
file: new BinaryPartData(
    WriteContentAsync: (stream, ct) => { stream.Write(photoBytes); return default; },
    ContentType: "image/png",
    FileName: "bella-park.png")

// Async source (e.g. Azure Blob Storage)
file: new BinaryPartData(
    WriteContentAsync: (stream, ct) => blob.DownloadToAsync(stream, ct),
    ContentType: "application/octet-stream",
    FileName: "report.pdf")
```

File downloads expose the raw `Stream` via `MatchResult<ValueTask>` for async handling:

```csharp
await downloadResponse.MatchResult<ValueTask>(
    matchOkStream: async stream => { await stream.CopyToAsync(fileStream); },
    matchNotFound: error => { Console.WriteLine($"Not found: {error.Message}"); return default; },
    matchDefault: statusCode => { Console.WriteLine($"Unexpected: {statusCode}"); return default; });
```

### Form Bodies

URL-encoded and multipart bodies use the same typed builder pattern as JSON — the generated code handles encoding:

```csharp
// The builder mirrors the schema — no manual encoding needed
body: new PostAdoptionApplyBody.Source((ref PostAdoptionApplyBody.Builder b) =>
{
    b.Create(
        applicantName: "Jane Smith"u8,
        email: "jane@example.com"u8,
        housingType: "house"u8,
        petId: "pet-42"u8);
})
// Generated output: applicantName=Jane+Smith&email=jane%40example.com&housingType=house&petId=pet-42
```

## Running

```bash
dotnet build
dotnet run
```

> **Note:** This example calls a fictional API. You'll need a running Petstore-compatible server (see [030-OpenApiServer](../030-OpenApiServer/) for how to build one).

## Project Structure

```
031-OpenApiAdvancedClient/
├── petstore-extended.json     # Extended OpenAPI 3.2 spec
├── OpenApiAdvancedClient.csproj
├── Program.cs                 # 8 scenarios demonstrating all features
├── README.md
└── Generated/                 # 132 generated files (corvusjson output)
    ├── ApiPetsClient.cs       # Pets operations (list, create, batch)
    ├── ApiPhotosClient.cs     # Photo upload/download
    ├── ApiChatClient.cs       # Vet chat (SSE) + activity feed (NDJSON)
    ├── ApiAdoptionClient.cs   # Adoption form submission
    └── Models/                # 106 typed models
```

## Key Patterns

### Cookie Authentication

Cookie parameters appear as regular method parameters — the generated request struct sets the `Cookie` header:

```csharp
await petsClient.CreatePetAsync(
    session_token: "sess_k7j2m9x4"u8,  // Becomes: Cookie: session_token=sess_k7j2m9x4
    body: ...);
```

### Exhaustive Response Matching

Every response type provides `MatchResult()` with one handler per declared status code plus a default for unmatched codes:

```csharp
createResponse.MatchResult(
    matchCreated: pet => { /* 201 */ return Ok(); },
    matchUnauthorized: error => { /* 401 */ return Unauthorized(); },
    matchDefault: statusCode => { /* anything else */ return StatusCode(statusCode); });
```

### Pooled Streaming Documents

Each document from `EnumerateOkItems()` is backed by pooled memory. Use `using` to return memory promptly — this prevents heap pressure even for long-lived streams:

```csharp
await foreach (ParsedJsonDocument<ActivityEvent> doc in response.EnumerateOkItems())
{
    using (doc)
    {
        // Process doc.RootElement here — fast, zero-copy access
    }
    // Memory returned to pool after using block
}
```
