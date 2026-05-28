// <copyright file="InMemoryMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// An in-memory implementation of <see cref="IMessageTransport"/> for testing.
/// </summary>
/// <remarks>
/// <para>
/// This transport captures published messages and allows tests to deliver messages
/// to subscribers. It serializes payloads via <c>WriteTo(Utf8JsonWriter)</c> on
/// publish, and parses incoming bytes via <c>ParsedJsonDocument&lt;T&gt;.Parse()</c>
/// on subscribe delivery — mirroring what a real transport would do.
/// </para>
/// </remarks>
internal sealed class InMemoryMessageTransport : IMessageTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    private readonly object syncRoot = new();
    private readonly List<PublishedMessage> publishedMessages = [];
    private readonly List<DeadLetteredMessage> deadLetteredMessages = [];
    private readonly Dictionary<string, Delegate> subscriptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource<(byte[] Payload, byte[] Headers)>> pendingRequests = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the list of messages published via this transport.
    /// </summary>
    public IReadOnlyList<PublishedMessage> PublishedMessages => this.publishedMessages;

    /// <summary>
    /// Gets the list of dead-lettered messages.
    /// </summary>
    public IReadOnlyList<DeadLetteredMessage> DeadLetteredMessages => this.deadLetteredMessages;

    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        byte[] payloadBytes = SerializeToOwnedBytes(in payload);
        byte[] headerBytes = headers.ValueKind == JsonValueKind.Undefined
            ? []
            : SerializeToOwnedBytes(in headers);

        lock (this.syncRoot)
        {
            this.publishedMessages.Add(new PublishedMessage(channel, payloadBytes, headerBytes));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string requestChannel = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);
        byte[] requestBytes = SerializeToOwnedBytes(in request);
        byte[] headerBytes = headers.ValueKind == JsonValueKind.Undefined
            ? []
            : SerializeToOwnedBytes(in headers);

        lock (this.syncRoot)
        {
            this.publishedMessages.Add(new PublishedMessage(requestChannel, requestBytes, headerBytes));
        }

        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs = new();

        lock (this.syncRoot)
        {
            this.pendingRequests[correlationId] = tcs;
        }

        return CompleteRequestAsync<TReply>(tcs, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        lock (this.syncRoot)
        {
            this.subscriptions[channel] = handler;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        lock (this.syncRoot)
        {
            this.subscriptions.Remove(channel);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeadLetterAsync(
        ReadOnlyMemory<byte> deadLetterChannelUtf8,
        ReadOnlyMemory<byte> originalChannelUtf8,
        in JsonElement payload,
        in JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        string deadLetterChannel = Encoding.UTF8.GetString(deadLetterChannelUtf8.Span);
        string originalChannel = Encoding.UTF8.GetString(originalChannelUtf8.Span);

        byte[] payloadBytes = payload.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in payload)
            : [];

        byte[] headerBytes = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in headers)
            : [];

        lock (this.syncRoot)
        {
            this.deadLetteredMessages.Add(new DeadLetteredMessage(
                deadLetterChannel, originalChannel, payloadBytes, headerBytes, exception));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Delivers a raw JSON payload to the subscriber on the specified channel.
    /// </summary>
    /// <typeparam name="TPayload">The payload type expected by the subscriber.</typeparam>
    /// <param name="channel">The channel address.</param>
    /// <param name="payloadJson">The raw JSON payload bytes.</param>
    /// <param name="headersJson">The raw JSON headers bytes (empty for no headers).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the delivery.</returns>
    public async ValueTask DeliverAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> payloadJson,
        ReadOnlyMemory<byte> headersJson = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        Delegate handler;

        lock (this.syncRoot)
        {
            if (!this.subscriptions.TryGetValue(channel, out handler!))
            {
                throw new InvalidOperationException($"No subscription for channel '{channel}'.");
            }
        }

        TPayload payload = JsonElementHelpers.ParseValue<TPayload>(payloadJson.Span);

        JsonElement headers = default;
        if (headersJson.Length > 0)
        {
            headers = JsonElementHelpers.ParseValue<JsonElement>(headersJson.Span);
        }

        await ((Func<TPayload, JsonElement, CancellationToken, ValueTask>)handler)(
            payload, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Completes a pending request/reply by correlationId.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="replyPayloadJson">The reply payload JSON bytes.</param>
    /// <param name="replyHeadersJson">The reply headers JSON bytes.</param>
    public void CompleteRequest(string correlationId, byte[] replyPayloadJson, byte[] replyHeadersJson = default!)
    {
        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs;

        lock (this.syncRoot)
        {
            if (!this.pendingRequests.Remove(correlationId, out tcs!))
            {
                throw new InvalidOperationException($"No pending request for correlationId '{correlationId}'.");
            }
        }

        tcs.SetResult((replyPayloadJson, replyHeadersJson ?? []));
    }

    private static async ValueTask<(TReply Payload, JsonElement Headers)> CompleteRequestAsync<TReply>(
        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        (byte[] replyBytes, byte[] headerBytes) = await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        TReply reply = JsonElementHelpers.ParseValue<TReply>(replyBytes);

        JsonElement headers = default;
        if (headerBytes.Length > 0)
        {
            headers = JsonElementHelpers.ParseValue<JsonElement>(headerBytes);
        }

        return (reply, headers);
    }

    private static byte[] SerializeToOwnedBytes<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        using Utf8JsonWriter writer = new(buffer);
        value.WriteTo(writer);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Represents a message that was published via the transport.
    /// </summary>
    /// <param name="Channel">The channel the message was published to.</param>
    /// <param name="PayloadBytes">The serialized payload bytes.</param>
    /// <param name="HeaderBytes">The serialized header bytes (empty if none).</param>
    internal sealed record PublishedMessage(string Channel, byte[] PayloadBytes, byte[] HeaderBytes);

    /// <summary>
    /// Represents a message that was sent to a dead-letter channel.
    /// </summary>
    /// <param name="DeadLetterChannel">The dead-letter channel it was sent to.</param>
    /// <param name="OriginalChannel">The original channel the message arrived on.</param>
    /// <param name="PayloadBytes">The serialized payload bytes.</param>
    /// <param name="HeaderBytes">The serialized header bytes (empty if none).</param>
    /// <param name="Exception">The exception that caused dead-lettering.</param>
    internal sealed record DeadLetteredMessage(
        string DeadLetterChannel,
        string OriginalChannel,
        byte[] PayloadBytes,
        byte[] HeaderBytes,
        Exception Exception);
}