// <copyright file="AmqpMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
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
public sealed class AmqpMessageTransport : IMessageTransport
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
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BasicDeliverEventArgs>> pendingReplies = new(StringComparer.Ordinal);
    private bool disposed;

    private AmqpMessageTransport(AmqpTransportOptions options, IConnection connection, IChannel publishChannel)
    {
        this.options = options;
        this.connection = connection;
        this.publishChannel = publishChannel;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
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
        in TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        in JsonElement headers = default,
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

        return RequestCoreAsync<TReply>(requestChannel, replyChannel, requestRented, requestLen, correlationId, correlationIdUtf8, headerBytes, cancellationToken);
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
        return SubscribeCoreAsync(channel, channelUtf8, handler, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        if (this.subscriptions.TryRemove(channel, out SubscriptionState? state))
        {
            await state.CancellationSource.CancelAsync().ConfigureAwait(false);
            await state.Channel.BasicCancelAsync(state.ConsumerTag, cancellationToken: cancellationToken).ConfigureAwait(false);
            await state.Channel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await state.Channel.DisposeAsync().ConfigureAwait(false);
            state.CancellationSource.Dispose();
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

        // Cancel all subscriptions
        foreach ((string _, SubscriptionState state) in this.subscriptions)
        {
            await state.CancellationSource.CancelAsync().ConfigureAwait(false);
            await state.Channel.CloseAsync().ConfigureAwait(false);
            await state.Channel.DisposeAsync().ConfigureAwait(false);
            state.CancellationSource.Dispose();
        }

        this.subscriptions.Clear();

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
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        TaskCompletionSource<BasicDeliverEventArgs> replyTcs = new();
        this.pendingReplies[correlationId] = replyTcs;

        try
        {
            // Ensure reply subscription is active
            if (!this.subscriptions.ContainsKey(replyChannel))
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

            // Wait for reply
            BasicDeliverEventArgs reply = await replyTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            // Parse reply using pooled document
            using ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(reply.Body.ToArray());
            TReply replyPayload = replyDoc.RootElement;

            using ParsedJsonDocument<JsonElement>? headersDoc = ExtractHeadersDocument(reply);
            JsonElement replyHeaders = headersDoc?.RootElement ?? default;

            return (replyPayload, replyHeaders);
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
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string dlChannel = channel + this.options.DeadLetterRoutingKeySuffix;
        IChannel consumerChannel = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await consumerChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: this.options.PrefetchCount, global: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        string queueName = $"{this.options.ConsumerTagPrefix}.{channel}";

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

        AsyncEventingBasicConsumer consumer = new(consumerChannel);
        string? actualTag = null;
        consumer.ReceivedAsync += async (_, args) =>
        {
            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

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
                    await this.DeadLetterRawAsync(dlChannel, channelUtf8, args.Body, ex, cts.Token).ConfigureAwait(false);
                    await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                }
                else if (action == MessageErrorAction.Abort)
                {
                    if (actualTag is not null)
                    {
                        await consumerChannel.BasicCancelAsync(actualTag, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                    }

                    await cts.CancelAsync().ConfigureAwait(false);
                }
                else
                {
                    await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                }

                return;
            }

            using (payloadDoc)
            {
                TPayload payload = payloadDoc.RootElement;
                JsonElement payloadElement = JsonElement.From(in payload);
                using ParsedJsonDocument<JsonElement>? headersDoc = ExtractHeadersDocument(args);
                JsonElement headers = headersDoc?.RootElement ?? default;

                try
                {
                    if (this.middleware is not null)
                    {
                        await this.middleware((ct) => handler(payload, headers, ct), cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await handler(payload, headers, cts.Token).ConfigureAwait(false);
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
                        (byte[] payloadRented, int payloadLen) = SerializeToRented(in payloadElement);
                        byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
                            ? SerializeToOwnedBytes(in headers)
                            : null;
                        await this.DeadLetterCoreAsync(dlChannel, channelUtf8, payloadRented, payloadLen, headerBytes, ex, cts.Token).ConfigureAwait(false);
                        await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                    }
                    else if (action == MessageErrorAction.Abort)
                    {
                        if (actualTag is not null)
                        {
                            await consumerChannel.BasicCancelAsync(actualTag, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                        }

                        await cts.CancelAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await consumerChannel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cts.Token).ConfigureAwait(false);
                    }
                }
            }
        };

        string consumerTag = $"{this.options.ConsumerTagPrefix}.{channel}";
        actualTag = await consumerChannel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumerTag: consumerTag,
            consumer: consumer,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        SubscriptionState state = new(consumerChannel, cts, actualTag);
        this.subscriptions[channel] = state;
    }

    private async ValueTask SubscribeForRepliesAsync(string replyChannel, CancellationToken cancellationToken)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IChannel replyConsumerChannel = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        string queueName = $"{this.options.ConsumerTagPrefix}.reply.{replyChannel}";

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
        string actualTag = await replyConsumerChannel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumerTag: consumerTag,
            consumer: consumer,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        SubscriptionState state = new(replyConsumerChannel, cts, actualTag);
        this.subscriptions[replyChannel] = state;
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
        string ConsumerTag);
}