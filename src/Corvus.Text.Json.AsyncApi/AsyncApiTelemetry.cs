// <copyright file="AsyncApiTelemetry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Provides OpenTelemetry-compliant instrumentation for AsyncAPI transports.
/// </summary>
/// <remarks>
/// <para>
/// Register with the OpenTelemetry pipeline using:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(b =&gt; b.AddSource(AsyncApiTelemetry.ActivitySourceName))
///     .WithMetrics(b =&gt; b.AddMeter(AsyncApiTelemetry.MeterName));
/// </code>
/// </para>
/// <para>
/// All instruments are zero-cost when no listener is attached: <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns <see langword="null"/> without a listener, and <see cref="Counter{T}.Add(T)"/> is a no-op
/// without a <see cref="MeterListener"/>.
/// </para>
/// </remarks>
public static class AsyncApiTelemetry
{
    /// <summary>
    /// The <see cref="ActivitySource"/> name. Use with <c>AddSource("Corvus.AsyncApi")</c>.
    /// </summary>
    public const string ActivitySourceName = "Corvus.AsyncApi";

    /// <summary>
    /// The <see cref="Meter"/> name. Use with <c>AddMeter("Corvus.AsyncApi")</c>.
    /// </summary>
    public const string MeterName = "Corvus.AsyncApi";

    private static readonly string Version =
        typeof(AsyncApiTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> for distributed tracing of messaging operations.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, Version);

    /// <summary>
    /// Gets the <see cref="Meter"/> for messaging metrics.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName, Version);

    /// <summary>
    /// Gets the counter for messages sent to the broker.
    /// </summary>
    /// <remarks>OTel semantic convention: <c>messaging.client.sent.messages</c>.</remarks>
    public static Counter<long> MessagesSent { get; } =
        Meter.CreateCounter<long>(
            "messaging.client.sent.messages",
            "{message}",
            "Number of messages sent to broker");

    /// <summary>
    /// Gets the counter for messages delivered to the application handler.
    /// </summary>
    /// <remarks>OTel semantic convention: <c>messaging.client.consumed.messages</c>.</remarks>
    public static Counter<long> MessagesConsumed { get; } =
        Meter.CreateCounter<long>(
            "messaging.client.consumed.messages",
            "{message}",
            "Number of messages delivered to application handler");

    /// <summary>
    /// Gets the histogram measuring the duration of messaging client operations (publish, request).
    /// </summary>
    /// <remarks>OTel semantic convention: <c>messaging.client.operation.duration</c>. Unit: seconds.</remarks>
    public static Histogram<double> OperationDuration { get; } =
        Meter.CreateHistogram<double>(
            "messaging.client.operation.duration",
            "s",
            "Duration of messaging client operations (publish, request)");

    /// <summary>
    /// Gets the histogram measuring consumer handler processing time.
    /// </summary>
    /// <remarks>OTel semantic convention: <c>messaging.process.duration</c>. Unit: seconds.</remarks>
    public static Histogram<double> ProcessDuration { get; } =
        Meter.CreateHistogram<double>(
            "messaging.process.duration",
            "s",
            "Duration of consumer message processing (handler execution)");

    /// <summary>
    /// Gets the counter for messages moved to a dead-letter channel.
    /// </summary>
    public static Counter<long> DeadLetters { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.dead_letters",
            "{message}",
            "Messages moved to dead-letter channel");

    /// <summary>
    /// Gets the counter for dead-letter operations that themselves failed.
    /// </summary>
    /// <remarks>
    /// This indicates a message could not be processed AND could not be dead-lettered,
    /// meaning it was effectively dropped. This is a critical operational alert.
    /// </remarks>
    public static Counter<long> DeadLetterFailures { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.dead_letter_failures",
            "{message}",
            "Dead-letter operations that failed (message dropped)");

    /// <summary>
    /// Gets the counter for messages that failed schema validation.
    /// </summary>
    public static Counter<long> ValidationFailures { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.validation_failures",
            "{message}",
            "Messages that failed schema validation");

    /// <summary>
    /// Gets the counter for message processing retry attempts.
    /// </summary>
    public static Counter<long> RetryAttempts { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.retries",
            "{retry}",
            "Message processing retry attempts (via middleware or error policy)");

    /// <summary>
    /// Gets the counter for subscriber abort events.
    /// </summary>
    /// <remarks>
    /// This indicates a subscriber has stopped consuming messages due to the error policy
    /// returning <see cref="MessageErrorAction.Abort"/>. This is a critical operational alert
    /// that means no further messages will be processed on this channel until re-subscription.
    /// </remarks>
    public static Counter<long> SubscriberAborts { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.subscriber_aborts",
            "{event}",
            "Subscriber stopped due to error policy abort decision");

    /// <summary>
    /// Gets the counter for messages that were skipped (not processed, not dead-lettered).
    /// </summary>
    public static Counter<long> MessagesSkipped { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.messages_skipped",
            "{message}",
            "Messages skipped due to error policy decision");

    /// <summary>
    /// Gets the histogram measuring message payload size in bytes.
    /// </summary>
    /// <remarks>OTel semantic convention: <c>messaging.message.body.size</c>.</remarks>
    public static Histogram<int> MessageBodySize { get; } =
        Meter.CreateHistogram<int>(
            "messaging.message.body.size",
            "By",
            "Size of message payload in bytes");

    /// <summary>
    /// Gets the counter for heartbeat ping attempts (success and failure).
    /// </summary>
    public static Counter<long> Heartbeats { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.heartbeats",
            "{heartbeat}",
            "Transport heartbeat ping attempts");

    /// <summary>
    /// Gets the counter for transport state transitions (connected↔disconnected).
    /// </summary>
    public static Counter<long> TransportStateTransitions { get; } =
        Meter.CreateCounter<long>(
            "corvus.asyncapi.transport_state_transitions",
            "{event}",
            "Transport connectivity state transitions");

    /// <summary>
    /// Records a successful dead-letter operation from within a transport's internal error handling.
    /// </summary>
    /// <param name="destination">The dead-letter channel name.</param>
    /// <param name="originalChannel">The original channel the message was received on.</param>
    /// <param name="messagingSystem">The messaging system identifier.</param>
    public static void RecordDeadLetter(string destination, string originalChannel, string messagingSystem)
    {
        DeadLetters.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", destination },
                { "corvus.asyncapi.original_channel", originalChannel },
            });
    }

    /// <summary>
    /// Records a dead-letter operation that itself failed (message was dropped).
    /// </summary>
    /// <param name="destination">The dead-letter channel name.</param>
    /// <param name="originalChannel">The original channel the message was received on.</param>
    /// <param name="messagingSystem">The messaging system identifier.</param>
    /// <param name="exception">The exception that caused the dead-letter operation to fail.</param>
    public static void RecordDeadLetterFailure(
        string destination,
        string originalChannel,
        string messagingSystem,
        Exception exception)
    {
        DeadLetterFailures.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", destination },
                { "corvus.asyncapi.original_channel", originalChannel },
                { "error.type", exception.GetType().FullName },
            });
    }

    /// <summary>
    /// Records that a subscriber aborted (stopped consuming) due to the error policy.
    /// </summary>
    /// <param name="channel">The channel the subscriber was consuming from.</param>
    /// <param name="messagingSystem">The messaging system identifier.</param>
    /// <param name="errorKind">The kind of error that triggered the abort.</param>
    public static void RecordAbort(string channel, string messagingSystem, MessageErrorKind errorKind)
    {
        SubscriberAborts.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", channel },
                { "corvus.asyncapi.error_kind", errorKind.ToString() },
            });
    }

    /// <summary>
    /// Records that a message was skipped (not processed and not dead-lettered).
    /// </summary>
    /// <param name="channel">The channel the message was received on.</param>
    /// <param name="messagingSystem">The messaging system identifier.</param>
    /// <param name="errorKind">The kind of error that caused the skip.</param>
    public static void RecordSkip(string channel, string messagingSystem, MessageErrorKind errorKind)
    {
        MessagesSkipped.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", channel },
                { "corvus.asyncapi.error_kind", errorKind.ToString() },
            });
    }

    /// <summary>
    /// Records a retry attempt for message processing.
    /// </summary>
    /// <param name="channel">The channel the message was received on.</param>
    /// <param name="messagingSystem">The messaging system identifier.</param>
    /// <param name="attemptNumber">The retry attempt number (1 for first retry).</param>
    public static void RecordRetry(string channel, string messagingSystem, int attemptNumber)
    {
        RetryAttempts.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", channel },
                { "corvus.asyncapi.retry_attempt", attemptNumber },
            });
    }
}