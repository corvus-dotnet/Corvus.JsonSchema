// <copyright file="BudgetLimits.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>
/// The six execution-budget limits (ADR 0068) as the CLI carries them: any of them may be unnamed. It is how an
/// override's limits outlive the response they were read from, and how the limits a command names are laid over them.
/// </summary>
/// <param name="MaxSteps">Fuel.</param>
/// <param name="WallClockSeconds">The wall clock, in seconds.</param>
/// <param name="MaxSubWorkflowDepth">The sub-workflow depth cap.</param>
/// <param name="RetryAfterCeilingSeconds">The retry-after ceiling, in seconds.</param>
/// <param name="StepTimeoutSeconds">The step timeout, in seconds.</param>
/// <param name="MaxResponseBytes">The largest response a step may read, in bytes.</param>
internal readonly record struct BudgetLimits(long? MaxSteps, long? WallClockSeconds, long? MaxSubWorkflowDepth, long? RetryAfterCeilingSeconds, long? StepTimeoutSeconds, long? MaxResponseBytes)
{
    /// <summary>Gets the limits in the order they are shown, each with its label and how it is written.</summary>
    public static IReadOnlyList<Row> Rows { get; } =
    [
        new("Max steps", static l => l.MaxSteps, static v => v.ToString("N0", CultureInfo.InvariantCulture)),
        new("Wall clock", static l => l.WallClockSeconds, FormatSeconds),
        new("Sub-workflow depth", static l => l.MaxSubWorkflowDepth, static v => v.ToString(CultureInfo.InvariantCulture)),
        new("Retry-after ceiling", static l => l.RetryAfterCeilingSeconds, FormatSeconds),
        new("Step timeout", static l => l.StepTimeoutSeconds, FormatSeconds),
        new("Max response", static l => l.MaxResponseBytes, FormatBytes),
    ];

    /// <summary>Gets a value indicating whether no limit is named.</summary>
    public bool IsEmpty => this == default;

    /// <summary>Reads an authored override, whose limits are each optional.</summary>
    /// <param name="budget">The override, or undefined.</param>
    /// <returns>The limits it names.</returns>
    public static BudgetLimits From(in Models.ExecutionBudget budget)
        => budget.IsUndefined()
            ? default
            : new(
                budget.MaxSteps.IsNotUndefined() ? (long)budget.MaxSteps : null,
                budget.WallClockSeconds.IsNotUndefined() ? (long)budget.WallClockSeconds : null,
                budget.MaxSubWorkflowDepth.IsNotUndefined() ? (long)budget.MaxSubWorkflowDepth : null,
                budget.RetryAfterCeilingSeconds.IsNotUndefined() ? (long)budget.RetryAfterCeilingSeconds : null,
                budget.StepTimeoutSeconds.IsNotUndefined() ? (long)budget.StepTimeoutSeconds : null,
                budget.MaxResponseBytes.IsNotUndefined() ? (long)budget.MaxResponseBytes : null);

    /// <summary>Reads a resolved budget, which names every limit.</summary>
    /// <param name="budget">The resolved budget, or undefined.</param>
    /// <returns>Its limits.</returns>
    public static BudgetLimits From(in Models.ResolvedExecutionBudget budget)
        => budget.IsUndefined()
            ? default
            : new((long)budget.MaxSteps, (long)budget.WallClockSeconds, (long)budget.MaxSubWorkflowDepth, (long)budget.RetryAfterCeilingSeconds, (long)budget.StepTimeoutSeconds, (long)budget.MaxResponseBytes);

    /// <summary>Lays these limits over others: a limit named here wins, and one left out is the other's.</summary>
    /// <param name="under">The limits underneath.</param>
    /// <returns>The combined limits.</returns>
    public BudgetLimits Over(in BudgetLimits under)
        => new(
            this.MaxSteps ?? under.MaxSteps,
            this.WallClockSeconds ?? under.WallClockSeconds,
            this.MaxSubWorkflowDepth ?? under.MaxSubWorkflowDepth,
            this.RetryAfterCeilingSeconds ?? under.RetryAfterCeilingSeconds,
            this.StepTimeoutSeconds ?? under.StepTimeoutSeconds,
            this.MaxResponseBytes ?? under.MaxResponseBytes);

    /// <summary>Writes the limits as an override for a request body. With no limit named it is an empty override,
    /// which is how the API says an environment has none.</summary>
    /// <returns>The model source.</returns>
    public Models.ExecutionBudget.Source ToSource()
        => Models.ExecutionBudget.Build(
            maxResponseBytes: this.MaxResponseBytes is { } bytes ? (Models.BudgetMaxResponseBytes.Source)bytes : default,
            maxSteps: this.MaxSteps is { } steps ? (Models.BudgetMaxSteps.Source)steps : default,
            maxSubWorkflowDepth: this.MaxSubWorkflowDepth is { } depth ? (Models.BudgetMaxSubWorkflowDepth.Source)depth : default,
            retryAfterCeilingSeconds: this.RetryAfterCeilingSeconds is { } retry ? (Models.BudgetRetryAfterCeilingSeconds.Source)retry : default,
            stepTimeoutSeconds: this.StepTimeoutSeconds is { } timeout ? (Models.BudgetStepTimeoutSeconds.Source)timeout : default,
            wallClockSeconds: this.WallClockSeconds is { } clock ? (Models.BudgetWallClockSeconds.Source)clock : default);

    // The exact number is what an operator types back in, so it is always shown; the readable form follows it.
    private static string FormatSeconds(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        string readable = span.TotalDays >= 1 && span.Hours == 0 && span.Minutes == 0 && span.Seconds == 0 ? $"{(long)span.TotalDays}d"
            : span.TotalHours >= 1 && span.Minutes == 0 && span.Seconds == 0 ? $"{(long)span.TotalHours}h"
            : span.TotalMinutes >= 1 && span.Seconds == 0 ? $"{(long)span.TotalMinutes}m"
            : string.Empty;
        return readable.Length == 0 ? $"{seconds.ToString(CultureInfo.InvariantCulture)}s" : $"{seconds.ToString(CultureInfo.InvariantCulture)}s ({readable})";
    }

    private static string FormatBytes(long bytes)
    {
        const long Mebibyte = 1024 * 1024;
        string exact = bytes.ToString("N0", CultureInfo.InvariantCulture);
        return bytes >= Mebibyte && bytes % Mebibyte == 0 ? $"{exact} ({(bytes / Mebibyte).ToString(CultureInfo.InvariantCulture)} MiB)"
            : bytes >= 1024 && bytes % 1024 == 0 ? $"{exact} ({(bytes / 1024).ToString(CultureInfo.InvariantCulture)} KiB)"
            : exact;
    }

    /// <summary>One limit, as a row of a budget table.</summary>
    /// <param name="Label">The limit's label.</param>
    /// <param name="Select">Picks the limit out of a set.</param>
    /// <param name="Write">Writes a value of the limit.</param>
    internal sealed record Row(string Label, Func<BudgetLimits, long?> Select, Func<long, string> Write)
    {
        /// <summary>Writes this limit's value in a set, or a dash where the set does not name it.</summary>
        /// <param name="limits">The set.</param>
        /// <returns>The cell text.</returns>
        public string Format(in BudgetLimits limits) => this.Select(limits) is { } value ? this.Write(value) : "—";
    }
}