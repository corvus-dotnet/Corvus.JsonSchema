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
    /// Gets the histogram measuring message payload size in bytes.
    /// </summary>
    /// <remarks>OTel semantic convention: <c>messaging.message.body.size</c>.</remarks>
    public static Histogram<int> MessageBodySize { get; } =
        Meter.CreateHistogram<int>(
            "messaging.message.body.size",
            "By",
            "Size of message payload in bytes");
}