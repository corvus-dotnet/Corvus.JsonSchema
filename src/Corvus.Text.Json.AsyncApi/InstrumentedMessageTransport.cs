// <copyright file="InstrumentedMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// A decorator that adds OpenTelemetry-compliant distributed tracing and metrics
/// to any <see cref="IMessageTransport"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Wrap any transport with this decorator to gain automatic:
/// </para>
/// <list type="bullet">
/// <item><description>Distributed trace spans (Activities) for publish, subscribe, request, and dead-letter operations.</description></item>
/// <item><description>Metrics counters and histograms for message throughput, processing duration, and errors.</description></item>
/// <item><description>W3C trace context propagation via message headers (traceparent/tracestate).</description></item>
/// </list>
/// <para>
/// All instrumentation is zero-cost when no listener is attached.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// IMessageTransport raw = await NatsMessageTransport.CreateAsync(options);
/// IMessageTransport transport = new InstrumentedMessageTransport(raw, "nats");
/// </code>
/// </para>
/// </remarks>
public sealed class InstrumentedMessageTransport : IMessageTransport
{
    private readonly IMessageTransport inner;
    private readonly string messagingSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedMessageTransport"/> class.
    /// </summary>
    /// <param name="inner">The transport to decorate with instrumentation.</param>
    /// <param name="messagingSystem">The messaging system identifier (e.g., <c>"nats"</c>,
    /// <c>"amqp"</c>, <c>"mqtt"</c>, <c>"websocket"</c>, <c>"kafka"</c>).
    /// Used as the <c>messaging.system</c> tag on all spans and metrics.</param>
    public InstrumentedMessageTransport(IMessageTransport inner, string messagingSystem)
    {
        this.inner = inner;
        this.messagingSystem = messagingSystem;
    }

    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        TPayload payloadCopy = payload;
        JsonElement headersCopy = headers;
        return PublishCoreAsync(channelUtf8, payloadCopy, headersCopy, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in MessageContext context,
        in JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        TPayload payloadCopy = payload;
        MessageContext contextCopy = context;
        JsonElement headersCopy = headers;
        return PublishWithContextCoreAsync(channelUtf8, payloadCopy, contextCopy, headersCopy, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        return RequestCoreAsync<TRequest, TReply>(
            requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, headers, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        in MessageContext context,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        MessageContext contextCopy = context;
        return RequestWithContextCoreAsync<TRequest, TReply>(
            requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, contextCopy, headers, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);

        return this.inner.SubscribeAsync(
            channelUtf8,
            CreateInstrumentedHandler(handler, destination),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        in MessageContext context,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        MessageContext contextCopy = context;

        return this.inner.SubscribeAsync(
            channelUtf8,
            CreateInstrumentedHandler(handler, destination),
            in contextCopy,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask UnsubscribeAsync(
        ReadOnlyMemory<byte> channelUtf8,
        CancellationToken cancellationToken)
    {
        return this.inner.UnsubscribeAsync(channelUtf8, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return this.inner.DisposeAsync();
    }

    private async ValueTask PublishCoreAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        TPayload payload,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);

        using Activity? activity = AsyncApiTelemetry.ActivitySource.StartActivity(
            $"send {destination}",
            ActivityKind.Producer);

        SetCommonTags(activity, "send", destination);

        headers = TraceContextPropagator.Inject(in headers, activity);

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await this.inner.PublishAsync(channelUtf8, in payload, in headers, cancellationToken)
                .ConfigureAwait(false);

            AsyncApiTelemetry.MessagesSent.Add(
                1,
                new TagList
                {
                    { "messaging.system", this.messagingSystem },
                    { "messaging.operation.name", "send" },
                    { "messaging.destination.name", destination },
                });
        }
        catch (Exception ex)
        {
            RecordException(activity, ex, destination);
            throw;
        }
        finally
        {
            RecordDuration(AsyncApiTelemetry.OperationDuration, startTimestamp, "send", destination);
        }
    }

    private async ValueTask PublishWithContextCoreAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        TPayload payload,
        MessageContext context,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);

        using Activity? activity = AsyncApiTelemetry.ActivitySource.StartActivity(
            $"send {destination}",
            ActivityKind.Producer);

        SetCommonTags(activity, "send", destination);

        headers = TraceContextPropagator.Inject(in headers, activity);

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await this.inner.PublishAsync(channelUtf8, in payload, in context, in headers, cancellationToken)
                .ConfigureAwait(false);

            AsyncApiTelemetry.MessagesSent.Add(
                1,
                new TagList
                {
                    { "messaging.system", this.messagingSystem },
                    { "messaging.operation.name", "send" },
                    { "messaging.destination.name", destination },
                });
        }
        catch (Exception ex)
        {
            RecordException(activity, ex, destination);
            throw;
        }
        finally
        {
            RecordDuration(AsyncApiTelemetry.OperationDuration, startTimestamp, "send", destination);
        }
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestCoreAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string destination = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);

        using Activity? activity = AsyncApiTelemetry.ActivitySource.StartActivity(
            $"request {destination}",
            ActivityKind.Producer);

        SetCommonTags(activity, "request", destination);
        activity?.SetTag("messaging.message.conversation_id", correlationId);

        headers = TraceContextPropagator.Inject(in headers, activity);

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await this.inner.RequestAsync<TRequest, TReply>(
                requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, headers, cancellationToken)
                .ConfigureAwait(false);

            AsyncApiTelemetry.MessagesSent.Add(
                1,
                new TagList
                {
                    { "messaging.system", this.messagingSystem },
                    { "messaging.operation.name", "request" },
                    { "messaging.destination.name", destination },
                });

            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex, destination);
            throw;
        }
        finally
        {
            RecordDuration(AsyncApiTelemetry.OperationDuration, startTimestamp, "request", destination);
        }
    }

    private async ValueTask<(TReply Payload, JsonElement Headers)> RequestWithContextCoreAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        MessageContext context,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string destination = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        string correlationId = Encoding.UTF8.GetString(correlationIdUtf8.Span);

        using Activity? activity = AsyncApiTelemetry.ActivitySource.StartActivity(
            $"request {destination}",
            ActivityKind.Producer);

        SetCommonTags(activity, "request", destination);
        activity?.SetTag("messaging.message.conversation_id", correlationId);

        headers = TraceContextPropagator.Inject(in headers, activity);

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await this.inner.RequestAsync<TRequest, TReply>(
                requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, in context, headers, cancellationToken)
                .ConfigureAwait(false);

            AsyncApiTelemetry.MessagesSent.Add(
                1,
                new TagList
                {
                    { "messaging.system", this.messagingSystem },
                    { "messaging.operation.name", "request" },
                    { "messaging.destination.name", destination },
                });

            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex, destination);
            throw;
        }
        finally
        {
            RecordDuration(AsyncApiTelemetry.OperationDuration, startTimestamp, "request", destination);
        }
    }

    private Func<TPayload, JsonElement, CancellationToken, ValueTask> CreateInstrumentedHandler<TPayload>(
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        string destination)
        where TPayload : struct, IJsonElement<TPayload>
    {
        return async (payload, headers, ct) =>
        {
            ActivityContext parentContext = default;
            bool hasParent = TraceContextPropagator.TryExtractParentContext(in headers, out parentContext);

            using Activity? activity = hasParent
                ? AsyncApiTelemetry.ActivitySource.StartActivity(
                    $"process {destination}",
                    ActivityKind.Consumer,
                    parentContext)
                : AsyncApiTelemetry.ActivitySource.StartActivity(
                    $"process {destination}",
                    ActivityKind.Consumer);

            SetCommonTags(activity, "process", destination);

            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await handler(payload, headers, ct).ConfigureAwait(false);

                AsyncApiTelemetry.MessagesConsumed.Add(
                    1,
                    new TagList
                    {
                        { "messaging.system", this.messagingSystem },
                        { "messaging.operation.name", "process" },
                        { "messaging.destination.name", destination },
                    });
            }
            catch (Exception ex)
            {
                RecordError(activity, ex);
                throw;
            }
            finally
            {
                RecordDuration(AsyncApiTelemetry.ProcessDuration, startTimestamp, "process", destination);
            }
        };
    }

    private void SetCommonTags(Activity? activity, string operationName, string destination)
    {
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("messaging.system", this.messagingSystem);
            activity.SetTag("messaging.operation.type", operationName == "process" ? "process" : "send");
            activity.SetTag("messaging.operation.name", operationName);
            activity.SetTag("messaging.destination.name", destination);
        }
    }

    private void RecordDuration(
        Histogram<double> histogram,
        long startTimestamp,
        string operationName,
        string destination)
    {
        double elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        histogram.Record(
            elapsed,
            new TagList
            {
                { "messaging.system", this.messagingSystem },
                { "messaging.operation.name", operationName },
                { "messaging.destination.name", destination },
            });
    }

    private void RecordException(Activity? activity, Exception ex, string destination)
    {
        RecordError(activity, ex);
        AsyncApiTelemetry.MessagesSent.Add(
            1,
            new TagList
            {
                { "messaging.system", this.messagingSystem },
                { "messaging.destination.name", destination },
                { "error.type", ex.GetType().FullName },
            });
    }

    private static void RecordError(Activity? activity, Exception ex)
    {
        if (activity is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("error.type", ex.GetType().FullName);
        }
    }
}