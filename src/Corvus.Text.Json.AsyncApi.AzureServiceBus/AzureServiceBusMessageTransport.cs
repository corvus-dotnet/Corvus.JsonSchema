// <copyright file="AzureServiceBusMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.ServiceBus;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.AzureServiceBus;

/// <summary>
/// Azure Service Bus message transport.
/// </summary>
public sealed class AzureServiceBusMessageTransport : IMessageTransport
{
    private readonly AzureServiceBusTransportOptions options;
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;
    private readonly ServiceBusProcessor? processor;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly byte[] deadLetterSuffixUtf8;
    private readonly ConcurrentDictionary<string, TaskCompletionSource> subscriptions = new(StringComparer.Ordinal);
    private bool disposed;

    private AzureServiceBusMessageTransport(
        AzureServiceBusTransportOptions options,
        ServiceBusClient client,
        ServiceBusSender sender,
        ServiceBusProcessor? processor)
    {
        this.options = options;
        this.client = client;
        this.sender = sender;
        this.processor = processor;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
        this.deadLetterSuffixUtf8 = Encoding.UTF8.GetBytes(options.DeadLetterSuffix);
    }

    /// <inheritdoc/>
    public bool IsConnected => !this.disposed && !this.client.IsClosed;

    /// <inheritdoc/>
    public string MessagingSystem => "azureservicebus";

    /// <inheritdoc/>
    public ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        // Service Bus doesn't have a native ping operation
        // Return IsConnected state
        return new ValueTask<bool>(this.IsConnected);
    }

    /// <summary>
    /// Creates a new <see cref="AzureServiceBusMessageTransport"/> instance.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A configured transport instance.</returns>
    public static ValueTask<AzureServiceBusMessageTransport> CreateAsync(
        AzureServiceBusTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ServiceBusClient client;

        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            client = new ServiceBusClient(options.ConnectionString);
        }
        else if (!string.IsNullOrEmpty(options.FullyQualifiedNamespace) && options.Credential is not null)
        {
            client = new ServiceBusClient(options.FullyQualifiedNamespace, options.Credential);
        }
        else
        {
            throw new InvalidOperationException(
                "Either ConnectionString or (FullyQualifiedNamespace + Credential) must be provided.");
        }

        string entityPath = options.UseTopic ? options.TopicName! : options.QueueName!;
        ServiceBusSender sender = client.CreateSender(entityPath);

        // Processor created only when subscribing
        ServiceBusProcessor? processor = null;

        return new ValueTask<AzureServiceBusMessageTransport>(
            new AzureServiceBusMessageTransport(options, client, sender, processor));
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

        // Copy values before async work
        TPayload payloadCopy = payload;
        JsonElement headersCopy = headers;

        return this.PublishCoreAsync(channelUtf8, payloadCopy, headersCopy, cancellationToken);
    }

    private async ValueTask PublishCoreAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        TPayload payload,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        byte[]? rentedArray = null;

        try
        {
            // Serialize to rented buffer
            int estimatedSize = EstimateSerializedSize(payload);
            rentedArray = ArrayPool<byte>.Shared.Rent(estimatedSize);

            int bytesWritten = SerializeToBuffer(payload, rentedArray);

            ServiceBusMessage message = new(new ReadOnlyMemory<byte>(rentedArray, 0, bytesWritten));

            // Add headers as application properties
            if (headers.ValueKind != JsonValueKind.Undefined)
            {
                foreach (JsonProperty<JsonElement> property in headers.EnumerateObject())
                {
                    string value = property.Value.ToString();
                    message.ApplicationProperties[property.Name] = value;
                }
            }

            await this.sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
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
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);

        // Use correlation ID as session ID for exclusive reply handling
        string sessionId = correlationId;

        // Create sender for request queue/topic
        ServiceBusSender requestSender = this.options.UseTopic
            ? this.client.CreateSender(this.options.TopicName!)
            : this.client.CreateSender(requestChannel);

        // Create session receiver for reply queue - accepts only messages with this session ID
        ServiceBusSessionReceiver replyReceiver = await this.client.AcceptSessionAsync(
            replyChannel,
            sessionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        try
        {
            // Serialize request
            byte[]? rentedArray = null;
            try
            {
                ArrayBufferWriter<byte> buffer = new();
                Utf8JsonWriter writer = new(buffer);
                request.WriteTo(writer);
                writer.Flush();

                int length = buffer.WrittenCount;
                rentedArray = length <= 256  // StackallocByteThreshold
                    ? null
                    : ArrayPool<byte>.Shared.Rent(length);

                ReadOnlyMemory<byte> payload = rentedArray is null
                    ? buffer.WrittenMemory
                    : new ReadOnlyMemory<byte>(rentedArray, 0, length);

                if (rentedArray is not null)
                {
                    buffer.WrittenSpan.CopyTo(rentedArray);
                }

                // Build Service Bus message with SessionId, ReplyTo, and CorrelationId
                ServiceBusMessage message = new(payload)
                {
                    SessionId = sessionId,
                    ReplyTo = replyChannel,
                    CorrelationId = correlationId,
                };

                // Add headers as application properties
                if (headers.ValueKind != JsonValueKind.Undefined)
                {
                    foreach (JsonProperty<JsonElement> property in headers.EnumerateObject())
                    {
                        message.ApplicationProperties[property.Name] = property.Value.ToString();
                    }
                }

                // Send request
                await requestSender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (rentedArray is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedArray);
                }
            }

            // Wait for reply - session receiver guarantees only messages with our session ID
            ServiceBusReceivedMessage replyMessage = await replyReceiver.ReceiveMessageAsync(
                maxWaitTime: this.options.RequestTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (replyMessage is null)
            {
                throw new TimeoutException($"No reply received on channel '{replyChannel}' for session '{sessionId}' within {this.options.RequestTimeout.TotalSeconds} seconds.");
            }

            // Parse reply with error handling
            try
            {
                using ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(replyMessage.Body);
                TReply replyPayload = replyDoc.RootElement;
                JsonElement replyHeaders = BuildHeadersElement(replyMessage.ApplicationProperties);

                await replyReceiver.CompleteMessageAsync(replyMessage, cancellationToken: cancellationToken).ConfigureAwait(false);

                return (replyPayload, replyHeaders);
            }
            catch (Exception parseEx)
            {
                // Parse error - apply error policy
                MessageErrorAction action = MessageErrorAction.Abort;

                if (this.options.ErrorPolicy is not null)
                {
                    action = await this.options.ErrorPolicy.HandleErrorAsync(
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
                        AsyncApiTelemetry.RecordAbort(replyChannel, "azureservicebus", MessageErrorKind.Deserialization);
                        await replyReceiver.AbandonMessageAsync(replyMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                        throw;

                    case MessageErrorAction.DeadLetter:
                        try
                        {
                            // Send bad reply to native Service Bus DLQ
                            await replyReceiver.DeadLetterMessageAsync(
                                replyMessage,
                                deadLetterReason: "Reply deserialization failed",
                                deadLetterErrorDescription: parseEx.Message,
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            AsyncApiTelemetry.RecordDeadLetter(replyChannel + "/$DeadLetterQueue", replyChannel, "azureservicebus");
                        }
                        catch (Exception dlEx)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(replyChannel + "/$DeadLetterQueue", replyChannel, "azureservicebus", dlEx);
                        }

                        // Request failed even though we DLQ'd the bad reply
                        throw;

                    case MessageErrorAction.Skip:
                    default:
                        // Skip means abandon and fail for request-reply (can't skip a reply)
                        AsyncApiTelemetry.RecordSkip(replyChannel, "azureservicebus", MessageErrorKind.Deserialization);
                        await replyReceiver.AbandonMessageAsync(replyMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                        throw;
                }
            }
        }
        finally
        {
            await requestSender.DisposeAsync().ConfigureAwait(false);
            await replyReceiver.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Build dead-letter channel UTF-8 bytes
        Span<byte> dlChannelUtf8 = stackalloc byte[channelUtf8.Length + this.deadLetterSuffixUtf8.Length];
        channelUtf8.Span.CopyTo(dlChannelUtf8);
        this.deadLetterSuffixUtf8.CopyTo(dlChannelUtf8[channelUtf8.Length..]);
        string dlChannel = Encoding.UTF8.GetString(dlChannelUtf8);

        this.options.Heartbeat?.Start(channel, "azureservicebus");

        ServiceBusProcessor processor = this.options.UseTopic
            ? this.client.CreateProcessor(this.options.TopicName!, this.options.SubscriptionName!)
            : this.client.CreateProcessor(this.options.QueueName!);

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        this.subscriptions[channel] = tcs;

        processor.ProcessMessageAsync += async args =>
        {
            this.options.Heartbeat?.Tick(channel, "azureservicebus");

            ReadOnlyMemory<byte> bodyBytes = args.Message.Body;

            // Parse
            ParsedJsonDocument<TPayload> payloadDoc;
            try
            {
                payloadDoc = ParsedJsonDocument<TPayload>.Parse(bodyBytes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);

                if (action == MessageErrorAction.Abort)
                {
                    AsyncApiTelemetry.RecordAbort(channel, "azureservicebus", MessageErrorKind.Deserialization);
                    tcs.TrySetResult();
                    await args.DeadLetterMessageAsync(args.Message, "Deserialization failed", ex.Message).ConfigureAwait(false);
                    return;
                }

                if (action == MessageErrorAction.DeadLetter)
                {
                    try
                    {
                        await args.DeadLetterMessageAsync(args.Message, "Deserialization failed", ex.Message).ConfigureAwait(false);
                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "azureservicebus");
                    }
                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                    {
                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "azureservicebus", dlEx);
                    }

                    return;
                }

                AsyncApiTelemetry.RecordSkip(channel, "azureservicebus", MessageErrorKind.Deserialization);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                return;
            }

            // Handle
            using (payloadDoc)
            {
                TPayload payload = payloadDoc.RootElement;
                JsonElement headersElement = BuildHeadersElement(args.Message.ApplicationProperties);

                try
                {
                    if (this.middleware is not null)
                    {
                        await this.middleware(
                            (ct) => handler(payload, headersElement, ct),
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await handler(payload, headersElement, cancellationToken).ConfigureAwait(false);
                    }

                    await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetResult();
                    await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);

                    if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "azureservicebus", MessageErrorKind.Handler);
                        tcs.TrySetResult();
                        await args.DeadLetterMessageAsync(args.Message, "Handler failed", ex.Message).ConfigureAwait(false);
                        return;
                    }

                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            await args.DeadLetterMessageAsync(args.Message, "Handler failed", ex.Message).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "azureservicebus");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "azureservicebus", dlEx);
                        }

                        return;
                    }

                    AsyncApiTelemetry.RecordSkip(channel, "azureservicebus", MessageErrorKind.Handler);
                    await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                }
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            // Log error but don't fail - processor will continue
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes to request messages on a channel and replies to each — the responder counterpart of
    /// <see cref="RequestAsync{TRequest, TReply}(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, TRequest, ReadOnlyMemory{byte}, JsonElement, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// For every request the processor delivers, this reads the native <see cref="ServiceBusReceivedMessage.ReplyTo"/>,
    /// <see cref="ServiceBusReceivedMessage.CorrelationId"/> and <see cref="ServiceBusReceivedMessage.SessionId"/> fields
    /// (the same ones <c>RequestAsync</c> sets), invokes the handler to obtain the typed reply, and sends that reply to the
    /// request's reply-to entity echoing the request's session and correlation identifiers so the requester's session
    /// receiver correlates it.
    /// </remarks>
    /// <typeparam name="TRequest">The request payload type the responder parses into.</typeparam>
    /// <typeparam name="TReply">The reply payload type the handler returns.</typeparam>
    /// <param name="channelUtf8">The request channel address as UTF-8 bytes.</param>
    /// <param name="handler">The handler invoked with each request payload and its headers, returning the reply payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask SubscribeReplyAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Build dead-letter channel UTF-8 bytes
        Span<byte> dlChannelUtf8 = stackalloc byte[channelUtf8.Length + this.deadLetterSuffixUtf8.Length];
        channelUtf8.Span.CopyTo(dlChannelUtf8);
        this.deadLetterSuffixUtf8.CopyTo(dlChannelUtf8[channelUtf8.Length..]);
        string dlChannel = Encoding.UTF8.GetString(dlChannelUtf8);

        this.options.Heartbeat?.Start(channel, "azureservicebus");

        ServiceBusProcessor processor = this.options.UseTopic
            ? this.client.CreateProcessor(this.options.TopicName!, this.options.SubscriptionName!)
            : this.client.CreateProcessor(this.options.QueueName!);

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        this.subscriptions[channel] = tcs;

        processor.ProcessMessageAsync += async args =>
        {
            this.options.Heartbeat?.Tick(channel, "azureservicebus");

            ReadOnlyMemory<byte> bodyBytes = args.Message.Body;

            // Parse the request
            ParsedJsonDocument<TRequest> requestDoc;
            try
            {
                requestDoc = ParsedJsonDocument<TRequest>.Parse(bodyBytes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);

                if (action == MessageErrorAction.Abort)
                {
                    AsyncApiTelemetry.RecordAbort(channel, "azureservicebus", MessageErrorKind.Deserialization);
                    tcs.TrySetResult();
                    await args.DeadLetterMessageAsync(args.Message, "Deserialization failed", ex.Message).ConfigureAwait(false);
                    return;
                }

                if (action == MessageErrorAction.DeadLetter)
                {
                    try
                    {
                        await args.DeadLetterMessageAsync(args.Message, "Deserialization failed", ex.Message).ConfigureAwait(false);
                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "azureservicebus");
                    }
                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                    {
                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "azureservicebus", dlEx);
                    }

                    return;
                }

                AsyncApiTelemetry.RecordSkip(channel, "azureservicebus", MessageErrorKind.Deserialization);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                return;
            }

            // Handle the request and publish the reply
            using (requestDoc)
            {
                TRequest request = requestDoc.RootElement;
                JsonElement headersElement = BuildHeadersElement(args.Message.ApplicationProperties);

                try
                {
                    TReply reply;
                    if (this.middleware is not null)
                    {
                        TReply captured = default;
                        await this.middleware(
                            async (ct) => captured = await handler(request, headersElement, ct).ConfigureAwait(false),
                            cancellationToken).ConfigureAwait(false);
                        reply = captured;
                    }
                    else
                    {
                        reply = await handler(request, headersElement, cancellationToken).ConfigureAwait(false);
                    }

                    // Echo the request's reply-to address, session ID and correlation ID so the requester's
                    // session receiver (keyed on the correlation ID it used as the session ID) receives the reply.
                    string? replyTo = args.Message.ReplyTo;
                    if (!string.IsNullOrEmpty(replyTo))
                    {
                        await this.SendReplyAsync(replyTo, reply, args.Message.SessionId, args.Message.CorrelationId, cancellationToken).ConfigureAwait(false);
                    }

                    await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetResult();
                    await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler);
                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);

                    if (action == MessageErrorAction.Abort)
                    {
                        AsyncApiTelemetry.RecordAbort(channel, "azureservicebus", MessageErrorKind.Handler);
                        tcs.TrySetResult();
                        await args.DeadLetterMessageAsync(args.Message, "Handler failed", ex.Message).ConfigureAwait(false);
                        return;
                    }

                    if (action == MessageErrorAction.DeadLetter)
                    {
                        try
                        {
                            await args.DeadLetterMessageAsync(args.Message, "Handler failed", ex.Message).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "azureservicebus");
                        }
                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "azureservicebus", dlEx);
                        }

                        return;
                    }

                    AsyncApiTelemetry.RecordSkip(channel, "azureservicebus", MessageErrorKind.Handler);
                    await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                }
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            // Log error but don't fail - processor will continue
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        if (this.subscriptions.TryRemove(channel, out TaskCompletionSource? tcs))
        {
            tcs.TrySetResult();

            if (this.processor is not null)
            {
                await this.processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
            }

            this.options.Heartbeat?.Stop(channel, "azureservicebus");
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

        JsonElement payloadCopy = payload;
        JsonElement headersCopy = headers;
        return DeadLetterCoreAsync(deadLetterChannelUtf8, originalChannelUtf8, payloadCopy, headersCopy, exception, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (this.processor is not null)
        {
            await this.processor.StopProcessingAsync().ConfigureAwait(false);
            await this.processor.DisposeAsync().ConfigureAwait(false);
        }

        await this.sender.DisposeAsync().ConfigureAwait(false);
        await this.client.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask DeadLetterCoreAsync(
        ReadOnlyMemory<byte> deadLetterChannelUtf8,
        ReadOnlyMemory<byte> originalChannelUtf8,
        JsonElement payload,
        JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string deadLetterChannel = Encoding.UTF8.GetString(deadLetterChannelUtf8.Span);
        string originalChannel = Encoding.UTF8.GetString(originalChannelUtf8.Span);

        byte[]? rentedArray = null;

        try
        {
            int estimatedSize = payload.ValueKind != JsonValueKind.Undefined
                ? Math.Max(1024, payload.ToString()!.Length * 4)
                : 256;

            rentedArray = ArrayPool<byte>.Shared.Rent(estimatedSize);
            int bytesWritten = payload.ValueKind != JsonValueKind.Undefined
                ? SerializeToBuffer(payload, rentedArray)
                : 0;

            ServiceBusMessage message = new(new ReadOnlyMemory<byte>(rentedArray, 0, bytesWritten));
            message.ApplicationProperties["Corvus-Original-Channel"] = originalChannel;
            message.ApplicationProperties["Corvus-Error"] = exception.Message;
            message.ApplicationProperties["Corvus-Error-Type"] = exception.GetType().FullName ?? exception.GetType().Name;

            if (headers.ValueKind != JsonValueKind.Undefined)
            {
                foreach (JsonProperty<JsonElement> property in headers.EnumerateObject())
                {
                    message.ApplicationProperties[property.Name] = property.Value.ToString();
                }
            }

            await using ServiceBusSender dlSender = this.client.CreateSender(deadLetterChannel);
            await dlSender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    private async ValueTask SendReplyAsync<TReply>(
        string replyChannel,
        TReply reply,
        string? sessionId,
        string? correlationId,
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        byte[]? rentedArray = null;

        try
        {
            // Serialize the reply, mirroring RequestAsync's request serialization.
            ArrayBufferWriter<byte> buffer = new();
            Utf8JsonWriter writer = new(buffer);
            reply.WriteTo(writer);
            writer.Flush();

            int length = buffer.WrittenCount;
            rentedArray = length <= 256  // StackallocByteThreshold
                ? null
                : ArrayPool<byte>.Shared.Rent(length);

            ReadOnlyMemory<byte> payload = rentedArray is null
                ? buffer.WrittenMemory
                : new ReadOnlyMemory<byte>(rentedArray, 0, length);

            if (rentedArray is not null)
            {
                buffer.WrittenSpan.CopyTo(rentedArray);
            }

            // Echo the request's session and correlation identifiers so the requester's session
            // receiver (which accepts the session whose ID equals the correlation ID) gets the reply.
            ServiceBusMessage message = new(payload);

            if (!string.IsNullOrEmpty(sessionId))
            {
                message.SessionId = sessionId;
            }

            if (!string.IsNullOrEmpty(correlationId))
            {
                message.CorrelationId = correlationId;
            }

            await using ServiceBusSender replySender = this.client.CreateSender(replyChannel);
            await replySender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    private static int EstimateSerializedSize<TPayload>(TPayload payload)
        where TPayload : struct, IJsonElement<TPayload>
    {
        // Conservative estimate: 4x the ToString length
        string stringForm = payload.ToString() ?? string.Empty;
        return Math.Max(1024, stringForm.Length * 4);
    }

    private static int SerializeToBuffer<TPayload>(TPayload payload, byte[] buffer)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ArrayBufferWriter<byte> writer = new(buffer.Length);
        using Utf8JsonWriter jsonWriter = new(writer);
        payload.WriteTo(jsonWriter);
        jsonWriter.Flush();

        writer.WrittenSpan.CopyTo(buffer);
        return writer.WrittenCount;
    }

    private static JsonElement BuildHeadersElement(IReadOnlyDictionary<string, object> applicationProperties)
    {
        if (applicationProperties.Count == 0)
        {
            return default;
        }

        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);

        writer.WriteStartObject();
        foreach (KeyValuePair<string, object> kvp in applicationProperties)
        {
            writer.WriteString(kvp.Key, kvp.Value?.ToString() ?? string.Empty);
        }

        writer.WriteEndObject();
        writer.Flush();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        return doc.RootElement;
    }
}