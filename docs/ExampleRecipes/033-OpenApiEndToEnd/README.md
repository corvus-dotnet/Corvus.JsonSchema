# 033 — OpenAPI End-to-End: Generated Client Calling Generated Server

This recipe demonstrates the **full round-trip** of the Corvus.Text.Json OpenAPI code generation
system: a generated HTTP client calls a generated HTTP server over real TCP, with both sides
generated from the same OpenAPI specification.

## What This Shows

| Feature | Where |
|---------|-------|
| Server-side parameter deserialization (path, query, header, cookie) | Generated `ApiEndpointRegistration` |
| Server-side request body schema validation (JSON + form-encoded) | Generated middleware |
| Server-side response body schema validation | `result.ValidateBody()` |
| Client-side request building and serialization | Generated `ApiPetsClient`, `ApiAdoptionClient` |
| Client-side typed response parsing with `MatchResult()` | Response dispatch |
| Form URL-encoded request/response round-trip | Scenario 5 |
| Streaming SSE and NDJSON responses | Scenario 6 |
| ProblemDetails error responses for validation failures | Scenarios 7–8 |

## Running It

```bash
dotnet run --project docs/ExampleRecipes/033-OpenApiEndToEnd
```

The application starts an ASP.NET server on a random port, runs 8 scenarios, writes the generated client/server round-trip to the console, and exits. The launch profile is configured not to open a browser because the sample is a console-driven walkthrough.

## How It Works

### Server Side

The code generator produces:

- **Handler interfaces** (`IApiPetsHandler`, `IApiAdoptionHandler`, etc.) — you implement these
  with your business logic.
- **`ApiEndpointRegistration.MapApiEndpoints()`** — wires all routes with:
  - Parameter parsing (path, query, header, cookie) with type coercion
  - Schema validation of request parameters and bodies
  - Response body validation before serialization
  - Proper HTTP status codes and `Content-Type` headers

You write only the handler:

```csharp
app.MapApiEndpoints(handler, handler, handler, handler);
```

### Client Side

The code generator produces:

- **Client classes** (`ApiPetsClient`, `ApiAdoptionClient`) — type-safe methods for each operation
- **Request/Response types** — handle serialization, query string building, cookie injection
- **`MatchResult()`** — exhaustive pattern matching over all possible response status codes

You write only the call:

```csharp
await using CreatePetResponse response = await petsClient.CreatePetAsync(
    session_token: "admin-token-123"u8,
    body: new NewPet.Source(...));

response.MatchResult<string>(
    matchCreated: (pet) => pet.ToString(),
    matchUnauthorized: (error) => ...,
    matchDefault: (error) => ...);
```

## Scenarios

1. **Create a pet** — POST with cookie auth + JSON body → 201
2. **List pets with filters** — GET with deep-object query, array query, required header → 200 + response headers
3. **Show pet by ID** — GET with path parameter → 200
4. **Show non-existent pet** — GET → 404 typed error
5. **Submit adoption** — POST with `application/x-www-form-urlencoded` → 202 JSON response
6. **Streaming responses** — Generated client reads SSE chat chunks and NDJSON activity events
7. **Missing required header** — Raw GET without `x-request-id` → 400 ProblemDetails
8. **Missing cookie** — Raw POST without `session_token` → 400 ProblemDetails

## Regenerating

Both client and server are generated from `petstore-extended.json`:

```bash
# Client
corvusjson openapi-client petstore-extended.json \
    --outputPath Generated/Client \
    --rootNamespace Petstore.EndToEnd.Client

# Server
corvusjson openapi-server petstore-extended.json \
    --outputPath Generated/Server \
    --rootNamespace Petstore.EndToEnd.Server
```
