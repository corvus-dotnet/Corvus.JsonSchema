// <copyright file="WebSocketMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.WebSocket;

/// <summary>
/// WebSocket implementation of <see cref="IMessageTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// This transport uses a WebSocket connection to a message broker or server.
/// Messages are framed as JSON envelopes containing the channel, payload,
/// and optional headers. The envelope format is:
/// </para>
/// <code>
/// { "channel": "...", "payload": ..., "headers": ..., "correlationId": "..." }
/// </code>
/// <para>
/// Channel-based routing is performed client-side by inspecting the incoming
/// envelope's <c>channel</c> field and dispatching to the appropriate handler.
/// </para>
/// </remarks>
public sealed class WebSocketMessageTransport : IMessageDeliveryContextTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly WebSocketTransportOptions options;
    private readonly ClientWebSocket webSocket;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, Subscription> subscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> pendingReplies = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim sendSemaphore = new(1, 1);
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;
    private volatile bool disposed;

    private WebSocketMessageTransport(WebSocketTransportOptions options, ClientWebSocket webSocket)
    {
        this.options = options;
        this.webSocket = webSocket;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
    }

    /// <summary>
    /// Creates a new <see cref="WebSocketMessageTransport"/> instance, establishing
    /// the WebSocket connection and starting the receive loop.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A configured and connected transport instance.</returns>
    public static async ValueTask<WebSocketMessageTransport> CreateAsync(
        WebSocketTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ClientWebSocket ws = new();
        await ws.ConnectAsync(new Uri(options.ServerUri), cancellationToken).ConfigureAwait(false);

        WebSocketMessageTransport transport = new(options, ws);
        transport.StartReceiveLoop();
        return transport;
    }

    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        ObjectDisposedException.ThrowIf(this.disposed, this);

        // Build and rent envelope bytes so they survive the async send
        (byte[] rented, int length) = BuildPublishEnvelopeRented(channel, in payload, in headers, correlationId: null);

        return SendAndReturnAsync(rented, length, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.SubscribeCoreAsync(
            channel,
            new Subscription
            {
                DataHandler = (payload, headers, ct) => this.DispatchToContextHandlerAsync(channel, channelUtf8, handler, payload, headers, ct),
            },
            cancellationToken);
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
        string replyChannel = Encoding.UTF8.GetString(replyChannelUtf8.Span);
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);
        ObjectDisposedException.ThrowIf(this.disposed, this);

        (byte[] rented, int length) = BuildPublishEnvelopeRented(requestChannel, in request, in headers, correlationId, replyChannel);

        return RequestCoreAsync<TReply>(replyChannel, rented, length, correlationId, workspace, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.SubscribeCoreAsync(
            channel,
            new Subscription
            {
                DataHandler = (payload, headers, ct) => this.DispatchToHandlerAsync(channel, channelUtf8, handler, payload, headers, ct),
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeReplyAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.SubscribeCoreAsync(
            channel,
            new Subscription
            {
                ReplyHandler = (payload, headers, replyChannel, correlationId, ct) =>
                    this.DispatchToReplyHandlerAsync(channel, channelUtf8, handler, payload, headers, replyChannel, correlationId, ct),
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Local effect first, before any await: an unsubscribe always stops delivery to the
        // caller's handler, whatever the token state or the fate of the control envelope.
        if (!this.subscriptions.TryRemove(channel, out Subscription? removed))
        {
            return ValueTask.CompletedTask;
        }

        this.options.Heartbeat?.Stop(channel, "websocket", removed);

        (byte[] rented, int length) = BuildControlEnvelopeRented(channel, "unsubscribe"u8);
        return this.SendAndReturnAsync(rented, length, cancellationToken);
    }

    private ValueTask SubscribeCoreAsync(string channel, Subscription subscription, CancellationToken cancellationToken)
    {
        // A channel carries exactly one subscription. Claiming the slot with TryAdd is atomic,
        // so no lock is needed, and a second subscribe is refused rather than displacing the
        // first: displacing would mean tearing the previous subscription down on the subscribe
        // path, which a handler calling back in (the generated Abort path does) would deadlock.
        if (!this.subscriptions.TryAdd(channel, subscription))
        {
            throw new InvalidOperationException(
                $"Channel '{channel}' already has a subscription. Unsubscribe before subscribing again.");
        }

        this.options.Heartbeat?.Start(channel, "websocket", subscription);

        // Re-checked after the heartbeat start: a dispose walk that ran entirely between the
        // claim and the start has already stopped a heartbeat that did not exist yet, so
        // without this undo a disposed transport would report the channel Running forever.
        // The stop is unconditional — when the walk already removed the entry, the keyed
        // remove fails but the just-resurrected heartbeat still needs stopping.
        if (this.disposed)
        {
            this.subscriptions.TryRemove(KeyValuePair.Create(channel, subscription));
            this.options.Heartbeat?.Stop(channel, "websocket", subscription);
            throw new ObjectDisposedException(nameof(WebSocketMessageTransport));
        }

        return this.SendSubscribeAsync(channel, subscription, cancellationToken);
    }

    private async ValueTask SendSubscribeAsync(string channel, Subscription claimed, CancellationToken cancellationToken)
    {
        try
        {
            (byte[] rented, int length) = BuildControlEnvelopeRented(channel, "subscribe"u8);
            await this.SendAndReturnAsync(rented, length, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The relay never registered the channel, so release the slot and the heartbeat —
            // but only OUR claim: an unsubscribe-then-resubscribe may have replaced this slot
            // while the send was queued, and a key-only remove would evict the new subscriber
            // and stop its heartbeat.
            if (this.subscriptions.TryRemove(KeyValuePair.Create(channel, claimed)))
            {
                this.options.Heartbeat?.Stop(channel, "websocket", claimed);
            }

            throw;
        }
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
        ObjectDisposedException.ThrowIf(this.disposed, this);

        (byte[] rented, int length) = BuildDeadLetterEnvelopeRented(
            deadLetterChannel, originalChannel, in payload, in headers, exception);

        return SendAndReturnAsync(rented, length, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Removal is atomic and takes no lock, so disposal is never blocked behind a
        // subscribe or unsubscribe that is stuck on an unbounded socket send; cancelling
        // the receive loop and closing the socket below is what unblocks such a send. Each
        // live subscription's heartbeat is stopped as its entry goes, so a disposed
        // transport leaves no channel reporting Running, matching the other transports.
        foreach (string channel in this.subscriptions.Keys)
        {
            if (this.subscriptions.TryRemove(channel, out Subscription? removed))
            {
                this.options.Heartbeat?.Stop(channel, "websocket", removed);
            }
        }

        // A requester parked on a reply can only be released by this transport, so each pending
        // wait is failed rather than silently discarded; clearing alone would strand the awaiters.
        foreach (string correlationId in this.pendingReplies.Keys)
        {
            if (this.pendingReplies.TryRemove(correlationId, out TaskCompletionSource<byte[]>? pendingReply))
            {
                pendingReply.TrySetException(new ObjectDisposedException(nameof(WebSocketMessageTransport), "The transport was disposed before the reply arrived."));
            }
        }

        if (this.receiveCts is not null)
        {
            await this.receiveCts.CancelAsync().ConfigureAwait(false);

            if (this.receiveTask is not null)
            {
                try
                {
                    await this.receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            this.receiveCts.Dispose();
        }

        if (this.webSocket.State == WebSocketState.Open)
        {
            await this.webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Disposing",
                CancellationToken.None).ConfigureAwait(false);
        }

        this.webSocket.Dispose();
    }

    private void StartReceiveLoop()
    {
        this.receiveCts = new CancellationTokenSource();
        this.receiveTask = Task.Run(() => ReceiveLoopAsync(this.receiveCts.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[this.options.ReceiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   this.webSocket.State == WebSocketState.Open)
            {
                using MemoryStream ms = new();
                WebSocketReceiveResult result;

                do
                {
                    result = await this.webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                byte[] messageBytes = ms.ToArray();
                await DispatchEnvelopeAsync(messageBytes, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException)
        {
            // Connection dropped
        }
    }

    private async Task DispatchEnvelopeAsync(byte[] envelopeBytes, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> channelUtf8 = ReadOnlyMemory<byte>.Empty;
        string channel = string.Empty;

        try
        {
            using ParsedJsonDocument<WebSocketEnvelope> doc = ParsedJsonDocument<WebSocketEnvelope>.Parse(envelopeBytes);
            WebSocketEnvelope envelope = doc.RootElement;

            JsonString channelProp = envelope.Channel;
            if (channelProp.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("Received WebSocket envelope is missing a channel.");
            }

            channel = channelProp.GetString() ?? throw new InvalidOperationException("Received WebSocket envelope channel was null.");
            channelUtf8 = Encoding.UTF8.GetBytes(channel);

            JsonString corrIdProp = envelope.CorrelationId;
            string? envelopeCorrelationId = null;
            if (corrIdProp.ValueKind != JsonValueKind.Undefined)
            {
                envelopeCorrelationId = corrIdProp.GetString();

                // An envelope that names a reply channel while arriving on a channel this client
                // subscribes is a request in flight (the loopback shape), not a reply; matching it
                // against pendingReplies would let that subscription consume its own request as
                // its reply. On any other channel the correlation match stands, so a peer that
                // sets a reply channel on its reply (soliciting a follow-up) still completes the
                // pending request.
                if (envelopeCorrelationId is not null &&
                    (!envelope.TryGetProperty("replyChannel"u8, out JsonElement _) || !this.subscriptions.ContainsKey(channel)) &&
                    this.pendingReplies.TryRemove(envelopeCorrelationId, out TaskCompletionSource<byte[]>? tcs))
                {
                    tcs.SetResult(envelopeBytes);
                    return;
                }
            }

            JsonElement payload = envelope.Payload;
            JsonElement headers = envelope.Headers;

            if (payload.ValueKind != JsonValueKind.Undefined &&
                this.subscriptions.TryGetValue(channel, out Subscription? subscription))
            {
                this.options.Heartbeat?.Tick(channel, "websocket", subscription);
                if (subscription.ReplyHandler is not null)
                {
                    string? replyChannel = null;
                    if (envelope.TryGetProperty("replyChannel"u8, out JsonElement replyChannelEl) &&
                        replyChannelEl.ValueKind == JsonValueKind.String)
                    {
                        replyChannel = replyChannelEl.GetString();
                    }

                    await subscription.ReplyHandler(payload, headers, replyChannel, envelopeCorrelationId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await subscription.DataHandler!(payload, headers, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
            if (action == MessageErrorAction.DeadLetter)
            {
                string deadLetterChannel = channel.Length > 0
                    ? channel + this.options.DeadLetterSuffix
                    : this.options.DeadLetterSuffix;
                try
                {
                    await this.DeadLetterRawAsync(deadLetterChannel, channel, envelopeBytes, ex, cancellationToken).ConfigureAwait(false);
                    AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "websocket");
                }
                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                {
                    AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "websocket", dlEx);
                }
            }
            else if (action == MessageErrorAction.Abort && this.receiveCts is not null)
            {
                AsyncApiTelemetry.RecordAbort(channel, "websocket", MessageErrorKind.Deserialization);
                await this.receiveCts.CancelAsync().ConfigureAwait(false);
            }
            else
            {
                AsyncApiTelemetry.RecordSkip(channel, "websocket", MessageErrorKind.Deserialization);
            }
        }
    }

    private ValueTask DispatchToHandlerAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        JsonElement payload,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        TPayload typedPayload = JsonElementHelpers.Reinterpret<JsonElement, TPayload>(in payload);
        return this.DispatchDataAsync(
            channel,
            channelUtf8,
            payload,
            headers,
            (ct) => handler(typedPayload, headers, ct),
            cancellationToken);
    }

    private ValueTask DispatchToContextHandlerAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        JsonElement payload,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        TPayload typedPayload = JsonElementHelpers.Reinterpret<JsonElement, TPayload>(in payload);
        MessageDeliveryContext context = new()
        {
            ChannelUtf8 = channelUtf8,
            Headers = headers,
            NativeMessage = null,
        };
        return this.DispatchDataAsync(
            channel,
            channelUtf8,
            payload,
            headers,
            (ct) => handler(typedPayload, context, ct),
            cancellationToken);
    }

    // The single data-dispatch core: middleware invocation, the cancellation filter, and the
    // error-policy dead-letter/abort/skip block live here once, so the legacy and
    // delivery-context paths cannot drift.
    private async ValueTask DispatchDataAsync(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        JsonElement payload,
        JsonElement headers,
        Func<CancellationToken, ValueTask> invoke,
        CancellationToken cancellationToken)
    {
        try
        {
            if (this.middleware is not null)
            {
                await this.middleware(invoke, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await invoke(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, payload, headers);
            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
            if (action == MessageErrorAction.DeadLetter)
            {
                try
                {
                    string dlChannel = channel + this.options.DeadLetterSuffix;
                    (byte[] rented, int length) = BuildDeadLetterEnvelopeRented(dlChannel, channel, in payload, in headers, ex);
                    await this.SendAndReturnAsync(rented, length, cancellationToken).ConfigureAwait(false);
                    AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "websocket");
                }
                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                {
                    AsyncApiTelemetry.RecordDeadLetterFailure(channel + this.options.DeadLetterSuffix, channel, "websocket", dlEx);
                }
            }
            else if (action == MessageErrorAction.Abort)
            {
                AsyncApiTelemetry.RecordAbort(channel, "websocket", MessageErrorKind.Handler);
                await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                AsyncApiTelemetry.RecordSkip(channel, "websocket", MessageErrorKind.Handler);
            }
        }
    }

    private async ValueTask DispatchToReplyHandlerAsync<TRequest, TReply>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        JsonElement payload,
        JsonElement headers,
        string? replyChannel,
        string? correlationId,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        try
        {
            TRequest typedRequest = JsonElementHelpers.Reinterpret<JsonElement, TRequest>(in payload);

            TReply reply;
            if (this.middleware is not null)
            {
                TReply captured = default;
                await this.middleware(
                    async (ct) => captured = await handler(typedRequest, headers, ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                reply = captured;
            }
            else
            {
                reply = await handler(typedRequest, headers, cancellationToken).ConfigureAwait(false);
            }

            // Without a reply channel there is nowhere to send the response; the request cannot be answered.
            if (replyChannel is null)
            {
                AsyncApiTelemetry.RecordSkip(channel, "websocket", MessageErrorKind.Handler);
                return;
            }

            // Send the reply on the reply channel with the request's correlation id so the requester's
            // RequestAsync (which correlates by that id) receives it.
            JsonElement replyHeaders = default;
            (byte[] rented, int length) = BuildPublishEnvelopeRented(replyChannel, in reply, in replyHeaders, correlationId);
            await this.SendAndReturnAsync(rented, length, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, payload, headers);
            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
            if (action == MessageErrorAction.DeadLetter)
            {
                try
                {
                    string dlChannel = channel + this.options.DeadLetterSuffix;
                    (byte[] rented, int length) = BuildDeadLetterEnvelopeRented(dlChannel, channel, in payload, in headers, ex);
                    await this.SendAndReturnAsync(rented, length, cancellationToken).ConfigureAwait(false);
                    AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "websocket");
                }
                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                {
                    AsyncApiTelemetry.RecordDeadLetterFailure(channel + this.options.DeadLetterSuffix, channel, "websocket", dlEx);
                }
            }
            else if (action == MessageErrorAction.Abort)
            {
                AsyncApiTelemetry.RecordAbort(channel, "websocket", MessageErrorKind.Handler);
                await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                AsyncApiTelemetry.RecordSkip(channel, "websocket", MessageErrorKind.Handler);
            }
        }
    }

    private ValueTask DeadLetterRawAsync(
        string deadLetterChannel,
        string originalChannel,
        byte[] rawPayload,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (byte[] rented, int length) = BuildDeadLetterRawEnvelopeRented(deadLetterChannel, originalChannel, rawPayload, exception);
        return SendAndReturnAsync(rented, length, cancellationToken);
    }

    private async ValueTask SendAndReturnAsync(byte[] rented, int length, CancellationToken cancellationToken)
    {
        try
        {
            await this.SendEnvelopeCoreAsync(rented, length, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TReply>(
        string replyChannel,
        byte[] envelopeRented,
        int envelopeLength,
        string correlationId,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        _ = replyChannel;
        TaskCompletionSource<byte[]> replyTcs = new();
        this.pendingReplies[correlationId] = replyTcs;

        // Dispose may have walked pendingReplies before this entry landed; whichever side
        // removes it fails the wait, so a request racing disposal never parks forever.
        if (this.disposed)
        {
            this.pendingReplies.TryRemove(correlationId, out _);
            throw new ObjectDisposedException(nameof(WebSocketMessageTransport), "The transport was disposed before the reply arrived.");
        }

        try
        {
            await this.SendEnvelopeCoreAsync(envelopeRented, envelopeLength, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(envelopeRented);
        }

        try
        {
            byte[] replyEnvelopeBytes = await replyTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            // Parse the reply envelope with error handling
            try
            {
                // One envelope document backs both the returned payload and headers, and both are used after this
                // method returns, so the caller's workspace owns it (disposed with the workspace).
                ParsedJsonDocument<WebSocketEnvelope> replyDoc = ParsedJsonDocument<WebSocketEnvelope>.Parse(replyEnvelopeBytes);
                workspace.TakeOwnership(replyDoc);
                WebSocketEnvelope replyEnvelope = replyDoc.RootElement;

                JsonElement payloadEl = replyEnvelope.Payload;
                JsonElement headersEl = replyEnvelope.Headers;

                TReply replyPayload = JsonElementHelpers.Reinterpret<JsonElement, TReply>(in payloadEl);

                return (replyPayload, headersEl);
            }
            catch (Exception parseEx)
            {
                // Parse error - apply error policy
                ReadOnlyMemory<byte> replyChannelUtf8 = Encoding.UTF8.GetBytes(replyChannel);
                MessageErrorAction action = MessageErrorAction.Abort;

                if (this.errorPolicy is not null)
                {
                    action = await this.errorPolicy.HandleErrorAsync(
                        parseEx,
                        new MessageErrorContext(
                            replyChannelUtf8,
                            MessageErrorKind.Deserialization,
                            default,
                            default),
                        cancellationToken).ConfigureAwait(false);
                }

                switch (action)
                {
                    case MessageErrorAction.Abort:
                        AsyncApiTelemetry.RecordAbort(replyChannel, "websocket", MessageErrorKind.Deserialization);
                        throw;

                    case MessageErrorAction.DeadLetter:
                        string dlChannel = replyChannel + this.options.DeadLetterSuffix;
                        try
                        {
                            await DeadLetterRawAsync(dlChannel, replyChannel, replyEnvelopeBytes, parseEx, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, replyChannel, "websocket");
                        }
                        catch (Exception dlEx)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, replyChannel, "websocket", dlEx);
                        }

                        // Request failed even though we DLQ'd the bad reply
                        throw;

                    case MessageErrorAction.Skip:
                    default:
                        // Skip means fail for request-reply (can't skip a reply)
                        AsyncApiTelemetry.RecordSkip(replyChannel, "websocket", MessageErrorKind.Deserialization);
                        throw;
                }
            }
        }
        finally
        {
            this.pendingReplies.TryRemove(correlationId, out _);
        }
    }

    private async ValueTask SendEnvelopeCoreAsync(byte[] envelopeRented, int envelopeLength, CancellationToken cancellationToken)
    {
        await this.sendSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await this.webSocket.SendAsync(
                new ReadOnlyMemory<byte>(envelopeRented, 0, envelopeLength),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.sendSemaphore.Release();
        }
    }

    private static (byte[] Rented, int Length) BuildPublishEnvelopeRented<TPayload>(
        string channel,
        in TPayload payload,
        in JsonElement headers,
        string? correlationId,
        string? replyChannel = null)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);

        writer.WriteStartObject();
        writer.WriteString("channel"u8, channel);
        writer.WriteString("type"u8, "publish"u8);
        writer.WritePropertyName("payload"u8);
        payload.WriteTo(writer);

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("headers"u8);
            headers.WriteTo(writer);
        }

        if (correlationId is not null)
        {
            writer.WriteString("correlationId"u8, correlationId);
        }

        if (replyChannel is not null)
        {
            writer.WriteString("replyChannel"u8, replyChannel);
        }

        writer.WriteEndObject();
        writer.Flush();

        int length = buffer.WrittenCount;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(rented);
        return (rented, length);
    }

    private static (byte[] Rented, int Length) BuildControlEnvelopeRented(string channel, ReadOnlySpan<byte> type)
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);

        writer.WriteStartObject();
        writer.WriteString("channel"u8, channel);
        writer.WriteString("type"u8, type);
        writer.WriteEndObject();
        writer.Flush();

        int length = buffer.WrittenCount;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(rented);
        return (rented, length);
    }

    private static (byte[] Rented, int Length) BuildDeadLetterRawEnvelopeRented(
        string deadLetterChannel,
        string originalChannel,
        ReadOnlySpan<byte> rawPayload,
        Exception exception)
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);

        writer.WriteStartObject();
        writer.WriteString("channel"u8, deadLetterChannel);
        writer.WriteString("type"u8, "dead-letter"u8);
        writer.WriteString("originalChannel"u8, originalChannel);
        writer.WriteString("error"u8, exception.Message);
        writer.WriteString("errorType"u8, exception.GetType().FullName ?? exception.GetType().Name);
        writer.WriteString("payload"u8, Convert.ToBase64String(rawPayload));
        writer.WriteEndObject();
        writer.Flush();

        int length = buffer.WrittenCount;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(rented);
        return (rented, length);
    }

    private static (byte[] Rented, int Length) BuildDeadLetterEnvelopeRented(
        string deadLetterChannel,
        string originalChannel,
        in JsonElement payload,
        in JsonElement headers,
        Exception exception)
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);

        writer.WriteStartObject();
        writer.WriteString("channel"u8, deadLetterChannel);
        writer.WriteString("type"u8, "dead-letter"u8);
        writer.WriteString("originalChannel"u8, originalChannel);
        writer.WriteString("error"u8, exception.Message);
        writer.WriteString("errorType"u8, exception.GetType().FullName ?? exception.GetType().Name);

        if (payload.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("payload"u8);
            payload.WriteTo(writer);
        }

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("headers"u8);
            headers.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        int length = buffer.WrittenCount;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(rented);
        return (rented, length);
    }

    /// <summary>
    /// One channel's subscription. A channel has exactly one: data subscriptions (legacy and
    /// delivery-context) store <see cref="DataHandler"/>; responders store <see cref="ReplyHandler"/>.
    /// </summary>
    private sealed class Subscription
    {
        public Func<JsonElement, JsonElement, CancellationToken, ValueTask>? DataHandler { get; init; }

        public Func<JsonElement, JsonElement, string?, string?, CancellationToken, ValueTask>? ReplyHandler { get; init; }
    }
}