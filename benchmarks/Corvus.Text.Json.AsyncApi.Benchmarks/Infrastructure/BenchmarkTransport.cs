// <copyright file="BenchmarkTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.Internal;

namespace AsyncApiBenchmark.Infrastructure;

/// <summary>
/// A zero-overhead transport for benchmarks. Publish writes to a pre-allocated
/// buffer (measuring serialize + pipeline cost). Subscribe delivers pre-built bytes
/// to the handler.
/// </summary>
public sealed class BenchmarkTransport : IMessageTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_buffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? subscribeHandler;
    private byte[]? prebuiltReplyPayload;

    /// <summary>
    /// Gets the number of bytes written by the last publish operation.
    /// </summary>
    public int LastPublishBytesWritten { get; private set; }

    /// <summary>
    /// Sets the pre-built reply payload for request/reply benchmarks.
    /// </summary>
    /// <param name="replyPayloadJson">The reply payload JSON bytes.</param>
    public void SetReplyPayload(byte[] replyPayloadJson)
    {
        this.prebuiltReplyPayload = replyPayloadJson;
    }

    /// <summary>
    /// Delivers pre-built bytes to the registered subscribe handler.
    /// </summary>
    /// <param name="payloadJson">The payload bytes to deliver.</param>
    /// <param name="headersJson">Optional headers bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the operation.</returns>
    public ValueTask DeliverAsync(
        ReadOnlyMemory<byte> payloadJson,
        ReadOnlyMemory<byte> headersJson = default,
        CancellationToken cancellationToken = default)
    {
        return this.subscribeHandler is not null
            ? this.subscribeHandler(payloadJson, headersJson, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ArrayBufferWriter<byte> buffer = t_buffer ??= new ArrayBufferWriter<byte>(1024);
        buffer.ResetWrittenCount();

        Utf8JsonWriter writer = t_writer ??= new Utf8JsonWriter(buffer);
        writer.Reset(buffer);

        payload.WriteTo(writer);
        writer.Flush();

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            writer.Reset(buffer);
            headers.WriteTo(writer);
            writer.Flush();
        }

        this.LastPublishBytesWritten = (int)writer.BytesCommitted;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        in TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        // Publish-side: serialize the request
        ArrayBufferWriter<byte> buffer = t_buffer ??= new ArrayBufferWriter<byte>(1024);
        buffer.ResetWrittenCount();

        Utf8JsonWriter writer = t_writer ??= new Utf8JsonWriter(buffer);
        writer.Reset(buffer);

        request.WriteTo(writer);
        writer.Flush();
        this.LastPublishBytesWritten = (int)writer.BytesCommitted;

        // Subscribe-side: parse the pre-built reply.
        // For benchmarks, use ParseValue (non-disposable copy) to avoid lifetime issues.
        TReply reply = JsonElementHelpers.ParseValue<TReply>(this.prebuiltReplyPayload);
        return ValueTask.FromResult((reply, default(JsonElement)));
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        this.subscribeHandler = async (payloadBytes, headersBytes, ct) =>
        {
            using ParsedJsonDocument<TPayload> doc =
                ParsedJsonDocument<TPayload>.Parse(payloadBytes);

            JsonElement headers = default;
            if (headersBytes.Length > 0)
            {
                using ParsedJsonDocument<JsonElement> headerDoc =
                    ParsedJsonDocument<JsonElement>.Parse(headersBytes);
                headers = headerDoc.RootElement.Clone();
            }

            await handler(doc.RootElement, headers, ct).ConfigureAwait(false);
        };

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask UnsubscribeAsync(
        ReadOnlyMemory<byte> channelUtf8,
        CancellationToken cancellationToken = default)
    {
        this.subscribeHandler = null;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeadLetterAsync(
        ReadOnlyMemory<byte> deadLetterChannelUtf8,
        ReadOnlyMemory<byte> originalChannelUtf8,
        in JsonElement payload,
        in JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}