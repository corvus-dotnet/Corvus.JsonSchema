// <copyright file="InMemoryMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.Testing;

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
public sealed class InMemoryMessageTransport : IMessageTransport, IHealthCheckableTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly object syncRoot = new();
    private readonly List<PublishedMessage> publishedMessages = [];
    private readonly List<DeadLetteredMessage> deadLetteredMessages = [];
    private readonly Dictionary<string, Delegate> subscriptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource<(byte[] Payload, byte[] Headers)>> pendingRequests = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool IsConnected => true;

    /// <inheritdoc/>
    public string MessagingSystem => "in-memory";

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
        in TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        in JsonElement headers = default,
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

    /// <inheritdoc/>
    public ValueTask<bool> PingAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

    /// <summary>
    /// Delivers a raw JSON payload to the subscriber on the specified channel.
    /// </summary>
    /// <typeparam name="TPayload">The payload type expected by the subscriber.</typeparam>
    /// <param name="channel">The channel address.</param>
    /// <param name="payloadJson">The raw JSON payload bytes.</param>
    /// <param name="headersJson">The raw JSON headers bytes (empty for no headers).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the delivery.</returns>
    /// <exception cref="InvalidOperationException">No subscription exists for the channel.</exception>
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

        using ParsedJsonDocument<TPayload> payloadDoc = ParsedJsonDocument<TPayload>.Parse(payloadJson);
        TPayload payload = payloadDoc.RootElement;

        JsonElement headers = default;
        ParsedJsonDocument<JsonElement>? headersDoc = null;
        if (headersJson.Length > 0)
        {
            headersDoc = ParsedJsonDocument<JsonElement>.Parse(headersJson);
            headers = headersDoc.RootElement;
        }

        try
        {
            await ((Func<TPayload, JsonElement, CancellationToken, ValueTask>)handler)(
                payload, headers, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            headersDoc?.Dispose();
        }
    }

    /// <summary>
    /// Completes a pending request/reply by correlationId.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="replyPayloadJson">The reply payload JSON bytes.</param>
    /// <param name="replyHeadersJson">The reply headers JSON bytes.</param>
    /// <exception cref="InvalidOperationException">No pending request for the correlationId.</exception>
    public void CompleteRequest(string correlationId, byte[] replyPayloadJson, byte[]? replyHeadersJson = null)
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

    /// <summary>
    /// Delivers raw bytes to the subscriber on the specified channel, simulating how a real
    /// transport handles deserialization: attempts to parse, and if parsing fails, invokes
    /// the provided error policy to determine the action.
    /// </summary>
    /// <typeparam name="TPayload">The payload type expected by the subscriber.</typeparam>
    /// <param name="channel">The channel address.</param>
    /// <param name="rawBytes">The raw bytes (may be malformed JSON).</param>
    /// <param name="errorPolicy">The error policy to invoke on deserialization failure.</param>
    /// <param name="headersJson">The raw JSON headers bytes (empty for no headers).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The <see cref="MessageErrorAction"/> taken, or <c>null</c> if delivery succeeded without error.</returns>
    /// <exception cref="InvalidOperationException">No subscription exists for the channel.</exception>
    public async ValueTask<MessageErrorAction?> DeliverRawAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> rawBytes,
        IMessageErrorPolicy errorPolicy,
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

        TPayload payload;
        ParsedJsonDocument<TPayload>? payloadDoc = null;
        try
        {
            payloadDoc = ParsedJsonDocument<TPayload>.Parse(rawBytes);
            payload = payloadDoc.RootElement;
        }
        catch (Exception ex)
        {
            payloadDoc?.Dispose();
            byte[] channelUtf8 = System.Text.Encoding.UTF8.GetBytes(channel);
            MessageErrorContext errorContext = new(channelUtf8, MessageErrorKind.Deserialization);
            MessageErrorAction action = await errorPolicy.HandleErrorAsync(ex, errorContext, cancellationToken).ConfigureAwait(false);

            if (action == MessageErrorAction.DeadLetter)
            {
                lock (this.syncRoot)
                {
                    this.deadLetteredMessages.Add(new DeadLetteredMessage(
                        "dead-letter." + channel, channel, rawBytes.ToArray(), [], ex));
                }
            }

            return action;
        }

        JsonElement headers = default;
        ParsedJsonDocument<JsonElement>? headersDoc = null;
        if (headersJson.Length > 0)
        {
            headersDoc = ParsedJsonDocument<JsonElement>.Parse(headersJson);
            headers = headersDoc.RootElement;
        }

        try
        {
            await ((Func<TPayload, JsonElement, CancellationToken, ValueTask>)handler)(
                payload, headers, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            payloadDoc?.Dispose();
            headersDoc?.Dispose();
        }

        return null;
    }

    /// <summary>
    /// Clears all recorded published messages and dead-lettered messages.
    /// </summary>
    public void Reset()
    {
        lock (this.syncRoot)
        {
            this.publishedMessages.Clear();
            this.deadLetteredMessages.Clear();
        }
    }

    private static async ValueTask<(TReply Payload, JsonElement Headers)> CompleteRequestAsync<TReply>(
        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        (byte[] replyBytes, byte[] headerBytes) = await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        // Documents are not disposed because the returned values reference their memory.
        // The backing buffers will be collected by the GC when the caller releases the
        // returned values. This is acceptable for request/reply (not a streaming hot path)
        // and matches the InMemory testing transport's semantics.
        ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(replyBytes);
        TReply reply = replyDoc.RootElement;

        JsonElement headers = default;
        if (headerBytes.Length > 0)
        {
            ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse(headerBytes);
            headers = headersDoc.RootElement;
        }

        return (reply, headers);
    }

    private static byte[] SerializeToOwnedBytes<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);
        value.WriteTo(writer);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}