// <copyright file="AmqpMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json.AsyncApi.Internal;
using Corvus.Text.Json.Internal;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Corvus.Text.Json.AsyncApi.Amqp;

/// <summary>
/// AMQP 0-9-1 implementation of <see cref="IMessageTransport"/> using RabbitMQ.Client.
/// </summary>
/// <remarks>
/// <para>
/// This transport maps AsyncAPI channels to AMQP routing keys. Messages are serialized
/// as UTF-8 JSON using <c>WriteTo(Utf8JsonWriter)</c> for zero-allocation serialization.
/// Headers are stored in AMQP message properties headers table.
/// </para>
/// <para>
/// Each subscription creates a consumer on a dedicated queue bound to the configured
/// exchange with the channel name as the routing key. Queue names follow the pattern
/// <c>{consumerTagPrefix}.{channel}</c>.
/// </para>
/// </remarks>
public sealed class AmqpMessageTransport : IMessageDeliveryContextTransport, IHealthCheckableTransport
{
    private static readonly byte[] HeadersKey = "corvus-headers"u8.ToArray();
    private const string HeadersKeyString = "corvus-headers";
    private static readonly byte[] CorrelationIdKey = "corvus-correlation-id"u8.ToArray();
    private const string CorrelationIdKeyString = "corvus-correlation-id";

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly AmqpTransportOptions options;
    private readonly IConnection connection;
    private readonly IChannel publishChannel;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, SubscriptionState> subscriptions = new(StringComparer.Ordinal);

    // Flows from each received-message wrapper into the handlers it invokes, so teardown can
    // recognize a handler stopping its own subscription and defer the channel close that would
    // otherwise join the dispatcher worker executing the caller.
    private static readonly AsyncLocal<object?> ExecutingSubscription = new();

    // Internal reply-channel consumers for RequestAsync live apart from the application-visible
    // registry: they are transport-scoped machinery, so they must neither block an application
    // subscribe on the reply channel nor die with the first requester's token.
    private readonly ConcurrentDictionary<string, SubscriptionState> replyConsumers = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<BasicDeliverEventArgs>> pendingReplies = new(StringComparer.Ordinal);
    private volatile bool disposed;

    private AmqpMessageTransport(AmqpTransportOptions options, IConnection connection, IChannel publishChannel)
    {
        this.options = options;
        this.connection = connection;
        this.publishChannel = publishChannel;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
    }

    /// <inheritdoc/>
    public bool IsConnected => !this.disposed && this.connection.IsOpen;

    /// <inheritdoc/>
    public string MessagingSystem => "amqp";

    /// <inheritdoc/>
    public async ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return false;
        }

        try
        {
            // Creating and immediately closing a channel is the lightest AMQP operation
            // that validates broker connectivity end-to-end.
            IChannel testChannel = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await testChannel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a new <see cref="AmqpMessageTransport"/> instance, establishing the connection
    /// and configuring the publish channel.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A configured transport instance.</returns>
    public static async ValueTask<AmqpMessageTransport> CreateAsync(
        AmqpTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ConnectionFactory factory = new() { Uri = new Uri(options.ConnectionUri) };
        IConnection connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        IChannel publishChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        // Declare exchange if specified
        if (!string.IsNullOrEmpty(options.ExchangeName))
        {
            await publishChannel.ExchangeDeclareAsync(
                exchange: options.ExchangeName,
                type: options.ExchangeType,
                durable: options.ExchangeDurable,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Declare dead-letter exchange
        if (!string.IsNullOrEmpty(options.DeadLetterExchange))
        {
            await publishChannel.ExchangeDeclareAsync(
                exchange: options.DeadLetterExchange,
                type: "topic",
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new AmqpMessageTransport(options, connection, publishChannel);
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
        (byte[] payloadRented, int payloadLen) = SerializeToRented(in payload);

        // Headers go into a Dictionary<string, object?> as byte[] — needs owned array.
        // Payload goes into BasicPublishAsync as ReadOnlyMemory<byte> — rental is beneficial.
        byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in headers)
            : null;

        return PublishCoreAsync(channel, payloadRented, payloadLen, headerBytes, correlationId: null, cancellationToken);
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
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);
        (byte[] requestRented, int requestLen) = SerializeToRented(in request);
        byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in headers)
            : null;

        return RequestCoreAsync<TReply>(requestChannel, replyChannel, requestRented, requestLen, correlationId, correlationIdUtf8, headerBytes, workspace, cancellationToken);
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

        // Remove first so delivery stops regardless of what teardown does, and tear down
        // outside any lock so a handler-initiated unsubscribe cannot wedge the transport.
        if (this.subscriptions.TryRemove(channel, out SubscriptionState? state))
        {
            // Teardown drains in-flight handlers first, so their final ticks precede the stop
            // and the started/stopped/tick event ordering stays truthful.
            await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
            this.options.Heartbeat?.Stop(channel, "amqp", state.Marker);
        }
    }

    private static async ValueTask TearDownSubscriptionAsync(string channel, SubscriptionState state)
    {
        await state.CancellationSource.CancelAsync().ConfigureAwait(false);

        // A handler runs on the client library's consumer-dispatcher worker, and closing the
        // channel waits for that worker to finish — so a handler stopping its own subscription
        // would self-join. The self case defers the broker stop to the thread pool: it
        // completes as soon as the handler returns and frees the worker, and every fault
        // inside CloseChannelAsync is recorded, so the unawaited task can never surface an
        // unobserved exception.
        if (ReferenceEquals(ExecutingSubscription.Value, state.Marker))
        {
            _ = Task.Run(() => CloseChannelAsync(channel, state));
            return;
        }

        await CloseChannelAsync(channel, state).ConfigureAwait(false);
    }

    private static async Task CloseChannelAsync(string channel, SubscriptionState state)
    {
        // Deliberately no caller token anywhere in here: on the handler-initiated path the
        // handler's token is the subscription's own, cancelled before this runs, so passing
        // it would abandon the broker-side cancel and leak the channel.
        try
        {
            await state.Channel.BasicCancelAsync(state.ConsumerTag).ConfigureAwait(false);
            await state.Channel.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Recorded rather than rethrown: a broker that has already closed the channel must
            // not prevent the local handles being released, nor abort a dispose walking others.
            AsyncApiTelemetry.RecordSubscriptionTeardownFailure(channel, "amqp", ex);
        }

        try
        {
            await state.Channel.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AsyncApiTelemetry.RecordSubscriptionTeardownFailure(channel, "amqp", ex);
        }

        state.CancellationSource.Dispose();
    }

    private static void ThrowAlreadySubscribed(string channel)
        => throw new InvalidOperationException(
            $"Channel '{channel}' already has a subscription. Unsubscribe before subscribing again.");

    private void ThrowIfSubscribed(string channel)
    {
        if (this.subscriptions.ContainsKey(channel))
        {
            ThrowAlreadySubscribed(channel);
        }
    }

    // A subscribe that fails before its claim owns only what it created itself; the channel
    // is released quietly (recording, never throwing) so the original failure propagates.
    private static async ValueTask DisposeChannelOnSetupFailureAsync(string channel, IChannel consumerChannel)
    {
        try
        {
            await consumerChannel.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AsyncApiTelemetry.RecordSubscriptionTeardownFailure(channel, "amqp", ex);
        }
    }

    private async ValueTask ClaimSubscriptionAsync(string channel, SubscriptionState state)
    {
        // TryAdd is the authoritative claim on the channel's single subscription slot, so no
        // lock is needed. A second subscribe is refused rather than displacing the first:
        // displacing would tear the previous consumer down on the subscribe path, which a
        // handler calling back in (the generated Abort path) would deadlock.
        if (!this.subscriptions.TryAdd(channel, state))
        {
            await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);

            // The heartbeat has not been started for this subscription, but its consumer may
            // already have ticked an entry into existence; the owner-guarded stop clears that
            // without touching whatever the winning subscription has recorded.
            this.options.Heartbeat?.Stop(channel, "amqp", state.Marker);
            ThrowAlreadySubscribed(channel);
        }

        // Dispose may have snapshotted the registry before this claim landed; whichever of us
        // removes the entry tears it down, so a subscribe racing disposal never leaks a live
        // consumer past DisposeAsync.
        if (this.disposed)
        {
            if (this.subscriptions.TryRemove(KeyValuePair.Create(channel, state)))
            {
                await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
            }

            this.options.Heartbeat?.Stop(channel, "amqp", state.Marker);
            throw new ObjectDisposedException(nameof(AmqpMessageTransport));
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

        byte[] payloadRented;
        int payloadLen;
        if (payload.ValueKind != JsonValueKind.Undefined)
        {
            (payloadRented, payloadLen) = SerializeToRented(in payload);
        }
        else
        {
            payloadRented = ArrayPool<byte>.Shared.Rent(0);
            payloadLen = 0;
        }

        byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in headers)
            : null;

        return DeadLetterCoreAsync(deadLetterChannel, originalChannelUtf8, payloadRented, payloadLen, headerBytes, exception, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Remove each subscription before tearing it down, so a subscribe racing disposal
        // either loses its claim (and cleans itself up) or is torn down by this loop. The
        // shared teardown records rather than throws, so one broken channel cannot abort it.
        foreach (string channel in this.subscriptions.Keys)
        {
            if (this.subscriptions.TryRemove(channel, out SubscriptionState? state))
            {
                // Teardown drains in-flight handlers first, so a first-ever tick from a
                // subscription this walk stole from a racing subscribe cannot create a fresh
                // Running entry after the stop has already been applied.
                await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
                this.options.Heartbeat?.Stop(channel, "amqp", state.Marker);
            }
        }

        // The internal reply-channel consumers live apart from the application registry but
        // share the transport's lifetime, so disposal walks them the same way.
        foreach (string channel in this.replyConsumers.Keys)
        {
            if (this.replyConsumers.TryRemove(channel, out SubscriptionState? state))
            {
                await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
            }
        }

        await this.publishChannel.CloseAsync().ConfigureAwait(false);
        await this.publishChannel.DisposeAsync().ConfigureAwait(false);
        await this.connection.CloseAsync().ConfigureAwait(false);
        await this.connection.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask PublishCoreAsync(
        string channel,
        byte[] payloadRented,
        int payloadLength,
        byte[]? headerBytes,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            BasicProperties props = new()
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
            };

            if (headerBytes is not null)
            {
                props.Headers ??= new Dictionary<string, object?>();
                props.Headers[HeadersKeyString] = headerBytes;
            }

            if (correlationId is not null)
            {
                props.CorrelationId = correlationId;
                props.Headers ??= new Dictionary<string, object?>();
                props.Headers[CorrelationIdKeyString] = Encoding.UTF8.GetBytes(correlationId);
            }

            string exchange = this.options.ExchangeName;
            string routingKey = channel;

            await this.publishChannel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: new ReadOnlyMemory<byte>(payloadRented, 0, payloadLength),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payloadRented);
        }
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TReply>(
        string requestChannel,
        string replyChannel,
        byte[] requestRented,
        int requestLength,
        string correlationId,
        ReadOnlyMemory<byte> correlationIdUtf8,
        byte[]? headerBytes,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        TaskCompletionSource<BasicDeliverEventArgs> replyTcs = new();
        this.pendingReplies[correlationId] = replyTcs;

        try
        {
            // Ensure reply subscription is active
            if (!this.replyConsumers.ContainsKey(replyChannel))
            {
                await this.SubscribeForRepliesAsync(replyChannel, cancellationToken).ConfigureAwait(false);
            }

            // Publish the request with correlation ID and reply-to
            BasicProperties props = new()
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = correlationId,
                ReplyTo = replyChannel,
            };

            if (headerBytes is not null)
            {
                props.Headers ??= new Dictionary<string, object?>();
                props.Headers[HeadersKeyString] = headerBytes;
            }

            await this.publishChannel.BasicPublishAsync(
                exchange: this.options.ExchangeName,
                routingKey: requestChannel,
                mandatory: false,
                basicProperties: props,
                body: new ReadOnlyMemory<byte>(requestRented, 0, requestLength),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Wait for the correlated reply, bounded by the request timeout so a lost reply surfaces as a fast
            // cancellation rather than waiting forever (the caller's token still cancels earlier if it fires first).
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(this.options.RequestTimeout);
            BasicDeliverEventArgs reply = await replyTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            // Parse reply with error handling
            try
            {
                ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(reply.Body.ToArray());
                workspace.TakeOwnership(replyDoc);
                TReply replyPayload = replyDoc.RootElement;

                ParsedJsonDocument<JsonElement>? headersDoc = ExtractHeadersDocument(reply);
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
                MessageErrorAction action = MessageErrorAction.Abort;

                if (this.errorPolicy is not null)
                {
                    action = await this.errorPolicy.HandleErrorAsync(
                        parseEx,
                        new MessageErrorContext(
                            correlationIdUtf8,
                            MessageErrorKind.Deserialization,
                            default,
                            default),
                        cancellationToken).ConfigureAwait(false);
                }

                switch (action)
                {
                    case MessageErrorAction.Abort:
                        AsyncApiTelemetry.RecordAbort(replyChannel, "amqp", MessageErrorKind.Deserialization);
                        throw;

                    case MessageErrorAction.DeadLetter:
                        string dlChannel = replyChannel + this.options.DeadLetterRoutingKeySuffix;
                        try
                        {
                            await DeadLetterRawAsync(dlChannel, correlationIdUtf8, reply.Body.ToArray(), parseEx, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, replyChannel, "amqp");
                        }
                        catch (Exception dlEx)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, replyChannel, "amqp", dlEx);
                        }

                        // Request failed even though we DLQ'd the bad reply
                        throw;

                    case MessageErrorAction.Skip:
                    default:
                        // Skip means fail for request-reply (can't skip a reply)
                        AsyncApiTelemetry.RecordSkip(replyChannel, "amqp", MessageErrorKind.Deserialization);
                        throw;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(requestRented);
            this.pendingReplies.TryRemove(correlationId, out _);
        }
    }

    private async ValueTask SubscribeCoreAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        MessageHandler<TPayload> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ThrowIfSubscribed(channel);
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string dlChannel = channel + this.options.DeadLetterRoutingKeySuffix;
        IChannel consumerChannel;
        try
        {
            consumerChannel = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Failed before the claim: nothing is registered anywhere, so the only thing to
            // release is the linked source (and with it the registration on the caller's token).
            cts.Dispose();
            throw;
        }

        string queueName = $"{this.options.ConsumerTagPrefix}.{channel}";
        try
        {
            await consumerChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: this.options.PrefetchCount, global: false, cancellationToken: cancellationToken).ConfigureAwait(false);

            await consumerChannel.QueueDeclareAsync(
                queue: queueName,
                durable: this.options.QueueDurable,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(this.options.ExchangeName))
            {
                await consumerChannel.QueueBindAsync(
                    queue: queueName,
                    exchange: this.options.ExchangeName,
                    routingKey: channel,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await DisposeChannelOnSetupFailureAsync(channel, consumerChannel).ConfigureAwait(false);
            cts.Dispose();
            throw;
        }

        AsyncEventingBasicConsumer consumer = new(consumerChannel);
        string? actualTag = null;
        object marker = new();
        consumer.ReceivedAsync += async (_, args) =>
        {
            ExecutingSubscription.Value = marker;
            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            this.options.Heartbeat?.Tick(channel, "amqp", marker);

            ParsedJsonDocument<TPayload> payloadDoc;
            try
            {
                payloadDoc = ParsedJsonDocument<TPayload>.Parse(args.Body);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                if (action == MessageErrorAction.DeadLetter)
                {
                    try
                    {
                        await this.DeadLetterRawAsync(dlChannel, channelUtf8, args.Body, ex, cts.Token).ConfigureAwait(false);
                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "amqp");
                    }
                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                    {
                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "amqp", dlEx);
                    }

                    await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                }
                else if (action == MessageErrorAction.Abort)
                {
                    AsyncApiTelemetry.RecordAbort(channel, "amqp", MessageErrorKind.Deserialization);

                    // The abort releases the whole subscription (registry entry, heartbeat, channel), so
                    // the channel is free to resubscribe rather than left a zombie claim; teardown detects
                    // the self case and defers the broker close until this handler returns.
                    await this.UnsubscribeAsync(channelUtf8, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    AsyncApiTelemetry.RecordSkip(channel, "amqp", MessageErrorKind.Deserialization);
                    await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
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
                    headersDoc = ExtractHeadersDocument(args);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            await this.DeadLetterRawAsync(dlChannel, channelUtf8, args.Body, ex, cts.Token).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "amqp");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "amqp", dlEx);
                        }

                        await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                    }
                    else if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "amqp", MessageErrorKind.Deserialization);

                        // The abort releases the whole subscription (registry entry, heartbeat, channel), so
                        // the channel is free to resubscribe rather than left a zombie claim; teardown detects
                        // the self case and defers the broker close until this handler returns.
                        await this.UnsubscribeAsync(channelUtf8, CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        AsyncApiTelemetry.RecordSkip(channel, "amqp", MessageErrorKind.Deserialization);
                        await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
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
                            await this.middleware((ct) => handler.Invoke(payload, channelUtf8, headers, args, ct), cts.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            await handler.Invoke(payload, channelUtf8, headers, args, cts.Token).ConfigureAwait(false);
                        }

                        await consumerChannel.BasicAckAsync(args.DeliveryTag, multiple: false, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                    {
                        // Shutting down — don't ack
                    }
                    catch (Exception ex)
                    {
                        MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, payloadElement, headers);
                        MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                        if (action == MessageErrorAction.DeadLetter)
                        {
                            try
                            {
                                (byte[] payloadRented, int payloadLen) = SerializeToRented(in payloadElement);
                                byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
                                    ? SerializeToOwnedBytes(in headers)
                                    : null;
                                await this.DeadLetterCoreAsync(dlChannel, channelUtf8, payloadRented, payloadLen, headerBytes, ex, cts.Token).ConfigureAwait(false);
                                AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "amqp");
                            }
                            catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                            {
                                AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "amqp", dlEx);
                            }

                            await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                        }
                        else if (action == MessageErrorAction.Abort)
                        {
                            AsyncApiTelemetry.RecordAbort(channel, "amqp", MessageErrorKind.Handler);

                            // The abort releases the whole subscription (registry entry, heartbeat, channel), so
                            // the channel is free to resubscribe rather than left a zombie claim; teardown detects
                            // the self case and defers the broker close until this handler returns.
                            await this.UnsubscribeAsync(channelUtf8, CancellationToken.None).ConfigureAwait(false);
                        }
                        else
                        {
                            AsyncApiTelemetry.RecordSkip(channel, "amqp", MessageErrorKind.Handler);
                            await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                        }
                    }
                }
            }
        };

        string consumerTag = $"{this.options.ConsumerTagPrefix}.{channel}";
        try
        {
            actualTag = await consumerChannel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumerTag: consumerTag,
                consumer: consumer,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeChannelOnSetupFailureAsync(channel, consumerChannel).ConfigureAwait(false);
            cts.Dispose();
            throw;
        }

        SubscriptionState state = new(consumerChannel, cts, actualTag, marker);
        await this.ClaimSubscriptionAsync(channel, state).ConfigureAwait(false);

        // Started only once the claim is final: a subscribe that fails at or before the claim
        // must leave no phantom Running entry behind.
        this.options.Heartbeat?.Start(channel, "amqp", marker);

        // Re-checked after the heartbeat start: a dispose walk that ran entirely between the
        // claim re-check and the start has already stopped a heartbeat that did not exist
        // yet, so without this undo a disposed transport would report the channel Running
        // forever — and this subscribe would return success for a torn-down consumer.
        if (this.disposed)
        {
            this.options.Heartbeat?.Stop(channel, "amqp", marker);
            if (this.subscriptions.TryRemove(KeyValuePair.Create(channel, state)))
            {
                await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
            }

            throw new ObjectDisposedException(nameof(AmqpMessageTransport));
        }
    }

    private async ValueTask SubscribeForRepliesAsync(string replyChannel, CancellationToken cancellationToken)
    {
        // Deliberately NOT linked to any requester's token: the consumer serves every request
        // that ever uses this reply channel, so its lifetime is the transport's. Linking it to
        // the first requester meant that token's cancellation made every later reply ack throw.
        CancellationTokenSource cts = new();
        IChannel replyConsumerChannel;
        try
        {
            replyConsumerChannel = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            cts.Dispose();
            throw;
        }

        string queueName = $"{this.options.ConsumerTagPrefix}.reply.{replyChannel}";
        try
        {
            await replyConsumerChannel.QueueDeclareAsync(
                queue: queueName,
                durable: false,
                exclusive: true,
                autoDelete: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(this.options.ExchangeName))
            {
                await replyConsumerChannel.QueueBindAsync(
                    queue: queueName,
                    exchange: this.options.ExchangeName,
                    routingKey: replyChannel,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await DisposeChannelOnSetupFailureAsync(replyChannel, replyConsumerChannel).ConfigureAwait(false);
            cts.Dispose();
            throw;
        }

        AsyncEventingBasicConsumer consumer = new(replyConsumerChannel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            string? corrId = args.BasicProperties?.CorrelationId;
            if (corrId is not null && this.pendingReplies.TryRemove(corrId, out TaskCompletionSource<BasicDeliverEventArgs>? tcs))
            {
                tcs.SetResult(args);
            }

            await replyConsumerChannel.BasicAckAsync(args.DeliveryTag, multiple: false, cts.Token).ConfigureAwait(false);
        };

        string consumerTag = $"{this.options.ConsumerTagPrefix}.reply.{replyChannel}";
        string actualTag;
        try
        {
            actualTag = await replyConsumerChannel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumerTag: consumerTag,
                consumer: consumer,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeChannelOnSetupFailureAsync(replyChannel, replyConsumerChannel).ConfigureAwait(false);
            cts.Dispose();
            throw;
        }

        // The reply listener runs no user code, so no handler can self-unsubscribe from it;
        // its marker exists only to satisfy the state shape and is never set. Concurrent
        // requests race to establish this consumer, and losing that race is not a caller
        // error: the loser discards its consumer and uses the winner's.
        SubscriptionState state = new(replyConsumerChannel, cts, actualTag, new object());
        if (!this.replyConsumers.TryAdd(replyChannel, state))
        {
            _ = CloseChannelAsync(replyChannel, state);
            return;
        }

        // Internal path, so no throw: a request racing disposal fails on the disposed
        // connection anyway, but the reply consumer must still not outlive the transport.
        if (this.disposed && this.replyConsumers.TryRemove(KeyValuePair.Create(replyChannel, state)))
        {
            _ = CloseChannelAsync(replyChannel, state);
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
        ThrowIfSubscribed(channel);
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string dlChannel = channel + this.options.DeadLetterRoutingKeySuffix;
        IChannel consumerChannel;
        try
        {
            consumerChannel = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Failed before the claim: nothing is registered anywhere, so the only thing to
            // release is the linked source (and with it the registration on the caller's token).
            cts.Dispose();
            throw;
        }

        string queueName = $"{this.options.ConsumerTagPrefix}.{channel}";
        try
        {
            await consumerChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: this.options.PrefetchCount, global: false, cancellationToken: cancellationToken).ConfigureAwait(false);

            await consumerChannel.QueueDeclareAsync(
                queue: queueName,
                durable: this.options.QueueDurable,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(this.options.ExchangeName))
            {
                await consumerChannel.QueueBindAsync(
                    queue: queueName,
                    exchange: this.options.ExchangeName,
                    routingKey: channel,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await DisposeChannelOnSetupFailureAsync(channel, consumerChannel).ConfigureAwait(false);
            cts.Dispose();
            throw;
        }

        AsyncEventingBasicConsumer consumer = new(consumerChannel);
        string? actualTag = null;
        object marker = new();
        consumer.ReceivedAsync += async (_, args) =>
        {
            ExecutingSubscription.Value = marker;
            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            this.options.Heartbeat?.Tick(channel, "amqp", marker);

            ParsedJsonDocument<TRequest> requestDoc;
            try
            {
                requestDoc = ParsedJsonDocument<TRequest>.Parse(args.Body);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                if (action == MessageErrorAction.DeadLetter)
                {
                    try
                    {
                        await this.DeadLetterRawAsync(dlChannel, channelUtf8, args.Body, ex, cts.Token).ConfigureAwait(false);
                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "amqp");
                    }
                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                    {
                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "amqp", dlEx);
                    }

                    await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                }
                else if (action == MessageErrorAction.Abort)
                {
                    AsyncApiTelemetry.RecordAbort(channel, "amqp", MessageErrorKind.Deserialization);

                    // The abort releases the whole subscription (registry entry, heartbeat, channel), so
                    // the channel is free to resubscribe rather than left a zombie claim; teardown detects
                    // the self case and defers the broker close until this handler returns.
                    await this.UnsubscribeAsync(channelUtf8, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    AsyncApiTelemetry.RecordSkip(channel, "amqp", MessageErrorKind.Deserialization);
                    await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
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
                    headersDoc = ExtractHeadersDocument(args);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            await this.DeadLetterRawAsync(dlChannel, channelUtf8, args.Body, ex, cts.Token).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "amqp");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "amqp", dlEx);
                        }

                        await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                    }
                    else if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "amqp", MessageErrorKind.Deserialization);

                        // The abort releases the whole subscription (registry entry, heartbeat, channel), so
                        // the channel is free to resubscribe rather than left a zombie claim; teardown detects
                        // the self case and defers the broker close until this handler returns.
                        await this.UnsubscribeAsync(channelUtf8, CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        AsyncApiTelemetry.RecordSkip(channel, "amqp", MessageErrorKind.Deserialization);
                        await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
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
                            // Capture the typed reply produced inside the middleware pipeline.
                            TReply captured = default;
                            await this.middleware(
                                async (ct) => captured = await handler(request, headers, ct).ConfigureAwait(false),
                                cts.Token).ConfigureAwait(false);
                            reply = captured;
                        }
                        else
                        {
                            reply = await handler(request, headers, cts.Token).ConfigureAwait(false);
                        }

                        // Publish the reply to the request's reply-to routing key, echoing the
                        // request's correlation ID so the requester's reply consumer can correlate it.
                        string? replyTo = args.BasicProperties?.ReplyTo;
                        string? corrId = args.BasicProperties?.CorrelationId;
                        if (!string.IsNullOrEmpty(replyTo))
                        {
                            (byte[] replyRented, int replyLen) = SerializeToRented(in reply);
                            try
                            {
                                BasicProperties replyProps = new()
                                {
                                    ContentType = "application/json",
                                    DeliveryMode = DeliveryModes.Persistent,
                                };

                                if (corrId is not null)
                                {
                                    replyProps.CorrelationId = corrId;
                                }

                                // Publish the reply on the shared publish channel with CancellationToken.None, not
                                // this subscription's cts: a one-shot responder (ReceiveOneAndReplyAsync) signals its
                                // completion the instant the handler returns, so the caller can unsubscribe - which
                                // cancels cts - while this reply is still in flight. Using cts here let that teardown
                                // abort the reply, so the requester never received it and (before RequestTimeout)
                                // waited forever. The publish channel is only torn down on full DisposeAsync, well
                                // after the reply has gone out, so None is safe.
                                await this.publishChannel.BasicPublishAsync(
                                    exchange: this.options.ExchangeName,
                                    routingKey: replyTo,
                                    mandatory: false,
                                    basicProperties: replyProps,
                                    body: new ReadOnlyMemory<byte>(replyRented, 0, replyLen),
                                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(replyRented);
                            }
                        }

                        await consumerChannel.BasicAckAsync(args.DeliveryTag, multiple: false, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                    {
                        // Shutting down — don't ack
                    }
                    catch (Exception ex)
                    {
                        MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, requestElement, headers);
                        MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                        if (action == MessageErrorAction.DeadLetter)
                        {
                            try
                            {
                                (byte[] payloadRented, int payloadLen) = SerializeToRented(in requestElement);
                                byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
                                    ? SerializeToOwnedBytes(in headers)
                                    : null;
                                await this.DeadLetterCoreAsync(dlChannel, channelUtf8, payloadRented, payloadLen, headerBytes, ex, cts.Token).ConfigureAwait(false);
                                AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "amqp");
                            }
                            catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                            {
                                AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "amqp", dlEx);
                            }

                            await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                        }
                        else if (action == MessageErrorAction.Abort)
                        {
                            AsyncApiTelemetry.RecordAbort(channel, "amqp", MessageErrorKind.Handler);

                            // The abort releases the whole subscription (registry entry, heartbeat, channel), so
                            // the channel is free to resubscribe rather than left a zombie claim; teardown detects
                            // the self case and defers the broker close until this handler returns.
                            await this.UnsubscribeAsync(channelUtf8, CancellationToken.None).ConfigureAwait(false);
                        }
                        else
                        {
                            AsyncApiTelemetry.RecordSkip(channel, "amqp", MessageErrorKind.Handler);
                            await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                        }
                    }
                }
            }
        };

        string consumerTag = $"{this.options.ConsumerTagPrefix}.{channel}";
        try
        {
            actualTag = await consumerChannel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumerTag: consumerTag,
                consumer: consumer,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeChannelOnSetupFailureAsync(channel, consumerChannel).ConfigureAwait(false);
            cts.Dispose();
            throw;
        }

        SubscriptionState state = new(consumerChannel, cts, actualTag, marker);
        await this.ClaimSubscriptionAsync(channel, state).ConfigureAwait(false);

        // Started only once the claim is final: a subscribe that fails at or before the claim
        // must leave no phantom Running entry behind.
        this.options.Heartbeat?.Start(channel, "amqp", marker);

        // Re-checked after the heartbeat start: a dispose walk that ran entirely between the
        // claim re-check and the start has already stopped a heartbeat that did not exist
        // yet, so without this undo a disposed transport would report the channel Running
        // forever — and this subscribe would return success for a torn-down consumer.
        if (this.disposed)
        {
            this.options.Heartbeat?.Stop(channel, "amqp", marker);
            if (this.subscriptions.TryRemove(KeyValuePair.Create(channel, state)))
            {
                await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
            }

            throw new ObjectDisposedException(nameof(AmqpMessageTransport));
        }
    }

    private async ValueTask DeadLetterCoreAsync(
        string deadLetterChannel,
        ReadOnlyMemory<byte> originalChannelUtf8,
        byte[] payloadRented,
        int payloadLength,
        byte[]? headerBytes,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            BasicProperties props = new()
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?>
                {
                    ["corvus-original-channel"] = originalChannelUtf8.ToArray(),
                    ["corvus-error"] = Encoding.UTF8.GetBytes(exception.Message),
                    ["corvus-error-type"] = Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name),
                },
            };

            if (headerBytes is not null)
            {
                props.Headers[HeadersKeyString] = headerBytes;
            }

            string exchange = this.options.DeadLetterExchange;
            string routingKey = deadLetterChannel;

            await this.publishChannel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: new ReadOnlyMemory<byte>(payloadRented, 0, payloadLength),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payloadRented);
        }
    }

    private async ValueTask DeadLetterRawAsync(
        string deadLetterChannel,
        ReadOnlyMemory<byte> originalChannelUtf8,
        ReadOnlyMemory<byte> rawPayload,
        Exception exception,
        CancellationToken cancellationToken)
    {
        BasicProperties props = new()
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                ["corvus-original-channel"] = originalChannelUtf8.ToArray(),
                ["corvus-error"] = Encoding.UTF8.GetBytes(exception.Message),
                ["corvus-error-type"] = Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name),
            },
        };

        await this.publishChannel.BasicPublishAsync(
            exchange: this.options.DeadLetterExchange,
            routingKey: deadLetterChannel,
            mandatory: false,
            basicProperties: props,
            body: rawPayload,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static ParsedJsonDocument<JsonElement>? ExtractHeadersDocument(BasicDeliverEventArgs args)
    {
        if (args.BasicProperties?.Headers?.TryGetValue(HeadersKeyString, out object? value) == true &&
            value is byte[] headerBytes)
        {
            return ParsedJsonDocument<JsonElement>.Parse(headerBytes);
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

    /// <summary>
    /// Serializes to an owned byte array. Used for AMQP headers which go into
    /// <c>Dictionary&lt;string, object?&gt;</c> and require an owned <c>byte[]</c>.
    /// </summary>
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

    private sealed record SubscriptionState(
        IChannel Channel,
        CancellationTokenSource CancellationSource,
        string ConsumerTag,
        object Marker);
}