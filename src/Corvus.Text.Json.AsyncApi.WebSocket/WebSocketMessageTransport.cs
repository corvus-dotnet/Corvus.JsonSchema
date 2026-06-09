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
public sealed class WebSocketMessageTransport : IMessageTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly WebSocketTransportOptions options;
    private readonly ClientWebSocket webSocket;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, Func<JsonElement, JsonElement, CancellationToken, ValueTask>> handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<JsonElement, JsonElement, string?, string?, CancellationToken, ValueTask>> replyHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> pendingReplies = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim sendSemaphore = new(1, 1);
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;
    private bool disposed;

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
        string replyChannel = Encoding.UTF8.GetString(replyChannelUtf8.Span);
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);
        ObjectDisposedException.ThrowIf(this.disposed, this);

        (byte[] rented, int length) = BuildPublishEnvelopeRented(requestChannel, in request, in headers, correlationId, replyChannel);

        return RequestCoreAsync<TReply>(replyChannel, rented, length, correlationId, cancellationToken);
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
        this.handlers[channel] = (payload, headers, ct) => this.DispatchToHandlerAsync(channel, channelUtf8, handler, payload, headers, ct);

        this.options.Heartbeat?.Start(channel, "websocket");

        // Send a subscribe envelope to the server
        (byte[] rented, int length) = BuildControlEnvelopeRented(channel, "subscribe"u8);
        return SendAndReturnAsync(rented, length, cancellationToken);
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
        this.replyHandlers[channel] = (payload, headers, replyChannel, correlationId, ct) =>
            this.DispatchToReplyHandlerAsync(channel, channelUtf8, handler, payload, headers, replyChannel, correlationId, ct);

        this.options.Heartbeat?.Start(channel, "websocket");

        // Send a subscribe envelope to the server so requests are routed to this connection
        (byte[] rented, int length) = BuildControlEnvelopeRented(channel, "subscribe"u8);
        return SendAndReturnAsync(rented, length, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        this.handlers.TryRemove(channel, out _);
        this.replyHandlers.TryRemove(channel, out _);

        this.options.Heartbeat?.Stop(channel, "websocket");

        // Send an unsubscribe envelope to the server
        (byte[] rented, int length) = BuildControlEnvelopeRented(channel, "unsubscribe"u8);
        return SendAndReturnAsync(rented, length, cancellationToken);
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
        this.handlers.Clear();
        this.replyHandlers.Clear();
        this.pendingReplies.Clear();

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
                if (envelopeCorrelationId is not null &&
                    this.pendingReplies.TryRemove(envelopeCorrelationId, out TaskCompletionSource<byte[]>? tcs))
                {
                    tcs.SetResult(envelopeBytes);
                    return;
                }
            }

            JsonElement payload = envelope.Payload;
            JsonElement headers = envelope.Headers;

            if (payload.ValueKind != JsonValueKind.Undefined &&
                this.replyHandlers.TryGetValue(channel, out Func<JsonElement, JsonElement, string?, string?, CancellationToken, ValueTask>? replyHandler))
            {
                string? replyChannel = null;
                if (envelope.TryGetProperty("replyChannel"u8, out JsonElement replyChannelEl) &&
                    replyChannelEl.ValueKind == JsonValueKind.String)
                {
                    replyChannel = replyChannelEl.GetString();
                }

                this.options.Heartbeat?.Tick(channel, "websocket");
                await replyHandler(payload, headers, replyChannel, envelopeCorrelationId, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (payload.ValueKind != JsonValueKind.Undefined &&
                this.handlers.TryGetValue(channel, out Func<JsonElement, JsonElement, CancellationToken, ValueTask>? handler))
            {
                this.options.Heartbeat?.Tick(channel, "websocket");
                await handler(payload, headers, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask DispatchToHandlerAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        JsonElement payload,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        try
        {
            TPayload typedPayload = JsonElementHelpers.Reinterpret<JsonElement, TPayload>(in payload);
            if (this.middleware is not null)
            {
                await this.middleware((ct) => handler(typedPayload, headers, ct), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await handler(typedPayload, headers, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        _ = replyChannel;
        TaskCompletionSource<byte[]> replyTcs = new();
        this.pendingReplies[correlationId] = replyTcs;

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
                ParsedJsonDocument<WebSocketEnvelope> replyDoc = ParsedJsonDocument<WebSocketEnvelope>.Parse(replyEnvelopeBytes);
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
}