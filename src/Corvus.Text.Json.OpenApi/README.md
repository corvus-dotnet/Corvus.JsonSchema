# Corvus.Text.Json.OpenApi

The byte-native transport and wire-serialization runtime that generated OpenAPI (and AsyncAPI) clients call at run time. It contains no spec-walking or code-generation logic. That lives in `Corvus.Text.Json.OpenApi.CodeGeneration` and the per-version generator packages.

A generated client composes a request and decomposes a response by combining two things. The generated model companions build request-body bytes and validate values. This package serializes the parameters and body onto the wire, sends the bytes through a pluggable transport, and hands the response bytes back for decoding. Nothing here materialises an intermediate object graph.

## Key types

- **`IApiTransport`** is the abstraction for sending a request and receiving a response, the byte pipe. Inject an implementation to control how bytes travel. The default `HttpClient`-based transport is in `Corvus.Text.Json.OpenApi.HttpTransport`, and a mock transport drives the conformance tests.
- **`IApiRequest`** is the request seam that a generated request module implements. Its `write*` methods serialize path, query, header, and cookie parameters into the correct wire format, and it carries the request body.
- **`IApiResponse`** is the strongly-typed response (a CRTP base). It decodes response bytes into typed models and routes by status code. `ResponseMatcher` is the per-status match callback, and `IResponseHeaders` gives read access to the response headers.
- **`ValidationMode`** controls the level of JSON Schema validation applied to request parameters, request bodies, and response bodies.
- **`OperationMethod`** is the HTTP method of an OpenAPI operation, or the messaging action of an AsyncAPI operation.

## Serialization

The wire formats every generated client needs live here, not in the transport, so the transport stays a thin byte pipe.

- Parameter styles: `StyleValueSplitter`, `PropertyEncoding`, and `HeaderValueParser` cover the OpenAPI `style` and `explode` matrix.
- `FormUrlEncodedSerializer` and `FormFieldReader` handle `application/x-www-form-urlencoded`.
- `MultipartFormDataSerializer`/`MultipartFormReader` and `MultipartMixedSerializer`/`MultipartMixedReader` handle multipart bodies, with `BinaryPartData` for binary parts.
- `JsonStreamReader`/`JsonStreamWriter` and `SseEvent` handle NDJSON and server-sent-event streaming bodies.
- `PooledBufferWriter` provides buffer reuse across the synchronous request-composition prelude.

## How the generators fit

All three OpenAPI versions generate against this one runtime through a single symmetric pipeline. There is no per-version bespoke path. Each `OpenApiNNCodeGenerator` walks the strongly-typed model with its `OpenApiNNWalker` (a subclass of the version-neutral `OpenApiWalkerBase`) and emits through its `OpenApiNNCSharpEmitter`, and both are sequenced by the shared `ClientEmitDriver`. The TypeScript engine reuses the same walk and driver with a TypeScript emitter.

## Related packages

- `Corvus.Text.Json.OpenApi.CodeGeneration` is the shared generator core (the walker base, the emit driver, and the schema-type resolver).
- `Corvus.Text.Json.OpenApi30`, `.OpenApi31`, and `.OpenApi32` provide the strongly-typed model types for each OpenAPI version and host the version generators.
- `Corvus.Text.Json.OpenApi.HttpTransport` provides the default `HttpClient`-based `IApiTransport` and the authentication providers.
- `Corvus.Text.Json` is the core library.
