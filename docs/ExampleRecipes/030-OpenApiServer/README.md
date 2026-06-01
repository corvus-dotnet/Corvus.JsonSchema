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

[Example code](./Program.cs)

### Registering endpoints

A single extension method wires all routes:

```csharp
using Petstore.Server;
using Petstore.Server.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

PetsHandler handler = new();
app.MapApiEndpoints(handler);

app.Run();
```

**For production:** Register your handler with dependency injection:

```csharp
builder.Services.AddSingleton<IApiPetsHandler, PetsHandler>();

app.MapApiEndpoints(app.Services.GetRequiredService<IApiPetsHandler>());
```

### Implementing the handler

Each handler method receives parsed, validated parameters and a `JsonWorkspace` for building responses:

```csharp
internal sealed class PetsHandler : IApiPetsHandler
{
    private readonly List<(long Id, string Name, string? Tag)> pets = [(1, "Luna", "cat"), (2, "Max", "dog")];
    private long nextId = 3;

    public ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        // Parameters are already validated — limit <= 100 is guaranteed
        int limit = parameters.Limit.IsNotUndefined()
            ? (int)parameters.Limit
            : 20;

        var petsToReturn = this.pets.Take(limit).ToList();

        // Build response using typed Result factory + array builder
        ListPetsResult result = ListPetsResult.Ok(
            body: new Pets.Source((ref Pets.Builder b) =>
            {
                foreach ((long id, string name, string? tag) in petsToReturn)
                {
                    b.AddItem(new Pet.Source((ref Pet.Builder pb) =>
                    {
                        if (tag is string t)
                            pb.Create(id: id, name: name, tag: t);
                        else
                            pb.Create(id: id, name: name);
                    }));
                }
            }),
            workspace: workspace,
            xNext: limit < this.pets.Count
                ? "\"/pets?offset=1\""u8
                : default);

        return ValueTask.FromResult(result);
    }
}
```

## Handler Pattern Deep Dive

### Pattern 1: Reading validated parameters

All parameters are fully parsed and type-checked before your handler runs:

```csharp
public ValueTask<ListPetsResult> HandleListPetsAsync(
    ListPetsParams parameters,
    JsonWorkspace workspace,
    CancellationToken cancellationToken = default)
{
    // Query parameter (optional integer with maximum: 100)
    if (parameters.Limit.IsNotUndefined())
    {
        int limit = (int)parameters.Limit;  // Safe: already validated <= 100
    }
    
    // If client sends ?limit=200, the generated middleware returns this BEFORE your handler runs:
    // HTTP 400 Problem Details with: "Value 200 exceeds maximum 100"
    
    // Your handler only sees valid inputs!
}
```

### Pattern 2: Path parameters

Path params are URI-decoded strings:

```csharp
public ValueTask<ShowPetByIdResult> HandleShowPetByIdAsync(
    ShowPetByIdParams parameters,
    JsonWorkspace workspace,
    CancellationToken cancellationToken = default)
{
    string petId = (string)parameters.PetId;  // URI-decoded (e.g., "pet%2042" → "pet 42")
    
    // Lookup logic here...
}
```

### Pattern 3: Request bodies

Bodies are fully deserialized and validated against their JSON Schema:

```csharp
public ValueTask<CreatePetResult> HandleCreatePetAsync(
    CreatePetParams parameters,
    JsonWorkspace workspace,
    CancellationToken cancellationToken = default)
{
    NewPet body = parameters.Body;
    
    // Access strongly-typed properties
    string name = (string)body.Name;              // Required — always present
    string? tag = body.Tag.IsNotUndefined()       // Optional — check first
        ? (string)body.Tag
        : null;
    
    // If the client sends {"name": 123} (wrong type) or {} (missing required field),
    // the middleware returns 400 Problem Details — your handler never runs.
}
```

### Pattern 4: Returning typed responses

Each Result struct has factory methods matching the spec's response definitions:

```csharp
// 201 Created with a typed body
return ValueTask.FromResult(CreatePetResult.Created(
    body: new Pet.Source((ref Pet.Builder b) => { b.Create(id: 1, name: "Fido"u8); }),
    workspace: workspace));

// 404 Not Found with custom error
return ValueTask.FromResult(ShowPetByIdResult.Default(
    statusCode: 404,
    body: new Error.Source((ref Error.Builder b) => { b.Create(code: 404, message: "Not found"u8); }),
    workspace: workspace));

// 200 OK with response headers (e.g., pagination link)
return ValueTask.FromResult(ListPetsResult.Ok(
    body: ...,
    workspace: workspace,
    xNext: "\"/pets?offset=10\""u8));
```

## Automatic Validation and Error Handling

The generated middleware validates all inputs before your handler runs:

| Invalid input | Generated behaviour | Handler called? |
|---|---|---|
| Query param fails schema (e.g., `limit=200` exceeds `maximum: 100`) | 400 Problem Details | ❌ No |
| Required path param missing | 400 Problem Details | ❌ No |
| Request body fails JSON parse | 400 Problem Details | ❌ No |
| Request body fails schema validation | 400 Problem Details | ❌ No |
| Valid inputs | Handler executes | ✅ Yes |

You **never** write validation code. Your handler logic assumes valid, typed data.

## Advanced Patterns

### Pattern 5: Async operations (database, external API)

Handlers return `ValueTask<TResult>` — use `async`/`await` for IO-bound work:

```csharp
public async ValueTask<ListPetsResult> HandleListPetsAsync(
    ListPetsParams parameters,
    JsonWorkspace workspace,
    CancellationToken cancellationToken = default)
{
    int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 20;
    
    // Async database query
    List<PetEntity> pets = await this.dbContext.Pets
        .Take(limit)
        .ToListAsync(cancellationToken);
    
    return ListPetsResult.Ok(
        body: new Pets.Source((ref Pets.Builder b) =>
        {
            foreach (PetEntity pet in pets)
            {
                b.AddItem(new Pet.Source((ref Pet.Builder pb) =>
                {
                    pb.Create(id: pet.Id, name: pet.Name);
                }));
            }
        }),
        workspace: workspace);
}
```

### Pattern 6: Error responses with rich detail

Return structured error payloads matching your schema:

```csharp
if (!IsValidPetName(name))
{
    return ValueTask.FromResult(CreatePetResult.Default(
        statusCode: 422,  // Unprocessable Entity
        body: new Error.Source((ref Error.Builder b) =>
        {
            b.Create(
                code: 422,
                message: $"Invalid pet name: '{name}'. Must be 3-50 characters, alphanumeric only."u8);
        }),
        workspace: workspace));
}
```

### Pattern 7: Dependency injection in handlers

Production handlers should accept dependencies via constructor injection:

```csharp
internal sealed class PetsHandler : IApiPetsHandler
{
    private readonly IPetRepository repository;
    private readonly ILogger<PetsHandler> logger;
    
    public PetsHandler(IPetRepository repository, ILogger<PetsHandler> logger)
    {
        this.repository = repository;
        this.logger = logger;
    }
    
    public async ValueTask<ListPetsResult> HandleListPetsAsync(
        ListPetsParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Listing pets with limit {Limit}", parameters.Limit);
        
        var pets = await this.repository.GetPetsAsync(
            limit: parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 20,
            cancellationToken);
        
        return ListPetsResult.Ok(/*...*/);
    }
}

// In Program.cs:
builder.Services.AddSingleton<IPetRepository, InMemoryPetRepository>();
builder.Services.AddSingleton<IApiPetsHandler, PetsHandler>();

app.MapApiEndpoints(app.Services.GetRequiredService<IApiPetsHandler>());
```

## Testing Your Server

### Pattern 8: Testing with WebApplicationFactory

Use ASP.NET's `TestServer` for integration tests without real HTTP:

```csharp
public class PetsHandlerTests
{
    [TestMethod]
    public async Task CreatePet_ValidBody_Returns201()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        
        var body = new StringContent(
            """{"name":"Fido","tag":"dog"}""",
            Encoding.UTF8,
            "application/json");
        
        // Act
        HttpResponseMessage response = await client.PostAsync("/pets", body);
        
        // Assert
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        
        string json = await response.Content.ReadAsStringAsync();
        using var doc = ParsedJsonDocument<Pet>.Parse(json);
        Assert.AreEqual("Fido", (string)doc.RootElement.Name);
    }
    
    [TestMethod]
    public async Task CreatePet_MissingName_Returns400()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        
        var body = new StringContent("""{"tag":"dog"}""", Encoding.UTF8, "application/json");
        
        HttpResponseMessage response = await client.PostAsync("/pets", body);
        
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        
        string problemDetails = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(problemDetails, "name");  // Missing required property
    }
}
```

### Pattern 9: Mocking dependencies for unit tests

Test handler logic in isolation:

```csharp
[TestMethod]
public async Task HandleListPetsAsync_LimitParam_ReturnsCorrectCount()
{
    // Arrange
    var mockRepo = new Mock<IPetRepository>();
    mockRepo.Setup(r => r.GetPetsAsync(5, default))
        .ReturnsAsync(new List<PetEntity> { /*...*/ });
    
    var handler = new PetsHandler(mockRepo.Object, Mock.Of<ILogger<PetsHandler>>());
    var parameters = new ListPetsParams { Limit = JsonInteger.ParseValue(5) };
    using JsonWorkspace workspace = JsonWorkspace.Create();
    
    // Act
    ListPetsResult result = await handler.HandleListPetsAsync(parameters, workspace, default);
    
    // Assert
    Assert.IsTrue(result.IsOk);
    Pets pets = result.GetOk();
    Assert.AreEqual(5, pets.GetArrayLength());
}
```

## Common Pitfalls

### Pitfall 1: Not disposing JsonWorkspace in synchronous handlers

**Problem:**

```csharp
public ValueTask<ListPetsResult> HandleListPetsAsync(...)
{
    JsonWorkspace workspace = JsonWorkspace.Create();  // ❌ Never disposed!
    
    return ValueTask.FromResult(ListPetsResult.Ok(body: ..., workspace: workspace));
}
```

**Why:** The workspace you create is **different** from the parameter `workspace` passed to your handler. The generated middleware manages the parameter workspace's lifetime. If you create your own, you must dispose it.

**Fix:** Use the parameter workspace, not a new one:

```csharp
public ValueTask<ListPetsResult> HandleListPetsAsync(
    ListPetsParams parameters,
    JsonWorkspace workspace,  // ← Use this one!
    CancellationToken cancellationToken = default)
{
    return ValueTask.FromResult(ListPetsResult.Ok(body: ..., workspace: workspace));
}
```

### Pitfall 2: Forgetting to check IsNotUndefined() for optional properties

**Problem:**

```csharp
string tag = (string)body.Tag;  // ❌ Throws if Tag is undefined!
```

**Fix:**

```csharp
string? tag = body.Tag.IsNotUndefined() ? (string)body.Tag : null;
```

### Pitfall 3: Returning the wrong Result type

**Problem:**

```csharp
public ValueTask<CreatePetResult> HandleCreatePetAsync(...)
{
    return ValueTask.FromResult(ListPetsResult.Ok(...));  // ❌ Wrong Result type!
}
```

**Fix:** Each handler method has its own Result type. Use the one that matches the method name:

```csharp
return ValueTask.FromResult(CreatePetResult.Created(...));
```

## Troubleshooting

### "Client gets 400 even though my handler validates the input"

**Cause:** The generated middleware validates **before** calling your handler. Your handler validation is redundant and never executes.

**Solution:** Remove duplicate validation. Trust the generated middleware.

### "Response serialization fails with 'workspace disposed' error"

**Cause:** You're creating and disposing a workspace inside the handler, then passing it to the Result factory.

**Solution:** Use the `workspace` parameter provided to your handler — the generated middleware manages its lifetime.

### "TestServer returns 500 instead of expected error"

**Cause:** Your handler threw an unhandled exception.

**Solution:** Check the test output logs. ASP.NET logs exceptions. Common causes:
- Accessing optional properties without checking `IsNotUndefined()`
- Null reference in business logic
- Database connection issues (use mocks for unit tests)

## Performance Tips

1. **Use UTF-8 literals** — `"value"u8` avoids UTF-16 → UTF-8 conversion:

```csharp
b.Create(name: "Fido"u8, tag: "dog"u8);  // Zero-allocation
```

2. **Reuse JsonWorkspace for batch operations** — if your handler builds multiple responses (e.g., pagination), reuse the workspace:

```csharp
for (int page = 0; page < pageCount; page++)
{
    results[page] = ListPetsResult.Ok(body: ..., workspace: workspace);
    // Same workspace reused across iterations
}
```

3. **Avoid `ToString()` on response bodies in hot paths** — it allocates a string. Write directly to `Utf8JsonWriter` instead if you need custom serialization.

## Best Practices

1. **Use `u8` literals for scalar parameters** — `name: "Fido"u8` avoids transcoding overhead.
2. **Use `Builder.Create()` for response bodies** — it mirrors schema properties and provides compile-time safety.
3. **Always accept `CancellationToken` and pass it through** — enables graceful shutdown.
4. **Return appropriate Result factory** — `Ok`, `Created`, `Default` match your spec's response definitions.
5. **Pass through the `workspace` parameter** — never create your own in synchronous handlers.
6. **Use dependency injection** — register handlers, repositories, and services in `Program.cs`.
7. **Write integration tests with `WebApplicationFactory`** — test the full request/response pipeline.

## Running the Example

```bash
cd docs/ExampleRecipes/030-OpenApiServer
dotnet run
```

The launch profile does not open a browser. The server writes its actual base URL, sample `curl` commands, request summaries, and handler activity to the console.

### Testing with curl:

```bash
# List pets
curl http://localhost:5000/pets

# Create a pet
curl -X POST http://localhost:5000/pets \
  -H "Content-Type: application/json" \
  -d '{"name":"Fido","tag":"dog"}'

# Get a pet by ID
curl http://localhost:5000/pets/1

# Invalid limit (should return 400)
curl "http://localhost:5000/pets?limit=200"
```

## Related Recipes

- [029 — OpenAPI Client](../029-OpenApiClient/) — calling the server from a generated client
- [033 — OpenAPI End-to-End](../033-OpenApiEndToEnd/) — full client + server round-trip
- [032 — OpenAPI Advanced Server](../032-OpenApiAdvancedServer/) — streaming, file uploads, forms