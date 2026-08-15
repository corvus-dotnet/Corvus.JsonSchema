// <copyright file="NatsMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json.AsyncApi.Internal;
using Corvus.Text.Json.Internal;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Corvus.Text.Json.AsyncApi.Nats;

/// <summary>
/// NATS implementation of <see cref="IMessageTransport"/> using NATS.Net.
/// </summary>
/// <remarks>
/// <para>
/// This transport maps AsyncAPI channels to NATS subjects. Messages are serialized
/// as UTF-8 JSON using <c>WriteTo(Utf8JsonWriter)</c>. NATS headers carry the
/// JSON headers and correlation IDs for request/reply patterns.
/// </para>
/// <para>
/// NATS has native request/reply support via its inbox mechanism. This transport
/// uses NATS request/reply for the RequestAsync
/// method, and standard pub/sub for publish and subscribe operations.
/// </para>
/// <para>
/// Publish operations use a custom <see cref="INatsSerialize{T}"/> implementation
/// that writes directly into the NATS protocol buffer via <see cref="IBufferWriter{T}"/>,
/// eliminating intermediate allocations entirely.
/// </para>
/// </remarks>
public sealed class NatsMessageTransport : IMessageDeliveryContextTransport, IHealthCheckableTransport
{
    private const string HeadersKey = "Corvus-Headers";
    private const string CorrelationIdKey = "Corvus-Correlation-Id";

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly NatsTransportOptions options;
    private readonly NatsConnection connection;
    private readonly INatsJSContext? jsContext;
    private readonly string? derivedStreamName;
    private readonly byte[] deadLetterSuffixUtf8;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, SubscriptionState> subscriptions = new(StringComparer.Ordinal);

    // Flows from each consume loop into the handlers it invokes, so teardown can recognize a
    // handler stopping its own subscription and skip the join that would otherwise self-deadlock.
    private static readonly AsyncLocal<object?> ExecutingSubscription = new();

    private volatile bool disposed;

    private NatsMessageTransport(NatsTransportOptions options, NatsConnection connection, INatsJSContext? jsContext, string? derivedStreamName)
    {
        this.options = options;
        this.connection = connection;
        this.jsContext = jsContext;
        this.derivedStreamName = derivedStreamName;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
        this.deadLetterSuffixUtf8 = Encoding.UTF8.GetBytes(options.DeadLetterSuffix);
    }

    /// <inheritdoc/>
    public bool IsConnected => !this.disposed && this.connection.ConnectionState == NatsConnectionState.Open;

    /// <inheritdoc/>
    public string MessagingSystem => "nats";

    /// <inheritdoc/>
    public async ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return false;
        }

        try
        {
            await this.connection.PingAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a new <see cref="NatsMessageTransport"/> instance, establishing
    /// the NATS connection.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A configured and connected transport instance.</returns>
    public static async ValueTask<NatsMessageTransport> CreateAsync(
        NatsTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        NatsOpts natsOpts = new()
        {
            Url = options.Url,
            Name = options.Name ?? string.Empty,
            AuthOpts = new NatsAuthOpts
            {
                Token = options.Token,
                Username = options.Username,
                Password = options.Password,
                Jwt = options.Jwt,
                Seed = options.NKeySeed,
            },
        };

        NatsConnection connection = new(natsOpts);
        await connection.ConnectAsync().ConfigureAwait(false);

        INatsJSContext? jsContext = null;
        string? derivedStreamName = null;

        if (options.UseJetStream)
        {
            jsContext = new NatsJSContext(connection);

            // Cache the stream name if explicitly provided
            derivedStreamName = options.StreamName;
        }

        return new NatsMessageTransport(options, connection, jsContext, derivedStreamName);
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

        if (this.options.UseJetStream && this.jsContext is not null)
        {
            // JetStream path: publish to stream
            // Copy values before async call (async can't have in parameters)
            TPayload payloadCopy = payload;
            JsonElement headersCopy = headers;

            // Use cached stream name or derive on-demand (first publish determines it)
            string streamName = this.derivedStreamName ?? DeriveStreamName(channel);
            return this.PublishToJetStreamAsync(channel, streamName, payloadCopy, headersCopy, cancellationToken);
        }
        else
        {
            // Core NATS path: existing behavior
            return this.PublishToCoreNatsAsync(channel, in payload, in headers, cancellationToken);
        }
    }

    private ValueTask PublishToCoreNatsAsync<TPayload>(
        string channel,
        in TPayload payload,
        in JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        NatsHeaders? natsHeaders = null;

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            natsHeaders = new NatsHeaders
            {
                [HeadersKey] = SerializeToBase64String(in headers),
            };
        }

        return this.connection.PublishAsync(
            subject: channel,
            data: payload,
            headers: natsHeaders,
            replyTo: null,
            serializer: JsonElementSerializer<TPayload>.Instance,
            opts: default,
            cancellationToken: cancellationToken);
    }

    private async ValueTask PublishToJetStreamAsync<TPayload>(
        string channel,
        string streamName,
        TPayload payload,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        // Ensure stream exists
        await this.EnsureStreamExistsAsync(streamName, channel, cancellationToken).ConfigureAwait(false);

        // Publish to stream
        NatsHeaders? natsHeaders = null;
        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            natsHeaders = new NatsHeaders
            {
                [HeadersKey] = SerializeToBase64String(in headers),
            };
        }

        await this.jsContext!.PublishAsync(
            subject: channel,
            data: payload,
            headers: natsHeaders,
            serializer: JsonElementSerializer<TPayload>.Instance,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string DeriveStreamName(string subject)
    {
        // Convert subject like "events.temperature" to stream name "EVENTS"
        string firstSegment = subject.Split('.', '/')[0];
        return firstSegment.ToUpperInvariant();
    }

    private async ValueTask EnsureStreamExistsAsync(string streamName, string subject, CancellationToken cancellationToken)
    {
        try
        {
            await this.jsContext!.GetStreamAsync(streamName, request: default, cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            // Stream doesn't exist, create it
            StreamConfig config = new(streamName, [subject])
            {
                Storage = this.options.StorageType == StorageType.File
                    ? NATS.Client.JetStream.Models.StreamConfigStorage.File
                    : NATS.Client.JetStream.Models.StreamConfigStorage.Memory,
            };

            _ = await this.jsContext!.CreateStreamAsync(config, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(this.disposed, this);

        _ = replyChannelUtf8;
        string requestChannel = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        NatsHeaders natsHeaders = new()
        {
            [CorrelationIdKey] = Encoding.UTF8.GetString(correlationIdUtf8.Span),
        };

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            natsHeaders[HeadersKey] = SerializeToBase64String(in headers);
        }

        return RequestCoreAsync<TRequest, TReply>(requestChannel, request, natsHeaders, workspace, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        return this.SubscribeRoutedAsync(channelUtf8, MessageHandler<TPayload>.WithoutDeliveryContext(handler), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        return this.SubscribeRoutedAsync(channelUtf8, MessageHandler<TPayload>.WithDeliveryContext(handler), cancellationToken);
    }

    private ValueTask SubscribeRoutedAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        MessageHandler<TPayload> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        string channel = Encoding.UTF8.GetString(channelUtf8.Span);
        ThrowIfSubscribed(channel);

        if (this.options.UseJetStream && this.jsContext is not null)
        {
            // JetStream path: durable consumer. Use cached stream name or derive on-demand.
            string streamName = this.derivedStreamName ?? DeriveStreamName(channel);
            return this.SubscribeToJetStreamAsync(channel, streamName, channelUtf8, handler, cancellationToken);
        }

        // Core NATS path: existing behavior
        return this.SubscribeToCoreNatsAsync(channel, channelUtf8, handler, cancellationToken);
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
        ThrowIfSubscribed(channel);
        return this.SubscribeReplyToCoreNatsAsync(channel, channelUtf8, handler, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
    {
        string channel = Encoding.UTF8.GetString(channelUtf8.Span);

        // Remove first so delivery stops regardless of what teardown does, and tear down
        // outside any lock so a handler-initiated unsubscribe cannot wedge the transport.
        if (this.subscriptions.TryRemove(channel, out SubscriptionState? state))
        {
            await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
        }
    }

    private static async ValueTask TearDownSubscriptionAsync(string channel, SubscriptionState state)
    {
        try
        {
            await state.CancellationSource.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The consume loop owns the source and disposes it as it unwinds, so a disposed
            // source means the loop has already exited; there is nothing left to cancel and
            // the join below completes immediately.
        }

        // A handler that stops its own subscription is running INSIDE the consume task this
        // method would otherwise await; joining would deadlock. The self case cancels and
        // returns — the loop unwinds once the handler returns, and its finally releases the
        // resources it owns (the NATS subscription and the cancellation source).
        if (ReferenceEquals(ExecutingSubscription.Value, state.Marker))
        {
            return;
        }

        try
        {
            await state.ConsumeTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on clean shutdown.
        }
        catch (Exception ex)
        {
            // Recorded rather than rethrown: teardown must not abort a dispose that is
            // walking every subscription, nor fail an unsubscribe whose entry is already gone.
            AsyncApiTelemetry.RecordSubscriptionTeardownFailure(channel, "nats", ex);
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

    private async ValueTask ClaimSubscriptionAsync(string channel, SubscriptionState state)
    {
        // TryAdd is the authoritative claim on the channel's single subscription slot, so no
        // lock is needed. A second subscribe is refused rather than displacing the first:
        // displacing would tear the previous consumer down on the subscribe path, which a
        // handler calling back in (the generated Abort path) would deadlock.
        if (!this.subscriptions.TryAdd(channel, state))
        {
            await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
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

            throw new ObjectDisposedException(nameof(NatsMessageTransport));
        }
    }

    // The loop can die while the subscribe path is still recording the claim (a broker
    // fault, or the error policy aborting on an early message), in which case its
    // self-release ran before there was anything to release. Undo the claim and the
    // heartbeat and fail the subscribe, rather than returning a channel claimed by a dead
    // loop with a heartbeat that reports it running. The caller passes its loop-exited flag
    // because Task.IsCompleted only turns true after the loop's slow cleanup finishes.
    private async ValueTask ThrowIfLoopTerminatedAsync(string channel, SubscriptionState state, bool loopExited, string messagingSystem)
    {
        if (!loopExited && !state.ConsumeTask.IsCompleted)
        {
            return;
        }

        if (this.subscriptions.TryRemove(KeyValuePair.Create(channel, state)))
        {
            await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
        }

        this.options.Heartbeat?.Stop(channel, messagingSystem, state.Marker);
        throw new InvalidOperationException(
            $"The processing loop for channel '{channel}' terminated while the subscription was being established. The cause was reported through the error policy or recorded on the corvus.asyncapi.subscription_faults counter.");
    }

    // The loop's exit ends the subscription it belongs to: release the channel slot (a
    // faulted loop must not leave a zombie entry refusing resubscription) and stop the
    // heartbeat. Both are owner-guarded, so if a later subscription already owns the channel
    // (this loop lost the claim race, or a resubscribe landed while it was unwinding) its
    // live state is untouched.
    private void ReleaseSubscriptionOnLoopExit(string channel, object marker, string messagingSystem)
    {
        if (this.subscriptions.TryGetValue(channel, out SubscriptionState? current)
            && ReferenceEquals(current.Marker, marker))
        {
            this.subscriptions.TryRemove(KeyValuePair.Create(channel, current));
        }

        this.options.Heartbeat?.Stop(channel, messagingSystem, marker);
    }

    // The unacknowledged message redelivers after AckWait, so an acknowledge failure is
    // recorded and the loop keeps consuming rather than faulting on a broker hiccup.
    private static async ValueTask AckSafeAsync(NatsJSMsg<byte[]> msg, string channel, CancellationToken cancellationToken)
    {
        try
        {
            await msg.AckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AsyncApiTelemetry.RecordAcknowledgeFailure(channel, "nats-jetstream", ex);
        }
    }

    private static async ValueTask NakSafeAsync(NatsJSMsg<byte[]> msg, string channel, CancellationToken cancellationToken)
    {
        try
        {
            await msg.NakAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AsyncApiTelemetry.RecordAcknowledgeFailure(channel, "nats-jetstream", ex);
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

        NatsHeaders natsHeaders = new()
        {
            ["Corvus-Original-Channel"] = originalChannel,
            ["Corvus-Error"] = exception.Message,
            ["Corvus-Error-Type"] = exception.GetType().FullName ?? exception.GetType().Name,
        };

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            natsHeaders[HeadersKey] = SerializeToBase64String(in headers);
        }

        // Serializer handles ValueKind.Undefined as no-op (empty payload)
        return this.connection.PublishAsync(
            subject: deadLetterChannel,
            data: payload,
            headers: natsHeaders,
            replyTo: null,
            serializer: JsonElementSerializer<JsonElement>.Instance,
            opts: default,
            cancellationToken: cancellationToken);
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
        // either loses its claim (and cleans itself up) or is torn down by this loop.
        foreach (string channel in this.subscriptions.Keys)
        {
            if (this.subscriptions.TryRemove(channel, out SubscriptionState? state))
            {
                await TearDownSubscriptionAsync(channel, state).ConfigureAwait(false);
            }
        }

        await this.connection.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TRequest, TReply>(
        string subject,
        TRequest request,
        NatsHeaders headers,
        JsonWorkspace workspace,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(this.options.RequestTimeout);

        NatsMsg<byte[]> reply = await this.connection.RequestAsync<TRequest, byte[]>(
            subject,
            request,
            headers,
            JsonElementSerializer<TRequest>.Instance,
            NatsRawSerializer<byte[]>.Default,
            default,
            default,
            timeoutCts.Token).ConfigureAwait(false);

        TReply replyPayload;
        if (reply.Data is not null)
        {
            try
            {
                // Cold path — the caller's workspace owns the returned reply document (disposed with it), since
                // the returned value is used after this method returns.
                ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(reply.Data);
                workspace.TakeOwnership(replyDoc);
                replyPayload = replyDoc.RootElement;
            }
            catch (Exception parseEx)
            {
                // Parse error - apply error policy
                string messagingSystem = this.options.UseJetStream ? "nats-jetstream" : "nats";
                MessageErrorAction action = MessageErrorAction.Abort;

                if (this.options.ErrorPolicy is not null)
                {
                    ReadOnlyMemory<byte> subjectUtf8 = Encoding.UTF8.GetBytes(subject);
                    action = await this.options.ErrorPolicy.HandleErrorAsync(
                        parseEx,
                        new MessageErrorContext(
                            subjectUtf8,
                            MessageErrorKind.Deserialization,
                            default,
                            default),
                        cancellationToken).ConfigureAwait(false);
                }

                switch (action)
                {
                    case MessageErrorAction.Abort:
                        AsyncApiTelemetry.RecordAbort(subject, messagingSystem, MessageErrorKind.Deserialization);
                        throw;

                    case MessageErrorAction.DeadLetter:
                        string dlSubject = subject + this.options.DeadLetterSuffix;
                        try
                        {
                            await DeadLetterRawAsync(dlSubject, subject, reply.Data, parseEx, cancellationToken).ConfigureAwait(false);
                            AsyncApiTelemetry.RecordDeadLetter(dlSubject, subject, messagingSystem);
                        }
                        catch (Exception dlEx)
                        {
                            AsyncApiTelemetry.RecordDeadLetterFailure(dlSubject, subject, messagingSystem, dlEx);
                        }

                        // Request failed even though we DLQ'd the bad reply
                        throw;

                    case MessageErrorAction.Skip:
                    default:
                        // Skip means fail for request-reply (can't skip a reply)
                        AsyncApiTelemetry.RecordSkip(subject, messagingSystem, MessageErrorKind.Deserialization);
                        throw;
                }
            }
        }
        else
        {
            replyPayload = default;
        }

        ParsedJsonDocument<JsonElement>? headersDoc = DecodeHeadersDocument(reply.Headers);
        if (headersDoc is not null)
        {
            workspace.TakeOwnership(headersDoc);
        }

        JsonElement replyHeaders = headersDoc?.RootElement ?? default;
        return (replyPayload, replyHeaders);
    }

    private async ValueTask SubscribeToJetStreamAsync<TPayload>(
        string channel,
        string streamName,
        ReadOnlyMemory<byte> channelUtf8,
        MessageHandler<TPayload> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Build dead-letter channel UTF-8 bytes
        Span<byte> dlChannelUtf8 = stackalloc byte[channelUtf8.Length + this.deadLetterSuffixUtf8.Length];
        channelUtf8.Span.CopyTo(dlChannelUtf8);
        this.deadLetterSuffixUtf8.CopyTo(dlChannelUtf8[channelUtf8.Length..]);
        string dlChannel = Encoding.UTF8.GetString(dlChannelUtf8);

        INatsJSConsumer consumer;
        try
        {
            // Ensure stream exists
            await this.EnsureStreamExistsAsync(streamName, channel, cts.Token).ConfigureAwait(false);

            consumer = await this.CreateOrUpdateJetStreamConsumerAsync(streamName, channel, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Failed before the claim: nothing is registered anywhere, so the only thing to
            // release is the linked source (and with it the registration on the caller's token).
            cts.Dispose();
            throw;
        }

        object marker = new();
        bool loopExited = false;
        Task consumeTask = Task.Run(
            async () =>
            {
                ExecutingSubscription.Value = marker;
                try
                {
                    await foreach (NatsJSMsg<byte[]> msg in consumer.ConsumeAsync<byte[]>(cancellationToken: cts.Token).ConfigureAwait(false))
                    {
                        this.options.Heartbeat?.Tick(channel, "nats-jetstream", marker);

                        if (msg.Data is null)
                        {
                            await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                            continue;
                        }

                        // Parse
                        ParsedJsonDocument<TPayload> payloadDoc;
                        try
                        {
                            payloadDoc = ParsedJsonDocument<TPayload>.Parse(msg.Data);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                            MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                            if (action == MessageErrorAction.Abort)
                            {
                                AsyncApiTelemetry.RecordAbort(channel, "nats-jetstream", MessageErrorKind.Deserialization);
                                await NakSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                break;
                            }

                            if (action == MessageErrorAction.DeadLetter)
                            {
                                try
                                {
                                    await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                    AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats-jetstream");
                                    await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                }
                                catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                {
                                    AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats-jetstream", dlEx);
                                    await NakSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                AsyncApiTelemetry.RecordSkip(channel, "nats-jetstream", MessageErrorKind.Deserialization);
                                await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                            }

                            continue;
                        }

                        // Handle (through middleware if configured)
                        using (payloadDoc)
                        {
                            TPayload payload = payloadDoc.RootElement;

                            ParsedJsonDocument<JsonElement>? headersDoc;
                            try
                            {
                                headersDoc = DecodeHeadersDocument(msg.Headers);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                if (action == MessageErrorAction.Abort)
                                {
                                    AsyncApiTelemetry.RecordAbort(channel, "nats-jetstream", MessageErrorKind.Deserialization);
                                    await NakSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                    break;
                                }

                                if (action == MessageErrorAction.DeadLetter)
                                {
                                    try
                                    {
                                        await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats-jetstream");
                                        await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                    }
                                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                    {
                                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats-jetstream", dlEx);
                                        await NakSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    AsyncApiTelemetry.RecordSkip(channel, "nats-jetstream", MessageErrorKind.Deserialization);
                                    await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                }

                                continue;
                            }

                            try
                            {
                                using (headersDoc)
                                {
                                    JsonElement headers = headersDoc?.RootElement ?? default;

                                    // Box the native message struct only when a delivery-context
                                    // handler will consume it; legacy subscriptions discard it.
                                    object? nativeMessage = handler.UsesDeliveryContext ? msg : null;
                                    if (this.middleware is not null)
                                    {
                                        await this.middleware(
                                            (ct) => handler.Invoke(payload, channelUtf8, headers, nativeMessage, ct),
                                            cts.Token).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await handler.Invoke(payload, channelUtf8, headers, nativeMessage, cts.Token).ConfigureAwait(false);
                                    }

                                    await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                }
                            }
                            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                            {
                                await NakSafeAsync(msg, channel, CancellationToken.None).ConfigureAwait(false);
                                break;
                            }
                            catch (Exception ex)
                            {
                                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler);
                                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                if (action == MessageErrorAction.Abort)
                                {
                                    AsyncApiTelemetry.RecordAbort(channel, "nats-jetstream", MessageErrorKind.Handler);
                                    await NakSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                    break;
                                }

                                if (action == MessageErrorAction.DeadLetter)
                                {
                                    try
                                    {
                                        await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats-jetstream");
                                        await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                    }
                                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                    {
                                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats-jetstream", dlEx);
                                        await NakSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    AsyncApiTelemetry.RecordSkip(channel, "nats-jetstream", MessageErrorKind.Handler);
                                    await AckSafeAsync(msg, channel, cts.Token).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    // Normal shutdown via UnsubscribeAsync or parent cancellation
                }
                catch (Exception ex)
                {
                    // Nothing awaits this task on the happy path, so a fault that escapes the
                    // loop is recorded here; the release below frees the channel to resubscribe.
                    AsyncApiTelemetry.RecordSubscriptionFault(channel, "nats-jetstream", ex);
                }
                finally
                {
                    // Flagged before the release, so the subscribe path either observes the death
                    // after its claim, or the release runs after the claim landed and removes it.
                    Volatile.Write(ref loopExited, true);

                    // Full fence: the flag store must be globally visible before the
                    // lock-free registry read below can miss a claim, closing the
                    // store-buffer window in the died-during-claim handshake.
                    Interlocked.MemoryBarrier();
                    this.ReleaseSubscriptionOnLoopExit(channel, marker, "nats-jetstream");
                    cts.Dispose();
                }
            },
            CancellationToken.None);

        SubscriptionState state = new(cts, consumeTask, marker);
        await this.ClaimSubscriptionAsync(channel, state).ConfigureAwait(false);

        // Started only once the claim is final: a subscribe that fails at or before the claim
        // must leave no phantom Running entry behind.
        this.options.Heartbeat?.Start(channel, "nats-jetstream", marker);
        await this.ThrowIfLoopTerminatedAsync(channel, state, Volatile.Read(ref loopExited), "nats-jetstream").ConfigureAwait(false);
    }

    private async ValueTask<INatsJSConsumer> CreateOrUpdateJetStreamConsumerAsync(string streamName, string channel, CancellationToken cancellationToken)
    {
        string consumerName = this.options.ConsumerName ?? $"{streamName}_{channel}_consumer";
        ConsumerConfig consumerConfig = new(consumerName)
        {
            DurableName = consumerName,
            FilterSubject = channel,
            AckPolicy = NATS.Client.JetStream.Models.ConsumerConfigAckPolicy.Explicit,
            AckWait = this.options.AckWait,
            MaxDeliver = this.options.MaxDeliver,
            DeliverPolicy = this.options.DeliverPolicy switch
            {
                DeliverPolicy.All => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.All,
                DeliverPolicy.New => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.New,
                DeliverPolicy.ByStartSequence => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.ByStartSequence,
                DeliverPolicy.ByStartTime => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.ByStartTime,
                DeliverPolicy.Last => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.Last,
                DeliverPolicy.LastPerSubject => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.LastPerSubject,
                _ => NATS.Client.JetStream.Models.ConsumerConfigDeliverPolicy.All,
            },
        };

        return await this.jsContext!.CreateOrUpdateConsumerAsync(streamName, consumerConfig, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SubscribeToCoreNatsAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        MessageHandler<TPayload> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Build dead-letter channel UTF-8 bytes
        Span<byte> dlChannelUtf8 = stackalloc byte[channelUtf8.Length + this.deadLetterSuffixUtf8.Length];
        channelUtf8.Span.CopyTo(dlChannelUtf8);
        this.deadLetterSuffixUtf8.CopyTo(dlChannelUtf8[channelUtf8.Length..]);
        string dlChannel = Encoding.UTF8.GetString(dlChannelUtf8);

        // Register the subscription with the server before starting the background
        // consumption loop. Using SubscribeCoreAsync (rather than SubscribeAsync
        // inside Task.Run) guarantees the SUB command is sent before this method
        // returns, eliminating the race where a published message arrives at the
        // server before the subscription is active.
        INatsSub<byte[]> sub;
        try
        {
            sub = await this.connection.SubscribeCoreAsync<byte[]>(
                subject: channel,
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Failed before the claim: nothing is registered anywhere, so the only thing to
            // release is the linked source (and with it the registration on the caller's token).
            cts.Dispose();
            throw;
        }

        object marker = new();
        bool loopExited = false;
        Task consumeTask = Task.Run(
            async () =>
            {
                ExecutingSubscription.Value = marker;
                try
                {
                    await using (sub)
                    {
                        await foreach (NatsMsg<byte[]> msg in sub.Msgs.ReadAllAsync(cts.Token).ConfigureAwait(false))
                        {
                            this.options.Heartbeat?.Tick(channel, "nats", marker);

                            if (msg.Data is null)
                            {
                                continue;
                            }

                            // Parse
                            ParsedJsonDocument<TPayload> payloadDoc;
                            try
                            {
                                payloadDoc = ParsedJsonDocument<TPayload>.Parse(msg.Data);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                if (action == MessageErrorAction.Abort)
                                {
                                    AsyncApiTelemetry.RecordAbort(channel, "nats", MessageErrorKind.Deserialization);
                                    break;
                                }

                                if (action == MessageErrorAction.DeadLetter)
                                {
                                    try
                                    {
                                        await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats");
                                    }
                                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                    {
                                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats", dlEx);
                                    }
                                }
                                else
                                {
                                    AsyncApiTelemetry.RecordSkip(channel, "nats", MessageErrorKind.Deserialization);
                                }

                                continue;
                            }

                            // Handle (through middleware if configured)
                            using (payloadDoc)
                            {
                                TPayload payload = payloadDoc.RootElement;

                                ParsedJsonDocument<JsonElement>? headersDoc;
                                try
                                {
                                    headersDoc = DecodeHeadersDocument(msg.Headers);
                                }
                                catch (Exception ex) when (ex is not OperationCanceledException)
                                {
                                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                    if (action == MessageErrorAction.Abort)
                                    {
                                        AsyncApiTelemetry.RecordAbort(channel, "nats", MessageErrorKind.Deserialization);
                                        break;
                                    }

                                    if (action == MessageErrorAction.DeadLetter)
                                    {
                                        try
                                        {
                                            await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats");
                                        }
                                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                        {
                                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats", dlEx);
                                        }
                                    }
                                    else
                                    {
                                        AsyncApiTelemetry.RecordSkip(channel, "nats", MessageErrorKind.Deserialization);
                                    }

                                    continue;
                                }

                                try
                                {
                                    using (headersDoc)
                                    {
                                        JsonElement headers = headersDoc?.RootElement ?? default;

                                        // Box the native message struct only when a delivery-context
                                        // handler will consume it; legacy subscriptions discard it.
                                        object? nativeMessage = handler.UsesDeliveryContext ? msg : null;
                                        if (this.middleware is not null)
                                        {
                                            await this.middleware(
                                                (ct) => handler.Invoke(payload, channelUtf8, headers, nativeMessage, ct),
                                                cts.Token).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            await handler.Invoke(payload, channelUtf8, headers, nativeMessage, cts.Token).ConfigureAwait(false);
                                        }
                                    }
                                }
                                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                                {
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler);
                                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                    if (action == MessageErrorAction.Abort)
                                    {
                                        AsyncApiTelemetry.RecordAbort(channel, "nats", MessageErrorKind.Handler);
                                        break;
                                    }

                                    if (action == MessageErrorAction.DeadLetter)
                                    {
                                        try
                                        {
                                            await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats");
                                        }
                                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                        {
                                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats", dlEx);
                                        }
                                    }
                                    else
                                    {
                                        AsyncApiTelemetry.RecordSkip(channel, "nats", MessageErrorKind.Handler);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    // Normal shutdown via UnsubscribeAsync or parent cancellation
                }
                catch (Exception ex)
                {
                    // Nothing awaits this task on the happy path, so a fault that escapes the
                    // loop is recorded here; the release below frees the channel to resubscribe.
                    AsyncApiTelemetry.RecordSubscriptionFault(channel, "nats", ex);
                }
                finally
                {
                    // Flagged before the release, so the subscribe path either observes the death
                    // after its claim, or the release runs after the claim landed and removes it.
                    Volatile.Write(ref loopExited, true);

                    // Full fence: the flag store must be globally visible before the
                    // lock-free registry read below can miss a claim, closing the
                    // store-buffer window in the died-during-claim handshake.
                    Interlocked.MemoryBarrier();
                    this.ReleaseSubscriptionOnLoopExit(channel, marker, "nats");
                    cts.Dispose();
                }
            },
            CancellationToken.None);

        SubscriptionState state = new(cts, consumeTask, marker);
        await this.ClaimSubscriptionAsync(channel, state).ConfigureAwait(false);

        // Started only once the claim is final: a subscribe that fails at or before the claim
        // must leave no phantom Running entry behind.
        this.options.Heartbeat?.Start(channel, "nats", marker);
        await this.ThrowIfLoopTerminatedAsync(channel, state, Volatile.Read(ref loopExited), "nats").ConfigureAwait(false);
    }

    private async ValueTask SubscribeReplyToCoreNatsAsync<TRequest, TReply>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Build dead-letter channel UTF-8 bytes
        Span<byte> dlChannelUtf8 = stackalloc byte[channelUtf8.Length + this.deadLetterSuffixUtf8.Length];
        channelUtf8.Span.CopyTo(dlChannelUtf8);
        this.deadLetterSuffixUtf8.CopyTo(dlChannelUtf8[channelUtf8.Length..]);
        string dlChannel = Encoding.UTF8.GetString(dlChannelUtf8);

        // Register the subscription with the server before starting the background
        // consumption loop so the SUB command is sent before this method returns,
        // matching SubscribeToCoreNatsAsync.
        INatsSub<byte[]> sub;
        try
        {
            sub = await this.connection.SubscribeCoreAsync<byte[]>(
                subject: channel,
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Failed before the claim: nothing is registered anywhere, so the only thing to
            // release is the linked source (and with it the registration on the caller's token).
            cts.Dispose();
            throw;
        }

        object marker = new();
        bool loopExited = false;
        Task consumeTask = Task.Run(
            async () =>
            {
                ExecutingSubscription.Value = marker;
                try
                {
                    await using (sub)
                    {
                        await foreach (NatsMsg<byte[]> msg in sub.Msgs.ReadAllAsync(cts.Token).ConfigureAwait(false))
                        {
                            this.options.Heartbeat?.Tick(channel, "nats", marker);

                            if (msg.Data is null)
                            {
                                continue;
                            }

                            // Parse the request
                            ParsedJsonDocument<TRequest> requestDoc;
                            try
                            {
                                requestDoc = ParsedJsonDocument<TRequest>.Parse(msg.Data);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                if (action == MessageErrorAction.Abort)
                                {
                                    AsyncApiTelemetry.RecordAbort(channel, "nats", MessageErrorKind.Deserialization);
                                    break;
                                }

                                if (action == MessageErrorAction.DeadLetter)
                                {
                                    try
                                    {
                                        await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                        AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats");
                                    }
                                    catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                    {
                                        AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats", dlEx);
                                    }
                                }
                                else
                                {
                                    AsyncApiTelemetry.RecordSkip(channel, "nats", MessageErrorKind.Deserialization);
                                }

                                continue;
                            }

                            // Handle the request and publish the reply
                            using (requestDoc)
                            {
                                TRequest request = requestDoc.RootElement;

                                ParsedJsonDocument<JsonElement>? headersDoc;
                                try
                                {
                                    headersDoc = DecodeHeadersDocument(msg.Headers);
                                }
                                catch (Exception ex) when (ex is not OperationCanceledException)
                                {
                                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Deserialization);
                                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                    if (action == MessageErrorAction.Abort)
                                    {
                                        AsyncApiTelemetry.RecordAbort(channel, "nats", MessageErrorKind.Deserialization);
                                        break;
                                    }

                                    if (action == MessageErrorAction.DeadLetter)
                                    {
                                        try
                                        {
                                            await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats");
                                        }
                                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                        {
                                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats", dlEx);
                                        }
                                    }
                                    else
                                    {
                                        AsyncApiTelemetry.RecordSkip(channel, "nats", MessageErrorKind.Deserialization);
                                    }

                                    continue;
                                }

                                try
                                {
                                    using (headersDoc)
                                    {
                                        JsonElement headers = headersDoc?.RootElement ?? default;

                                        TReply reply;
                                        if (this.middleware is not null)
                                        {
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

                                        // Publish the reply to the request's reply-to subject. NATS correlates
                                        // the reply with the original RequestAsync caller via its inbox subject,
                                        // so a Corvus requester receives this without any explicit correlation id.
                                        if (msg.ReplyTo is not null)
                                        {
                                            await this.connection.PublishAsync(
                                                subject: msg.ReplyTo,
                                                data: reply,
                                                headers: null,
                                                replyTo: null,
                                                serializer: JsonElementSerializer<TReply>.Instance,
                                                opts: default,
                                                cancellationToken: cts.Token).ConfigureAwait(false);
                                        }
                                    }
                                }
                                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                                {
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler);
                                    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cts.Token).ConfigureAwait(false);
                                    if (action == MessageErrorAction.Abort)
                                    {
                                        AsyncApiTelemetry.RecordAbort(channel, "nats", MessageErrorKind.Handler);
                                        break;
                                    }

                                    if (action == MessageErrorAction.DeadLetter)
                                    {
                                        try
                                        {
                                            await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                                            AsyncApiTelemetry.RecordDeadLetter(dlChannel, channel, "nats");
                                        }
                                        catch (Exception dlEx) when (dlEx is not OperationCanceledException)
                                        {
                                            AsyncApiTelemetry.RecordDeadLetterFailure(dlChannel, channel, "nats", dlEx);
                                        }
                                    }
                                    else
                                    {
                                        AsyncApiTelemetry.RecordSkip(channel, "nats", MessageErrorKind.Handler);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    // Normal shutdown via UnsubscribeAsync or parent cancellation
                }
                catch (Exception ex)
                {
                    // Nothing awaits this task on the happy path, so a fault that escapes the
                    // loop is recorded here; the release below frees the channel to resubscribe.
                    AsyncApiTelemetry.RecordSubscriptionFault(channel, "nats", ex);
                }
                finally
                {
                    // Flagged before the release, so the subscribe path either observes the death
                    // after its claim, or the release runs after the claim landed and removes it.
                    Volatile.Write(ref loopExited, true);

                    // Full fence: the flag store must be globally visible before the
                    // lock-free registry read below can miss a claim, closing the
                    // store-buffer window in the died-during-claim handshake.
                    Interlocked.MemoryBarrier();
                    this.ReleaseSubscriptionOnLoopExit(channel, marker, "nats");
                    cts.Dispose();
                }
            },
            CancellationToken.None);

        SubscriptionState state = new(cts, consumeTask, marker);
        await this.ClaimSubscriptionAsync(channel, state).ConfigureAwait(false);

        // Started only once the claim is final: a subscribe that fails at or before the claim
        // must leave no phantom Running entry behind.
        this.options.Heartbeat?.Start(channel, "nats", marker);
        await this.ThrowIfLoopTerminatedAsync(channel, state, Volatile.Read(ref loopExited), "nats").ConfigureAwait(false);
    }

    private static ParsedJsonDocument<JsonElement>? DecodeHeadersDocument(NatsHeaders? headers)
    {
        if (headers is null)
        {
            return null;
        }

        if (headers.TryGetValue(HeadersKey, out StringValues values) &&
            values.Count > 0 &&
            values[0] is string base64)
        {
            int maxBytes = ((base64.Length * 3) + 3) / 4;
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
            if (Convert.TryFromBase64String(base64, rented, out int bytesWritten))
            {
                // Transfer ownership — document returns the array on Dispose()
                return ParsedJsonDocument<JsonElement>.Parse(rented.AsMemory(0, bytesWritten), rented);
            }

            ArrayPool<byte>.Shared.Return(rented);
        }

        return null;
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

    private ValueTask DeadLetterRawAsync(
        string deadLetterChannel,
        string originalChannel,
        byte[] rawPayload,
        Exception exception,
        CancellationToken cancellationToken)
    {
        NatsHeaders natsHeaders = new()
        {
            ["Corvus-Original-Channel"] = originalChannel,
            ["Corvus-Error"] = exception.Message,
            ["Corvus-Error-Type"] = exception.GetType().FullName ?? exception.GetType().Name,
        };

        return this.connection.PublishAsync(
            subject: deadLetterChannel,
            data: rawPayload,
            headers: natsHeaders,
            replyTo: null,
            cancellationToken: cancellationToken);
    }

    private sealed record SubscriptionState(
        CancellationTokenSource CancellationSource,
        Task ConsumeTask,
        object Marker);

    /// <summary>
    /// Writes an <see cref="IJsonElement{T}"/> directly into the NATS protocol buffer
    /// via <see cref="IBufferWriter{T}"/>, eliminating intermediate allocations.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    private sealed class JsonElementSerializer<T> : INatsSerialize<T>
        where T : struct, IJsonElement<T>
    {
        public static readonly JsonElementSerializer<T> Instance = new();

        [ThreadStatic]
        private static Utf8JsonWriter? t_serializer;

        public void Serialize(IBufferWriter<byte> bufferWriter, T value)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                return;
            }

            Utf8JsonWriter writer = t_serializer ??= new(bufferWriter);
            writer.Reset(bufferWriter);
            value.WriteTo(writer);
            writer.Flush();
        }
    }
}