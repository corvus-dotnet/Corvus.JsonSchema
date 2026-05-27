// <copyright file="KafkaMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.Kafka;

/// <summary>
/// Kafka implementation of <see cref="IMessageTransport"/> using Confluent.Kafka.
/// </summary>
/// <remarks>
/// <para>
/// This transport maps AsyncAPI channels to Kafka topics. Messages are serialized
/// as UTF-8 JSON using <c>WriteTo(Utf8JsonWriter)</c> for zero-allocation serialization.
/// Headers are stored as a separate JSON blob in a Kafka message header named
/// <c>corvus-headers</c>.
/// </para>
/// <para>
/// Subscriptions run on background tasks using the Kafka consumer poll loop.
/// Each channel subscription creates a dedicated consumer instance to allow
/// independent offset tracking and partition assignment.
/// </para>
/// </remarks>
public sealed class KafkaMessageTransport : IMessageTransport, IHealthCheckableTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private static readonly byte[] HeadersKey = "corvus-headers"u8.ToArray();
    private const string HeadersKeyString = "corvus-headers";
    private static readonly byte[] CorrelationIdKey = "corvus-correlation-id"u8.ToArray();
    private const string CorrelationIdKeyString = "corvus-correlation-id";

    private readonly KafkaTransportOptions options;
    private readonly IProducer<Null, byte[]> producer;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, SubscriptionState> subscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ReadOnlyMemory<byte>, TaskCompletionSource<ConsumeResult<Null, byte[]>>> pendingReplies = new(ReadOnlyMemoryByteComparer.Instance);
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaMessageTransport"/> class.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    public KafkaMessageTransport(KafkaTransportOptions options)
    {
        this.options = options;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;

        ProducerConfig producerConfig = options.ProducerConfig ?? new ProducerConfig();
        producerConfig.BootstrapServers = options.BootstrapServers;
        producerConfig.MessageTimeoutMs = options.MessageTimeoutMs;

        this.producer = new ProducerBuilder<Null, byte[]>(producerConfig).Build();
    }

    /// <inheritdoc/>
    public bool IsConnected => !this.disposed;

    /// <inheritdoc/>
    public string MessagingSystem => "kafka";

    /// <inheritdoc/>
    public ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return ValueTask.FromResult(false);
        }

        try
        {
            // Flush with a short timeout validates broker connectivity.
            this.producer.Flush(TimeSpan.FromSeconds(2));
            return ValueTask.FromResult(true);
        }
        catch (Exception)
        {
            return ValueTask.FromResult(false);
        }
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
        byte[] payloadBytes = SerializeToOwnedBytes(in payload);

        Message<Null, byte[]> message = new() { Value = payloadBytes };

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            byte[] headerBytes = SerializeToOwnedBytes(in headers);
            message.Headers = [new Header(HeadersKeyString, headerBytes)];
        }

        return PublishCoreAsync(channel, message, cancellationToken);
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
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string requestChannel = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        string replyChannel = Encoding.UTF8.GetString(replyChannelUtf8.Span);

        byte[] requestBytes = SerializeToOwnedBytes(in request);
        byte[]? headerBytes = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in headers)
            : null;

        return RequestCoreAsync<TReply>(requestChannel, replyChannel, requestBytes, correlationIdUtf8, headerBytes, cancellationToken);
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
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IConsumer<Null, byte[]> consumer = CreateConsumer(channel);

        Task consumeTask = Task.Run(
            () => this.ConsumeLoop<TPayload>(channel, channelUtf8, consumer, handler, cts.Token),
            CancellationToken.None);

        SubscriptionState state = new(consumer, cts, consumeTask);
        this.subscriptions[channel] = state;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        if (this.subscriptions.TryRemove(channel, out SubscriptionState? state))
        {
            await state.CancellationSource.CancelAsync().ConfigureAwait(false);

            try
            {
                await state.ConsumeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }

            state.Consumer.Close();
            state.Consumer.Dispose();
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

        return this.DeadLetterCoreAsync(deadLetterChannel, originalChannelUtf8, in payload, in headers, exception, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Stop all subscriptions
        foreach ((string channel, SubscriptionState state) in this.subscriptions)
        {
            await state.CancellationSource.CancelAsync().ConfigureAwait(false);

            try
            {
                await state.ConsumeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            state.Consumer.Close();
            state.Consumer.Dispose();
            state.CancellationSource.Dispose();
        }

        this.subscriptions.Clear();
        this.producer.Flush(TimeSpan.FromSeconds(5));
        this.producer.Dispose();
    }

    private async ValueTask PublishCoreAsync(
        string channel,
        Message<Null, byte[]> message,
        CancellationToken cancellationToken)
    {
        await this.producer.ProduceAsync(channel, message, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask DeadLetterRawAsync(
        string deadLetterChannel,
        ReadOnlyMemory<byte> originalChannelUtf8,
        byte[] rawPayload,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Message<Null, byte[]> message = new()
        {
            Value = rawPayload,
            Headers =
            [
                new Header("corvus-original-channel", originalChannelUtf8.ToArray()),
                new Header("corvus-error", Encoding.UTF8.GetBytes(exception.Message)),
                new Header("corvus-error-type", Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name)),
            ],
        };

        return this.PublishCoreAsync(deadLetterChannel, message, cancellationToken);
    }

    private ValueTask DeadLetterCoreAsync(
        string deadLetterChannel,
        ReadOnlyMemory<byte> originalChannelUtf8,
        in JsonElement payload,
        in JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken)
    {
        byte[] payloadBytes = payload.ValueKind != JsonValueKind.Undefined
            ? SerializeToOwnedBytes(in payload)
            : [];

        Message<Null, byte[]> message = new()
        {
            Value = payloadBytes,
            Headers =
            [
                new Header("corvus-original-channel", originalChannelUtf8.ToArray()),
                new Header("corvus-error", Encoding.UTF8.GetBytes(exception.Message)),
                new Header("corvus-error-type", Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name)),
            ],
        };

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            byte[] headerBytes = SerializeToOwnedBytes(in headers);
            message.Headers.Add(HeadersKeyString, headerBytes);
        }

        return this.PublishCoreAsync(deadLetterChannel, message, cancellationToken);
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TReply>(
        string requestChannel,
        string replyChannel,
        byte[] requestBytes,
        ReadOnlyMemory<byte> correlationIdUtf8,
        byte[]? headerBytes,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        // Register for the reply before sending the request
        TaskCompletionSource<ConsumeResult<Null, byte[]>> replyTcs = new();
        this.pendingReplies[correlationIdUtf8] = replyTcs;

        // Rent a buffer for the Header value. Kafka's Header(string, byte[]) needs
        // an exactly-sized array. The caller's byte[36] is already exact-sized, so
        // we can use the backing array directly via MemoryMarshal.
        byte[] corrIdHeaderBytes = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(correlationIdUtf8, out ArraySegment<byte> segment)
            && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : correlationIdUtf8.ToArray();

        try
        {
            // Ensure we're subscribed to the reply channel
            if (!this.subscriptions.ContainsKey(replyChannel))
            {
                this.SubscribeForReplies(replyChannel, cancellationToken);
            }

            // Send the request with correlation ID header — zero allocation when
            // the caller passes an exactly-sized byte[] (which generated code does).
            Message<Null, byte[]> message = new()
            {
                Value = requestBytes,
                Headers =
                [
                    new Header(CorrelationIdKeyString, corrIdHeaderBytes),
                ],
            };

            if (headerBytes is not null)
            {
                message.Headers.Add(HeadersKeyString, headerBytes);
            }

            await this.producer.ProduceAsync(requestChannel, message, cancellationToken).ConfigureAwait(false);

            // Wait for the reply
            ConsumeResult<Null, byte[]> reply = await replyTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            // Parse reply with error handling
            try
            {
                using ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(reply.Message.Value);
                TReply replyPayload = replyDoc.RootElement;

                using ParsedJsonDocument<JsonElement>? headersDoc = ExtractHeadersDocument(reply);
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
                        AsyncApiTelemetry.RecordAbort(replyChannel, "kafka", MessageErrorKind.Deserialization);
                        throw;

                    case MessageErrorAction.DeadLetter:
                        string dlChannel = replyChannel + this.options.DeadLetterSuffix;
                        try
                        {
                            await DeadLetterRawAsync(dlChannel, replyChannelUtf8, reply.Message.Value, parseEx, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, replyChannel, "kafka");
                        }
                        catch (Exception dlEx)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, replyChannel, "kafka", dlEx);
                        }

                        // Request failed even though we DLQ'd the bad reply
                        throw;

                    case MessageErrorAction.Skip:
                    default:
                        // Skip means fail for request-reply (can't skip a reply)
                        AsyncApiTelemetry.RecordSkip(replyChannel, "kafka", MessageErrorKind.Deserialization);
                        throw;
                }
            }
        }
        finally
        {
            this.pendingReplies.TryRemove(correlationIdUtf8, out _);
        }
    }

    private async Task ConsumeLoop<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        IConsumer<Null, byte[]> consumer,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        // Pre-compute dead-letter channel string once (for Kafka SDK which takes string)
        string dlChannel = channel + this.options.DeadLetterSuffix;

        this.options.Heartbeat?.Start(channel, "kafka");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                this.options.Heartbeat?.Tick(channel, "kafka");

                ConsumeResult<Null, byte[]>? result;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(this.options.PollTimeoutMs));
                }
                catch (ConsumeException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Transport-level errors (e.g., topic not yet auto-created, broker unavailable).
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Transport);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                    if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "kafka", MessageErrorKind.Transport);
                        break;
                    }

                    // Skip or DeadLetter → continue polling after brief delay
                    AsyncApiTelemetry.RecordSkip(channel, "kafka", MessageErrorKind.Transport);
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (result?.IsPartitionEOF != false)
                {
                    continue;
                }

                ParsedJsonDocument<TPayload> payloadDoc;
                try
                {
                    payloadDoc = ParsedJsonDocument<TPayload>.Parse(result.Message.Value);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            await this.DeadLetterRawAsync(dlChannel, channelUtf8, result.Message.Value, ex, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "kafka");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "kafka", dlEx);
                        }
                    }

                    consumer.Commit(result);
                    if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "kafka", MessageErrorKind.Deserialization);
                        break;
                    }

                    if (action != MessageErrorAction.DeadLetter)
                    {
                        AsyncApiTelemetry.RecordSkip(channel, "kafka", MessageErrorKind.Deserialization);
                    }

                    continue;
                }

                using (payloadDoc)
                {
                    TPayload payload = payloadDoc.RootElement;
                    JsonElement payloadElement = JsonElement.From(in payload);

                    ParsedJsonDocument<JsonElement>? headersDoc;
                    try
                    {
                        headersDoc = ExtractHeadersDocument(result);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                        MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                        if (action == MessageErrorAction.DeadLetter)
                        {
                            try
                            {
                                await this.DeadLetterRawAsync(dlChannel, channelUtf8, result.Message.Value, ex, cancellationToken).ConfigureAwait(false);
                                AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "kafka");
                            }
                            catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                            {
                                AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "kafka", dlEx);
                            }
                        }

                        consumer.Commit(result);
                        if (action == MessageErrorAction.Abort)
                        {
                            AsyncApiTelemetry.RecordAbort(channel, "kafka", MessageErrorKind.Deserialization);
                            break;
                        }

                        if (action != MessageErrorAction.DeadLetter)
                        {
                            AsyncApiTelemetry.RecordSkip(channel, "kafka", MessageErrorKind.Deserialization);
                        }

                        continue;
                    }

                    using (headersDoc)
                    {
                        JsonElement headers = headersDoc?.RootElement ?? default;

                        try
                        {
                            if (this.middleware is not null)
                            {
                                await this.middleware((ct) => handler(payload, headers, ct), cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                await handler(payload, headers, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, payloadElement, headers);
                            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                            if (action == MessageErrorAction.DeadLetter)
                            {
                                try
                                {
                                    await this.DeadLetterCoreAsync(dlChannel, channelUtf8, in payloadElement, in headers, ex, cancellationToken).ConfigureAwait(false);
                                    AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "kafka");
                                }
                                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                {
                                    AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "kafka", dlEx);
                                }
                            }

                            consumer.Commit(result);
                            if (action == MessageErrorAction.Abort)
                            {
                                AsyncApiTelemetry.RecordAbort(channel, "kafka", MessageErrorKind.Handler);
                                break;
                            }

                            if (action != MessageErrorAction.DeadLetter)
                            {
                                AsyncApiTelemetry.RecordSkip(channel, "kafka", MessageErrorKind.Handler);
                            }

                            continue;
                        }
                    }
                }

                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            this.options.Heartbeat?.Stop(channel, "kafka");
        }
    }

    private static ParsedJsonDocument<JsonElement>? ExtractHeadersDocument(ConsumeResult<Null, byte[]> result)
    {
        if (result.Message.Headers is null)
        {
            return null;
        }

        if (result.Message.Headers.TryGetLastBytes(HeadersKeyString, out byte[]? headerBytes))
        {
            return ParsedJsonDocument<JsonElement>.Parse(headerBytes);
        }

        return null;
    }

    private IConsumer<Null, byte[]> CreateConsumer(string topic)
    {
        ConsumerConfig consumerConfig = this.options.ConsumerConfig ?? new ConsumerConfig();
        consumerConfig.BootstrapServers = this.options.BootstrapServers;
        consumerConfig.GroupId = this.options.GroupId;
        consumerConfig.AutoOffsetReset = this.options.AutoOffsetReset;
        consumerConfig.EnableAutoCommit = false;

        IConsumer<Null, byte[]> consumer = new ConsumerBuilder<Null, byte[]>(consumerConfig).Build();
        consumer.Subscribe(topic);
        return consumer;
    }

    private void SubscribeForReplies(string replyChannel, CancellationToken cancellationToken)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IConsumer<Null, byte[]> consumer = CreateConsumer(replyChannel);

        Task consumeTask = Task.Run(
            () => ReplyConsumeLoop(consumer, cts.Token),
            CancellationToken.None);

        SubscriptionState state = new(consumer, cts, consumeTask);
        this.subscriptions[replyChannel] = state;
    }

    private async Task ReplyConsumeLoop(IConsumer<Null, byte[]> consumer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ConsumeResult<Null, byte[]>? result;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(100));
                }
                catch (ConsumeException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Transport-level errors in reply loop — route through error policy.
                    MessageErrorContext ctx = new(ReadOnlyMemory<byte>.Empty, MessageErrorKind.Transport);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                    if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort("(reply-loop)", "kafka", MessageErrorKind.Transport);
                        break;
                    }

                    AsyncApiTelemetry.RecordSkip("(reply-loop)", "kafka", MessageErrorKind.Transport);
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (result?.IsPartitionEOF != false)
                {
                    continue;
                }

                // Check for correlation ID in headers — look up directly by bytes
                // without decoding to string.
                if (result.Message.Headers?.TryGetLastBytes(CorrelationIdKeyString, out byte[]? corrBytes) == true)
                {
                    if (this.pendingReplies.TryRemove(new ReadOnlyMemory<byte>(corrBytes), out TaskCompletionSource<ConsumeResult<Null, byte[]>>? tcs))
                    {
                        tcs.SetResult(result);
                    }
                }

                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <summary>
    /// Serializes a value to an owned byte array. Kafka requires owned <c>byte[]</c>
    /// for both <c>Message.Value</c> and <c>Header</c> values (no <c>IBufferWriter</c>
    /// or <c>ReadOnlyMemory</c> API exists in Confluent.Kafka 2.x). The thread-static
    /// buffer avoids per-call buffer allocation; the final <c>.ToArray()</c> is the one
    /// unavoidable allocation.
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
        IConsumer<Null, byte[]> Consumer,
        CancellationTokenSource CancellationSource,
        Task ConsumeTask);
}