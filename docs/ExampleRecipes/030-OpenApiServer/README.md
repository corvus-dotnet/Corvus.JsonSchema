# JSON Schema Patterns in .NET — OpenAPI Server Generation

This recipe demonstrates how to generate and implement an ASP.NET Core minimal API server from an [OpenAPI 3.2](https://spec.openapis.org/oas/v3.2.0) specification using `corvusjson openapi-server`. The example uses the same [Petstore API](https://github.com/OAI/OpenAPI-Specification) spec as the client recipe.

> **Version compatibility:** This example targets OpenAPI 3.2, but the same workflow applies to OpenAPI 3.1 and 3.0 specifications.

## What the Generator Does For You

When you run `corvusjson openapi-server`, the generator reads your OpenAPI spec and produces:

| Generated artifact | What it handles |
|---|---|
| **Handler interface** (`IApiPetsHandler`) | One async method per operation — you implement your business logic here |
| **Endpoint registration** (`ApiEndpointRegistration`) | Maps all routes to ASP.NET Core minimal API endpoints with the correct HTTP methods and path templates |
| **Params structs** (`ListPetsParams`, etc.) | Strongly-typed, schema-validated request parameters (path, query, header, cookie) and request bodies |
| **Result structs** (`ListPetsResult`, etc.) | Factory methods for each declared response status code, with typed body builders and response headers |
| **Model types** (`Pet`, `NewPet`, `Pets`, `Error`) | Same strongly-typed JSON Schema models as the client |

The generated middleware handles:
- **Parameter deserialization** — query strings, path segments, headers, and cookies are parsed into typed values
- **Request body parsing** — JSON bodies are deserialized into pooled-memory documents
- **Schema validation** — all parameters and bodies are validated; failures return `400 Problem Details` automatically
- **Response serialization** — your typed Result is serialized to the HTTP response with correct status code and Content-Type
- **Response headers** — typed header values are written to the response

## Generating the Server

```bash
corvusjson openapi-server petstore.json \
    --rootNamespace Petstore.Server \
    --outputPath ./Generated
```

This produces:
- `Generated/IApiPetsHandler.cs` — the handler interface
- `Generated/ApiEndpointRegistration.cs` — minimal API route mapping
- `Generated/ListPetsParams.cs`, `CreatePetParams.cs`, `ShowPetByIdParams.cs` — parameter structs
- `Generated/ListPetsResult.cs`, `CreatePetResult.cs`, `ShowPetByIdResult.cs` — result structs
- `Generated/Models/` — strongly-typed JSON Schema models

## Implementing the Server

### Registering endpoints

A single extension method wires all routes:

```csharp
using Petstore.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

PetsHandler handler = new();
app.MapApiEndpoints(handler);

app.Run();
```

### Implementing the handler

Each handler method receives parsed, validated parameters and a `JsonWorkspace` for building responses:

```csharp
internal sealed class PetsHandler : IApiPetsHandler
{
    public ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Parameters are already validated — limit <= 100 is guaranteed
        int limit = parameters.Limit.IsNotUndefined()
            ? (int)parameters.Limit
            : 20;

        // Build response using typed Result factory + array builder
        ListPetsResult result = ListPetsResult.Ok(
            body: new Pets.Source((ref Pets.Builder b) =>
            {
                b.AddItem(new Pet.Source((ref Pet.Builder pb) =>
                {
                    pb.Create(id: 1, name: "Luna"u8, tag: "cat"u8);
                }));
            }),
            workspace: workspace);

        return ValueTask.FromResult(result);
    }
}
```

### Returning typed responses

Each Result struct has factory methods matching the spec's response definitions:

```csharp
// 201 Created with a typed body
CreatePetResult.Created(body: new Pet.Source(...), workspace: workspace)

// Default error with custom status code
ShowPetByIdResult.Default(statusCode: 404, body: new Error.Source(...), workspace: workspace)

// 200 Ok with response headers
ListPetsResult.Ok(body: ..., workspace: workspace, xNext: paginationLink)
```

### Automatic validation and error handling

The generated middleware validates all inputs before your handler runs:

| Invalid input | Generated behaviour |
|---|---|
| Query param fails schema (e.g., `limit=200` exceeds `maximum: 100`) | 400 Problem Details — handler never called |
| Required path param missing | 400 Problem Details |
| Request body fails JSON parse | 400 Problem Details |
| Request body fails schema validation | 400 Problem Details |

You never write validation code — the generated middleware handles it. Your handler only runs with valid, typed data.

## Best Practices

1. **Use `u8` literals in builders** — `name: "Fido"u8` avoids UTF-16→UTF-8 transcoding.
2. **Use `Builder.Create()` for response bodies** — mirrors schema properties with typed parameters.
3. **Return appropriate Result factory** — `Ok`, `Created`, `Default` match your spec's response definitions.
4. **Pass through the `workspace`** — the generated code uses it for efficient pooled-memory JSON building.
5. **Let the middleware validate** — don't re-validate inputs that the generated code already checked.
6. **Use `ValueTask.FromResult`** — for synchronous handlers, wrap the result efficiently.