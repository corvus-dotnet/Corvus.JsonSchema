// <copyright file="ArazzoTelemetry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// OpenTelemetry-compliant instrumentation for Arazzo workflow execution.
/// </summary>
/// <remarks>
/// <para>
/// Register with the OpenTelemetry pipeline using:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(b =&gt; b.AddSource(ArazzoTelemetry.ActivitySourceName))
///     .WithMetrics(b =&gt; b.AddMeter(ArazzoTelemetry.MeterName));
/// </code>
/// </para>
/// <para>
/// All instruments are zero-cost when no listener is attached: <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns <see langword="null"/> without a listener, and counter/histogram operations are no-ops
/// without a <see cref="MeterListener"/>. Built on <see cref="System.Diagnostics"/> only — no
/// dependency on any OpenTelemetry package.
/// </para>
/// </remarks>
public static class ArazzoTelemetry
{
    /// <summary>
    /// The <see cref="ActivitySource"/> name. Use with <c>AddSource("Corvus.Arazzo")</c>.
    /// </summary>
    public const string ActivitySourceName = "Corvus.Arazzo";

    /// <summary>
    /// The <see cref="Meter"/> name. Use with <c>AddMeter("Corvus.Arazzo")</c>.
    /// </summary>
    public const string MeterName = "Corvus.Arazzo";

    private static readonly string Version =
        typeof(ArazzoTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> for distributed tracing of workflow execution.
    /// </summary>
    /// <remarks>
    /// The engine emits a workflow span (root) with a child span per step; sub-workflow steps nest
    /// their own workflow span, and operation/message requests nest beneath the step.
    /// </remarks>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, Version);

    /// <summary>
    /// Gets the <see cref="Meter"/> for workflow execution metrics.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName, Version);

    /// <summary>
    /// Gets the counter for workflows started.
    /// </summary>
    public static Counter<long> WorkflowsStarted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.started", "{workflow}", "Workflows started");

    /// <summary>
    /// Gets the counter for workflows that completed successfully.
    /// </summary>
    public static Counter<long> WorkflowsCompleted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.completed", "{workflow}", "Workflows completed successfully");

    /// <summary>
    /// Gets the counter for workflows that faulted (terminal-but-recoverable failure).
    /// </summary>
    public static Counter<long> WorkflowsFaulted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.faulted", "{workflow}", "Workflows that faulted");

    /// <summary>
    /// Gets the counter for steps executed.
    /// </summary>
    public static Counter<long> StepsExecuted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.steps.executed", "{step}", "Steps executed");

    /// <summary>
    /// Gets the counter for step retry attempts.
    /// </summary>
    public static Counter<long> StepRetries { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.steps.retries", "{retry}", "Step retry attempts");

    /// <summary>
    /// Gets the counter for control-flow transfers (<c>goto</c> success/failure actions).
    /// </summary>
    public static Counter<long> Gotos { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.gotos", "{goto}", "Control-flow goto transfers");

    /// <summary>
    /// Gets the histogram measuring whole-workflow execution duration, in seconds.
    /// </summary>
    public static Histogram<double> WorkflowDuration { get; } =
        Meter.CreateHistogram<double>("corvus.arazzo.workflow.duration", "s", "Duration of workflow execution");

    /// <summary>
    /// Gets the histogram measuring per-step execution duration, in seconds.
    /// </summary>
    public static Histogram<double> StepDuration { get; } =
        Meter.CreateHistogram<double>("corvus.arazzo.step.duration", "s", "Duration of step execution");
}