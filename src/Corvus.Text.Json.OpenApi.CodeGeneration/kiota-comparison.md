# Corvus OpenAPI Client vs Kiota — Feature Comparison

This document compares the Corvus OpenAPI client generator with [Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) across architecture, generated code features, and performance characteristics.

## Architecture

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Generation model | CLI tool (`corvusjson openapi-client`) | CLI tool (`kiota generate`) |
| Runtime dependency | `Corvus.Text.Json.OpenApi` (thin transport abstraction) | `Microsoft.Kiota.Bundle` (abstractions + HTTP + serialization + auth) |
| Serialization | Zero-copy over pooled JSON document (no POCO hydration) | POCO model classes with `IParsable` self-deserialization |
| Memory model | Struct-based types backed by pooled `byte[]` — GC-free hot path | Class-based models allocated per response |
| Transport abstraction | `IApiTransport` (4 overloads: no-body, typed, stream, writer) | `IRequestAdapter` wrapping `HttpClient` with middleware pipeline |
| HttpClient support | `HttpClientTransport` wraps `HttpClient` (same as Kiota) — but transport is swappable | Tightly coupled to `HttpClient` via `HttpClientRequestAdapter` |
| Schema validation | Built-in, configurable per-request (`None`/`Basic`/`Detailed`) | None — no schema validation support |

### Transport Design

Corvus separates the transport concern from the generated client via the `IApiTransport` interface (in `Corvus.Text.Json.OpenApi`). The default production implementation is `HttpClientTransport` (in `Corvus.Text.Json.OpenApi.HttpTransport`), which wraps a standard `HttpClient` — the same underlying HTTP stack that Kiota uses. This means the HTTP layer is identical; the performance difference comes entirely from how request/response serialization and validation are handled above the transport.

The `IApiTransport` abstraction enables:
- **Testing**: mock transports return fixed bytes without any HTTP stack (as used in benchmarks)
- **Alternative transports**: gRPC, in-process, or message-queue transports can implement the same interface
- **Composition**: transports can be wrapped for logging, retries, or metrics without modifying generated code

### Middleware / Resilience

Kiota ships its own `DelegatingHandler` pipeline with default handlers for retry (exponential backoff on 429/503/504, honours `Retry-After`), redirect (301/302/303/307/308 with auth-stripping on host change), parameter name decoding, user-agent injection, and header inspection. It disables `SocketsHttpHandler.AllowAutoRedirect` so that its own redirect handler has full control.

Corvus takes a different approach: since `HttpClientTransport` wraps a standard `HttpClient`, resilience and middleware are composed using platform-native .NET patterns:

- **Retry / circuit-breaker**: `Microsoft.Extensions.Http.Resilience` (Polly v8) via `IHttpClientFactory` — richer policies than Kiota's built-in handler (jitter, circuit breakers, hedging, per-endpoint configuration)
- **Redirect**: `SocketsHttpHandler.AllowAutoRedirect = true` (the platform default) handles standard redirects without a custom handler
- **Observability**: OpenTelemetry `HttpClient` instrumentation integrates automatically
- **Custom handlers**: any `DelegatingHandler` can be added to the `HttpClient` pipeline via `IHttpClientFactory.AddHttpMessageHandler()`

This means Corvus doesn't reimplement retry/redirect logic that the platform already provides, and users can mix any community middleware (Polly, OpenTelemetry, logging, etc.) without learning a Kiota-specific handler API.
The downside of this is that there is no transport-independent middleware model in Corvus.

| Concern | Corvus approach | Kiota approach |
|---------|----------------|----------------|
| Retry | `Microsoft.Extensions.Http.Resilience` (Polly) | Built-in `RetryHandler` (exponential backoff) |
| Redirect | `SocketsHttpHandler.AllowAutoRedirect` (platform) | Built-in `RedirectHandler` (custom impl) |
| Auth header stripping on redirect | Platform `SocketsHttpHandler` handles this | Custom logic in `RedirectHandler` |
| Observability | OpenTelemetry `HttpClient` instrumentation | Custom `Activity` tracing in each handler |
| Per-request options | Standard `HttpRequestMessage.Options` | Custom `IRequestOption` per handler |
| Composition | `IHttpClientFactory.AddHttpMessageHandler()` | `KiotaClientFactory.CreateDefaultHandlers()` |

## Response Headers

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Typed header access | Generated properties on response struct (e.g. `response.XNextHeader`) | Not generated — headers discarded by default |
| Lazy parsing | Yes — header value parsed on first access only | N/A |
| How to access headers | Direct property access | Must use `NativeResponseHandler` + extract from raw `HttpResponseMessage` manually |
| Code generation | Automatic from OpenAPI `responses.*.headers` | Not supported |

**Kiota workaround for response headers:**

```csharp
// Kiota requires a custom response handler to access headers
var handler = new NativeResponseHandler();
await client.Pets.GetAsync(config =>
    config.Options.Add(new ResponseHandlerOption { ResponseHandler = handler }));
var nativeResponse = handler.Value as HttpResponseMessage;
string? xNext = nativeResponse?.Headers.GetValues("x-next").FirstOrDefault();
```

**Corvus equivalent:**

```csharp
// Corvus generates typed, lazily-parsed header properties
ListPetsResponse response = await client.ListPetsAsync();
JsonString? xNext = response.XNextHeader; // parsed on first access, zero extra allocation
```

## Request Body Construction

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Body construction | `Build()` delegate pattern — deferred serialization, zero allocation at call site | Mutable POCO with property setters |
| Serialization timing | Lazy — serialized inside transport only when needed | Eager — serialized by `ISerializationWriter` before send |
| Form-urlencoded | Native codegen support | Not supported (JSON only for typed bodies) |
| Multipart/mixed | Native codegen with binary parts | Limited multipart support |

**Corvus body construction (zero-allocation delegate):**

```csharp
await client.CreatePetAsync(
    NewPet.Build(static (ref NewPet.Builder b) => b.Create("Rex"u8, "dog"u8)));
```

**Kiota body construction (POCO allocation):**

```csharp
await client.Pets.PostAsync(new NewPet { Name = "Rex", Tag = "dog" });
```

## Validation

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Request validation | Compile-time generated schema evaluator, configurable per-call | None |
| Response validation | Compile-time generated schema evaluator, configurable per-call | None |
| Validation modes | `None`, `Basic` (fast bool), `Detailed` (full error report) | N/A |
| Validation cost | ~2× no-validation baseline (Basic mode) | N/A |
| Allocation overhead | Zero additional allocation for validation | N/A |

## Response Handling

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Return type | Value-type response struct (`IApiResponse<T>`) with `IAsyncDisposable` | Nullable POCO or `Task<T?>` |
| Status discrimination | `MatchResult()` with typed handlers per status code | Exception-based (`ApiException` subclasses) |
| Error responses | Typed error body accessible via `TryGetDefault()` or `MatchResult` | `ApiException` with deserialized error model |
| Memory lifetime | Response owns pooled document; disposed via `await using` | GC-managed; no explicit lifetime |
| Multiple content types | Discriminated by content-type in response struct | Single content type per operation |

## Path and Query Parameters

| Aspect | Corvus | Kiota |
|--------|--------|-------|
| Path parameters | UTF-8 formatted directly into URL template | String interpolation via fluent API indexers |
| Query parameters | Typed `Source` values, serialized per OpenAPI `style`/`explode` | Lambda-configured query parameter object |
| Cookie parameters | Supported | Not supported |
| Header parameters (request) | Supported | Supported (via request configuration headers) |

## Performance (Petstore Benchmark, .NET 10.0, i7-13800H)

All benchmarks use mock transports (no real I/O) measuring the full client pipeline:
construct request → serialize → transport → parse response → typed access.

```
BenchmarkDotNet v0.15.8, Windows 11, .NET 10.0.8
13th Gen Intel Core i7-13800H 2.90GHz, 1 CPU, 20 logical / 14 physical cores
Last run: 2026-05-22
```

### GET /pets (10-item array response)

| Client | Mean | Allocated |
|--------|------|-----------|
| Corvus (no validation) | 1.34 μs | 1.02 KB |
| Corvus (with validation) | 3.06 μs | 1.02 KB |
| Kiota (no validation) | 6.09 μs | 17.45 KB |

### GET /pets/{petId} (single object, path parameter)

| Client | Mean | Allocated |
|--------|------|-----------|
| Corvus (no validation) | 409 ns | 560 B |
| Corvus (with validation) | 610 ns | 560 B |
| Kiota (no validation) | 2,290 ns | 8,017 B |

### POST /pets (JSON body via builder delegate)

| Client | Mean | Allocated |
|--------|------|-----------|
| Corvus (no validation) | 434 ns | 560 B |
| Corvus (with validation) | 701 ns | 560 B |
| Kiota (no validation) | 2,910 ns | 8,665 B |

### PUT /pets/{petId} (form-urlencoded body + path parameter)

| Client | Mean | Allocated |
|--------|------|-----------|
| Corvus (no validation) | 536 ns | 792 B |
| Corvus (with validation) | 833 ns | 792 B |
| Kiota (no validation) | 2,957 ns | 9,497 B |

### Response Header Parsing

| Client | Mean | Allocated |
|--------|------|-----------|
| Corvus (with x-next header access) | 472 ns | 896 B |

### Validation Mode Comparison (GET /pets, 3-item array)

| Mode | Mean | Ratio to None | Allocated |
|------|------|---------------|-----------|
| None | 616 ns | 1.0× | 1.02 KB |
| Basic | 1,272 ns | 2.1× | 1.02 KB |
| Detailed | 2,039 ns | 3.3× | 1.02 KB |

All validation modes produce zero additional allocation.

### Summary

- **Corvus WITH full validation is 2–5× faster than Kiota WITHOUT validation** across all operation types
- **Corvus allocates 12–17× less memory** than Kiota per request/response cycle
- Validation overhead in Basic mode is ~2× the no-validation baseline — still faster than Kiota without any validation
- Response header parsing adds negligible cost (472 ns including the full request cycle)

## Feature Support Matrix

| Feature | Corvus | Kiota |
|---------|--------|-------|
| OpenAPI 3.0 | ✅ | ✅ |
| OpenAPI 3.1 | ✅ | ✅ |
| OpenAPI 3.2 | ✅ | Partial |
| JSON request/response bodies | ✅ | ✅ |
| Form-urlencoded bodies | ✅ | ❌ |
| Multipart/mixed bodies | ✅ | Limited |
| Binary upload (octet-stream) | ✅ | ✅ |
| Typed response headers | ✅ | ❌ |
| Cookie parameters | ✅ | ❌ |
| Schema validation | ✅ (None/Basic/Detailed) | ❌ |
| Error response discrimination | ✅ (typed `MatchResult`) | ✅ (exception-based) |
| Authentication | ✅ Built-in (Bearer, ApiKey, Basic) | ✅ Built-in (Bearer, ApiKey, Basic, Anonymous) |
| Middleware pipeline | Platform-native (`IHttpClientFactory` + Polly) | Built-in `DelegatingHandler` chain |
| Multiple languages | C# only | C#, Java, Go, TypeScript, Python, PHP, Ruby, Swift, CLI |
| IDE autocompletion | ✅ (pre-generated files) | ✅ (pre-generated files) |

## When to Choose Corvus

- Performance-critical services where latency and allocation matter
- APIs requiring request/response validation without external tooling
- .NET-only projects wanting zero-copy JSON and compile-time safety
- APIs using form-urlencoded, multipart/mixed, or cookie parameters
- Scenarios requiring typed response header access

## When to Choose Kiota

- Multi-language projects needing consistent client generation across platforms
- Teams wanting a self-contained middleware pipeline without configuring `IHttpClientFactory`
- Projects where POCO-style models are preferred over struct-based types
- APIs where performance is not the primary concern
