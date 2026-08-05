// <copyright file="OpenApiTelemetry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Provides OpenTelemetry-compliant instrumentation for OpenAPI transports (the HTTP-client analogue of
/// <c>Corvus.Text.Json.AsyncApi.AsyncApiTelemetry</c>).
/// </summary>
/// <remarks>
/// <para>
/// Register with the OpenTelemetry pipeline using:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(b =&gt; b.AddSource(OpenApiTelemetry.ActivitySourceName))
///     .WithMetrics(b =&gt; b.AddMeter(OpenApiTelemetry.MeterName));
/// </code>
/// </para>
/// <para>
/// All instruments are zero-cost when no listener is attached: <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns <see langword="null"/> without a listener, and <see cref="Histogram{T}.Record(T)"/> is a no-op
/// without a <see cref="MeterListener"/>.
/// </para>
/// </remarks>
public static class OpenApiTelemetry
{
    /// <summary>
    /// The <see cref="ActivitySource"/> name. Use with <c>AddSource("Corvus.OpenApi")</c>.
    /// </summary>
    public const string ActivitySourceName = "Corvus.OpenApi";

    /// <summary>
    /// The <see cref="Meter"/> name. Use with <c>AddMeter("Corvus.OpenApi")</c>.
    /// </summary>
    public const string MeterName = "Corvus.OpenApi";

    private static readonly string Version =
        typeof(OpenApiTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> for distributed tracing of outbound API operations.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, Version);

    /// <summary>
    /// Gets the <see cref="Meter"/> for API-client metrics.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName, Version);

    /// <summary>
    /// Gets the histogram measuring the duration of outbound HTTP client requests.
    /// </summary>
    /// <remarks>OTel semantic convention: <c>http.client.request.duration</c>. Unit: seconds.</remarks>
    public static Histogram<double> RequestDuration { get; } =
        Meter.CreateHistogram<double>(
            "http.client.request.duration",
            "s",
            "Duration of outbound HTTP client requests");

    /// <summary>
    /// Gets the counter for outbound HTTP client requests.
    /// </summary>
    public static Counter<long> Requests { get; } =
        Meter.CreateCounter<long>(
            "http.client.requests",
            "{request}",
            "Number of outbound HTTP client requests");
}