# Corvus.Text.Json.OpenApi.HttpTransport

`HttpClient`-based transport implementation for Corvus OpenAPI generated clients.

## Usage

```csharp
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

await using var transport = new HttpClientTransport(new HttpClient
{
    BaseAddress = new Uri("https://api.example.com"),
});

var client = new PetstorePetsClient(transport);
using ApiResponse response = await client.ListPetsAsync();
response.EnsureSuccess();

using var doc = ParsedJsonDocument<Pets>.Parse(response.Body);
// ... work with doc.RootElement ...
```

## Design

This package provides a single type — `HttpClientTransport` — that bridges generated client code to `System.Net.Http.HttpClient`. Response bodies are read into `ArrayPool<byte>`-rented buffers, enabling zero-copy parsing via `ParsedJsonDocument<T>.Parse(ReadOnlyMemory<byte>)`.

The transport is shipped in a separate assembly so consumers don't pull in HTTP dependencies unless they need them. Alternative transports (e.g. for testing, gRPC, or message queues) can implement `IApiTransport` independently.