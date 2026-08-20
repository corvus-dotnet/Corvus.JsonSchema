// <copyright file="InstrumentedMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
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
/// Create instances with <see cref="Create(IMessageTransport, string)"/>, which returns a wrapper that implements
/// <see cref="IMessageDeliveryContextTransport"/> and/or <see cref="IHealthCheckableTransport"/>
/// exactly when the wrapped transport does, so capability probes against the wrapper answer
/// for the wrapped transport. The constructor always produces a plain
/// <see cref="IMessageTransport"/> wrapper that surfaces neither capability.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// IMessageTransport raw = await NatsMessageTransport.CreateAsync(options);
/// IMessageTransport transport = InstrumentedMessageTransport.Create(raw, "nats");
/// </code>
/// </para>
/// </remarks>
public class InstrumentedMessageTransport : IMessageTransport
{
    private readonly IMessageTransport inner;

    // Per-channel destination and span-name strings, computed once per channel rather than per
    // operation. The key is a copy of the caller's bytes (callers pass rented or reused
    // memory); past the cap - a very dynamic channel set - names fall back to per-call
    // computation rather than growing without bound.
    private readonly ConcurrentDictionary<ReadOnlyMemory<byte>, ChannelNames> channelNames = new(ReadOnlyMemoryByteComparer.Instance);
    private readonly string messagingSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedMessageTransport"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor always produces a plain <see cref="IMessageTransport"/> wrapper that
    /// surfaces none of the wrapped transport's optional capabilities. Prefer
    /// <see cref="Create(IMessageTransport, string)"/>, whose wrapper implements exactly the
    /// capability interfaces the wrapped transport implements; take the result as the capability
    /// interface you need and cast for any additional one it carries.
    /// </remarks>
    /// <param name="inner">The transport to decorate with instrumentation.</param>
    /// <param name="messagingSystem">The messaging system identifier (e.g., <c>"nats"</c>,
    /// <c>"amqp"</c>, <c>"mqtt"</c>, <c>"websocket"</c>, <c>"kafka"</c>).
    /// Used as the <c>messaging.system</c> tag on all spans and metrics.</param>
    public InstrumentedMessageTransport(IMessageTransport inner, string messagingSystem)
    {
        this.inner = inner;
        this.messagingSystem = messagingSystem;
    }

    /// <summary>
    /// Creates an instrumented wrapper that preserves the wrapped transport's capabilities:
    /// the returned instance implements <see cref="IMessageDeliveryContextTransport"/> and/or
    /// <see cref="IHealthCheckableTransport"/> exactly when <paramref name="inner"/> does, so
    /// a capability probe such as <c>transport is IMessageDeliveryContextTransport</c> answers
    /// for the wrapped transport instead of failing at subscribe time.
    /// </summary>
    /// <param name="inner">The transport to decorate with instrumentation.</param>
    /// <param name="messagingSystem">The messaging system identifier (e.g., <c>"nats"</c>,
    /// <c>"amqp"</c>, <c>"mqtt"</c>, <c>"websocket"</c>, <c>"kafka"</c>).
    /// Used as the <c>messaging.system</c> tag on all spans and metrics.</param>
    /// <returns>The capability-matched instrumented transport.</returns>
    public static InstrumentedMessageTransport Create(IMessageTransport inner, string messagingSystem)
        => inner switch
        {
            IMessageDeliveryContextTransport and IHealthCheckableTransport => new WithDeliveryContextAndHealthCheck(inner, messagingSystem),
            IMessageDeliveryContextTransport => new WithDeliveryContext(inner, messagingSystem),
            IHealthCheckableTransport => new WithHealthCheck(inner, messagingSystem),
            _ => new InstrumentedMessageTransport(inner, messagingSystem),
        };

    /// <summary>
    /// Creates an instrumented wrapper for a transport that exposes delivery context, typed so
    /// the result can be handed straight to a generated <c>*WithDeliveryContextConsumer</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Create(IMessageTransport, string)"/> returns the base type, which does not itself implement
    /// <see cref="IMessageDeliveryContextTransport"/> — the capability lives on the wrapper it
    /// selects — so its result needs a cast before a delivery-context consumer will accept it.
    /// Taking the capability as the parameter type lets the compiler prove the wrapper carries
    /// it, so this overload returns it directly.
    /// </remarks>
    /// <param name="inner">The delivery-context-capable transport to decorate.</param>
    /// <param name="messagingSystem">The messaging system identifier (e.g., <c>"nats"</c>,
    /// <c>"amqp"</c>, <c>"mqtt"</c>, <c>"websocket"</c>, <c>"kafka"</c>).
    /// Used as the <c>messaging.system</c> tag on all spans and metrics.</param>
    /// <returns>The instrumented transport, still exposing delivery context.</returns>
    public static IMessageDeliveryContextTransport Create(IMessageDeliveryContextTransport inner, string messagingSystem)
        => inner is IHealthCheckableTransport
            ? new WithDeliveryContextAndHealthCheck(inner, messagingSystem)
            : new WithDeliveryContext(inner, messagingSystem);

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
        JsonWorkspace workspace,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        return RequestCoreAsync<TRequest, TReply>(
            requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, workspace, headers, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        in MessageContext context,
        JsonWorkspace workspace,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        MessageContext contextCopy = context;
        return RequestWithContextCoreAsync<TRequest, TReply>(
            requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, contextCopy, workspace, headers, cancellationToken);
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

    private ValueTask SubscribeWithDeliveryContextCoreAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        // Only the context-capable nested wrappers call this, and Create constructs them only
        // when the wrapped transport implements the capability, so the cast cannot fail.
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        return ((IMessageDeliveryContextTransport)this.inner).SubscribeWithDeliveryContextAsync(
            channelUtf8,
            CreateInstrumentedContextHandler(handler, destination),
            cancellationToken);
    }

    private ValueTask SubscribeWithDeliveryContextCoreAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        in MessageContext context,
        CancellationToken cancellationToken)
        where TPayload : struct, IJsonElement<TPayload>
    {
        // The binding overload forwards to the wrapped transport's own overload so a transport
        // that honors bindings still receives them through the instrumentation.
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        return ((IMessageDeliveryContextTransport)this.inner).SubscribeWithDeliveryContextAsync(
            channelUtf8,
            CreateInstrumentedContextHandler(handler, destination),
            in context,
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
    public ValueTask SubscribeReplyAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        return this.inner.SubscribeReplyAsync(
            channelUtf8,
            CreateInstrumentedReplyHandler(handler, destination),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SubscribeReplyAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        in MessageContext context,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        return this.inner.SubscribeReplyAsync(
            channelUtf8,
            CreateInstrumentedReplyHandler(handler, destination),
            in context,
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
    public ValueTask DeadLetterAsync(
        ReadOnlyMemory<byte> deadLetterChannelUtf8,
        ReadOnlyMemory<byte> originalChannelUtf8,
        in JsonElement payload,
        in JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken)
    {
        JsonElement payloadCopy = payload;
        JsonElement headersCopy = headers;
        return DeadLetterCoreAsync(
            deadLetterChannelUtf8, originalChannelUtf8, payloadCopy, headersCopy, exception, cancellationToken);
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
        ChannelNames names = this.GetChannelNames(channelUtf8);
        string destination = names.Destination;

        // Without a listener StartActivity returns null and every string this span needs is
        // wasted work, so the whole start is gated.
        using Activity? activity = AsyncApiTelemetry.ActivitySource.HasListeners()
            ? AsyncApiTelemetry.ActivitySource.StartActivity(names.SendSpanName, ActivityKind.Producer)
            : null;

        SetCommonTags(activity, "send", destination);

        using TraceContextPropagator.InjectedHeaders injectedHeaders = TraceContextPropagator.Inject(in headers, activity);
        headers = injectedHeaders.Headers;

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
            RecordException(activity, ex, destination, "send");
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
        ChannelNames names = this.GetChannelNames(channelUtf8);
        string destination = names.Destination;

        // Without a listener StartActivity returns null and every string this span needs is
        // wasted work, so the whole start is gated.
        using Activity? activity = AsyncApiTelemetry.ActivitySource.HasListeners()
            ? AsyncApiTelemetry.ActivitySource.StartActivity(names.SendSpanName, ActivityKind.Producer)
            : null;

        SetCommonTags(activity, "send", destination);

        using TraceContextPropagator.InjectedHeaders injectedHeaders = TraceContextPropagator.Inject(in headers, activity);
        headers = injectedHeaders.Headers;

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
            RecordException(activity, ex, destination, "send");
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
        JsonWorkspace workspace,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ChannelNames names = this.GetChannelNames(requestChannelUtf8);
        string destination = names.Destination;

        using Activity? activity = AsyncApiTelemetry.ActivitySource.HasListeners()
            ? AsyncApiTelemetry.ActivitySource.StartActivity(names.RequestSpanName, ActivityKind.Producer)
            : null;

        SetCommonTags(activity, "request", destination);

        // Conditional access short-circuits the argument too, so the correlation id becomes a
        // string only when a span exists to carry it.
        activity?.SetTag("messaging.message.conversation_id", Encoding.UTF8.GetString(correlationIdUtf8.Span));

        using TraceContextPropagator.InjectedHeaders injectedHeaders = TraceContextPropagator.Inject(in headers, activity);
        headers = injectedHeaders.Headers;

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await this.inner.RequestAsync<TRequest, TReply>(
                requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, workspace, headers, cancellationToken)
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
            RecordException(activity, ex, destination, "request");
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
        JsonWorkspace workspace,
        JsonElement headers,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ChannelNames names = this.GetChannelNames(requestChannelUtf8);
        string destination = names.Destination;

        using Activity? activity = AsyncApiTelemetry.ActivitySource.HasListeners()
            ? AsyncApiTelemetry.ActivitySource.StartActivity(names.RequestSpanName, ActivityKind.Producer)
            : null;

        SetCommonTags(activity, "request", destination);

        // Conditional access short-circuits the argument too, so the correlation id becomes a
        // string only when a span exists to carry it.
        activity?.SetTag("messaging.message.conversation_id", Encoding.UTF8.GetString(correlationIdUtf8.Span));

        using TraceContextPropagator.InjectedHeaders injectedHeaders = TraceContextPropagator.Inject(in headers, activity);
        headers = injectedHeaders.Headers;

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = await this.inner.RequestAsync<TRequest, TReply>(
                requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, in context, workspace, headers, cancellationToken)
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
            RecordException(activity, ex, destination, "request");
            throw;
        }
        finally
        {
            RecordDuration(AsyncApiTelemetry.OperationDuration, startTimestamp, "request", destination);
        }
    }

    private async ValueTask DeadLetterCoreAsync(
        ReadOnlyMemory<byte> deadLetterChannelUtf8,
        ReadOnlyMemory<byte> originalChannelUtf8,
        JsonElement payload,
        JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string destination = Encoding.UTF8.GetString(deadLetterChannelUtf8.Span);
        string originalChannel = Encoding.UTF8.GetString(originalChannelUtf8.Span);

        using Activity? activity = AsyncApiTelemetry.ActivitySource.StartActivity(
            $"dead-letter {destination}",
            ActivityKind.Producer);

        SetCommonTags(activity, "dead-letter", destination);
        activity?.SetTag("corvus.asyncapi.original_channel", originalChannel);
        activity?.SetTag("error.type", exception.GetType().FullName);

        try
        {
            await this.inner.DeadLetterAsync(
                deadLetterChannelUtf8, originalChannelUtf8, in payload, in headers, exception, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception deadLetterEx)
        {
            // A failed dead-letter means the message was dropped: the span must carry the
            // failure and the alert counter must fire before the exception surfaces.
            RecordError(activity, deadLetterEx);
            AsyncApiTelemetry.RecordDeadLetterFailure(destination, originalChannel, this.messagingSystem, deadLetterEx);
            throw;
        }

        AsyncApiTelemetry.DeadLetters.Add(
            1,
            new TagList
            {
                { "messaging.system", this.messagingSystem },
                { "messaging.destination.name", destination },
                { "corvus.asyncapi.original_channel", originalChannel },
            });
    }

    // The activity creation/tagging and the consumed-counter contents live in these two helpers
    // so the three instrumented handler shapes below cannot drift in the telemetry they emit.
    private Activity? StartProcessActivity(in JsonElement headers, string processSpanName, string destination)
    {
        // Without a listener StartActivity returns null; the parent-context extraction (which
        // materializes the producer's traceparent/tracestate strings) exists only to feed it,
        // so the whole start is gated. The span name is computed once per subscription.
        if (!AsyncApiTelemetry.ActivitySource.HasListeners())
        {
            return null;
        }

        ActivityContext parentContext = default;
        bool hasParent = TraceContextPropagator.TryExtractParentContext(in headers, out parentContext);

        Activity? activity = hasParent
            ? AsyncApiTelemetry.ActivitySource.StartActivity(
                processSpanName, ActivityKind.Consumer, parentContext)
            : AsyncApiTelemetry.ActivitySource.StartActivity(
                processSpanName, ActivityKind.Consumer);

        SetCommonTags(activity, "process", destination);
        return activity;
    }

    private ChannelNames GetChannelNames(ReadOnlyMemory<byte> channelUtf8)
    {
        if (this.channelNames.TryGetValue(channelUtf8, out ChannelNames? names))
        {
            return names;
        }

        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        ChannelNames created = new(destination, "send " + destination, "request " + destination);
        if (this.channelNames.Count < 1024)
        {
            this.channelNames.TryAdd(channelUtf8.ToArray(), created);
        }

        return created;
    }

    private sealed record ChannelNames(string Destination, string SendSpanName, string RequestSpanName);

    private void RecordProcessed(string destination)
    {
        AsyncApiTelemetry.MessagesConsumed.Add(
            1,
            new TagList
            {
                { "messaging.system", this.messagingSystem },
                { "messaging.operation.name", "process" },
                { "messaging.destination.name", destination },
            });
    }

    private Func<TPayload, JsonElement, CancellationToken, ValueTask> CreateInstrumentedHandler<TPayload>(
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        string destination)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string processSpanName = "process " + destination;
        return async (payload, headers, ct) =>
        {
            using Activity? activity = StartProcessActivity(in headers, processSpanName, destination);
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await handler(payload, headers, ct).ConfigureAwait(false);
                RecordProcessed(destination);
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

    private Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> CreateInstrumentedContextHandler<TPayload>(
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        string destination)
        where TPayload : struct, IJsonElement<TPayload>
    {
        string processSpanName = "process " + destination;
        return async (payload, context, ct) =>
        {
            JsonElement headers = context.Headers;
            using Activity? activity = StartProcessActivity(in headers, processSpanName, destination);
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await handler(payload, context, ct).ConfigureAwait(false);
                RecordProcessed(destination);
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

    private Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> CreateInstrumentedReplyHandler<TRequest, TReply>(
        Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
        string destination)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string processSpanName = "process " + destination;
        return async (request, headers, ct) =>
        {
            using Activity? activity = StartProcessActivity(in headers, processSpanName, destination);
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                TReply reply = await handler(request, headers, ct).ConfigureAwait(false);
                RecordProcessed(destination);
                return reply;
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

    private Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> CreateInstrumentedReplyContextHandler<TRequest, TReply>(
        Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
        string destination)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        string processSpanName = "process " + destination;
        return async (request, context, ct) =>
        {
            JsonElement headers = context.Headers;
            using Activity? activity = StartProcessActivity(in headers, processSpanName, destination);
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                TReply reply = await handler(request, context, ct).ConfigureAwait(false);
                RecordProcessed(destination);
                return reply;
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

    private ValueTask SubscribeReplyWithDeliveryContextCoreAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        // Only the context-capable nested wrappers call this, and Create constructs them only
        // when the wrapped transport implements the capability, so the cast cannot fail.
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        return ((IMessageDeliveryContextTransport)this.inner).SubscribeReplyWithDeliveryContextAsync(
            channelUtf8,
            CreateInstrumentedReplyContextHandler(handler, destination),
            cancellationToken);
    }

    private ValueTask SubscribeReplyWithDeliveryContextCoreAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
        in MessageContext context,
        CancellationToken cancellationToken)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        // The binding overload forwards to the wrapped transport's own overload so a transport
        // that honors bindings still receives them through the instrumentation.
        string destination = Encoding.UTF8.GetString(channelUtf8.Span);
        return ((IMessageDeliveryContextTransport)this.inner).SubscribeReplyWithDeliveryContextAsync(
            channelUtf8,
            CreateInstrumentedReplyContextHandler(handler, destination),
            in context,
            cancellationToken);
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

    private void RecordException(Activity? activity, Exception ex, string destination, string operationName)
    {
        RecordError(activity, ex);
        AsyncApiTelemetry.MessagesSent.Add(
            1,
            new TagList
            {
                { "messaging.system", this.messagingSystem },
                { "messaging.operation.name", operationName },
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

    /// <summary>
    /// Instrumented wrapper for transports that expose message delivery context.
    /// </summary>
    private sealed class WithDeliveryContext : InstrumentedMessageTransport, IMessageDeliveryContextTransport
    {
        public WithDeliveryContext(IMessageTransport inner, string messagingSystem)
            : base(inner, messagingSystem)
        {
        }

        /// <inheritdoc/>
        public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
            CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.SubscribeWithDeliveryContextCoreAsync(channelUtf8, handler, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
            in MessageContext context,
            CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.SubscribeWithDeliveryContextCoreAsync(channelUtf8, handler, in context, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
            => this.SubscribeReplyWithDeliveryContextCoreAsync(channelUtf8, handler, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
            in MessageContext context,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
            => this.SubscribeReplyWithDeliveryContextCoreAsync(channelUtf8, handler, in context, cancellationToken);
    }

    /// <summary>
    /// Instrumented wrapper for transports that expose broker health checks.
    /// </summary>
    private sealed class WithHealthCheck : InstrumentedMessageTransport, IHealthCheckableTransport
    {
        public WithHealthCheck(IMessageTransport inner, string messagingSystem)
            : base(inner, messagingSystem)
        {
        }

        /// <inheritdoc/>
        public bool IsConnected => ((IHealthCheckableTransport)this.inner).IsConnected;

        /// <inheritdoc/>
        public string MessagingSystem => ((IHealthCheckableTransport)this.inner).MessagingSystem;

        /// <inheritdoc/>
        public ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
            => ((IHealthCheckableTransport)this.inner).PingAsync(cancellationToken);
    }

    /// <summary>
    /// Instrumented wrapper for transports that expose both message delivery context and
    /// broker health checks.
    /// </summary>
    private sealed class WithDeliveryContextAndHealthCheck : InstrumentedMessageTransport, IMessageDeliveryContextTransport, IHealthCheckableTransport
    {
        public WithDeliveryContextAndHealthCheck(IMessageTransport inner, string messagingSystem)
            : base(inner, messagingSystem)
        {
        }

        /// <inheritdoc/>
        public bool IsConnected => ((IHealthCheckableTransport)this.inner).IsConnected;

        /// <inheritdoc/>
        public string MessagingSystem => ((IHealthCheckableTransport)this.inner).MessagingSystem;

        /// <inheritdoc/>
        public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
            CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.SubscribeWithDeliveryContextCoreAsync(channelUtf8, handler, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
            in MessageContext context,
            CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.SubscribeWithDeliveryContextCoreAsync(channelUtf8, handler, in context, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
            => this.SubscribeReplyWithDeliveryContextCoreAsync(channelUtf8, handler, cancellationToken);

        /// <inheritdoc/>
        public ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
            in MessageContext context,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
            => this.SubscribeReplyWithDeliveryContextCoreAsync(channelUtf8, handler, in context, cancellationToken);

        /// <inheritdoc/>
        public ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
            => ((IHealthCheckableTransport)this.inner).PingAsync(cancellationToken);
    }
}

// End of file.