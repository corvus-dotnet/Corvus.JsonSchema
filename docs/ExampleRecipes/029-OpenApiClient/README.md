# JSON Schema Patterns in .NET — OpenAPI Client Generation

This recipe demonstrates how to generate and use a strongly-typed HTTP client from an [OpenAPI 3.2](https://spec.openapis.org/oas/v3.2.0) specification using `corvusjson openapi-client`. The example uses the [Petstore API](https://github.com/OAI/OpenAPI-Specification) — the canonical example from the OpenAPI Initiative's specification.

> **Version compatibility:** This example targets OpenAPI 3.2, but the same workflow applies to OpenAPI 3.1 and 3.0 specifications. The generator auto-detects the spec version from the `openapi` field.

## What the Generator Does For You

When you run `corvusjson openapi-client`, the generator reads your OpenAPI spec and produces:

| Generated artifact | What it handles |
|---|---|
| **Client class** (`ApiPetsClient`) | Orchestrates the request lifecycle: builds parameters, validates against JSON Schema, sends via transport, parses response |
| **Request structs** (`ListPetsRequest`, etc.) | Serializes path/query/header/cookie parameters into the correct wire format (URI-encoding, `application/x-www-form-urlencoded`, header values) |
| **Response structs** (`ListPetsResponse`, etc.) | Parses the HTTP response body into typed models, exposes `TryGet*` and `MatchResult` patterns for exhaustive status code handling |
| **Model types** (`Pet`, `NewPet`, `Pets`, `Error`) | Strongly-typed JSON Schema models with full validation, zero-allocation access, and builder patterns |
| **Interface** (`IApiPetsClient`) | Contract for dependency injection and testing |

The generated code handles:
- **Parameter serialization** — path parameters are URI-encoded; query strings use `?key=value&...` format; headers are written per RFC 7230
- **Request validation** — parameters and bodies are validated against their JSON Schema *before* the HTTP call leaves your process
- **Response parsing** — the body is deserialized into a pooled-memory document with typed access to every field
- **Response headers** — typed accessors for declared response headers (e.g., `x-next` pagination link)
- **Discriminated response handling** — `TryGetOk`/`TryGetDefault` for simple checks, `MatchResult` for exhaustive pattern matching

## The Spec

File: `petstore.json`

This is the OpenAPI Initiative's Petstore example expressed as OpenAPI 3.2:

```json
{
  "openapi": "3.2.0",
  "info": { "title": "Petstore", "version": "1.0.0" },
  "servers": [{ "url": "https://petstore.example.com/v1" }],
  "paths": {
    "/pets": {
      "get": {
        "operationId": "listPets",
        "parameters": [{ "name": "limit", "in": "query", "schema": { "type": "integer", "maximum": 100, "format": "int32" } }],
        "responses": {
          "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pets" } } }, "headers": { "x-next": { "schema": { "type": "string" } } } },
          "default": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Error" } } } }
        }
      },
      "post": {
        "operationId": "createPet",
        "requestBody": { "required": true, "content": { "application/json": { "schema": { "$ref": "#/components/schemas/NewPet" } } } },
        "responses": {
          "201": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } },
          "default": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Error" } } } }
        }
      }
    },
    "/pets/{petId}": {
      "get": {
        "operationId": "showPetById",
        "parameters": [{ "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } }],
        "responses": {
          "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } },
          "default": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Error" } } } }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "Pet": { "type": "object", "required": ["id", "name"], "properties": { "id": { "type": "integer", "format": "int64" }, "name": { "type": "string" }, "tag": { "type": "string" } } },
      "NewPet": { "type": "object", "required": ["name"], "properties": { "name": { "type": "string" }, "tag": { "type": "string" } } },
      "Pets": { "type": "array", "items": { "$ref": "#/components/schemas/Pet" } },
      "Error": { "type": "object", "required": ["code", "message"], "properties": { "code": { "type": "integer", "format": "int32" }, "message": { "type": "string" } } }
    }
  }
}
```

## Generating the Client

```bash
corvusjson openapi-client petstore.json \
    --rootNamespace Petstore.Client \
    --outputPath ./Generated
```

This produces:
- `Generated/ApiPetsClient.cs` — the client implementation
- `Generated/IApiPetsClient.cs` — the client interface
- `Generated/ListPetsRequest.cs`, `CreatePetRequest.cs`, `ShowPetByIdRequest.cs` — request structs
- `Generated/ListPetsResponse.cs`, `CreatePetResponse.cs`, `ShowPetByIdResponse.cs` — response structs
- `Generated/Models/` — strongly-typed JSON Schema models
- `Generated/corvusjson-openapi.lock` — lock file for incremental regeneration

## Using the Client

### Setting up the transport

The client delegates HTTP communication to an `IApiTransport`. The library provides `HttpClientTransport` which wraps a standard `HttpClient`:

```csharp
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Petstore.Client;
using Petstore.Client.Models;

HttpClient httpClient = new() { BaseAddress = new Uri("https://petstore.example.com/v1") };
await using HttpClientTransport transport = new(httpClient, disposeClient: true);
ApiPetsClient client = new(transport);
```

### Listing pets with a query parameter

Optional parameters accept C# literals directly via implicit conversion. The generated code validates the value against the schema (e.g., `maximum: 100`) before sending:

```csharp
await using ListPetsResponse listResponse = await client.ListPetsAsync(limit: 10);

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

// Typed response header access (outside the match — headers live on the response)
JsonString nextPage = listResponse.XNextHeader;
if (nextPage.IsNotUndefined())
{
    Console.WriteLine($"Next page: {nextPage}");
}
```

### Creating a pet with a request body

Object bodies use the generated `Builder` pattern. The `Builder.Create()` method has typed parameters matching the schema's properties — required properties are mandatory parameters, optional ones have defaults:

```csharp
await using CreatePetResponse createResponse = await client.CreatePetAsync(
    body: new NewPet.Source(static (ref NewPet.Builder b) =>
    {
        b.Create(name: "Fido"u8, tag: "dog"u8);
    }));

createResponse.MatchResult(
    matchCreated: createdPet =>
    {
        Console.WriteLine($"Created: [{createdPet.Id}] {createdPet.Name}");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"Error {error.Code}: {error.Message}");
        return 0;
    });
```

Behind the scenes, the generated code:
1. Builds a JSON object in pooled memory using the builder delegate
2. Validates it against the `NewPet` JSON Schema (verifies `name` is present, types are correct)
3. Serializes it as `application/json` in the request body
4. Parses the 201 response body into a typed `Pet`

### Path parameters

Path parameters accept strings — the generated code URI-encodes the value and substitutes it into the path template:

```csharp
await using ShowPetByIdResponse showResponse = await client.ShowPetByIdAsync(petId: "pet-123"u8);

showResponse.MatchResult(
    matchOk: foundPet =>
    {
        Console.WriteLine($"Found: [{foundPet.Id}] {foundPet.Name}");
        return 0;
    },
    matchDefault: error =>
    {
        Console.WriteLine($"Error {error.Code}: {error.Message}");
        return 0;
    });
```

The generated request struct handles `{petId}` → `pet-123` substitution with proper percent-encoding (e.g., spaces become `%20`).

### Returning values from MatchResult

`MatchResult<TResult>` returns a value from each handler, making it ideal for functional-style response processing:

```csharp
string message = showResponse.MatchResult<string>(
    matchOk: static pet => $"Found: {pet.Name}",
    matchDefault: static error => $"Error {error.Code}: {error.Message}");
```

### Request validation

By default (`ValidationMode.Basic`), all parameters and bodies are validated before the HTTP call:

```csharp
try
{
    // Throws: limit > 100 violates the schema's "maximum: 100" constraint
    await using ListPetsResponse _ = await client.ListPetsAsync(
        limit: 200,
        validationMode: ValidationMode.Detailed);
}
catch (ArgumentException ex)
{
    // Detailed mode includes JSON Schema validation output showing exactly
    // which constraint was violated
    Console.WriteLine($"Validation failed: {ex.Message}");
}
```

Validation modes:
| Mode | Behaviour |
|------|-----------|
| `ValidationMode.Basic` | Validates parameters and bodies; throws on failure with a brief message |
| `ValidationMode.Detailed` | Same validation but includes full JSON Schema evaluation output in the exception |
| `ValidationMode.None` | Skips all validation — use for trusted inputs in performance-critical paths |

## Running

```bash
dotnet build
dotnet run
```

The runnable recipe uses an in-memory `IApiTransport` so it produces deterministic console output without needing the fictional `petstore.example.com` host to exist. In production code, replace the demo transport with `HttpClientTransport` configured with your API server's base address, as shown in the transport setup section above.

## Best Practices

1. **Use `u8` literals for scalar parameters** — `petId: "abc"u8`, `name: "Fido"u8`. The implicit conversions handle encoding and avoid UTF-16→UTF-8 transcoding.
2. **Use `Builder.Create()` for request bodies** — it mirrors the schema properties and provides compile-time guidance.
3. **Always `await using` responses** — they hold pooled memory that must be returned.
4. **Prefer `MatchResult` for response handling** — it ensures exhaustive coverage of all status codes defined in the spec.
5. **Leave `validationMode` at `Basic`** for development; consider `None` for production hot paths with trusted data.
6. **Use `ValidationMode.Detailed`** during debugging to get full JSON Schema diagnostic output.

## Lock File and Regeneration

The generator writes a `corvusjson-openapi.lock` file alongside the output. On subsequent runs, if the spec hasn't changed, generation is skipped automatically. Use `--force` to regenerate unconditionally.

The lock file records:
- A hash of the spec content
- The root namespace and client name settings
- Any filter patterns applied

This enables safe CI/CD integration — regeneration only happens when the spec actually changes.