// <copyright file="AzureServiceBusMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.ServiceBus;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Internal;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.AzureServiceBus;

/// <summary>
/// Azure Service Bus message transport.
/// </summary>
public sealed class AzureServiceBusMessageTransport : IMessageDeliveryContextTransport
{
    private readonly AzureServiceBusTransportOptions options;
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly byte[] deadLetterSuffixUtf8;

    // Each subscription keeps its own processor so Unsubscribe/Dispose can actually stop it. A single shared
    // processor field could not: a transport may hold several subscriptions, and it was never assigned (created
    // per-subscribe), so subscriptions leaked - their processors kept consuming and stole other work's messages.
    private readonly ConcurrentDictionary<string, (TaskCompletionSource Completion, ServiceBusProcessor Processor, object Marker)> subscriptions = new(StringComparer.Ordinal);

    // Flows from each ProcessMessageAsync callback into the handlers it invokes, so teardown
    // can recognize a handler stopping its own subscription and defer the processor stop that
    // would otherwise drain the very callback executing the caller.
    private static readonly AsyncLocal<object?> ExecutingSubscription = new();

    private volatile bool disposed;

    private AzureServiceBusMessageTransport(
        AzureServiceBusTransportOptions options,
        ServiceBusClient client,
        ServiceBusSender sender)
    {
        this.options = options;
        this.client = client;
        this.sender = sender;
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

        return new ValueTask<AzureServiceBusMessageTransport>(
            new AzureServiceBusMessageTransport(options, client, sender));
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
                // The parsed reply is returned to the caller and used after this method returns, so its document is
                // owned by the caller's workspace (disposed when the workspace is) rather than disposed here.
                ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(replyMessage.Body);
                workspace.TakeOwnership(replyDoc);
                TReply replyPayload = replyDoc.RootElement;
                JsonElement replyHeaders = BuildHeadersElement(replyMessage.ApplicationProperties, workspace);

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
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
        => this.SubscribeCoreAsync(channelUtf8, MessageHandler<TPayload>.WithoutDeliveryContext(handler), cancellationToken);

    /// <inheritdoc/>
    public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
        => this.SubscribeCoreAsync(channelUtf8, MessageHandler<TPayload>.WithDeliveryContext(handler), cancellationToken);

    private async ValueTask SubscribeCoreAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        MessageHandler<TPayload> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        ThrowIfSubscribed(channel);

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

        object marker = new();
        processor.ProcessMessageAsync += async args =>
        {
            ExecutingSubscription.Value = marker;
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
            using (JsonWorkspace headerWorkspace = JsonWorkspace.CreateUnrented())
            {
                TPayload payload = payloadDoc.RootElement;
                JsonElement headersElement = BuildHeadersElement(args.Message.ApplicationProperties, headerWorkspace);

                try
                {
                    if (this.middleware is not null)
                    {
                        await this.middleware(
                            (ct) => handler.Invoke(payload, channelUtf8, headersElement, args.Message, ct),
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await handler.Invoke(payload, channelUtf8, headersElement, args.Message, cancellationToken).ConfigureAwait(false);
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

        // Claimed only once the processor is wired and running, so a concurrent subscribe can
        // never observe — or tear down — a processor this call has not finished starting.
        await this.ClaimSubscriptionAsync(channel, (tcs, processor, marker)).ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes to request messages on a channel and replies to each — the responder counterpart of
    /// <see cref="RequestAsync{TRequest, TReply}(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, TRequest, ReadOnlyMemory{byte}, JsonWorkspace, JsonElement, CancellationToken)"/>.
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
        ThrowIfSubscribed(channel);

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

        object marker = new();
        processor.ProcessMessageAsync += async args =>
        {
            ExecutingSubscription.Value = marker;
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
            using (JsonWorkspace headerWorkspace = JsonWorkspace.CreateUnrented())
            {
                TRequest request = requestDoc.RootElement;
                JsonElement headersElement = BuildHeadersElement(args.Message.ApplicationProperties, headerWorkspace);

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

        // Claimed only once the processor is wired and running, so a concurrent subscribe can
        // never observe — or tear down — a processor this call has not finished starting.
        await this.ClaimSubscriptionAsync(channel, (tcs, processor, marker)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Remove first so delivery stops regardless of what teardown does, and tear down
        // outside any lock so a handler-initiated unsubscribe cannot wedge the transport.
        if (this.subscriptions.TryRemove(channel, out (TaskCompletionSource Completion, ServiceBusProcessor Processor, object Marker) subscription))
        {
            await TearDownSubscriptionAsync(channel, subscription).ConfigureAwait(false);
            this.options.Heartbeat?.Stop(channel, "azureservicebus");
        }
    }

    private static async ValueTask TearDownSubscriptionAsync(
        string channel,
        (TaskCompletionSource Completion, ServiceBusProcessor Processor, object Marker) subscription)
    {
        subscription.Completion.TrySetResult();

        // StopProcessingAsync drains in-flight handlers, so a handler stopping its own
        // subscription would wait for itself. The self case defers the stop to the thread
        // pool: it completes as soon as the handler returns, and every fault inside
        // StopProcessorAsync is recorded, so the unawaited task can never surface an
        // unobserved exception.
        if (ReferenceEquals(ExecutingSubscription.Value, subscription.Marker))
        {
            _ = Task.Run(() => StopProcessorAsync(channel, subscription.Processor));
            return;
        }

        await StopProcessorAsync(channel, subscription.Processor).ConfigureAwait(false);
    }

    private static async Task StopProcessorAsync(string channel, ServiceBusProcessor processor)
    {
        // Deliberately no caller token: on the handler-initiated path the caller's token is
        // the one the handler was given, so passing it would abandon the drain half-done.
        try
        {
            await processor.StopProcessingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Recorded rather than rethrown: teardown must not abort a dispose walking others.
            AsyncApiTelemetry.RecordSubscriptionTeardownFailure(channel, "azureservicebus", ex);
        }

        try
        {
            await processor.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AsyncApiTelemetry.RecordSubscriptionTeardownFailure(channel, "azureservicebus", ex);
        }
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

    private async ValueTask ClaimSubscriptionAsync(
        string channel,
        (TaskCompletionSource Completion, ServiceBusProcessor Processor, object Marker) subscription)
    {
        // TryAdd is the authoritative claim on the channel's single subscription slot, so no
        // lock is needed. A second subscribe is refused rather than displacing the first:
        // displacing would drain the previous processor on the subscribe path, which a handler
        // calling back in (the generated Abort path) would deadlock.
        if (!this.subscriptions.TryAdd(channel, subscription))
        {
            await TearDownSubscriptionAsync(channel, subscription).ConfigureAwait(false);
            ThrowAlreadySubscribed(channel);
        }

        // Dispose may have snapshotted the registry before this claim landed; whichever of us
        // removes the entry tears it down, so a subscribe racing disposal never leaks a live
        // processor past DisposeAsync.
        if (this.disposed)
        {
            if (this.subscriptions.TryRemove(KeyValuePair.Create(channel, subscription)))
            {
                await TearDownSubscriptionAsync(channel, subscription).ConfigureAwait(false);
            }

            throw new ObjectDisposedException(nameof(AzureServiceBusMessageTransport));
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

        // Remove each subscription before tearing it down, so a subscribe racing disposal
        // either loses its claim (and cleans itself up) or is torn down by this loop. The
        // shared teardown records rather than throws, so one stuck processor cannot abort it.
        foreach (string channel in this.subscriptions.Keys)
        {
            if (this.subscriptions.TryRemove(channel, out (TaskCompletionSource Completion, ServiceBusProcessor Processor, object Marker) subscription))
            {
                await TearDownSubscriptionAsync(channel, subscription).ConfigureAwait(false);
            }
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

    private static JsonElement BuildHeadersElement(IReadOnlyDictionary<string, object> applicationProperties, JsonWorkspace workspace)
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

        // The returned element is used after this method returns (by the caller's handler, or by RequestAsync's
        // caller), so its document is owned by the caller's workspace rather than disposed here.
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        workspace.TakeOwnership(doc);
        return doc.RootElement;
    }
}