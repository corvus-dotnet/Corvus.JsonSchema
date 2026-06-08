// <copyright file="ExecutorFakes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Benchmarks.Fakes;

/// <summary>A minimal generated-style request (<c>GET /pets/{petId}</c>).</summary>
public readonly struct BenchRequest(JsonElement petId) : IApiRequest<BenchRequest>
{
    private readonly JsonElement petId = petId;

    public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{petId}"u8;

    public static OperationMethod Method => OperationMethod.Get;

    public static bool HasPathParameters => true;

    public static bool HasQueryParameters => false;

    public static bool HasHeaderParameters => false;

    public static bool HasCookieParameters => false;

    public void WriteResolvedPath(IBufferWriter<byte> writer) => writer.Write("/pets/x"u8);

    public int WriteQueryString(IBufferWriter<byte> writer) => 0;

    public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
    {
    }

    public int WriteCookies(IBufferWriter<byte> writer) => 0;

    public void Validate(ValidationMode mode = ValidationMode.Basic)
    {
    }
}

/// <summary>
/// A minimal generated-style response whose JSON body is parsed once into a shared document, so the
/// transport contributes no per-call allocation and the benchmark measures only the executor.
/// </summary>
public readonly struct BenchResponse : IApiResponse<BenchResponse>
{
    private static readonly ParsedJsonDocument<JsonElement> BodyDocument =
        ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"name":"Fido"}"""));

    private readonly int statusCode;

    private BenchResponse(int statusCode) => this.statusCode = statusCode;

    public int StatusCode => this.statusCode;

    public JsonElement OkBody => BodyDocument.RootElement;

    public bool IsSuccess => this.statusCode is >= 200 and < 300;

    public static ValueTask<BenchResponse> CreateAsync(
        int statusCode,
        Stream contentStream,
        string? contentType = null,
        IResponseHeaders? responseHeaders = null,
        IAsyncDisposable? owner = null,
        IApiTransport? transport = null,
        CancellationToken cancellationToken = default)
        => new(new BenchResponse(statusCode));

    public void Validate(ValidationMode mode = ValidationMode.Basic)
    {
    }

    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// A minimal generated-style client (<c>GET /pets/{petId}</c>): it builds the request and sends it
/// via the transport, so the executor invokes the operation through the client as it would the real
/// generated code.
/// </summary>
public sealed class BenchClient(IApiTransport transport)
{
    private readonly IApiTransport transport = transport;

    public ValueTask<BenchResponse> GetPetAsync(
        JsonElement petId,
        CancellationToken cancellationToken = default,
        ValidationMode validationMode = ValidationMode.Basic,
        ValidationMode responseValidationMode = ValidationMode.None)
    {
        var request = new BenchRequest(petId);
        return this.transport.SendAsync<BenchRequest, BenchResponse>(in request, cancellationToken);
    }
}

/// <summary>A zero-overhead transport: returns a synchronously-completed cached response.</summary>
public sealed class BenchTransport : IApiTransport
{
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
        => TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() => default;
}