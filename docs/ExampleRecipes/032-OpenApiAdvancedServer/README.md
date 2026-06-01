# 032 — OpenAPI Advanced Server

Demonstrates the server-side implementation of the extended Petstore API, showing how the generated code handles parameter deserialization, cookie extraction, streaming, file uploads, and form-encoded bodies — so your handler logic stays focused on business rules.

## What This Demonstrates

| Feature | Handler Method | What Generated Code Does For You |
|---------|---------------|----------------------------------|
| Deep-object filter params | `HandleListPetsAsync` | Deserializes `?filter[status]=x&filter[breed]=y` into typed struct |
| Array query params | `HandleListPetsAsync` | Parses `?tags=dog&tags=friendly` into typed array you enumerate |
| Response headers | `HandleListPetsAsync` | Emits `x-total-count` and `x-next` from your Result value |
| Cookie authentication | `HandleCreatePetAsync` | Extracts `Cookie: session_token=...` into a typed parameter |
| Multipart file upload | `HandleUploadPetPhotoAsync` | Parses metadata fields + binary content separately |
| Binary stream response | `HandleDownloadPhotoAsync` | Streams bytes without JSON serialization overhead |
| SSE streaming | `HandleStartVetChatAsync` | Wraps your items in SSE `data:` envelope + newlines |
| NDJSON streaming | `HandleStreamPetActivityAsync` | Writes each item as a newline-delimited JSON line |
| Form-encoded body | `HandleSubmitAdoptionApplicationAsync` | URL-decodes form into typed property struct |

## Prerequisites

```bash
dotnet tool install --global Corvus.Json.Cli
```

## Generating the Server

```bash
corvusjson openapi-server petstore-extended.json \
    --rootNamespace Petstore.Extended.Server \
    --outputPath Generated \
    --force
```

This produces:
- **4 handler interfaces** (`IApiPetsHandler`, `IApiPhotosHandler`, `IApiChatHandler`, `IApiAdoptionHandler`)
- **`ApiEndpointRegistration`** — a single extension method that wires all routes
- **Params/Result structs** — typed input and typed output for each operation
- **106 model types** — shared with the client generation

## The Handler Pattern

Your server code implements handler interfaces. Each method receives:
1. **`parameters`** — a struct with all deserialized params (path, query, header, cookie, body)
2. **`workspace`** — a `JsonWorkspace` for building response values efficiently
3. **`cancellationToken`** — standard cancellation support

You return a **Result** value created from factory methods (`Ok(...)`, `Created(...)`, `NotFound(...)`) that the generated endpoint registration serializes and sends.

```csharp
public ValueTask<CreatePetResult> HandleCreatePetAsync(
    CreatePetParams parameters,
    JsonWorkspace workspace,
    CancellationToken cancellationToken = default)
{
    // Cookie already extracted — just check it
    if (parameters.SessionToken.IsUndefined())
        return ValueTask.FromResult(CreatePetResult.Unauthorized(...));

    // Body already deserialized — read typed properties
    string name = (string)parameters.Body.Name;

    // Return typed result — generated code handles serialization
    return ValueTask.FromResult(CreatePetResult.Created(
        new Pet.Source((ref Pet.Builder b) => { b.Create(id: 1, name: name, status: "available"u8); }),
        workspace));
}
```

## Wiring Up

```csharp
WebApplication app = builder.Build();

// One call registers all routes from the spec
app.MapApiEndpoints(petsHandler, photosHandler, chatHandler, adoptionHandler);

app.Run();
```

The generated `MapApiEndpoints` extension method:
- Registers correct HTTP methods and route templates
- Deserializes parameters from the request (path, query, headers, cookies, body)
- Calls your handler method
- Serializes the Result to the response (body + headers + status code)

## Key Server Patterns

### Reading Deserialized Parameters

Parameters arrive fully typed. No raw string parsing:

```csharp
// Deep-object query: ?filter[status]=available&filter[breed]=labrador
GetPetsFilter filter = parameters.Filter;
string status = (string)filter.Status;  // "available"
string breed = (string)filter.Breed;    // "labrador"

// Array query: ?tags=dog&tags=friendly
foreach (JsonString tag in parameters.Tags.EnumerateArray())
{
    Console.WriteLine((string)tag);  // "dog", "friendly"
}

// Cookie: session_token=sess_xyz
string token = (string)parameters.SessionToken;
```

### Response Headers

Return headers as part of the Result factory. The generated code emits them:

```csharp
return ListPetsResult.Ok(
    body: ...,
    workspace: workspace,
    xTotalCount: 42,                        // x-total-count: 42
    xNext: "\"/pets?offset=10\""u8);        // x-next: /pets?offset=10
```

### Streaming Responses (SSE/NDJSON)

For streaming operations, return `Ok(...)` with a generated writer callback. The endpoint registration keeps the HTTP response body open while the callback runs and applies the media framing for each item:

```csharp
// SSE: generated code writes "data: {...}\n\n" for each appended item
return new(StartVetChatResult.Ok(static async (stream, cancellationToken) =>
{
    ChatChunk chunk = ChatChunk.ParseValue("""{"delta":"Hello!","done":true}"""u8);
    await stream.AppendChatChunk(chunk, cancellationToken);
}));

// NDJSON: generated code writes "{...}\n" for each appended item
return new(StreamPetActivityResult.Ok(static async (stream, cancellationToken) =>
{
    ActivityEvent activity = ActivityEvent.ParseValue("""{"eventId":"evt-1","timestamp":"2026-05-30T18:00:00Z","type":"check-in","description":"Bella checked in"}"""u8);
    await stream.AppendActivityEvent(activity, cancellationToken);
}));
```

SSE completion is implicit. When the callback returns, the generated endpoint flushes and completes the HTTP response; no `End*` call is generated or needed.

### Form Body Access

URL-encoded form fields are typed properties on the body:

```csharp
PostAdoptionApplyBody body = parameters.Body;
string applicantName = (string)body.ApplicantName;  // URL-decoded
string email = (string)body.Email;                  // URL-decoded
```

## Running

```bash
dotnet build
dotnet run
```

The launch profile does not open a browser. The server writes its actual base URL, sample `curl` commands, request summaries, and handler activity to the console. Pair with [031-OpenApiAdvancedClient](../031-OpenApiAdvancedClient/) to exercise the full flow.

For deep-object query examples such as `filter[status]=available`, pass `--globoff` to curl:

```bash
curl --globoff "http://localhost:50516/pets?limit=2&filter[status]=available&tags=dog" -H "x-request-id: demo-request-1"
```

curl treats square brackets in URLs as range/glob syntax unless globbing is disabled. Encoding the brackets as `filter%5Bstatus%5D=available` is also valid, but `--globoff` keeps the OpenAPI deep-object parameter shape visible in the sample.

## Project Structure

```
032-OpenApiAdvancedServer/
├── petstore-extended.json         # Same spec as the client recipe
├── OpenApiAdvancedServer.csproj   # Web SDK
├── Program.cs                     # 4 handler implementations
├── README.md
└── Generated/                     # 129 generated files
    ├── IApiPetsHandler.cs         # Handler interface (4 operations)
    ├── IApiPhotosHandler.cs       # Handler interface (2 operations)
    ├── IApiChatHandler.cs         # Handler interface (2 operations)
    ├── IApiAdoptionHandler.cs     # Handler interface (1 operation)
    ├── ApiEndpointRegistration.cs # Route registration extension
    ├── *Params.cs                 # Typed input structs (9 files)
    ├── *Result.cs                 # Typed output structs (9 files)
    └── Models/                    # 106 typed models
```

## Comparison: Client vs Server

| Aspect | Client (031) | Server (032) |
|--------|-------------|-------------|
| Generated from | `corvusjson openapi-client` | `corvusjson openapi-server` |
| You write | Call methods, handle responses | Implement interfaces, return results |
| Parameters | You build Sources with typed builders | You read typed properties from Params |
| Responses | Use `MatchResult()` exhaustively | Return factory methods (Ok, Created, etc.) |
| Streaming | `await foreach` over `EnumerateOkItems()` | Return `Ok(...)` with a writer callback |
| Auth | Pass cookie as a parameter | Read cookie from Params, validate |
