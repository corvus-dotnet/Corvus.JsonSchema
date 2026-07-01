# OpenAPI Code Generation

> **[Try the OpenAPI Playground](/playground-openapi/)** — generate strongly-typed OpenAPI client code, inspect request/response types, and run samples in your browser.

## Overview

`Corvus.Text.Json` includes a code generator that produces strongly-typed HTTP **clients** and ASP.NET Core **server stubs** from [OpenAPI](https://spec.openapis.org/oas/latest.html) specifications (versions 3.0, 3.1, and 3.2).

Both sides are generated from the same spec. The generated code handles all the HTTP plumbing — parameter serialization, schema validation, request/response parsing, streaming, and error handling — so you focus purely on business logic.

The generator leverages the Corvus.JsonSchema V5 engine for model generation, producing zero-allocation, pooled-memory types with full JSON Schema validation built in.

## Installation

```bash
# Install the CLI tool globally
dotnet tool install --global Corvus.Json.Cli

# Or as a local tool
dotnet tool install Corvus.Json.Cli
```

For client projects, add the transport package:

```bash
dotnet add package Corvus.Text.Json.OpenApi.HttpTransport
```

For server projects, add the OpenAPI runtime:

```bash
dotnet add package Corvus.Text.Json.OpenApi
```

Both also need the core library:

```bash
dotnet add package Corvus.Text.Json
```

## Quick Start — Client

Generate a typed client from any OpenAPI spec:

```bash
corvusjson openapi-client petstore.json \
    --rootNamespace Petstore.Client \
    --outputPath ./Generated
```

Use it with just a few lines:

```csharp
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Petstore.Client;
using Petstore.Client.Models;

using HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v1") };
await using HttpClientTransport transport = new(httpClient);
await using ApiPetsClient client = new(transport);

// List pets — the generated code validates limit <= 100 before sending
await using ListPetsResponse response = await client.ListPetsAsync(limit: 10);

response.MatchResult(
    matchOk: pets =>
    {
        foreach (Pet pet in pets.EnumerateArray())
        {
            Console.WriteLine($"[{pet.Id}] {pet.Name}");
        }

        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"Error {error.Code}: {error.Message}");
        return 0;
    });
```

## Quick Start — Server

Generate handler interfaces and endpoint registration:

```bash
corvusjson openapi-server petstore.json \
    --rootNamespace Petstore.Server \
    --outputPath ./Generated
```

Wire up with ASP.NET Core minimal APIs:

```csharp
using Corvus.Text.Json;
using Petstore.Server;
using Petstore.Server.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

PetsHandler handler = new();
app.MapApiEndpoints(handler);

app.Run();
```

Implement your business logic:

```csharp
internal sealed class PetsHandler : IApiPetsHandler
{
    public ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Parameters are already validated — limit <= 100 is guaranteed
        // Build response using typed Result factory + array builder
        ListPetsResult result = ListPetsResult.Ok(
            body: new Pets.Source((ref Pets.Builder b) =>
            {
                b.AddItem(Pet.Build(id: 1, name: "Luna"u8, tag: "cat"u8));
            }),
            workspace: workspace);

        return new(result);
    }
}
```

## What the Generator Produces

### Client Generation (`openapi-client`)

| Generated artifact | What it handles |
|---|---|
| **Client class** (e.g., `ApiPetsClient`) | Orchestrates request lifecycle: builds parameters, validates against JSON Schema, sends via transport, parses response |
| **Request structs** | Serializes path/query/header/cookie parameters into correct wire format |
| **Response structs** | Parses response body into typed models; `MatchResult` for exhaustive status code handling |
| **Model types** (in `.Models` sub-namespace) | Strongly-typed JSON Schema models with validation, zero-allocation access, and builder patterns |
| **Client interface** | Contract for dependency injection and testing |

### Server Generation (`openapi-server`)

| Generated artifact | What it handles |
|---|---|
| **Handler interface** (e.g., `IApiPetsHandler`) | One async method per operation — you implement business logic here |
| **Endpoint registration** (`ApiEndpointRegistration`) | Maps all routes with correct HTTP methods and path templates |
| **Params structs** | Strongly-typed, schema-validated request parameters and bodies |
| **Result structs** | Factory methods for each response status code with typed body builders |
| **Model types** (in `.Models` sub-namespace) | Same models as client generation |

### Namespace Layout

The generator places types into two namespaces to avoid name collisions:

| Namespace | Contains | Example |
|---|---|---|
| `{rootNamespace}` | Client/server classes, request, response, params, result types | `Petstore.Client.ApiPetsClient`, `Petstore.Client.ListPetsResponse` |
| `{rootNamespace}.Models` | JSON Schema model types (from inline schemas and `#/components/schemas`) | `Petstore.Client.Models.Pet`, `Petstore.Client.Models.Error` |

Model types are generated into a `Models/` subdirectory of the output path and use the `.Models` sub-namespace. This prevents collisions when a schema name (e.g., `CreateUserRequest`) matches a generated request type name.

## Generated Code Architecture

### Client Flow

```
Your Code → Client.MethodAsync(params...)
  → Generated Request: serialize params (path/query/header/cookie)
  → Generated Validation: validate against JSON Schema
  → IApiTransport: send HTTP request
  → Generated Response: parse status + body + headers
  → MatchResult: dispatch to your handler
```

### Server Flow

```
HTTP Request arrives
  → Generated Middleware: parse path/query/header/cookie params
  → Generated Validation: validate params and body against schemas
  → If invalid: return 400 Problem Details (your handler never runs)
  → Your Handler: receives typed params, returns typed Result
  → Generated Middleware: validate response body, serialize, write HTTP
```

## Parameter Styles

The generator supports all OpenAPI 3.x parameter serialization styles:

| Style | Location | Example | Wire Format |
|-------|----------|---------|-------------|
| Simple | path | `{petId}` | `/pets/123` |
| Simple array | path | `{ids}` | `/pets/batch/1,2,3` |
| Form | query | `limit=10` | `?limit=10` |
| Form array (explode) | query | `tags` | `?tags=dog&tags=cat` |
| Deep object | query | `filter` | `?filter[status]=available&filter[breed]=lab` |
| Simple | header | `x-request-id` | `x-request-id: abc-123` |
| Form | cookie | `session_token` | `Cookie: session_token=tok123` |

### Deep-Object Filters (Client)

```csharp
await using ListPetsResponse response = await petsClient.ListPetsAsync(
    xRequestId: "req-abc-123"u8,
    filter: GetPetsFilter.Build(status: "available"u8, breed: "labrador"u8),
    tags: new GetPetsTags.Source((ref GetPetsTags.Builder b) =>
    {
        b.AddItem("dog"u8);
        b.AddItem("friendly"u8);
    }));
// Produces: GET /pets?filter[status]=available&filter[breed]=labrador&tags=dog&tags=friendly
```

### Deep-Object Filters (Server)

```csharp
public ValueTask<ListPetsResult> HandleListPetsAsync(
    ListPetsParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    // The generated code already parsed ?filter[status]=available into a typed object
    if (!parameters.Filter.IsUndefined())
    {
        string status = (string)parameters.Filter.GetProperty("status"u8);
        // Use for filtering...
    }

    // Array params are typed too
    foreach (JsonString tag in parameters.Tags.EnumerateArray())
    {
        // Filter by each tag...
    }
}
```

## Response Handling

### MatchResult (Exhaustive Pattern Matching)

Every response type provides `MatchResult()` with one handler per declared status code:

```csharp
string message = showResponse.MatchResult<string>(
    matchOk: static pet => $"Found: {pet.Name}",
    matchNotFound: static error => $"Not found: {error.Message}",
    matchDefault: static error => $"Unexpected: {error.Message}");
```

This is the preferred approach — it ensures you handle every possible response the API can return.

### Response Headers

Declared response headers are exposed as typed properties:

```csharp
await using ListPetsResponse response = await client.ListPetsAsync(limit: 10);
JsonInteger totalCount = response.XTotalCountHeader;
JsonString nextPage = response.XNextHeader;
```

## Request Bodies

### JSON Bodies (Client)

Object bodies use the generated `Builder` pattern. Required properties are mandatory parameters; optional ones have defaults:

```csharp
await using CreatePetResponse response = await client.CreatePetAsync(
    session_token: "admin-token"u8,
    body: NewPet.Build(
        name: "Fido"u8,
        status: "available"u8,
        tags: new NewPet.JsonStringArray.Source(
            (ref NewPet.JsonStringArray.Builder ab) =>
            {
                ab.AddItem("friendly"u8);
                ab.AddItem("vaccinated"u8);
            })));
```

### JSON Bodies (Server)

On the server, the body arrives pre-parsed and validated in the params struct:

```csharp
public ValueTask<CreatePetResult> HandleCreatePetAsync(
    CreatePetParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    // Body already deserialized and validated — read typed properties directly
    string name = (string)parameters.Body.Name;
    string status = (string)parameters.Body.Status;

    // Build a typed response
    return new(CreatePetResult.Created(
        body: Pet.Build(id: nextId++, name: name.AsSpan(), status: status.AsSpan()),
        workspace: workspace));
}
```

### Form-Encoded Bodies

URL-encoded forms use the same builder pattern — the generated code handles encoding:

```csharp
await using SubmitAdoptionApplicationResponse response =
    await adoptionClient.SubmitAdoptionApplicationAsync(
        body: PostAdoptionApplyBody.Build(
            applicantName: "Jane Smith"u8,
            email: "jane@example.com"u8,
            housingType: "house"u8,
            petId: "pet-42"u8));
// Wire format: applicantName=Jane+Smith&email=jane%40example.com&housingType=house&petId=pet-42
```

On the server, the generated middleware deserializes form data back into the same typed struct:

```csharp
public ValueTask<SubmitAdoptionApplicationResult> HandleSubmitAdoptionApplicationAsync(
    SubmitAdoptionApplicationParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    // parameters.Body is typed — same properties regardless of encoding
    string applicant = (string)parameters.Body.ApplicantName;
    string email = (string)parameters.Body.Email;

    return new(SubmitAdoptionApplicationResult.Accepted(
        body: PostAdoptionApplyAccepted.Build(applicationId: "app-42"u8, status: "received"u8),
        workspace: workspace));
}
```

## Streaming

### Server-Sent Events (SSE)

For server `text/event-stream` responses with an OpenAPI `itemSchema`, the generated result factory accepts a writer callback. Append typed items to the generated stream; the endpoint registration serializes each item as compact JSON and applies the SSE frame (`data: {...}\n\n`):

```csharp
public ValueTask<StartVetChatResult> HandleStartVetChatAsync(
    StartVetChatParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    return new(StartVetChatResult.Ok(static async (stream, cancellationToken) =>
    {
        ChatChunk greeting = ChatChunk.ParseValue(
            """{"delta":"Hello! ","done":false}"""u8);
        await stream.AppendChatChunk(greeting, cancellationToken);

        ChatChunk answer = ChatChunk.ParseValue(
            """{"delta":"How can I help?","done":true}"""u8);
        await stream.AppendChatChunk(answer, cancellationToken);
    }));
}
```

There is no generated `End*` method. For SSE, stream completion is the HTTP response completing: when the callback returns, the endpoint flushes the response and closes the response body. If the client disconnects, the callback receives cancellation through the provided token.

On the client, streaming responses expose `IAsyncEnumerable`. Use `EnumerateOkItems()` when you only need JSON payloads, or `EnumerateOkSseItems()` when you also need SSE metadata such as `id` or `event`:

```csharp
await foreach (ParsedJsonDocument<ChatChunk> chunk in chatResponse.EnumerateOkItems())
{
    using (chunk)
    {
        Console.Write(chunk.RootElement.Delta);
    }
}
```

### NDJSON (Newline-Delimited JSON)

For server `application/x-ndjson` responses, the same generated writer callback is used. Each appended item is written as one JSON line (`{...}\n`):

```csharp
public ValueTask<StreamPetActivityResult> HandleStreamPetActivityAsync(
    StreamPetActivityParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    return new(StreamPetActivityResult.Ok(static async (stream, cancellationToken) =>
    {
        ActivityEvent checkIn = ActivityEvent.ParseValue(
            """{"eventId":"evt-1","timestamp":"2026-05-30T18:00:00Z","type":"check-in","description":"Bella checked in"}"""u8);
        await stream.AppendActivityEvent(checkIn, cancellationToken);
    }));
}
```

The client reads each JSON line as a pooled typed document:

```csharp
await foreach (ParsedJsonDocument<ActivityEvent> doc in activityResponse.EnumerateOkItems())
{
    using (doc)
    {
        Console.WriteLine($"[{doc.RootElement.Timestamp}] {doc.RootElement.Type}: {doc.RootElement.Description}");
    }
}
```

## Binary Transfers

### File Upload (Multipart)

```csharp
await using UploadPetPhotoResponse uploadResponse = await photosClient.UploadPetPhotoAsync(
    petId: "pet-1"u8,
    metadata: PhotoMetadata.Build(petId: "pet-1"u8, description: "At the park"u8),
    file: new BinaryPartData(
        WriteContentAsync: (stream, ct) => { stream.Write(photoBytes); return default; },
        ContentType: "image/png",
        FileName: "bella-park.png"));
```

For async sources (e.g. downloading from cloud storage), the callback can be fully async:

```csharp
file: new BinaryPartData(
    WriteContentAsync: (stream, ct) => blob.DownloadToAsync(stream, ct),
    ContentType: "application/octet-stream",
    FileName: "report.pdf")
```

### File Download

Use `MatchResult<ValueTask>` to handle stream responses asynchronously:

```csharp
await downloadResponse.MatchResult<ValueTask>(
    matchOkStream: async stream =>
    {
        // Read binary data asynchronously from the raw stream
        await stream.CopyToAsync(fileStream);
    },
    matchNotFound: error =>
    {
        Console.WriteLine($"Photo not found: {error.Message}");
        return default;
    },
    matchDefault: statusCode =>
    {
        Console.WriteLine($"Unexpected: {statusCode}");
        return default;
    });
```

## Validation

### Client-Side Validation

By default, the generated client validates all parameters and request bodies before sending:

```csharp
try
{
    // limit: 200 exceeds the schema's "maximum: 100" constraint
    await using ListPetsResponse _ = await client.ListPetsAsync(
        limit: 200,
        validationMode: ValidationMode.Detailed);
}
catch (InvalidOperationException ex)
{
    // Detailed mode includes full JSON Schema evaluation output
    Console.WriteLine($"Validation failed: {ex.Message}");
}
```

| Mode | Behaviour |
|------|-----------|
| `ValidationMode.Basic` | Validates parameters and bodies; throws on failure with a brief message |
| `ValidationMode.Detailed` | Same, but includes full JSON Schema evaluation output in the exception |
| `ValidationMode.None` | Skips validation — use for trusted inputs in performance-critical paths |

### Server-Side Validation

The generated middleware validates all inputs before your handler runs:

| Invalid input | Generated behaviour |
|---|---|
| Required parameter missing | 400 Problem Details — handler never called |
| Parameter fails schema validation | 400 Problem Details |
| Request body fails JSON parse | 400 Problem Details |
| Request body fails schema validation | 400 Problem Details |
| Response body fails validation | 500 Internal Server Error |

You never write validation code. Your handler only receives valid, typed data.

## Customizing Generated Endpoints

By default `MapApiEndpoints` registers each operation as a bare minimal-API route. When you need per-endpoint conventions — authorization, route names, tags, `Produces` metadata, output caching, rate limiting — use the `configureEndpoint` overload. It is invoked once per generated endpoint, *after* the route is mapped (so your conventions win), with a descriptor identifying the operation and the route's `IEndpointConventionBuilder`:

```csharp
app.MapApiEndpoints(petsHandler, static (in EndpointDescriptor endpoint, IEndpointConventionBuilder builder) =>
{
    builder.WithName(endpoint.OperationId ?? endpoint.MethodName);
    builder.WithTags([.. endpoint.Tags]);

    // Webhook/callback operations (from openapi-callback-server) are flagged separately.
    if (endpoint.IsCallback)
    {
        builder.WithMetadata(new EndpointGroupNameAttribute("webhooks"));
    }
});
```

The bare `MapApiEndpoints(petsHandler)` overload is preserved unchanged; the callback variant is an **additional overload**, so adopting the hook is both source- and binary-compatible. No new package dependency is added — the callback receives `IEndpointConventionBuilder` from `Microsoft.AspNetCore.Routing`, which the generated server already references.

### EndpointDescriptor

`EndpointDescriptor`, `EndpointSecurityRequirementSet`, `EndpointSecurityRequirement`, and the `ConfigureEndpoint` delegate are read-only types generated alongside `ApiEndpointRegistration` in your server's root namespace.

| Member | Type | Description |
|---|---|---|
| `OperationId` | `string?` | OpenAPI `operationId`, or `null` if the operation declares none |
| `MethodName` | `string` | Generated handler method name (the `{MethodName}` in `Handle{MethodName}Async`) |
| `HttpMethod` | `string` | HTTP method (`GET`, `POST`, …) |
| `RouteTemplate` | `string` | ASP.NET route template as registered |
| `Tags` | `IReadOnlyList<string>` | OpenAPI tags for the operation |
| `IsCallback` | `bool` | `true` for webhook/callback operations, `false` for main `paths` |
| `SecurityRequirements` | `IReadOnlyList<EndpointSecurityRequirementSet>` | The operation's declared security as a list of **alternatives** (see below) |

`SecurityRequirements` is populated for all supported spec versions (OpenAPI 3.0, 3.1, and 3.2). Operation-level `security` takes precedence over the document-level default; operations that declare neither surface an empty list.

#### The OR/AND structure

OpenAPI security mirrors the spec's `security` array exactly: it is a list of **alternatives**, and the operation is satisfied if **any one** alternative is met (OR). Each alternative — an `EndpointSecurityRequirementSet` — is a group of scheme requirements that must **all** be met together (AND). For example `[{bearerAuth: []}, {apiKeyAuth: []}]` means *bearerAuth OR apiKeyAuth*, whereas `[{bearerAuth: [], apiKeyAuth: []}]` means *bearerAuth AND apiKeyAuth*.

`EndpointSecurityRequirementSet` (one alternative):

| Member | Type | Description |
|---|---|---|
| `Requirements` | `IReadOnlyList<EndpointSecurityRequirement>` | The scheme requirements that must all be satisfied together (AND) |
| `IsOptional` | `bool` | `true` when this alternative is the empty OpenAPI requirement (`{}`), which permits anonymous access |
| `PolicyName` | `string` | The canonical policy name for the alternative: the single requirement's `PolicyName`, or the requirement policy names joined with ` && ` (empty for an anonymous alternative) |

`EndpointSecurityRequirement` (one scheme within an alternative):

| Member | Type | Description |
|---|---|---|
| `SchemeName` | `string` | The security scheme name, as declared in `components.securitySchemes` |
| `Scopes` | `IReadOnlyList<string>` | The scopes required by this requirement (empty for non-scoped schemes) |
| `SchemeType` | `string?` | The scheme's OpenAPI `type` (`oauth2`, `apiKey`, `http`, `openIdConnect`), or `null` if the scheme is not declared in `components.securitySchemes`. Lets the callback branch on scheme type without cross-referencing the scheme table |
| `PolicyName` | `string` | The canonical policy name: the scheme name alone when no scopes are required, otherwise `{schemeName}:{scope+scope...}`. Use the same value when registering policies so endpoint mapping and policy registration stay in sync |

### Applying OpenAPI Security as Authorization

The generator surfaces `components.securitySchemes` and per-operation `security` on the descriptor, but it deliberately does **not** decide *how* a scheme maps to an authentication handler or how scopes are enforced — those are application concerns. It does, however, generate a small helper that applies the declared security using a sensible default convention, so the common case needs only one line.

#### The generated `RequireDeclaredAuthorization` helper

Alongside the registration types, the generator emits an `EndpointSecurityConventions.RequireDeclaredAuthorization` extension. Its behaviour follows the alternatives:

- No declared security, or any anonymous (`{}`) alternative → `AllowAnonymous`.
- A single alternative → `RequireAuthorization` for each scheme in it (AND), using each requirement's `PolicyName`.
- Multiple alternatives (OR) → a single `RequireAuthorization` with a combined name (the alternatives' `PolicyName`s joined by ` || `), because ASP.NET endpoint conventions cannot OR policies. You register that one policy with your own OR logic.

You register the referenced policies and wire the helper into the hook:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(/* your scheme(s) */);
builder.Services.AddAuthorization(options =>
{
    // Single-alternative operations require one policy per requirement PolicyName.
    options.AddPolicy("oauth2:read:pets", p => p.RequireClaim("scope", "read:pets"));

    // An OR operation ([{bearerAuth}, {apiKeyAuth}]) requires the combined policy; you supply the OR logic.
    options.AddPolicy("bearerAuth || apiKeyAuth", p => p.RequireAssertion(ctx =>
        ctx.User.HasClaim(c => c.Type == "bearer") || ctx.User.HasClaim(c => c.Type == "apikey")));
});

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

PetsHandler petsHandler = new();
app.MapApiEndpoints(petsHandler, static (in EndpointDescriptor endpoint, IEndpointConventionBuilder builder) =>
    builder.RequireDeclaredAuthorization(endpoint));

app.Run();
```

#### Writing the mapping yourself

`RequireDeclaredAuthorization` is just a convenience over `SecurityRequirements`. When you need different policy names or to branch on `SchemeType`, walk the alternatives yourself:

```csharp
app.MapApiEndpoints(petsHandler, static (in EndpointDescriptor endpoint, IEndpointConventionBuilder builder) =>
{
    if (endpoint.SecurityRequirements.Count == 0)
    {
        builder.AllowAnonymous();
        return;
    }

    foreach (EndpointSecurityRequirementSet alternative in endpoint.SecurityRequirements)
    {
        foreach (EndpointSecurityRequirement requirement in alternative.Requirements)
        {
            // requirement.SchemeType is "oauth2", "apiKey", "http", or "openIdConnect".
            _ = requirement.PolicyName;
        }
    }

    // ...translate the OR-of-ANDs however your app enforces it.
});
```

Because the callback just hands you an `IEndpointConventionBuilder`, every standard ASP.NET endpoint extension is available the same way — `RequireRateLimiting`, `CacheOutput`, `RequireCors`, `DisableAntiforgery`, and so on. See the `034-OpenApiCallbackServer` recipe for the hook applied to a callback/webhook server.

## Authentication

Corvus generates typed parameters for OpenAPI security schemes declared in the spec — but authentication itself is implemented using standard .NET `HttpClient` patterns. The generated transport (`HttpClientTransport`) wraps a regular `HttpClient`, so you configure auth the same way you would for any HTTP client.

Kiota provides built-in `IAuthenticationProvider` implementations (`BaseBearerTokenAuthenticationProvider`, `ApiKeyAuthenticationProvider`, `AnonymousAuthenticationProvider`, and an Azure Identity provider for Microsoft Entra). Corvus achieves the same result using `IHttpClientFactory` and standard `DelegatingHandler` middleware — the same patterns used across the .NET ecosystem.

**Key advantage:** Corvus extracts OAuth2 scopes from the specification and emits them as generated constants — per-operation (`ListPetsOauth2Scopes`) and unioned (`AllOauth2Scopes`). This eliminates hardcoded scope strings and ensures your token requests stay in sync with the API contract. Kiota does not surface per-operation scopes in generated code.

> **These `SecuritySchemes` / `SecurityRequirements` scope constants are emitted for OpenAPI 3.0, 3.1, and 3.2 specs that declare `securitySchemes`.** (This is distinct from the server-side `ApiEndpointRegistration.SecurityRequirements` endpoint metadata described under [Applying OpenAPI Security as Authorization](#applying-openapi-security-as-authorization), which is populated for all supported versions.) The same broad set of providers below applies to the TypeScript client — see the [TypeScript authentication guide](./typescript/authentication.md).

### Microsoft Entra ID (Azure AD / MSAL)

Kiota provides a dedicated Azure Identity authentication provider that wraps MSAL. The Corvus equivalent uses `Azure.Identity` (which wraps MSAL internally) with a standard `DelegatingHandler`.

The code generator emits per-operation OAuth2 scope constants directly from the specification's `security` requirements. This means you never need to hardcode scopes — they come from the spec:

```csharp
// Generated constants from the specification
IApiPetsClient.SecuritySchemes.Oauth2TokenUrl       // "https://auth.example.com/token"
IApiPetsClient.SecuritySchemes.Oauth2AuthorizationUrl // "https://auth.example.com/authorize"
IApiPetsClient.SecuritySchemes.Oauth2AvailableScopes // ["read:pets", "write:pets"]

// Per-operation scopes
IApiPetsClient.SecurityRequirements.ListPetsOauth2Scopes   // ["read:pets"]
IApiPetsClient.SecurityRequirements.CreatePetOauth2Scopes  // ["read:pets", "write:pets"]
IApiPetsClient.SecurityRequirements.AllOauth2Scopes        // ["read:pets", "write:pets"]
```

Use these with `Azure.Identity` and `TokenRequestContext`:

```csharp
// Register Azure.Identity credential + handler in DI
services.AddSingleton<TokenCredential>(
    new ClientSecretCredential(tenantId, clientId, clientSecret));

services.AddTransient<EntraTokenHandler>();
services.AddHttpClient("petstore")
    .AddHttpMessageHandler<EntraTokenHandler>();

// Handler acquires tokens using generated scope constants
public class EntraTokenHandler(TokenCredential credential) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(IApiPetsClient.SecurityRequirements.AllOauth2Scopes),
            cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

For per-operation scopes (e.g., requesting a minimal token for read-only operations), use `HttpRequestMessage.Options` to pass the required scopes to your handler:

```csharp
// A scope-aware handler that reads per-request scopes from Options
public class ScopeAwareEntraHandler(TokenCredential credential) : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<string[]> ScopesKey = new("OAuth2Scopes");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Read scopes from the request options (set by calling code)
        if (!request.Options.TryGetValue(ScopesKey, out string[]? scopes))
        {
            // Fall back to union of all scopes if none specified
            scopes = IApiPetsClient.SecurityRequirements.AllOauth2Scopes;
        }

        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(scopes), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sets the OAuth2 scopes for a specific request.
    /// </summary>
    public static void SetScopes(HttpRequestMessage request, string[] scopes)
    {
        request.Options.Set(ScopesKey, scopes);
    }
}
```

Then at the call site, set operation-specific scopes when you need fine-grained token permissions:

```csharp
// Read-only operation — request minimal scopes
var listRequest = new HttpRequestMessage(HttpMethod.Get, "/pets");
ScopeAwareEntraHandler.SetScopes(
    listRequest, IApiPetsClient.SecurityRequirements.ListPetsOauth2Scopes);

// Write operation — request elevated scopes
var createRequest = new HttpRequestMessage(HttpMethod.Post, "/pets");
ScopeAwareEntraHandler.SetScopes(
    createRequest, IApiPetsClient.SecurityRequirements.CreatePetOauth2Scopes);
```

#### Interactive and Device-Code Flows

For user-facing applications, substitute the credential type:

```csharp
// Interactive browser login (desktop/native apps)
services.AddSingleton<TokenCredential>(
    new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
    {
        ClientId = clientId,
        TenantId = tenantId,
        RedirectUri = new Uri("http://localhost:1234"),
    }));

// Device code flow (CLI tools, headless terminals)
services.AddSingleton<TokenCredential>(
    new DeviceCodeCredential(new DeviceCodeCredentialOptions
    {
        ClientId = clientId,
        TenantId = tenantId,
        DeviceCodeCallback = (info, cancel) =>
        {
            Console.WriteLine(info.Message); // "To sign in, use a web browser..."
            return Task.CompletedTask;
        },
    }));

// Managed identity (Azure-hosted services — no secrets needed)
services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
```

All credential types work with the same `ScopeAwareEntraHandler` — the handler acquires tokens using whichever `TokenCredential` is registered. The generated scope constants ensure the correct permissions are requested regardless of flow.

Targets `Azure.Identity` v1 (current major; `TokenCredential.GetTokenAsync` returning `Azure.Core.AccessToken`), which wraps `Microsoft.Identity.Client` (MSAL.NET) v4 (current major) internally. See the [Azure Identity client library for .NET docs](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme); for the underlying `AcquireTokenSilent`/`AcquireTokenInteractive` flows see the [MSAL.NET docs](https://learn.microsoft.com/entra/msal/dotnet/).

### Bearer Token (Generic OAuth2 / JWT)

For non-Entra APIs that use OAuth2 bearer tokens, implement a token service:

```csharp
services.AddTransient<BearerTokenHandler>();
services.AddHttpClient("petstore")
    .AddHttpMessageHandler<BearerTokenHandler>();

public class BearerTokenHandler(ITokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string token = await tokenService.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

// Create the Corvus client from the named HttpClient
HttpClient httpClient = httpClientFactory.CreateClient("petstore");
await using HttpClientTransport transport = new(httpClient, baseUri);
PetstorePetsClient client = new(transport);
```

#### The built-in `BearerTokenAuthenticationProvider` token factory

For **pure token acquisition** (as opposed to signing, which needs the request's method/path/body) there is a simpler wiring than a `DelegatingHandler`: pass a token factory straight to `HttpClientTransport` via the built-in `BearerTokenAuthenticationProvider`. The factory is re-invoked once per request, so a refreshed token is always picked up, and there is no handler to register:

```csharp
using Corvus.Text.Json.OpenApi.HttpTransport;

var http = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
var transport = new HttpClientTransport(
    http,
    new BearerTokenAuthenticationProvider(async ct => await tokenService.GetTokenAsync(ct)));
var client = new ApiPetsClient(transport);
```

Prefer this for token acquisition; use a `DelegatingHandler` when the same handler is shared by other SDKs, when you need a 401 → refresh retry, or when you are **signing** the request (SigV4 / HMAC). Every snippet below is shown in whichever form fits — the token-acquisition providers use the built-in factory, the signing schemes use a handler. The runtime also ships `BasicAuthenticationProvider` and `ApiKeyAuthenticationProvider` for those schemes.

### Microsoft Entra ID / Managed Identity — Azure Identity

Beyond the interactive/device-code MSAL flows above, the same `Azure.Identity` credentials drive server-to-server auth. `DefaultAzureCredential` falls back through Managed Identity, environment, and CLI credentials; the SDK caches and refreshes, so calling `GetTokenAsync` per request is cheap. Feed it the generated scope union:

```csharp
using Azure.Core;
using Azure.Identity;
using Corvus.Text.Json.OpenApi.HttpTransport;

var cred = new DefaultAzureCredential();   // or new ManagedIdentityCredential() / new ClientSecretCredential(tenantId, clientId, secret)
string[] scopes = IApiPetsClient.SecurityRequirements.AllOauth2Scopes;   // or "api://<app-id>/.default"

var http = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
var transport = new HttpClientTransport(
    http,
    new BearerTokenAuthenticationProvider(async ct =>
    {
        AccessToken t = await cred.GetTokenAsync(new TokenRequestContext(scopes), ct);
        return t.Token;   // Azure.Identity caches + refreshes internally
    }));
```

Managed Identity works only on Azure hosts that inject it (App Service, Functions, Container Apps, AKS with workload identity, VMs) — locally `DefaultAzureCredential` uses env / CLI / VS credentials. Use the resource's `/.default` scope for an app-only token; a bare resource scope throws `AADSTS70011`. Don't hand-cache the token — the SDK already does.

Targets `Azure.Identity` v1 (the current major): `GetTokenAsync(TokenRequestContext, …)` → `Azure.Core.AccessToken`. See the [Azure Identity client library for .NET docs](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme).

### Google (access tokens + ID tokens for Cloud Run / IAP)

`Google.Apis.Auth`. Application Default Credentials come from the environment or the GCP metadata server. For a Google **access token**:

```csharp
using Google.Apis.Auth.OAuth2;
using Corvus.Text.Json.OpenApi.HttpTransport;

GoogleCredential cred = (await GoogleCredential.GetApplicationDefaultAsync())
    .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
var auth = new BearerTokenAuthenticationProvider(async ct =>
    await ((ITokenAccess)cred).GetAccessTokenForRequestAsync(cancellationToken: ct));
```

For a private Cloud Run service or an IAP-protected resource you need an **ID token** whose `aud` is the service URL / IAP client ID:

```csharp
GoogleCredential adc = await GoogleCredential.GetApplicationDefaultAsync();
OidcToken oidc = await adc.GetOidcTokenAsync(OidcTokenOptions.FromTargetAudience(audience));
var auth = new BearerTokenAuthenticationProvider(ct => new(oidc.GetAccessTokenAsync(ct)));  // returns the ID token
```

Access token vs ID token is the trap: Google APIs want the access token; Cloud Run / IAP want the ID token. On GCP hosts ADC comes from the metadata server; locally set `GOOGLE_APPLICATION_CREDENTIALS` or run `gcloud auth application-default login`. Both libraries cache the token — don't re-create the credential per request.

Targets `Google.Apis.Auth` v1 (the current major): `GoogleCredential.GetOidcTokenAsync(OidcTokenOptions.FromTargetAudience(...))` and the `ITokenAccess` accessor shown are the current 1.x API. See the [GoogleCredential .NET docs](https://cloud.google.com/dotnet/docs/reference/Google.Apis/latest/Google.Apis.Auth.OAuth2.GoogleCredential) and the [Cloud Run service-to-service auth guide](https://cloud.google.com/run/docs/authenticating/service-to-service).

### Auth0 / Okta / Clerk / Supabase / Firebase / Cognito (SPA-issued tokens)

Auth0, Okta, Clerk, Supabase, Firebase, and AWS Cognito are **browser SDKs** — a native .NET client acquires their tokens through the *server-side* equivalent flow, not a port of the browser SDK:

- **Auth0 / Okta / generic OIDC** — a server-to-server **client-credentials** exchange against the provider's `/token` endpoint. Use the [generic OAuth2 client-credentials](#generic-oauth2-client-credentials-token-endpoint) pattern below (Auth0 requires the `audience` parameter to mint a real JWT access token, not an opaque `/userinfo` token).
- **Clerk / Supabase / Firebase / Cognito** — these mint a token in the browser; a .NET service typically **verifies** the JWT rather than acquiring it. Validate with `AddJwtBearer` pointed at the provider's issuer/JWKS (Firebase: `FirebaseAdmin`'s `VerifyIdTokenAsync`; Cognito: the pool JWKS at `https://cognito-idp.<region>.amazonaws.com/<userPoolId>`). For a machine token, Cognito and Okta expose a client-credentials app grant — again the generic pattern below.

Version/doc caveats for the third-party pieces named here (latest stable majors named where a package is involved):

- **Auth0** ([auth0.com/docs](https://auth0.com/docs)) and **Okta** ([developer.okta.com](https://developer.okta.com/docs/guides/protect-your-api/main/)) — client-credentials against the provider `/token` endpoint (no .NET SDK required beyond the generic pattern; `Okta.AspNetCore` v5 is optional for JWT-bearer middleware).
- **Firebase** — `FirebaseAdmin` v3 (the current major): `FirebaseAuth.DefaultInstance.VerifyIdTokenAsync`. See the [Firebase Admin .NET setup docs](https://firebase.google.com/docs/admin/setup).
- **AWS Cognito** — no acquisition SDK on the verify side; validate the pool JWKS. See [Verifying a Cognito JWT](https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html).
- **Clerk** ([clerk.com/docs](https://clerk.com/docs)) and **Supabase** ([supabase.com/docs](https://supabase.com/docs)) — standard `AddJwtBearer` against the provider issuer/JWKS; any community C# SDK (e.g. `Clerk.Net`, `supabase-csharp`) is optional and versions independently.

In every case, whichever flow yields the JWT, attach it with the built-in `BearerTokenAuthenticationProvider(factory)` shown above. The TypeScript client is where these providers are wired *client-side* — see the [TypeScript authentication guide](./typescript/authentication.md) for the browser SDK snippets (and the specific npm package + version caveat for each).

### Generic OAuth2 client-credentials (token endpoint)

For any non-Entra OAuth2 provider, exchange client credentials at the token endpoint and cache the token until shortly before it expires. `Duende.IdentityModel` (the renamed successor to `IdentityModel`) gives a typed helper; a plain `HttpClient` POST works too. Read the scopes from the generated union:

```csharp
using Duende.IdentityModel.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;

var idp = new HttpClient();
string scope = string.Join(' ', IApiPetsClient.SecurityRequirements.AllOauth2Scopes);   // space-separated
var auth = new BearerTokenAuthenticationProvider(async ct =>
{
    TokenResponse r = await idp.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = "https://idp.example.com/oauth/token",   // or IApiPetsClient.SecuritySchemes.Oauth2TokenUrl
        ClientId = "id", ClientSecret = "secret", Scope = scope,
    }, ct);
    if (r.IsError) throw new InvalidOperationException(r.Error);
    return r.AccessToken!;
});
```

For production, cache the `TokenResponse` yourself keyed on `ExpiresIn` (subtract a 30–60 s skew buffer — a per-request POST to the IdP will rate-limit you), or use `Duende.AccessTokenManagement`, which caches and refreshes for you. Scopes are a **space-separated** string in the form body.

Targets `Duende.IdentityModel` v8 (the current major; namespace `Duende.IdentityModel.Client`) — the renamed successor to the legacy `IdentityModel` package, so update the package reference and `using` if you are on the old one. See the [Duende IdentityModel docs](https://docs.duendesoftware.com/identitymodel/). A plain `HttpClient` POST (following [RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4)) needs no package at all.

### AWS SigV4 request signing

SigV4 signs the method, host, path, query, and body, so it must run where those are known — a `DelegatingHandler`. There is **no supported public SigV4 signer in the AWS SDK for .NET** (the `AWS4Signer` class is internal), so use the third-party `AwsSignatureVersion4` NuGet package, whose supported public surface is the `AwsSignatureHandler` message handler:

```csharp
using Amazon;
using Amazon.Runtime;
using AwsSignatureVersion4;   // NuGet: AwsSignatureVersion4
using Corvus.Text.Json.OpenApi.HttpTransport;

// AWSCredentials resolves via the SDK's fallback chain and carries the sessionToken for temporary/role credentials.
AWSCredentials creds = FallbackCredentialsFactory.GetCredentials();
var settings = new AwsSignatureHandlerSettings(
    RegionEndpoint.EUWest1.SystemName, "execute-api", creds);

// AwsSignatureHandler signs method + host + path + query + headers + body, then forwards to its inner handler.
var http = new HttpClient(new AwsSignatureHandler(settings) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri("https://<api-id>.execute-api.eu-west-1.amazonaws.com"),
};
var transport = new HttpClientTransport(http, disposeClient: true);
// With IHttpClientFactory: services.AddTransient(_ => settings); services.AddHttpClient("petstore").AddHttpMessageHandler<AwsSignatureHandler>();
```

The `host` header is mandatory (a missing one is a 403) and you must sign the query string, not just the path. Signatures are time-boxed (~5 min), so **clock skew** on the host causes `SignatureDoesNotMatch` — sync the clock (NTP). `service` is `execute-api` for API Gateway, `lambda` for Lambda function URLs. Temporary/role credentials carry a `sessionToken`, which the resolved `AWSCredentials` supplies automatically.

Targets the third-party `AwsSignatureVersion4` v5 (the current major; targets .NET 8 / .NET Standard 2.0). Its supported public API is the `AwsSignatureHandler` / `AwsSignatureHandlerSettings` message-handler pair shown (or the signing `HttpClient` extension overloads for direct calls) — there is deliberately no AWS-published dependency here, because the AWS SDK for .NET exposes no supported public SigV4 signer. See the [AwsSignatureVersion4 repo](https://github.com/FantasticFiasco/aws-signature-version-4) and the [AWS SigV4 signing process](https://docs.aws.amazon.com/IAM/latest/UserGuide/reference_sigv.html).

### HMAC request signing (custom shared-secret scheme)

For a bespoke HMAC scheme, sign the request in a `DelegatingHandler`. The **string-to-sign must match the server byte-for-byte** — canonicalize the path/query exactly as the server does:

```csharp
using System.Security.Cryptography;
using System.Text;

sealed class HmacHandler(string keyId, byte[] secret) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        string ts = DateTimeOffset.UtcNow.ToString("O");
        byte[] body = req.Content is null ? [] : await req.Content.ReadAsByteArrayAsync(ct);
        string bodyHash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        string stringToSign = $"{req.Method}\n{req.RequestUri!.PathAndQuery}\n{ts}\n{bodyHash}";
        string sig = Convert.ToBase64String(new HMACSHA256(secret).ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        req.Headers.Add("X-Date", ts);
        req.Headers.TryAddWithoutValidation("Authorization", $"HMAC {keyId}:{sig}");
        return await base.SendAsync(req, ct);
    }
}
```

Include the timestamp header **inside** the signature and validate a tight window server-side to defend against replay — that makes clock skew a live failure mode. Sign the body *hash* so the header stays small, and keep the secret out of source (env / secret store).

No package to pin — this is the built-in `System.Security.Cryptography` (`HMACSHA256` / `SHA256`). See the [`HMACSHA256` docs](https://learn.microsoft.com/dotnet/api/system.security.cryptography.hmacsha256).

### mTLS (client certificate)

There is no header to set — attach the client certificate to the `HttpClientHandler` (reuse the handler / `HttpClient`; a new handler per request exhausts sockets):

```csharp
using System.Security.Cryptography.X509Certificates;
using Corvus.Text.Json.OpenApi.HttpTransport;

var cert = X509CertificateLoader.LoadPkcs12FromFile("client.pfx", "password");  // .NET 9+; else new X509Certificate2(...)
var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual };
handler.ClientCertificates.Add(cert);
var http = new HttpClient(handler) { BaseAddress = new Uri("https://mtls.example.com") };
var transport = new HttpClientTransport(http, disposeClient: true);
```

Combine mTLS with any token/signing provider from above: pass the auth provider as `HttpClientTransport`'s second argument, or set the `HttpClientHandler` as the signing handler's `InnerHandler`. `X509CertificateLoader` is the .NET 9+ replacement for the obsolete `new X509Certificate2(bytes)` constructor; ensure the private key is present in the PFX, and on Linux the cert must have an accessible key.

No package to pin — this is the built-in `HttpClientHandler` / `X509CertificateLoader`. `X509CertificateLoader` requires .NET 9+; on earlier targets use `new X509Certificate2(...)`. See the [`HttpClientHandler.ClientCertificates` docs](https://learn.microsoft.com/dotnet/api/system.net.http.httpclienthandler.clientcertificates).

### API Key (Header)

Kiota provides `ApiKeyAuthenticationProvider` with `KeyLocation.Header`. The Corvus equivalent uses default request headers:

```csharp
services.AddHttpClient("petstore", client =>
{
    client.DefaultRequestHeaders.Add("X-Api-Key", configuration["ApiKey"]);
});
```

### API Key (Query Parameter)

Kiota provides `ApiKeyAuthenticationProvider` with `KeyLocation.QueryParameter`. The Corvus equivalent uses a `DelegatingHandler`:

```csharp
public class ApiKeyQueryHandler(string paramName, string apiKey) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder(request.RequestUri!);
        string separator = string.IsNullOrEmpty(uriBuilder.Query) ? "" : "&";
        uriBuilder.Query = uriBuilder.Query.TrimStart('?') + separator
            + Uri.EscapeDataString(paramName) + "=" + Uri.EscapeDataString(apiKey);
        request.RequestUri = uriBuilder.Uri;
        return base.SendAsync(request, cancellationToken);
    }
}

services.AddTransient(sp => new ApiKeyQueryHandler("api_key", configuration["ApiKey"]!));
services.AddHttpClient("petstore")
    .AddHttpMessageHandler<ApiKeyQueryHandler>();
```

### Basic Authentication

```csharp
services.AddHttpClient("petstore", client =>
{
    byte[] credentials = Encoding.UTF8.GetBytes($"{username}:{password}");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));
});
```

### Anonymous (No Authentication)

Kiota provides `AnonymousAuthenticationProvider`. With Corvus, this is the default — an `HttpClient` with no auth handler:

```csharp
services.AddHttpClient("petstore");

// No auth handler — requests go out without credentials
HttpClient httpClient = httpClientFactory.CreateClient("petstore");
await using HttpClientTransport transport = new(httpClient, baseUri);
```

### Combining Authentication with Resilience

Use `Microsoft.Extensions.Http.Resilience` (Polly v8) to compose authentication with retry policies:

```csharp
services.AddHttpClient("petstore")
    .AddHttpMessageHandler<EntraTokenHandler>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
    });
```

This gives you exponential backoff with jitter, circuit breaking, and request hedging — all composable with your auth handler via the standard `IHttpClientFactory` pipeline.

### Per-Request Authentication Override

Because `HttpClientTransport` wraps a standard `HttpClient`, you can also set auth per-request using `HttpRequestMessage.Options` with a custom handler that reads per-request tokens from the options dictionary — the same pattern used for differentiated auth in multi-tenant scenarios.

### Cookie Authentication

Cookie parameters appear as regular method parameters — the generated code sets the `Cookie` header:

```csharp
// Client: cookie becomes a method parameter
await using CreatePetResponse response = await petsClient.CreatePetAsync(
    session_token: "sess_k7j2m9x4"u8,
    body: ...);
// Wire: Cookie: session_token=sess_k7j2m9x4
```

On the server, cookie presence is validated automatically:

```csharp
// Server: cookie is extracted and validated before your handler runs
public ValueTask<CreatePetResult> HandleCreatePetAsync(
    CreatePetParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    // parameters.SessionToken is guaranteed non-undefined (required cookie)
    string token = (string)parameters.SessionToken;
    // Your auth logic here...
}
```

If the cookie is declared `required: true` and missing, the generated middleware returns 400 Problem Details automatically.

#### Integration with ASP.NET Core Cookie Authentication

In practice, cookie-based APIs typically use ASP.NET Core's cookie authentication middleware. The cookie is issued by a login endpoint and validated on subsequent requests by the framework — your handler receives an already-authenticated `ClaimsPrincipal`.

When the OpenAPI spec declares a security scheme with `type: apiKey` and `in: cookie`, the generator emits a `SecuritySchemes.{Name}KeyName` constant containing the cookie name from the spec. On the server side, this lives on the `ApiEndpointRegistration` class. Use this constant to configure the ASP.NET Core cookie name — keeping your server in sync with the API contract:

**Server setup** — configure cookie auth in the ASP.NET Core pipeline:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Use the generated constant — derived from the OpenAPI securitySchemes definition
        options.Cookie.Name = ApiEndpointRegistration.SecuritySchemes.SessionAuthKeyName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            // API servers return 401, not a redirect
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// Register the generated endpoints (validation runs before auth in the pipeline)
app.MapPetstoreEndpoints(new PetstoreHandlers());
```

**Login endpoint** — issue the cookie via ASP.NET's `SignInAsync`:

```csharp
app.MapPost("/login", async (LoginRequest login, HttpContext context) =>
{
    // Validate credentials (e.g., against a database)
    User? user = await userService.ValidateAsync(login.Username, login.Password);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    // Build claims and sign in — ASP.NET Core sets the cookie automatically
    List<Claim> claims =
    [
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.Role),
    ];

    ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });

    return Results.Ok();
});
```

**Handler** — in your generated handler, the cookie has already been validated by the middleware. Access the authenticated user via `HttpContext`:

```csharp
public ValueTask<CreatePetResult> HandleCreatePetAsync(
    CreatePetParams parameters, JsonWorkspace workspace, CancellationToken ct)
{
    // The cookie was validated by ASP.NET Core's CookieAuthenticationHandler
    // before this handler is reached. Access claims via HttpContext:
    // var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

    // The generated cookie parameter still gives you the raw value if needed
    string rawToken = (string)parameters.SessionToken;

    // Your business logic...
}
```

**How client and server cookie names stay in sync**

Both the client and server generators emit the cookie name as a constant derived from the same OpenAPI spec. The spec is the single source of truth:

| Side | Constant | Value |
|------|----------|-------|
| Server | `ApiEndpointRegistration.SecuritySchemes.SessionAuthKeyName` | `"session_token"` |
| Client | `IApiPetsClient.SecuritySchemes.SessionAuthKeyName` | `"session_token"` |

In the normal login flow, you don't need to reference the client constant explicitly — the server's `Set-Cookie: session_token=...` response header tells `CookieContainer` the name, and it matches automatically. The constant is useful when you need to **manually inject** a cookie (see below).

**Client — integration tests with `WebApplicationFactory`**

ASP.NET Core integration tests use `WebApplicationFactory<T>` to create an in-memory test server. Its `CreateClient()` method returns an `HttpClient` with cookies enabled by default (`HttpClientHandler.UseCookies = true`). Call the login endpoint, and subsequent requests include the cookie automatically:

```csharp
// Arrange: create the test server and a cookie-aware HttpClient
await using var factory = new WebApplicationFactory<Program>();
using var httpClient = factory.CreateClient();

// Act: log in — ASP.NET Core's SignInAsync sets the Set-Cookie header,
// and HttpClient's CookieContainer captures it automatically
using var loginResponse = await httpClient.PostAsJsonAsync("/login",
    new { username = "admin", password = "secret" });
Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);

// All subsequent requests from this HttpClient include the cookie automatically
var petsClient = new ApiPetsClient(new HttpClientTransport(httpClient));
await using var response = await petsClient.ListPetsAsync();
// The generated middleware validates the cookie and your handler runs normally
```

The `CookieContainer` on the underlying `HttpClientHandler` stores cookies from `Set-Cookie` response headers and attaches them to subsequent requests for the same domain — exactly the same way a browser behaves. No manual header extraction is needed.

**Client — production service-to-service with `IHttpClientFactory`**

For production, register a named client with `IHttpClientFactory`. The default `SocketsHttpHandler` has cookies enabled, so the login → capture → replay flow works the same way:

```csharp
// In Startup / Program.cs — register the named client
builder.Services.AddHttpClient("PetstoreApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
});

// At runtime — resolve and use the client
var httpClient = httpClientFactory.CreateClient("PetstoreApi");

// Login — the server's Set-Cookie is captured by the handler's CookieContainer
await httpClient.PostAsJsonAsync("/login", new { username = "svc", password = "secret" });

// Subsequent calls include the cookie automatically
var petsClient = new ApiPetsClient(new HttpClientTransport(httpClient));
await using var response = await petsClient.CreatePetAsync(body: ...);
```

> **Note:** `IHttpClientFactory` pools handlers (default lifetime: 2 minutes). Cookies persist within the handler's lifetime. For long-lived sessions, increase the handler lifetime via `SetHandlerLifetime()` or manage cookies externally.

**Client — manual cookie injection**

If you already have a session token (e.g., from a shared auth service or a config store) and need to inject it without calling a login endpoint, use the generated client constant to ensure the cookie name matches the server's expectation:

```csharp
var cookieContainer = new CookieContainer();
cookieContainer.Add(
    new Uri("https://api.example.com"),
    new Cookie(IApiPetsClient.SecuritySchemes.SessionAuthKeyName, existingToken));

var handler = new HttpClientHandler { CookieContainer = cookieContainer };
using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };

var petsClient = new ApiPetsClient(new HttpClientTransport(httpClient));
await using var response = await petsClient.ListPetsAsync();
```

## Webhooks and Callbacks

OpenAPI specifications can define **webhooks** (top-level, spec-wide notifications) and **callbacks** (per-operation, triggered by runtime expressions). The Corvus code generator supports both with dedicated commands that produce the same output structure as regular client/server generation.

### Understanding the Symmetry

Consider a spec where an API server sends webhook notifications to its clients:

```
┌────────────────┐  POST /subscribe    ┌────────────────┐
│   Client App   │ ──────────────────▶ │   API Server   │
│                │                      │                │
│                │ ◀────────────────── │                │
│   (webhook    │  POST {callbackUrl}  │   (sends       │
│    receiver)  │  "pet was adopted"   │    webhooks)   │
└────────────────┘                      └────────────────┘
```

This creates two new generation scenarios:

| You are building… | You need… | Command |
|---|---|---|
| The **client app** | A server to receive the webhooks | `openapi-callback-server` |
| The **API server** | A client to send the webhooks | `openapi-callback-client` |

### Callback Server — Receiving Webhooks

When your application subscribes to a service's events, you need a server endpoint to receive the callbacks. Generate the server stubs:

```bash
corvusjson openapi-callback-server petstore.json \
    --rootNamespace MyApp.WebhookReceiver \
    --outputPath ./Generated/WebhookReceiver
```

This produces:
- **Handler interfaces** — implement these to process incoming notifications
- **Endpoint registration** — wire up ASP.NET minimal API routes
- **Params/Result types** — schema-validated request bodies and typed responses

Wire it up in your client application:

```csharp
using Corvus.Text.Json;
using MyApp.WebhookReceiver;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

WebhookHandler handler = new();
app.MapApiEndpoints(handler);

app.Run();
```

### Callback Client — Sending Webhooks

When your server needs to notify subscribed clients at their registered callback URLs, generate a typed client:

```bash
corvusjson openapi-callback-client petstore.json \
    --rootNamespace MyApp.WebhookSender \
    --outputPath ./Generated/WebhookSender
```

This produces:
- **Client class** — type-safe methods for each webhook/callback operation
- **Request/Response types** — serialize the notification payload, parse the client's response

Use it in your server's business logic:

```csharp
using Corvus.Text.Json.OpenApi.HttpTransport;
using MyApp.WebhookSender;

// When a pet is adopted, notify the subscriber
using HttpClient httpClient = new() { BaseAddress = new Uri(subscriberCallbackUrl) };
await using HttpClientTransport transport = new(httpClient);
await using ApiWebhooksClient client = new(transport);

await using PetAdoptedWebhookResponse response = await client.PetAdoptedWebhookAsync(
    body: PetAdoptedEvent.Build(petId: "pet-123"u8, adopterId: "user-456"u8));
```

### Runtime Expressions and Context

OpenAPI callback keys use [runtime expressions](https://spec.openapis.org/oas/latest.html#runtime-expressions) like `{$request.body#/callbackUrl}` to specify where values come from. The code generator resolves these automatically — the generated client extracts values from the originating request/response context using typed property navigation.

| Expression | What it resolves to | Generated code |
|---|---|---|
| `$request.body#/callbackUrl` | A JSON Pointer into the request body | `sourceRequest.CallbackUrl` (typed accessor) |
| `$request.query.eventType` | A query parameter from the original request | `sourceRequest.EventType` |
| `$request.header.X-Correlation-Id` | A header from the original request | `sourceRequest.XCorrelationId` |
| `$response.body#/id` | A JSON Pointer into the response body | `response.CreatedBody.Id` |
| `$response.header.Location` | A response header | `response.LocationHeader` |

The generated `Response` struct captures all the context needed to follow links and invoke callbacks. The generated code automatically fills in parameters bound by runtime expressions — you don't write this code yourself; it is emitted by the generator:

```csharp
// This is GENERATED code — the generator creates this method and its
// runtime expression bindings from your OpenAPI spec automatically.
// After creating a subscription, the response carries context for callbacks.
await using CreateSubscriptionResponse response = await client.CreateSubscriptionAsync(
    body: Subscription.Build(callbackUrl: "https://my-app.example.com/hooks"u8, events: ...));

// response.Links gives typed access to linked/callback operations.
// Runtime expressions like $request.body#/callbackUrl are resolved automatically
// from the captured request/response context — no manual wiring needed.
```

This is the same runtime expression infrastructure used by [OpenAPI Links](https://spec.openapis.org/oas/latest.html#link-object) — both callbacks and links share the context-capture mechanism.

### Links

OpenAPI [Links](https://spec.openapis.org/oas/latest.html#link-object) define follow-on operations that can be invoked from a response, with parameters automatically populated from runtime expressions. The code generator supports links on response objects.

When a response declares links, the generated `Response` struct provides typed navigation methods:

```csharp
// Create a pet — the 201 response links to showPetById
await using CreatePetResponse response = await client.CreatePetAsync(
    body: NewPet.Build(name: "Luna"u8, tag: "cat"u8));

// Follow the link — petId is automatically populated from $response.body#/id
response.MatchResult(
    matchCreated: async (pet) =>
    {
        // The generated link method fills parameters from runtime expressions
        await using ShowPetByIdResponse petResponse = await response.CreatedLinks.ShowPetByIdAsync();
        // petId was automatically extracted from the create response body
    },
    matchDefault: (error) => { });
```

The generator captures request and response state in the response struct so that linked operations can resolve their parameter bindings without manual wiring. Parameters bound by `$request.body#/...`, `$request.header.X-...`, `$response.body#/...`, and `$response.header.X-...` expressions are all resolved automatically.

### Example Spec with Webhooks and Callbacks

```json
{
  "openapi": "3.2.0",
  "info": { "title": "Event API", "version": "1.0.0" },
  "paths": {
    "/subscriptions": {
      "post": {
        "operationId": "createSubscription",
        "callbacks": {
          "onEvent": {
            "{$request.body#/callbackUrl}": {
              "post": {
                "operationId": "onEventCallback",
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/Event" }
                    }
                  }
                },
                "responses": { "200": { "description": "Received" } }
              }
            }
          }
        }
      }
    }
  },
  "webhooks": {
    "statusChange": {
      "post": {
        "operationId": "statusChangeWebhook",
        "requestBody": {
          "content": {
            "application/json": {
              "schema": { "$ref": "#/components/schemas/StatusChange" }
            }
          }
        },
        "responses": { "200": { "description": "Acknowledged" } }
      }
    }
  }
}
```

## CLI Reference

### Client Generation

```bash
corvusjson openapi-client <spec-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `--rootNamespace` | Root namespace for generated types | Derived from spec title |
| `--outputPath` | Output directory | `./` |
| `--clientName` | Prefix for generated client type names | Derived from spec title |
| `--force` | Regenerate even if lock file is current | `false` |
| `--spec-url` | Original URL of the spec (recorded in lock file for update-style re-fetch) | — |
| `--include-path` | Glob patterns for paths to include (repeatable or comma-separated) | All paths |
| `--exclude-path` | Glob patterns for paths to exclude (repeatable or comma-separated) | None |
| `--specVersion` | Override auto-detected OpenAPI version (`3.0`, `3.1`, or `3.2`) | Auto-detected |
| `--ignoreEmptyFormUrlEncodedBody` | Treat form-urlencoded bodies with no schema properties as absent | `false` |

### Server Generation

```bash
corvusjson openapi-server <spec-path> [options]
```

Same options as client generation. The output includes handler interfaces, endpoint registration, and shared model types.

### Callback Server Generation

```bash
corvusjson openapi-callback-server <spec-path> [options]
```

Generates server stubs for **webhooks and callbacks** defined in the spec. Use this when you are building a *client* application that needs to receive webhook/callback notifications from a service.

Same options as `openapi-server`. Only webhook and per-operation callback path-items are processed — the main `paths` object is ignored.

### Callback Client Generation

```bash
corvusjson openapi-callback-client <spec-path> [options]
```

Generates a typed HTTP client for invoking **webhooks and callbacks** defined in the spec. Use this when you are building the *server* side of an API and need to call back into client-provided endpoints.

Same options as `openapi-client`. Only webhook and per-operation callback path-items are processed.

### Inspecting Operations

```bash
corvusjson openapi-show <spec-path> [options]
```

Displays the operation tree of an OpenAPI specification. Useful for previewing which operations a filter will select before running generation.

| Option | Description | Default |
|--------|-------------|---------|
| `--include-path` | Glob patterns for paths to include | All paths |
| `--exclude-path` | Glob patterns for paths to exclude | None |
| `--group-by` | Group operations by `path` or `tag` | `path` |
| `--specVersion` | Override auto-detected OpenAPI version | Auto-detected |

### Path Filtering

For large specs, use `--include-path` and `--exclude-path` to generate code for a subset of operations. Filters apply to both client and server generation and to `openapi-show`.

**Pattern syntax:**

| Pattern | Matches |
|---------|---------|
| `/pets` | Exactly `/pets` |
| `/pets/*` | One segment after `/pets/` (e.g., `/pets/{petId}`) |
| `/pets/**` | Any depth under `/pets/` (e.g., `/pets/{petId}/toys/{toyId}`) |
| `{param}` | Any path parameter segment (e.g., `/pets/{petId}` matches `/pets/*`) |

Patterns are case-insensitive. Include patterns are additive (union). Exclude patterns subtract from the include set. If no include patterns are specified, all paths are included.

**Examples:**

```bash
# Generate only the /pets endpoints (including nested paths like /pets/{petId})
corvusjson openapi-client petstore.json --include-path "/pets/**"

# Generate everything except admin endpoints
corvusjson openapi-client petstore.json --exclude-path "/admin/**"

# Combine: include /pets and /store, but exclude store admin
corvusjson openapi-client petstore.json \
    --include-path "/pets/**" --include-path "/store/**" \
    --exclude-path "/store/admin/**"

# Multiple patterns via comma-separated values
corvusjson openapi-client petstore.json --include-path "/pets/**,/users/**"

# Preview what will be generated before running
corvusjson openapi-show petstore.json --include-path "/pets/**"
```

**Workflow:** Use `openapi-show` with filters to preview, then apply the same filter to `openapi-client` or `openapi-server`:

```bash
# 1. See what's in the spec
corvusjson openapi-show petstore.json --group-by tag

# 2. Preview filtered subset
corvusjson openapi-show petstore.json --include-path "/pets/**"

# 3. Generate only that subset
corvusjson openapi-client petstore.json --include-path "/pets/**" \
    --rootNamespace MyApp.Pets --outputPath ./Generated/Pets
```

Only operations matching the filter are generated. Model types referenced by filtered operations are still included — filtering is at the operation level, not the schema level.

**Real-world example: Stripe Payments subset**

[Stripe publishes their OpenAPI spec](https://github.com/stripe/openapi) at `openapi/spec3.json` — it contains 300+ endpoints. You can generate a focused client for just the payment processing operations:

```bash
# Download the Stripe spec (or use --spec-url for automatic re-fetch)
corvusjson openapi-client spec3.json \
    --spec-url "https://raw.githubusercontent.com/stripe/openapi/master/openapi/spec3.json" \
    --include-path "/v1/payment_intents/**,/v1/charges/**,/v1/refunds/**" \
    --rootNamespace MyApp.Stripe.Payments \
    --outputPath ./Generated/StripePayments \
    --ignoreEmptyFormUrlEncodedBody

# Or preview what that includes first
corvusjson openapi-show spec3.json \
    --include-path "/v1/payment_intents/**,/v1/charges/**,/v1/refunds/**"
```

The `--ignoreEmptyFormUrlEncodedBody` flag is recommended for Stripe because their spec declares `application/x-www-form-urlencoded` request bodies on every operation — even those with no body properties (e.g. `GET /v1/charges`). Without this flag, the generated client methods include a redundant `body` parameter.

You can split a large spec into multiple domain-specific clients:

```bash
# Payments domain
corvusjson openapi-client spec3.json \
    --include-path "/v1/payment_intents/**,/v1/charges/**,/v1/refunds/**" \
    --rootNamespace MyApp.Stripe.Payments --outputPath ./Generated/Payments

# Customer domain
corvusjson openapi-client spec3.json \
    --include-path "/v1/customers/**,/v1/subscriptions/**" \
    --rootNamespace MyApp.Stripe.Customers --outputPath ./Generated/Customers

# Billing domain (everything except one-off payments)
corvusjson openapi-client spec3.json \
    --include-path "/v1/invoices/**,/v1/plans/**,/v1/prices/**" \
    --rootNamespace MyApp.Stripe.Billing --outputPath ./Generated/Billing
```

Each produces an independent client with only the operations and models relevant to that domain.

## Lock File and Regeneration

The generator writes a `corvusjson-openapi.lock` file alongside the output. On subsequent runs, if the spec hasn't changed, generation is skipped. Use `--force` to regenerate unconditionally.

## Version Support

| OpenAPI Version | Detected From | Notes |
|---|---|---|
| 3.2 | `"openapi": "3.2.0"` | Full support including `itemSchema` for streaming |
| 3.1 | `"openapi": "3.1.x"` | Full support; JSON Schema 2020-12 for models |
| 3.0 | `"openapi": "3.0.x"` | Full support; JSON Schema Draft 4 subset for models |

The generator auto-detects the version from the `openapi` field. All three versions produce the same client/server API surface — only the internal model schemas differ.

## Best Practices

1. **Use `u8` literals for scalar parameters** — `petId: "abc"u8`, `name: "Fido"u8`. Implicit conversions handle encoding and avoid UTF-16→UTF-8 transcoding.
2. **Use `Builder.Create()` for request/response bodies** — mirrors schema properties with typed parameters.
3. **Always `await using` responses and transports** — they hold pooled memory that must be returned.
4. **Prefer `MatchResult` for response handling** — ensures exhaustive coverage of all status codes.
5. **Leave `validationMode` at `Basic`** for development; consider `None` for production hot paths.
6. **Pass the `workspace` through** on the server — it enables efficient pooled-memory JSON building.
7. **Let the middleware validate** — don't re-validate inputs the generated code already checked.

## Comparison with Kiota

[Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) is the most widely-used OpenAPI client generator for .NET. This section compares the two generators across architecture, features, and performance.

### Architecture

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Generation model | CLI tool (`corvusjson openapi-client`) | CLI tool (`kiota generate`) |
| Runtime dependency | `Corvus.Text.Json.OpenApi` (thin transport abstraction) | `Microsoft.Kiota.Bundle` (abstractions + HTTP + serialization + auth) |
| Serialization | Zero-copy over pooled JSON document (no POCO hydration) | POCO model classes with `IParsable` self-deserialization |
| Memory model | Struct-based types backed by pooled `byte[]` — GC-free hot path | Class-based models allocated per response |
| Transport abstraction | `IApiTransport` (4 overloads: no-body, typed, stream, writer) | `IRequestAdapter` wrapping `HttpClient` with middleware pipeline |
| Schema validation | Built-in, configurable per-request (`None`/`Basic`/`Detailed`) | None — no schema validation support |

### Feature Support

| Feature | Corvus | Kiota |
|---------|--------|-------|
| OpenAPI 3.0 | ✅ | ✅ |
| OpenAPI 3.1 | ✅ | ✅ |
| OpenAPI 3.2 | ✅ | Partial |
| JSON request/response bodies | ✅ | ✅ |
| Form-urlencoded bodies | ✅ | ❌ |
| Multipart/mixed bodies | ✅ | Limited |
| Binary upload (octet-stream) | ✅ | ✅ |
| Server stub generation | ✅ | ❌ |
| Webhooks (top-level, OA 3.1+) | ✅ | ❌ ([confirmed by team](https://github.com/microsoft/kiota/issues/6394)) |
| Per-operation callbacks | ✅ | ❌ (no tracking issue) |
| OpenAPI Links (typed follow-on ops) | ✅ | ❌ (no tracking issue) |
| Runtime expression resolution | ✅ | ❌ (not applicable — no callbacks/links) |
| Typed response headers | ✅ | ❌ |
| Cookie parameters | ✅ | ❌ |
| Schema validation | ✅ (None/Basic/Detailed) | ❌ |
| Per-operation OAuth2 scopes | ✅ (generated constants) | ❌ (must hardcode) |
| Error response discrimination | ✅ (typed `MatchResult`) | ✅ (exception-based) |
| Middleware pipeline | Platform-native (`IHttpClientFactory` + Polly) | Built-in `DelegatingHandler` chain |
| Multiple languages | C# only | C#, Java, Go, TypeScript, Python, PHP, Ruby, Swift, CLI |
| IDE autocompletion | ✅ (pre-generated files) | ✅ (pre-generated files) |

### Response Handling

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Return type | Value-type response struct with `IAsyncDisposable` | Nullable POCO or `Task<T?>` |
| Status discrimination | `MatchResult()` with typed handlers per status code | Exception-based (`ApiException` subclasses) |
| Memory lifetime | Response owns pooled document; disposed via `await using` | GC-managed; no explicit lifetime |
| Response headers | Generated typed properties (lazy-parsed) | Not generated — must use `NativeResponseHandler` |

### Middleware and Resilience

Kiota ships its own `DelegatingHandler` pipeline with default handlers for retry, redirect, user-agent injection, and header inspection.

Corvus uses platform-native .NET patterns instead:

| Concern | Corvus | Kiota |
|---------|--------|-------|
| Retry | `Microsoft.Extensions.Http.Resilience` (Polly v8) | Built-in `RetryHandler` (exponential backoff) |
| Redirect | `SocketsHttpHandler.AllowAutoRedirect` (platform) | Built-in `RedirectHandler` |
| Observability | OpenTelemetry `HttpClient` instrumentation | Custom `Activity` tracing in handlers |
| Composition | `IHttpClientFactory.AddHttpMessageHandler()` | `KiotaClientFactory.CreateDefaultHandlers()` |

This means Corvus doesn't reimplement retry/redirect logic that the platform already provides, and users can mix any community middleware (Polly, OpenTelemetry, logging) without learning a framework-specific handler API.

The trade-off is that these middleware patterns are tied to `HttpClient` — if you swap `HttpClientTransport` for a non-HTTP transport (e.g., in-process or message queue), the `DelegatingHandler` pipeline no longer applies. Corvus has no transport-independent middleware abstraction; Kiota's built-in handler chain works regardless of the underlying transport adapter.

### Performance

All benchmarks use mock transports (no real I/O) measuring the full client pipeline: construct request → serialize → transport → parse response → typed access. "Corvus + Validation" uses `ValidationMode.Basic` — boolean pass/fail schema validation of both request and response bodies (no error-location collection).

| Operation | Corvus | Corvus + Basic Validation | Kiota | Speed (×Kiota) | Alloc (×Kiota) |
|-----------|-------:|--------------------------:|------:|:--------------:|:--------------:|
| GET /pets (10-item array) | 1,342 ns | 3,059 ns | 6,088 ns | **4.5×** / **2.0×** | **17×** |
| GET /pets/{petId} | 409 ns | 610 ns | 2,290 ns | **5.6×** / **3.8×** | **14×** |
| POST /pets (JSON body) | 434 ns | 701 ns | 2,910 ns | **6.7×** / **4.2×** | **15×** |
| PUT /pets/{petId} (form body) | 536 ns | 833 ns | 2,957 ns | **5.5×** / **3.5×** | **12×** |
| Response header (x-next) | 472 ns | — | N/A | — | — |

*Speed column: first number = Corvus (no validation) vs Kiota; second = Corvus + Basic validation vs Kiota. Alloc column: Kiota allocation ÷ Corvus allocation.*

Corvus **with full schema validation** is **2–4× faster** than Kiota **without any validation**, and allocates **12–17× less memory**.

Validation mode overhead (GET /pets, 3-item array):

| Mode | Mean | vs None | Alloc |
|------|-----:|--------:|------:|
| None | 616 ns | 1.0× | 1,044 B |
| Basic (boolean pass/fail) | 1,272 ns | 2.1× | 1,044 B |
| Detailed (error locations) | 2,039 ns | 3.3× | 1,044 B |

All validation modes produce zero additional allocation.

> *BenchmarkDotNet v0.15.8, .NET 10.0.8, 13th Gen Intel Core i7-13800H, Windows 11. OutlierMode=RemoveAll, RunStrategy=Throughput.*

### When to Choose Corvus

- Performance-critical services where latency and allocation matter
- APIs requiring request/response validation without external tooling
- .NET-only projects wanting zero-copy JSON and compile-time safety
- APIs using form-urlencoded, multipart/mixed, or cookie parameters
- Scenarios requiring typed response header access
- Projects needing both client and server from the same spec
- APIs with webhooks or callbacks that need both receiver stubs and sender clients
- OAuth2-secured APIs where you want spec-driven scope management (Corvus generates per-operation scope constants from `security` requirements; Kiota requires manual hardcoding)
- Specs using OpenAPI Links for typed operation chaining (Corvus generates follow-on methods with automatic runtime expression resolution)

### When to Choose Kiota

- Multi-language projects needing consistent client generation across platforms
- Teams wanting a self-contained middleware pipeline without configuring `IHttpClientFactory`
- Projects where POCO-style models are preferred over struct-based types
- APIs where performance is not the primary concern

## Example Recipes

| Recipe | What it shows |
|--------|---------------|
| [029-OpenApiClient](../ExampleRecipes/029-OpenApiClient/) | Basic client: list, create, show with MatchResult |
| [030-OpenApiServer](../ExampleRecipes/030-OpenApiServer/) | Basic server: handler interface + endpoint registration |
| [031-OpenApiAdvancedClient](../ExampleRecipes/031-OpenApiAdvancedClient/) | Advanced: streaming, binary, forms, cookies, deep-object queries |
| [032-OpenApiAdvancedServer](../ExampleRecipes/032-OpenApiAdvancedServer/) | Advanced server: all parameter styles, streaming, uploads |
| [033-OpenApiEndToEnd](../ExampleRecipes/033-OpenApiEndToEnd/) | Full round-trip: generated client calls generated server over real HTTP |
| [034-OpenApiCallbackServer](../ExampleRecipes/034-OpenApiCallbackServer/) | Webhook receiver: callback server stubs for client-side webhook handling |
| [035-OpenApiCallbackClient](../ExampleRecipes/035-OpenApiCallbackClient/) | Webhook sender: callback client for server-side webhook delivery |