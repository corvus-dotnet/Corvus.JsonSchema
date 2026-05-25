// <copyright file="NatsMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;

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
public sealed class NatsMessageTransport : IMessageTransport, IHealthCheckableTransport
{
    private const string HeadersKey = "Corvus-Headers";
    private const string CorrelationIdKey = "Corvus-Correlation-Id";

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_serializeBuffer;

    [ThreadStatic]
    private static Utf8JsonWriter? t_writer;

    private readonly NatsTransportOptions options;
    private readonly NatsConnection connection;
    private readonly IMessageErrorPolicy errorPolicy;
    private readonly MessageHandlerMiddleware? middleware;
    private readonly ConcurrentDictionary<string, SubscriptionState> subscriptions = new(StringComparer.Ordinal);
    private bool disposed;

    private NatsMessageTransport(NatsTransportOptions options, NatsConnection connection)
    {
        this.options = options;
        this.connection = connection;
        this.errorPolicy = options.ErrorPolicy ?? new DefaultMessageErrorPolicy();
        this.middleware = options.HandlerMiddleware;
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
        };

        NatsConnection connection = new(natsOpts);
        await connection.ConnectAsync().ConfigureAwait(false);

        return new NatsMessageTransport(options, connection);
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

        _ = replyChannelUtf8;
        string requestChannel = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        TRequest requestCopy = request;
        NatsHeaders natsHeaders = new()
        {
            [CorrelationIdKey] = Encoding.UTF8.GetString(correlationIdUtf8.Span),
        };

        if (headers.ValueKind != JsonValueKind.Undefined)
        {
            natsHeaders[HeadersKey] = SerializeToBase64String(in headers);
        }

        return RequestCoreAsync<TRequest, TReply>(requestChannel, requestCopy, natsHeaders, cancellationToken);
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

            try
            {
                await state.ConsumeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on clean shutdown
            }

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

        foreach ((string _, SubscriptionState state) in this.subscriptions)
        {
            await state.CancellationSource.CancelAsync().ConfigureAwait(false);

            try
            {
                await state.ConsumeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on clean shutdown
            }

            state.CancellationSource.Dispose();
        }

        this.subscriptions.Clear();
        await this.connection.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TRequest, TReply>(
        string subject,
        TRequest request,
        NatsHeaders headers,
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
            // Cold path — document not disposed; returned values reference its memory
            ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(reply.Data);
            replyPayload = replyDoc.RootElement;
        }
        else
        {
            replyPayload = default;
        }

        ParsedJsonDocument<JsonElement>? headersDoc = DecodeHeadersDocument(reply.Headers);
        JsonElement replyHeaders = headersDoc?.RootElement ?? default;
        return (replyPayload, replyHeaders);
    }

    private async ValueTask SubscribeCoreAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string dlChannel = channel + this.options.DeadLetterSuffix;

        Task consumeTask = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (NatsMsg<byte[]> msg in this.connection.SubscribeAsync<byte[]>(
                        subject: channel,
                        cancellationToken: cts.Token).ConfigureAwait(false))
                    {
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
                                break;
                            }

                            if (action == MessageErrorAction.DeadLetter)
                            {
                                await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                            }

                            continue;
                        }

                        // Handle (through middleware if configured)
                        try
                        {
                            using (payloadDoc)
                            {
                                TPayload payload = payloadDoc.RootElement;
                                using ParsedJsonDocument<JsonElement>? headersDoc = DecodeHeadersDocument(msg.Headers);
                                JsonElement headers = headersDoc?.RootElement ?? default;

                                if (this.middleware is not null)
                                {
                                    await this.middleware(
                                        (ct) => handler(payload, headers, ct),
                                        cts.Token).ConfigureAwait(false);
                                }
                                else
                                {
                                    await handler(payload, headers, cts.Token).ConfigureAwait(false);
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
                                break;
                            }

                            if (action == MessageErrorAction.DeadLetter)
                            {
                                await this.DeadLetterRawAsync(dlChannel, channel, msg.Data, ex, cts.Token).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    // Normal shutdown via UnsubscribeAsync or parent cancellation
                }
            },
            CancellationToken.None);

        SubscriptionState state = new(cts, consumeTask);
        this.subscriptions[channel] = state;
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
        Task ConsumeTask);

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