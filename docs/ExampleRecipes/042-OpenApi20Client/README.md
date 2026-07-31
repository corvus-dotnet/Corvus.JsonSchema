# JSON Schema Patterns in .NET — OpenAPI 2.0 (Swagger) Client Generation

This recipe demonstrates how to generate and use a strongly-typed HTTP client from an [OpenAPI 2.0 (Swagger)](https://spec.openapis.org/oas/v2.0.html) specification using `corvusjson openapi-client`. Many real-world APIs still publish Swagger 2.0 documents; the generator consumes them natively — no up-conversion step — and produces the same client shape as the 3.x generators.

> **Version compatibility:** The generator auto-detects the spec version from the `swagger: "2.0"` field, exactly as it detects `openapi` for 3.x documents. See [029-OpenApiClient](../029-OpenApiClient/) for the OpenAPI 3.2 version of this recipe.

## What Is Different About 2.0

OpenAPI 2.0 predates the `requestBody`/`content` model of 3.x. The generator lowers the 2.0 shapes onto the same generated-client architecture:

| 2.0 construct | What the generator does |
|---|---|
| `in: body` parameter | Becomes the typed request body, using the operation's effective `consumes` (default `application/json`) |
| `in: formData` parameters | Aggregated into a synthesized `<Operation>FormBody` model type, serialized as `application/x-www-form-urlencoded` or `multipart/form-data` per `consumes` |
| Parameter Object as schema | 2.0 puts `type`/`maximum`/`enum` directly on the parameter (no nested `schema`); the generator reads the Parameter Object itself as a draft-04-style schema |
| `collectionFormat` | `csv`/`ssv`/`pipes`/`tsv` become delimiter-joined values; `multi` repeats the query key |
| `host` + `basePath` + `schemes` | Combined into the server URI (https preferred when both schemes are listed) |
| `#/definitions/...` | Model types, same as 3.x `#/components/schemas/...` |
| Response `schema` × `produces` | Typed response bodies per status code |
| `securityDefinitions` | Flows into the request security requirements |

## The Spec

File: `petstore-2.0.json`

The Petstore example expressed as Swagger 2.0. The interesting 2.0-specific parts:

```json
{
  "swagger": "2.0",
  "info": { "title": "Petstore", "version": "1.0.0" },
  "host": "petstore.example.com",
  "basePath": "/v1",
  "schemes": ["https"],
  "consumes": ["application/json"],
  "produces": ["application/json"],
  "paths": {
    "/pets": {
      "get": {
        "operationId": "listPets",
        "parameters": [
          { "name": "limit", "in": "query", "type": "integer", "maximum": 100, "format": "int32" },
          { "name": "tags", "in": "query", "type": "array", "items": { "type": "string" }, "collectionFormat": "csv" }
        ]
      },
      "post": {
        "operationId": "createPet",
        "parameters": [
          { "name": "body", "in": "body", "required": true, "schema": { "$ref": "#/definitions/NewPet" } }
        ]
      }
    },
    "/pets/{petId}": {
      "post": {
        "operationId": "updatePetWithForm",
        "consumes": ["application/x-www-form-urlencoded"],
        "parameters": [
          { "name": "petId", "in": "path", "required": true, "type": "string" },
          { "name": "name", "in": "formData", "required": true, "type": "string" },
          { "name": "status", "in": "formData", "type": "string", "enum": ["available", "pending", "sold"] }
        ]
      }
    }
  },
  "definitions": {
    "Pet": { "type": "object", "required": ["id", "name"], "properties": { "id": { "type": "integer", "format": "int64" }, "name": { "type": "string" }, "tag": { "type": "string" } } },
    "NewPet": { "type": "object", "required": ["name"], "properties": { "name": { "type": "string" }, "tag": { "type": "string" } } }
  }
}
```

Note that `limit` carries its constraints (`type`, `maximum`, `format`) directly on the Parameter Object — there is no nested `schema` for non-body parameters in 2.0.

## Generating the Client

```bash
corvusjson openapi-client petstore-2.0.json \
    --rootNamespace Petstore.V2.Client \
    --outputPath ./Generated
```

The `swagger: "2.0"` field is auto-detected; `--specVersion 2.0` forces it explicitly. This produces the same artifact shape as the 3.x generators:

- `Generated/ApiPetsClient.cs` / `IApiPetsClient.cs` — the client and its interface
- `Generated/*Request.cs`, `*Response.cs` — request/response structs per operation
- `Generated/Models/` — strongly-typed JSON Schema models, including the synthesized `UpdatePetWithFormFormBody`
- `Generated/corvusjson-openapi.lock` — lock file for incremental regeneration

## Using the Client

### The server URI comes from host + basePath + schemes

```csharp
HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v1") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);
ApiPetsClient client = new(transport);
```

The generated request structs also expose `CreateServerUri()`, which assembles `https://petstore.example.com/v1` from the spec's `schemes`, `host`, and `basePath`.

### Array query parameters with collectionFormat

`tags` declares `collectionFormat: csv`, so the generated code joins the values with commas into a single query key — `?limit=10&tags=dog,friendly`:

```csharp
await using ListPetsResponse listResponse = await client.ListPetsAsync(
    limit: 10,
    tags: new GetPetsTags.Source(static (ref GetPetsTags.Builder b) =>
    {
        b.AddItem("dog"u8);
        b.AddItem("friendly"u8);
    }));
```

(`ssv`, `pipes`, and `tsv` join with space, `|`, and tab respectively; `multi` repeats the key: `?tags=dog&tags=friendly`.)

### Body parameters

The 2.0 `in: body` parameter behaves exactly like a 3.x `requestBody` — a typed model with a builder:

```csharp
await using CreatePetResponse createResponse = await client.CreatePetAsync(
    body: NewPet.Build(name: "Fido"u8, tag: "dog"u8));
```

### formData parameters — the synthesized form body

The `updatePetWithForm` operation declares its inputs as `in: formData` parameters. The generator aggregates them into a single `UpdatePetWithFormFormBody` model type, so form fields get the same typed-builder experience as JSON bodies:

```csharp
await using UpdatePetWithFormResponse updateResponse = await client.UpdatePetWithFormAsync(
    petId: "pet-123"u8,
    body: UpdatePetWithFormFormBody.Build(name: "Fido II"u8, status: "sold"u8));
```

On the wire the body is form-encoded per the operation's `consumes`:

```
POST /v1/pets/pet-123 HTTP/1.1
Content-Type: application/x-www-form-urlencoded

name=Fido+II&status=sold
```

The `status` field's `enum` constraint is enforced by validation before the request is sent.

### Response handling

Responses use the same `MatchResult` pattern as every other generated client — exhaustive handling of the status codes declared in the spec:

```csharp
listResponse.MatchResult(
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

// Typed response header access — 2.0 response headers come from the
// Response Object's `headers` map
JsonString nextPage = listResponse.XNextHeader;
```

### Request validation

2.0 parameter constraints (`maximum`, `enum`, `pattern`, ...) sit directly on the Parameter Object and flow into the generated schema types unchanged:

```csharp
try
{
    // Throws: limit > 100 violates the parameter's "maximum: 100" constraint
    await using ListPetsResponse _ = await client.ListPetsAsync(
        limit: 200,
        validationMode: ValidationMode.Detailed);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
}
```

## Running

```bash
dotnet build
dotnet run
```

The runnable recipe uses an in-memory `IApiTransport` so it produces deterministic console output without needing the fictional `petstore.example.com` host to exist. In production code, replace the demo transport with `HttpClientTransport` configured with your API server's base address.

## Best Practices

1. **Let the generator auto-detect the version** — `swagger: "2.0"` dispatches to the 2.0 generator; `--specVersion` is available when you need to force it.
2. **Use `u8` literals for scalar parameters and form fields** — the implicit conversions handle encoding and avoid UTF-16→UTF-8 transcoding.
3. **Treat the synthesized `<Operation>FormBody` like any other model** — build it with `Build(...)`, validate it, and reuse it across calls.
4. **Always `await using` responses** — they hold pooled memory that must be returned.
5. **Prefer `MatchResult` for response handling** — it ensures exhaustive coverage of all status codes defined in the spec.

## Related Documentation

- [OpenAPI client and server generation](../../OpenApi.md) — including the OpenAPI 2.0 notes section
- [029-OpenApiClient](../029-OpenApiClient/) — the OpenAPI 3.2 version of this recipe