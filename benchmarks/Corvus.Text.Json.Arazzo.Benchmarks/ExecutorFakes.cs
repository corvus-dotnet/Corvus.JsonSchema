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
/// A generated-style client (<c>GET /pets/{petId}</c>) that mirrors the real generated code: each
/// parameter is a <see cref="JsonElement.Source"/> that the client materializes into its own
/// workspace via <c>CreateBuilder</c> — so the benchmark measures the same build the real client does
/// (rather than skipping it by taking a reified <see cref="JsonElement"/>).
/// </summary>
public sealed class BenchClient(IApiTransport transport)
{
    private readonly IApiTransport transport = transport;

    // The public method is SYNC (a JsonElement.Source is a ref struct and can't be an async parameter):
    // it builds the request into a workspace, then hands both to the async core. This mirrors the real
    // generated client exactly.
    public ValueTask<BenchResponse> GetPetAsync(
        JsonElement.Source petId,
        CancellationToken cancellationToken = default,
        ValidationMode validationMode = ValidationMode.Basic,
        ValidationMode responseValidationMode = ValidationMode.None)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        JsonElement petIdValue = JsonElement.CreateBuilder(workspace, petId).RootElement;
        var request = new BenchRequest(petIdValue);
        return this.SendCore(workspace, request, cancellationToken);
    }

    // Async core: dispose the request workspace once the send has completed. The response is
    // independent and is disposed by the caller (the executor's `await response.DisposeAsync()`).
    private async ValueTask<BenchResponse> SendCore(JsonWorkspace workspace, BenchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await this.transport.SendAsync<BenchRequest, BenchResponse>(in request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
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
/// <summary>
/// A message transport for the channel-step probes. Publish discards; <see cref="SubscribeAsync"/> and
/// <see cref="SubscribeReplyAsync"/> deliver a single fixed message synchronously (so the one-shot
/// <c>ReceiveOneAsync</c>/<c>ReceiveOneAndReplyAsync</c> wrappers complete), reusing a per-type parsed
/// document so the transport itself allocates nothing per delivery.
/// </summary>
public sealed class BenchMessageTransport : Corvus.Text.Json.AsyncApi.IMessageTransport
{
    private static class Fixed<T>
        where T : struct, IJsonElement<T>
    {
        public static readonly ParsedJsonDocument<T> Message = ParsedJsonDocument<T>.Parse("""{"v":42}"""u8.ToArray());
    }

    public ValueTask PublishAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, in TPayload payload, in JsonElement headers = default, CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload> => default;

    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(ReadOnlyMemory<byte> requestChannelUtf8, ReadOnlyMemory<byte> replyChannelUtf8, TRequest request, ReadOnlyMemory<byte> correlationIdUtf8, JsonElement headers = default, CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply> => throw new NotSupportedException();

    public ValueTask SubscribeAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, Func<TPayload, JsonElement, CancellationToken, ValueTask> handler, CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload> => handler(Fixed<TPayload>.Message.RootElement, default, cancellationToken);

    public ValueTask SubscribeReplyAsync<TRequest, TReply>(ReadOnlyMemory<byte> channelUtf8, Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler, CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        return DeliverAsync();

        async ValueTask DeliverAsync() => _ = await handler(Fixed<TRequest>.Message.RootElement, default, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default) => default;

    public ValueTask DeadLetterAsync(ReadOnlyMemory<byte> deadLetterChannelUtf8, ReadOnlyMemory<byte> originalChannelUtf8, in JsonElement payload, in JsonElement headers, Exception exception, CancellationToken cancellationToken = default) => default;

    public ValueTask DisposeAsync() => default;
}

/// <summary>A generated-style AsyncAPI producer (mirrors the real producer's workspace materialisation).</summary>
public sealed class BenchProducer(Corvus.Text.Json.AsyncApi.IMessageTransport transport)
{
    private readonly Corvus.Text.Json.AsyncApi.IMessageTransport transport = transport;

    private static readonly ParsedJsonDocument<JsonElement> ReplyDoc = ParsedJsonDocument<JsonElement>.Parse("""{"answer":42,"status":"ok"}"""u8.ToArray());

    public ValueTask PublishBenchAsync(JsonElement.Source payload, CancellationToken cancellationToken = default)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        JsonElement built = JsonElement.CreateBuilder(workspace, payload).RootElement;
        return this.PublishCoreAsync(workspace, built, cancellationToken);
    }

    /// <summary>Sends a request and returns a fixed reply (materialising the request as the real producer would).</summary>
    public ValueTask<JsonElement> SendAndReceiveBenchAsync(JsonElement.Source payload, CancellationToken cancellationToken = default)
    {
        _ = this.transport;
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        _ = JsonElement.CreateBuilder(workspace, payload).RootElement;
        workspace.Dispose();
        return new ValueTask<JsonElement>(ReplyDoc.RootElement);
    }

    private async ValueTask PublishCoreAsync(JsonWorkspace workspace, JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            await this.transport.PublishAsync("bench"u8.ToArray(), payload, default, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
    }
}
