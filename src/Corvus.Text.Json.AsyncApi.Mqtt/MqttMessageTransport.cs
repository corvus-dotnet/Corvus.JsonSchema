// <copyright file="MqttMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json.Internal;
using MQTTnet;
using MQTTnet.Client;
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
public sealed class MqttMessageTransport : IMessageTransport
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
    private bool disposed;

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
        string? headersBase64 = headers.ValueKind != JsonValueKind.Undefined
            ? SerializeToBase64String(in headers)
            : null;

        (byte[] rented, int length) = SerializeToRented(in request);
        return RequestCoreAsync<TReply>(requestChannel, replyChannel, rented, length, correlationIdUtf8, headersBase64, cancellationToken);
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
        this.handlers.TryRemove(channel, out _);

        MqttClientUnsubscribeOptions unsubOptions = new MqttFactory().CreateUnsubscribeOptionsBuilder()
            .WithTopicFilter(channel)
            .Build();

        await this.client.UnsubscribeAsync(unsubOptions, cancellationToken).ConfigureAwait(false);
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
        this.handlers.Clear();
        this.pendingReplies.Clear();

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
        CancellationToken cancellationToken)
        where TReply : struct, IJsonElement<TReply>
    {
        TaskCompletionSource<MqttApplicationMessage> replyTcs = new();
        this.pendingReplies[correlationIdUtf8] = replyTcs;

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

            // Publish the request
            MqttApplicationMessage requestMsg = BuildMessage(requestChannel, rented, length, headersBase64, correlationIdUtf8);
            await this.client.PublishAsync(requestMsg, cancellationToken).ConfigureAwait(false);

            // Wait for correlated reply
            MqttApplicationMessage reply = await replyTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            TReply replyPayload;
            if (reply.PayloadSegment is { Count: > 0 })
            {
                // Cold path — document not disposed; returned values reference its memory
                ParsedJsonDocument<TReply> replyDoc = ParsedJsonDocument<TReply>.Parse(
                    reply.PayloadSegment.Array!.AsMemory(reply.PayloadSegment.Offset, reply.PayloadSegment.Count));
                replyPayload = replyDoc.RootElement;
            }
            else
            {
                replyPayload = default;
            }

            ParsedJsonDocument<JsonElement>? headersDoc = DecodeHeadersDocument(reply);
            JsonElement replyHeaders = headersDoc?.RootElement ?? default;
            return (replyPayload, replyHeaders);
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
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string dlChannel = channel + this.options.DeadLetterSuffix;
        this.handlers[channel] = (message, ct) => this.DispatchToHandlerAsync(channel, channelUtf8, dlChannel, handler, message, ct);

        MqttClientSubscribeOptions subOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
            .WithTopicFilter(channel, this.options.QualityOfServiceLevel)
            .Build();

        await this.client.SubscribeAsync(subOptions, cancellationToken).ConfigureAwait(false);
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
        ReadOnlyMemory<byte> correlationIdUtf8)
    {
        MqttApplicationMessage message = new()
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(rented, 0, length),
            QualityOfServiceLevel = this.options.QualityOfServiceLevel,
            Retain = this.options.Retain,
            ContentType = "application/json",
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

        if (corrData is { Length: > 0 } &&
            this.pendingReplies.TryRemove(new ReadOnlyMemory<byte>(corrData), out TaskCompletionSource<MqttApplicationMessage>? tcs))
        {
            tcs.SetResult(args.ApplicationMessage);
            return Task.CompletedTask;
        }

        if (this.handlers.TryGetValue(topic, out Func<MqttApplicationMessage, CancellationToken, ValueTask>? handler))
        {
            return handler(args.ApplicationMessage, CancellationToken.None).AsTask();
        }

        return Task.CompletedTask;
    }

    private async ValueTask DispatchToHandlerAsync<TPayload>(
        string channel,
        ReadOnlyMemory<byte> channelUtf8,
        string deadLetterChannel,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
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
                await this.DeadLetterRawAsync(deadLetterChannel, channel, message.PayloadSegment, ex, cancellationToken).ConfigureAwait(false);
            }
            else if (action == MessageErrorAction.Abort)
            {
                await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        using (payloadDoc)
        {
            TPayload payload = payloadDoc.RootElement;
            JsonElement payloadElement = JsonElement.From(in payload);
            using ParsedJsonDocument<JsonElement>? headersDoc = this.DecodeHeadersDocument(message);
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
                return;
            }
            catch (Exception ex)
            {
                MessageErrorContext ctx = new(channelUtf8, MessageErrorKind.Handler, payloadElement, headers);
                MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, ctx, cancellationToken).ConfigureAwait(false);
                if (action == MessageErrorAction.DeadLetter)
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
                }
                else if (action == MessageErrorAction.Abort)
                {
                    await this.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
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