// <copyright file="MqttMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json.AsyncApi.Internal;
using Corvus.Text.Json.Internal;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace Corvus.Text.Json.AsyncApi.Mqtt;

/// <summary>
/// MQTT implementation of <see cref="IMessageTransport"/> using MQTTnet.
/// </summary>
/// <remarks>
/// <para>
/// This transport maps AsyncAPI channels to MQTT topics. Messages are serialized
/// as UTF-8 JSON using <c>WriteTo(Utf8JsonWriter)</c>. MQTT 5.0 user properties
/// carry headers and correlation IDs for the request/reply pattern.
/// </para>
/// <para>
/// Subscriptions use MQTT topic filters. The transport maintains a single
/// persistent connection and multiplexes all publish/subscribe operations
/// over it.
/// </para>
/// <para>
/// Publish operations serialize into a thread-static buffer and rent from
/// <see cref="ArrayPool{T}"/> for the MQTT payload, returning the rental after
/// the publish completes.
/// </para>
/// </remarks>
public sealed class MqttMessageTransport : IMessageDeliveryContextTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly MqttTransportOptions options;
    private readonly IMqttClient client;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, Func<MqttApplicationMessage, CancellationToken, ValueTask>> handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ReadOnlyMemory<byte>, TaskCompletionSource<MqttApplicationMessage>> pendingReplies = new(ReadOnlyMemoryByteComparer.Instance);
    private volatile bool disposed;

    private MqttMessageTransport(MqttTransportOptions options, IMqttClient client)
    {
        this.options = options;
        this.client = client;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
    }

    /// <summary>
    /// Creates a new <see cref="MqttMessageTransport"/> instance, establishing the
    /// MQTT connection.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A configured and connected transport instance.</returns>
    public static async ValueTask<MqttMessageTransport> CreateAsync(
        MqttTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        MqttFactory factory = new();
        IMqttClient client = factory.CreateMqttClient();

        MqttClientOptionsBuilder optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Host, options.Port)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanSession(options.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(options.KeepAliveSeconds));

        if (!string.IsNullOrEmpty(options.ClientId))
        {
            optionsBuilder.WithClientId(options.ClientId);
        }

        if (options.Username is not null)
        {
            optionsBuilder.WithCredentials(options.Username, options.Password);
        }

        MqttClientOptions mqttOptions = optionsBuilder.Build();
        MqttMessageTransport transport = new(options, client);

        client.ApplicationMessageReceivedAsync += transport.HandleMessageReceivedAsync;

        await client.ConnectAsync(mqttOptions, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Serialize headers first (reuses the shared buffer, produces a string result)
        string? headersBase64 = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToBase64String(in headers)
            : null;

        // Serialize payload into the shared buffer, then rent for MQTT
        (byte[] rented, int length) = SerializeToRented(in payload);
        return PublishAndReturnAsync(channel, rented, length, headersBase64, cancellationToken);
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
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string requestChannel = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        string replyChannel = Encoding.UTF8.GetString(replyChannelUtf8.Span);
        string? headersBase64 = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToBase64String(in headers)
            : null;

        (byte[] rented, int length) = SerializeToRented(in request);
        return RequestCoreAsync<TReply>(requestChannel, replyChannel, rented, length, correlationIdUtf8, headersBase64, workspace, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        return SubscribeCoreAsync(channel, channelUtf8, MessageHandler<TPayload>.WithoutDeliveryContext(handler), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        return SubscribeCoreAsync(channel, channelUtf8, MessageHandler<TPayload>.WithDeliveryContext(handler), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeReplyAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        return SubscribeReplyCoreAsync(channel, channelUtf8, handler, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Local effect first, and an early out when there is nothing to remove: a double
        // unsubscribe (or one against a disposed transport) must be a quiet no-op rather
        // than a broker call that throws on a disposed client — matching every other transport.
        if (!this.handlers.TryRemove(channel, out Func<MqttApplicationMessage, CancellationToken, ValueTask>? removed))
        {
            return;
        }

        this.options.Heartbeat?.Stop(channel, "mqtt", removed);

        MqttClientUnsubscribeOptions unsubOptions = new MqttFactory().CreateUnsubscribeOptionsBuilder()
            .WithTopicFilter(channel)
            .Build();

        try
        {
            await this.client.UnsubscribeAsync(unsubOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The broker still holds the topic filter, so restore the local entry (and its
            // heartbeat) to keep the stop retryable — the generated consumer restores its
            // gate token on a throwing stop and retries, and without this restore the retry
            // would hit the early-out above, report success, and leave the broker delivering
            // to a topic with no handler. If a new subscription claimed the channel in the
            // window, it owns the topic now and the old entry is deliberately dropped.
            if (this.handlers.TryAdd(channel, removed))
            {
                this.options.Heartbeat?.Start(channel, "mqtt", removed);
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
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string deadLetterChannel = Encoding.UTF8.GetString(deadLetterChannelUtf8.Span);
        string originalChannel = Encoding.UTF8.GetString(originalChannelUtf8.Span);
        string? headersBase64 = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToBase64String(in headers)
            : null;

        byte[] rented;
        int length;
        if (payload.ValueKind != JsonValueKind.Undefined)
        {
            (rented, length) = SerializeToRented(in payload);
        }
        else
        {
            rented = [];
            length = 0;
        }

        return DeadLetterCoreAsync(deadLetterChannel, originalChannel, rented, length, headersBase64, exception, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Stop each live subscription's heartbeat as its entry is removed, so a disposed
        // transport leaves no channel reporting Running, matching the other transports.
        foreach (string channel in this.handlers.Keys)
        {
            if (this.handlers.TryRemove(channel, out Func<MqttApplicationMessage, CancellationToken, ValueTask>? removed))
            {
                this.options.Heartbeat?.Stop(channel, "mqtt", removed);
            }
        }

        // A requester parked on a reply can only be released by this transport, so each pending
        // wait is failed rather than silently discarded; clearing alone would strand the awaiters.
        foreach (ReadOnlyMemory<byte> correlationId in this.pendingReplies.Keys)
        {
            if (this.pendingReplies.TryRemove(correlationId, out TaskCompletionSource<MqttApplicationMessage>? pendingReply))
            {
                pendingReply.TrySetException(new ObjectDisposedException(nameof(MqttMessageTransport), "The transport was disposed before the reply arrived."));
            }
        }

        if (this.client.IsConnected)
        {
            await this.client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build()).ConfigureAwait(false);
        }

        this.client.Dispose();
    }

    private async ValueTask PublishAndReturnAsync(
        string channel,
        byte[] rented,
        int length,
        string? headersBase64,
        CancellationToken cancellationToken)
    {
        try
        {
            MqttApplicationMessage message = BuildMessage(channel, rented, length, headersBase64, default);
            await this.client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (rented.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TReply>(
        string requestChannel,
        string replyChannel,
        byte[] rented,
        int length,
        ReadOnlyMemory<byte> correlationIdUtf8,
        string? headersBase64,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        TaskCompletionSource<MqttApplicationMessage> replyTcs = new();
        this.pendingReplies[correlationIdUtf8] = replyTcs;

        // Dispose may have walked pendingReplies before this entry landed; whichever side
        // removes it fails the wait, so a request racing disposal never parks forever.
        if (this.disposed)
        {
            this.pendingReplies.TryRemove(correlationIdUtf8, out _);
            ArrayPool<byte>.Shared.Return(rented);
            throw new ObjectDisposedException(nameof(MqttMessageTransport), "The transport was disposed before the reply arrived.");
        }

        try
        {
            // Ensure we're subscribed to the reply topic
            if (!this.handlers.ContainsKey(replyChannel))
            {
                MqttClientSubscribeOptions subOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(replyChannel, this.options.QualityOfServiceLevel)
                    .Build();

                await this.client.SubscribeAsync(subOptions, cancellationToken).ConfigureAwait(false);
            }

            // Publish the request, advertising the reply topic via the native MQTT 5.0 ResponseTopic
            // so a Corvus responder (SubscribeReplyAsync) knows where to send the correlated reply.
            MqttApplicationMessage requestMsg = BuildMessage(requestChannel, rented, length, headersBase64, correlationIdUtf8, replyChannel);
            await this.client.PublishAsync(requestMsg, cancellationToken).ConfigureAwait(false);

            // Wait for correlated reply
            MqttApplicationMessage reply = await replyTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            // Parse reply with error handling
            try
            {
                TReply replyPayload;
                if (reply.PayloadSegment is { Count: > 0 })
                {
                    // Cold path — the caller's workspace owns the returned reply document (disposed with it),
                    // since the returned value is used after this method returns.
                    ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(
                        reply.PayloadSegment.Array!.AsMemory(reply.PayloadSegment.Offset, reply.PayloadSegment.Count));
                    workspace.TakeOwnership(replyDoc);
                    replyPayload = replyDoc.RootElement;
                }
                else
                {
                    replyPayload = default;
                }

                ParsedJsonDocument<JsonElement>? headersDoc = DecodeHeadersDocument(reply);
                if (headersDoc is not null)
                {
                    workspace.TakeOwnership(headersDoc);
                }

                JsonElement replyHeaders = headersDoc?.RootElement ?? default;
                return (replyPayload, replyHeaders);
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
                        AsyncApiTelemetry.RecordAbort(replyChannel, "mqtt", MessageErrorKind.Deserialization);
                        throw;

                    case MessageErrorAction.DeadLetter:
                        string dlChannel = replyChannel + this.options.DeadLetterSuffix;
                        try
                        {
                            await DeadLetterRawAsync(dlChannel, replyChannel, reply.PayloadSegment, parseEx, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, replyChannel, "mqtt");
                        }
                        catch (Exception dlEx)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, replyChannel, "mqtt", dlEx);
                        }

                        // Request failed even though we DLQ'd the bad reply
                        throw;

                    case MessageErrorAction.Skip:
                    default:
                        // Skip means fail for request-reply (can't skip a reply)
                        AsyncApiTelemetry.RecordSkip(replyChannel, "mqtt", MessageErrorKind.Deserialization);
                        throw;
                }
            }
        }
        finally
        {
            this.pendingReplies.TryRemove(correlationIdUtf8, out _);
            if (rented.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private async ValueTask SubscribeCoreAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        MessageHandler<TPayload> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string dlChannel = channel + this.options.DeadLetterSuffix;
        Func<MqttApplicationMessage, CancellationToken, ValueTask> dispatch =
            (message, ct) => this.DispatchToHandlerAsync(channel, channelUtf8, dlChannel, handler, message, ct);
        this.ClaimChannel(channel, dispatch);
        await this.SendSubscribeAsync(channel, dispatch, cancellationToken).ConfigureAwait(false);
    }

    private void ClaimChannel(string channel, Func<MqttApplicationMessage, CancellationToken, ValueTask> dispatch)
    {
        // A channel carries exactly one subscription, claimed atomically with TryAdd. A second
        // subscribe is refused rather than replacing the first, so a caller cannot silently
        // take over a channel another consumer is serving.
        if (!this.handlers.TryAdd(channel, dispatch))
        {
            throw new InvalidOperationException(
                $"Channel '{channel}' already has a subscription. Unsubscribe before subscribing again.");
        }

        this.options.Heartbeat?.Start(channel, "mqtt", dispatch);

        // Re-checked after the heartbeat start: a dispose walk that ran entirely between the
        // claim and the start has already stopped a heartbeat that did not exist yet, so
        // without this undo a disposed transport would report the channel Running forever.
        if (this.disposed)
        {
            // The stop is unconditional: when the walk already removed the entry, the keyed
            // remove fails but the just-resurrected heartbeat still needs stopping, and the
            // owner guard makes a stray stop harmless.
            this.handlers.TryRemove(KeyValuePair.Create(channel, dispatch));
            this.options.Heartbeat?.Stop(channel, "mqtt", dispatch);
            throw new ObjectDisposedException(nameof(MqttMessageTransport));
        }
    }

    private async ValueTask SendSubscribeAsync(
        string channel,
        Func<MqttApplicationMessage, CancellationToken, ValueTask> claimed,
        CancellationToken cancellationToken)
    {
        MqttClientSubscribeOptions subOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
            .WithTopicFilter(channel, this.options.QualityOfServiceLevel)
            .Build();

        try
        {
            await this.client.SubscribeAsync(subOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The broker never registered the topic filter, so release the slot and the
            // heartbeat — but only OUR claim: an unsubscribe-then-resubscribe may have
            // replaced this slot while the send was in flight, and a key-only remove would
            // evict the new subscriber and stop its heartbeat.
            if (this.handlers.TryRemove(KeyValuePair.Create(channel, claimed)))
            {
                this.options.Heartbeat?.Stop(channel, "mqtt", claimed);
            }

            throw;
        }
    }

    private async ValueTask SubscribeReplyCoreAsync<TRequest, TReply>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string dlChannel = channel + this.options.DeadLetterSuffix;
        Func<MqttApplicationMessage, CancellationToken, ValueTask> dispatch =
            (message, ct) => this.DispatchToResponderAsync(channel, channelUtf8, dlChannel, handler, message, ct);
        this.ClaimChannel(channel, dispatch);
        await this.SendSubscribeAsync(channel, dispatch, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask DeadLetterCoreAsync(
        string deadLetterChannel,
        string originalChannel,
        byte[] rented,
        int length,
        string? headersBase64,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            MqttApplicationMessage message = new()
            {
                Topic = deadLetterChannel,
                PayloadSegment = length > 0 ? new ArraySegment<byte>(rented, 0, length) : default,
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                Retain = false,
                ContentType = "application/json",
                UserProperties =
                [
                    new MqttUserProperty("corvus-original-channel", originalChannel),
                    new MqttUserProperty("corvus-error", exception.Message),
                    new MqttUserProperty("corvus-error-type", exception.GetType().FullName ?? exception.GetType().Name),
                ],
            };

            if (headersBase64 is not null)
            {
                message.UserProperties.Add(new MqttUserProperty(this.options.HeadersPropertyKey, headersBase64));
            }

            await this.client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (rented.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private async ValueTask DeadLetterRawAsync(
        string deadLetterChannel,
        string originalChannel,
        ArraySegment<byte> rawPayload,
        Exception exception,
        CancellationToken cancellationToken)
    {
        MqttApplicationMessage message = new()
        {
            Topic = deadLetterChannel,
            PayloadSegment = rawPayload,
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            ContentType = "application/json",
            UserProperties =
            [
                new MqttUserProperty("corvus-original-channel", originalChannel),
                new MqttUserProperty("corvus-error", exception.Message),
                new MqttUserProperty("corvus-error-type", exception.GetType().FullName ?? exception.GetType().Name),
            ],
        };

        await this.client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private MqttApplicationMessage BuildMessage(
        string topic,
        byte[] rented,
        int length,
        string? headersBase64,
        ReadOnlyMemory<byte> correlationIdUtf8,
        string? responseTopic = null)
    {
        MqttApplicationMessage message = new()
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(rented, 0, length),
            QualityOfServiceLevel = this.options.QualityOfServiceLevel,
            Retain = this.options.Retain,
            ContentType = "application/json",
            ResponseTopic = responseTopic,
        };

        if (headersBase64 is not null)
        {
            message.UserProperties ??= [];
            message.UserProperties.Add(new MqttUserProperty(this.options.HeadersPropertyKey, headersBase64));
        }

        if (!correlationIdUtf8.IsEmpty)
        {
            // Use the backing array directly for CorrelationData (zero-copy when exactly-sized).
            byte[] corrBytes = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(correlationIdUtf8, out ArraySegment<byte> segment)
                && segment.Offset == 0 && segment.Count == segment.Array!.Length
                ? segment.Array
                : correlationIdUtf8.ToArray();
            message.CorrelationData = corrBytes;
            message.UserProperties ??= [];
            message.UserProperties.Add(new MqttUserProperty(this.options.CorrelationIdPropertyKey, Encoding.UTF8.GetString(correlationIdUtf8.Span)));
        }

        return message;
    }

    private Task HandleMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        string topic = args.ApplicationMessage.Topic;

        byte[]? corrData = args.ApplicationMessage.CorrelationData;

        // A message that advertises a response topic while arriving on a topic this client
        // data-subscribes is a request in flight (the loopback shape, which brokers deliver back
        // to the sender), not a reply; matching it against pendingReplies would let that
        // subscription consume its own request as its reply. On any other topic the correlation
        // match stands, because an MQTT 5 responder may legitimately set a response topic on its
        // reply to solicit a follow-up. The handler lookup is lock-free, so an unsubscribe that
        // completes between a self-loopback request's publish and its delivery can make this read
        // miss and the request correlate as its own reply; that window needs the loopback shape
        // plus a concurrent unsubscribe of the same topic, and is accepted in trade for correct
        // MQTT 5 responder interop, which is deterministic rather than a race.
        if ((string.IsNullOrEmpty(args.ApplicationMessage.ResponseTopic) || !this.handlers.ContainsKey(topic)) &&
            corrData is { Length: > 0 } &&
            this.pendingReplies.TryRemove(new ReadOnlyMemory<byte>(corrData), out TaskCompletionSource<MqttApplicationMessage>? tcs))
        {
            tcs.SetResult(args.ApplicationMessage);
            return Task.CompletedTask;
        }

        if (this.handlers.TryGetValue(topic, out Func<MqttApplicationMessage, CancellationToken, ValueTask>? handler))
        {
            // Ticked here, where the registry lookup proves the dispatch delegate is the
            // channel's current owner, rather than inside the dispatch methods that lack it.
            this.options.Heartbeat?.Tick(topic, "mqtt", handler);
            return handler(args.ApplicationMessage, CancellationToken.None).AsTask();
        }

        return Task.CompletedTask;
    }

    private async ValueTask DispatchToHandlerAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        string deadLetterChannel,
        MessageHandler<TPayload> handler,
        MqttApplicationMessage message,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ParsedJsonDocument<TPayload> payloadDoc;
        try
        {
            ArraySegment<byte> payloadSegment = message.PayloadSegment;
            ReadOnlyMemory<byte> payloadMemory = payloadSegment.Array is null
                ? ReadOnlyMemory<byte>.Empty
                : payloadSegment.Array.AsMemory(payloadSegment.Offset, payloadSegment.Count);
            payloadDoc = ParsedJsonDocument<TPayload>.Parse(payloadMemory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
            if (action == MessageErrorAction.DeadLetter)
            {
                try
                {
                    await this.DeadLetterRawAsync(deadLetterChannel, channel, message.PayloadSegment, ex, cancellationToken).ConfigureAwait(false);
                    AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "mqtt");
                }
                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                {
                    AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "mqtt", dlEx);
                }
            }
            else if (action == MessageErrorAction.Abort)
            {
                AsyncApiTelemetry.RecordAbort(channel, "mqtt", MessageErrorKind.Deserialization);
                await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Deserialization);
            }

            return;
        }

        using (payloadDoc)
        {
            TPayload payload = payloadDoc.RootElement;
            JsonElement payloadElement = JsonElement.From(in payload);

            ParsedJsonDocument<JsonElement>? headersDoc;
            try
            {
                headersDoc = this.DecodeHeadersDocument(message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                if (action == MessageErrorAction.DeadLetter)
                {
                    try
                    {
                        await this.DeadLetterRawAsync(deadLetterChannel, channel, message.PayloadSegment, ex, cancellationToken).ConfigureAwait(false);
                        AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "mqtt");
                    }
                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                    {
                        AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "mqtt", dlEx);
                    }
                }
                else if (action == MessageErrorAction.Abort)
                {
                    AsyncApiTelemetry.RecordAbort(channel, "mqtt", MessageErrorKind.Deserialization);
                    await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Deserialization);
                }

                return;
            }

            using (headersDoc)
            {
                JsonElement headers = headersDoc?.RootElement ?? default;

                try
                {
                    if (this.middleware is not null)
                    {
                        await this.middleware((ct) => handler.Invoke(payload, channelUtf8, headers, message, ct), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await handler.Invoke(payload, channelUtf8, headers, message, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, payloadElement, headers);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            byte[] rented;
                            int length;
                            if (payloadElement.ValueKind != JsonValueKind.Undefined)
                            {
                                (rented, length) = SerializeToRented(in payloadElement);
                            }
                            else
                            {
                                rented = [];
                                length = 0;
                            }

                            string? headersBase64 = headers.ValueKind != JsonValueKind.Undefined
                                ? SerializeToBase64String(in headers)
                                : null;

                            await this.DeadLetterCoreAsync(deadLetterChannel, channel, rented, length, headersBase64, ex, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "mqtt");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "mqtt", dlEx);
                        }
                    }
                    else if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "mqtt", MessageErrorKind.Handler);
                        await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Handler);
                    }
                }
            }
        }
    }

    private async ValueTask DispatchToResponderAsync<TRequest, TReply>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        string deadLetterChannel,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        MqttApplicationMessage message,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ParsedJsonDocument<TRequest> requestDoc;
        try
        {
            ArraySegment<byte> payloadSegment = message.PayloadSegment;
            ReadOnlyMemory<byte> payloadMemory = payloadSegment.Array is null
                ? ReadOnlyMemory<byte>.Empty
                : payloadSegment.Array.AsMemory(payloadSegment.Offset, payloadSegment.Count);
            requestDoc = ParsedJsonDocument<TRequest>.Parse(payloadMemory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
            if (action == MessageErrorAction.DeadLetter)
            {
                try
                {
                    await this.DeadLetterRawAsync(deadLetterChannel, channel, message.PayloadSegment, ex, cancellationToken).ConfigureAwait(false);
                    AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "mqtt");
                }
                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                {
                    AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "mqtt", dlEx);
                }
            }
            else if (action == MessageErrorAction.Abort)
            {
                AsyncApiTelemetry.RecordAbort(channel, "mqtt", MessageErrorKind.Deserialization);
                await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Deserialization);
            }

            return;
        }

        using (requestDoc)
        {
            TRequest request = requestDoc.RootElement;
            JsonElement requestElement = JsonElement.From(in request);

            ParsedJsonDocument<JsonElement>? headersDoc;
            try
            {
                headersDoc = this.DecodeHeadersDocument(message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                if (action == MessageErrorAction.DeadLetter)
                {
                    try
                    {
                        await this.DeadLetterRawAsync(deadLetterChannel, channel, message.PayloadSegment, ex, cancellationToken).ConfigureAwait(false);
                        AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "mqtt");
                    }
                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                    {
                        AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "mqtt", dlEx);
                    }
                }
                else if (action == MessageErrorAction.Abort)
                {
                    AsyncApiTelemetry.RecordAbort(channel, "mqtt", MessageErrorKind.Deserialization);
                    await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Deserialization);
                }

                return;
            }

            using (headersDoc)
            {
                JsonElement headers = headersDoc?.RootElement ?? default;

                try
                {
                    TReply reply;
                    if (this.middleware is not null)
                    {
                        TReply captured = default;
                        await this.middleware(async (ct) => captured = await handler(request, headers, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                        reply = captured;
                    }
                    else
                    {
                        reply = await handler(request, headers, cancellationToken).ConfigureAwait(false);
                    }

                    // Determine where to send the reply: the requester advertises the reply topic via
                    // the native MQTT 5.0 ResponseTopic, and correlates the reply by CorrelationData.
                    string? responseTopic = message.ResponseTopic;
                    if (string.IsNullOrEmpty(responseTopic))
                    {
                        // No reply address — nothing to respond to.
                        AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Handler);
                        return;
                    }

                    ReadOnlyMemory<byte> correlationIdUtf8 = message.CorrelationData is { Length: > 0 } corr
                        ? corr
                        : default;

                    (byte[] rented, int length) = SerializeToRented(in reply);
                    try
                    {
                        MqttApplicationMessage replyMsg = BuildMessage(responseTopic, rented, length, null, correlationIdUtf8);
                        await this.client.PublishAsync(replyMsg, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (rented.Length > 0)
                        {
                            ArrayPool<byte>.Shared.Return(rented);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, requestElement, headers);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            byte[] rented;
                            int length;
                            if (requestElement.ValueKind != JsonValueKind.Undefined)
                            {
                                (rented, length) = SerializeToRented(in requestElement);
                            }
                            else
                            {
                                rented = [];
                                length = 0;
                            }

                            string? headersBase64 = headers.ValueKind != JsonValueKind.Undefined
                                ? SerializeToBase64String(in headers)
                                : null;

                            await this.DeadLetterCoreAsync(deadLetterChannel, channel, rented, length, headersBase64, ex, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(deadLetterChannel, channel, "mqtt");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(deadLetterChannel, channel, "mqtt", dlEx);
                        }
                    }
                    else if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "mqtt", MessageErrorKind.Handler);
                        await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        AsyncApiTelemetry.RecordSkip(channel, "mqtt", MessageErrorKind.Handler);
                    }
                }
            }
        }
    }

    private ParsedJsonDocument<JsonElement>? DecodeHeadersDocument(MqttApplicationMessage message)
    {
        if (message.UserProperties is null)
        {
            return null;
        }

        foreach (MqttUserProperty prop in message.UserProperties)
        {
            if (prop.Name == this.options.HeadersPropertyKey)
            {
                int maxBytes = ((prop.Value.Length * 3) + 3) / 4;
                byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
                if (Convert.TryFromBase64String(prop.Value, rented, out int bytesWritten))
                {
                    // Transfer ownership — document returns the array on Dispose()
                    return ParsedJsonDocument<JsonElement>.Parse(rented.AsMemory(0, bytesWritten), rented);
                }

                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        return null;
    }

    private static (byte[] Rented, int Length) SerializeToRented<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);
        value.WriteTo(writer);
        writer.Flush();
        int length = buffer.WrittenCount;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        buffer.WrittenSpan.CopyTo(rented);
        return (rented, length);
    }

    private static string SerializeToBase64String<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        ArrayBufferWriter<byte> buffer = t_serializeBuffer ??= new(512);
        buffer.Clear();
        Utf8JsonWriter writer = t_writer ??= new(buffer);
        writer.Reset(buffer);
        value.WriteTo(writer);
        writer.Flush();
        return Convert.ToBase64String(buffer.WrittenSpan);
    }
}