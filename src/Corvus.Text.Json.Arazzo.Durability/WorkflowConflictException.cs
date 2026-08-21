// <copyright file="WorkflowConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when a checkpoint save fails its optimistic-concurrency check: the run's stored etag no longer
/// matches the etag the caller expected, because another worker advanced (or deleted) the run in the
/// meantime. The caller should reload the checkpoint and decide whether to retry.
/// </summary>
public sealed class WorkflowConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowConflictException"/> class.</summary>
    /// <param name="address">The run whose save conflicted, by its full address (ADR 0065 decision 9).</param>
    /// <param name="expected">The etag the caller expected.</param>
    public WorkflowConflictException(WorkflowRunAddress address, WorkflowEtag expected)
        : base($"The checkpoint for workflow run '{address}' was modified concurrently (expected etag '{expected}'). Reload and retry.")
    {
        this.Address = address;
        this.Expected = expected;
    }

    /// <summary>Gets the run whose save conflicted, by its full <c>(environment, runId)</c> address.</summary>
    public WorkflowRunAddress Address { get; }

    /// <summary>Gets the run id half of <see cref="Address"/>.</summary>
    public WorkflowRunId RunId => this.Address.RunId;

    /// <summary>Gets the etag the caller expected when the conflict occurred.</summary>
    public WorkflowEtag Expected { get; }
}