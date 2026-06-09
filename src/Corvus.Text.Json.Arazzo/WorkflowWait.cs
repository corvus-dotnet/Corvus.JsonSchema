// <copyright file="WorkflowWait.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>The kind of external event a suspended workflow run is waiting for (plan §9.4).</summary>
public enum WorkflowWaitKind
{
    /// <summary>Waiting for a point in time — a durable timer (e.g. a <c>retryAfter</c> delay).</summary>
    Timer,

    /// <summary>Waiting for a correlated message on a channel — an AsyncAPI receive.</summary>
    Message,
}

/// <summary>
/// Why a durable workflow run is <see cref="WorkflowRunStatus.Suspended"/>: the external event that, once it
/// occurs, lets a worker resume the run. A <see cref="WorkflowWaitKind.Timer"/> wait carries the
/// <see cref="DueAt"/> instant; a <see cref="WorkflowWaitKind.Message"/> wait carries the
/// <see cref="Channel"/> and optional <see cref="CorrelationId"/> the receive is awaiting.
/// </summary>
/// <param name="Kind">The kind of event awaited.</param>
/// <param name="DueAt">For a timer wait, the instant the run becomes due to resume.</param>
/// <param name="Channel">For a message wait, the channel the run is awaiting a message on.</param>
/// <param name="CorrelationId">For a message wait, the correlation id the run is awaiting, if any.</param>
public readonly record struct WorkflowWait(
    WorkflowWaitKind Kind,
    DateTimeOffset DueAt,
    string? Channel,
    string? CorrelationId)
{
    /// <summary>Creates a timer wait due at the given instant.</summary>
    /// <param name="dueAt">When the run becomes due to resume.</param>
    /// <returns>The wait.</returns>
    public static WorkflowWait Timer(DateTimeOffset dueAt) => new(WorkflowWaitKind.Timer, dueAt, null, null);

    /// <summary>Creates a message wait on a channel (optionally for a specific correlation id).</summary>
    /// <param name="channel">The channel the run is awaiting a message on.</param>
    /// <param name="correlationId">The correlation id the run is awaiting, if any.</param>
    /// <returns>The wait.</returns>
    public static WorkflowWait Message(string channel, string? correlationId)
        => new(WorkflowWaitKind.Message, default, channel, correlationId);
}