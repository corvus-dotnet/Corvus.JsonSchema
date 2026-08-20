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
public sealed class InMemoryMessageTransport : IMessageDeliveryContextTransport, IHealthCheckableTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly object syncRoot = new();
    private readonly List<PublishedMessage> publishedMessages = [];
    private readonly List<DeliveryFailure> deliveryFailures = [];
    private readonly List<DeadLetteredMessage> deadLetteredMessages = [];

    // One slot per channel, holding whichever kind of subscription owns it (legacy data,
    // delivery-context data, or responder). Separate registries per kind are what let a channel
    // hold two subscriptions whose relative precedence then differed from the real transports.
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

    /// <summary>
    /// Gets the handler failures from loopback deliveries (a publish delivered to a subscription
    /// on the same transport whose handler threw). As on a real broker, these never surface to
    /// the publish call; assert on this list instead.
    /// </summary>
    public IReadOnlyList<DeliveryFailure> DeliveryFailures => this.deliveryFailures;

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

        // Automatically deliver to any matching subscriber (like a real broker would)
        Delegate? handler = null;
        lock (this.syncRoot)
        {
            this.subscriptions.TryGetValue(channel, out handler);
        }

        if (handler is not null)
        {
            return this.DeliverLoopbackAsync<TPayload>(handler, channelUtf8, channel, payloadBytes, headerBytes, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    // A broker decouples the publisher from its subscribers: a handler failure never surfaces
    // to the publish call. The loopback delivery matches that, recording the failure in
    // DeliveryFailures for test assertions instead of throwing it at the publisher.
    private async ValueTask DeliverLoopbackAsync<TPayload>(
        Delegate handler,
        ReadOnlyMemory<byte> channelUtf8,
        string channel,
        byte[] payloadBytes,
        byte[] headerBytes,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        try
        {
            await this.DeliverToSubscriberAsync<TPayload>(handler, channelUtf8, payloadBytes, headerBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (this.syncRoot)
            {
                this.deliveryFailures.Add(new DeliveryFailure(channel, ex));
            }
        }
    }

    private async ValueTask DeliverToSubscriberAsync<TPayload>(
        Delegate handler,
        ReadOnlyMemory<byte> channelUtf8,
        byte[] payloadBytes,
        byte[] headerBytes,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        using ParsedJsonDocument<TPayload> payloadDoc = ParsedJsonDocument<TPayload>.Parse(payloadBytes);
        TPayload parsedPayload = payloadDoc.RootElement;

        JsonElement parsedHeaders = default;
        ParsedJsonDocument<JsonElement>? headersDoc = null;
        if (headerBytes.Length > 0)
        {
            headersDoc = ParsedJsonDocument<JsonElement>.Parse(headerBytes);
            parsedHeaders = headersDoc.RootElement;
        }

        try
        {
            await InvokeSubscriberAsync(handler, parsedPayload, channelUtf8, parsedHeaders, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            headersDoc?.Dispose();
        }
    }

    // The single context-vs-legacy dispatch: every delivery path routes through here so the
    // shape of the MessageDeliveryContext a subscriber sees cannot vary by delivery path.
    private static ValueTask InvokeSubscriberAsync<TPayload>(
        Delegate handler,
        TPayload payload,
        ReadOnlyMemory<byte> channelUtf8,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        if (handler is Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> contextHandler)
        {
            return contextHandler(
                payload,
                new MessageDeliveryContext
                {
                    ChannelUtf8 = channelUtf8,
                    Headers = headers,
                },
                cancellationToken);
        }

        if (IsResponder(handler))
        {
            throw new InvalidOperationException(
                "The channel's subscription is a responder; deliver to it with RequestAsync rather than a plain publish.");
        }

        return ((Func<TPayload, JsonElement, CancellationToken, ValueTask>)handler)(
            payload, headers, cancellationToken);
    }

    // A responder returns the reply payload, so its delegate's return type is the generic
    // ValueTask<TReply>; a data handler returns the non-generic ValueTask. Both are four-arity
    // Funcs, so the return type is what separates them.
    private static bool IsResponder(Delegate handler)
        => handler.GetType().GetMethod("Invoke")!.ReturnType.IsGenericType;

    private void ClaimChannel(string channel, Delegate handler)
    {
        // A channel carries exactly one subscription of any kind, matching every real
        // transport: a second subscribe is refused rather than replacing what is there.
        lock (this.syncRoot)
        {
            if (!this.subscriptions.TryAdd(channel, handler))
            {
                throw new InvalidOperationException(
                    $"Channel '{channel}' already has a subscription. Unsubscribe before subscribing again.");
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        JsonWorkspace workspace,
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

        // If a responder is registered on the request channel, deliver the request to it in-process and
        // route its reply back; otherwise park the request for the test helper CompleteRequest. A plain
        // data subscription on the channel sees the request first, as on a real broker, where a request
        // is an ordinary message on its channel.
        Delegate? responder;
        Delegate? dataSubscriber;
        lock (this.syncRoot)
        {
            // A responder occupies the same single slot as a data subscription.
            this.subscriptions.TryGetValue(requestChannel, out Delegate? candidate);
            bool isResponder = candidate is not null && IsResponder(candidate);
            responder = isResponder ? candidate : null;
            dataSubscriber = isResponder ? null : candidate;
        }

        if (responder is not null)
        {
            return RespondAsync<TRequest, TReply>(responder, requestChannelUtf8, requestBytes, headerBytes, workspace, cancellationToken);
        }

        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs = new();

        lock (this.syncRoot)
        {
            this.pendingRequests[correlationId] = tcs;
        }

        if (dataSubscriber is not null)
        {
            return this.DeliverRequestThenAwaitReplyAsync<TRequest, TReply>(dataSubscriber, requestChannelUtf8, correlationId, requestBytes, headerBytes, tcs, workspace, cancellationToken);
        }

        return this.CompleteRequestAsync<TReply>(correlationId, tcs, workspace, cancellationToken);
    }

    // Delivers the request to the channel's data subscription through the same dispatch a publish
    // uses (so delivery-context subscriptions see the request exactly as any other message) before
    // waiting for the reply the test completes.
    private async ValueTask<(TReply Payload, JsonElement Headers)> DeliverRequestThenAwaitReplyAsync<TRequest, TReply>(
        Delegate subscriber,
        ReadOnlyMemory<byte> requestChannelUtf8,
        string correlationId,
        byte[] requestBytes,
        byte[] headerBytes,
        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        try
        {
            await this.DeliverToSubscriberAsync<TRequest>(subscriber, requestChannelUtf8, requestBytes, headerBytes, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The request faulted before its wait began; unpark it so a later CompleteRequest
            // for this id fails loudly instead of completing an abandoned wait.
            this.AbandonPendingRequest(correlationId, tcs);
            throw;
        }

        return await this.CompleteRequestAsync<TReply>(correlationId, tcs, workspace, cancellationToken).ConfigureAwait(false);
    }

    // Removes a parked request whose waiter has given up, if it is still the parked one.
    private void AbandonPendingRequest(string correlationId, TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs)
    {
        lock (this.syncRoot)
        {
            if (this.pendingRequests.TryGetValue(correlationId, out TaskCompletionSource<(byte[] Payload, byte[] Headers)>? current)
                && ReferenceEquals(current, tcs))
            {
                this.pendingRequests.Remove(correlationId);
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask SubscribeReplyAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ArgumentNullException.ThrowIfNull(handler);
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        this.ClaimChannel(channel, handler);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ArgumentNullException.ThrowIfNull(handler);
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        this.ClaimChannel(channel, handler);

        return ValueTask.CompletedTask;
    }

    // Parses a delivered request, invokes the responder handler, and returns its reply (the request and
    // reply documents are GC-backed, matching CompleteRequestAsync's semantics). The stored delegate is
    // either the legacy (request, headers) shape or the delivery-context shape — both are four-arity
    // Funcs returning ValueTask<TReply>, so a runtime type check picks the right one, matching how
    // DeliverToSubscriberAsync distinguishes the two plain-subscribe delegate shapes above.
    private static async ValueTask<(TReply Payload, JsonElement Headers)> RespondAsync<TRequest, TReply>(
        Delegate responder,
        ReadOnlyMemory<byte> requestChannelUtf8,
        byte[] requestBytes,
        byte[] headerBytes,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        using ParsedJsonDocument<TRequest> requestDoc = ParsedJsonDocument<TRequest>.Parse(requestBytes);
        JsonElement requestHeaders = default;
        ParsedJsonDocument<JsonElement>? headersDoc = null;
        if (headerBytes.Length > 0)
        {
            headersDoc = ParsedJsonDocument<JsonElement>.Parse(headerBytes);
            requestHeaders = headersDoc.RootElement;
        }

        try
        {
            TReply reply;
            if (responder is Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> contextHandler)
            {
                // The in-memory transport has no native broker message: the request is already
                // fully materialized as the parsed document and header bytes this method holds,
                // exactly as the plain delivery-context subscribe path leaves NativeMessage unset.
                MessageDeliveryContext context = new()
                {
                    ChannelUtf8 = requestChannelUtf8,
                    Headers = requestHeaders,
                };
                reply = await contextHandler(requestDoc.RootElement, context, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var handler = (Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>>)responder;
                reply = await handler(requestDoc.RootElement, requestHeaders, cancellationToken).ConfigureAwait(false);
            }

            // Re-parse the reply into a document owned by the caller's workspace so it outlives this handler.
            byte[] replyBytes = SerializeToOwnedBytes(in reply);
            ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(replyBytes);
            workspace.TakeOwnership(replyDoc);
            return (replyDoc.RootElement, default);
        }
        finally
        {
            headersDoc?.Dispose();
        }
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        this.ClaimChannel(channel, handler);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        this.ClaimChannel(channel, handler);

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
            await InvokeSubscriberAsync(handler, payload, Encoding.UTF8.GetBytes(channel), headers, cancellationToken).ConfigureAwait(false);
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
            await InvokeSubscriberAsync(handler, payload, Encoding.UTF8.GetBytes(channel), headers, cancellationToken).ConfigureAwait(false);
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
            this.deliveryFailures.Clear();
        }
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> CompleteRequestAsync<TReply>(
        string correlationId,
        TaskCompletionSource<(byte[] Payload, byte[] Headers)> tcs,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        byte[] replyBytes;
        byte[] headerBytes;
        try
        {
            (replyBytes, headerBytes) = await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The waiter gave up (cancellation, typically); unpark the request so a later
            // CompleteRequest for this id fails loudly instead of completing an abandoned wait.
            this.AbandonPendingRequest(correlationId, tcs);
            throw;
        }

        // The returned reply and headers reference their documents' memory, so the caller's workspace owns
        // those documents (disposed when the workspace is) rather than leaving them to the GC.
        ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(replyBytes);
        workspace.TakeOwnership(replyDoc);
        TReply reply = replyDoc.RootElement;

        JsonElement headers = default;
        if (headerBytes.Length > 0)
        {
            ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse(headerBytes);
            workspace.TakeOwnership(headersDoc);
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