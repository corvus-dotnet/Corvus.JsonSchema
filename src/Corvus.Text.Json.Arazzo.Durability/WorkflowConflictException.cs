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
    /// <param name="runId">The run whose save conflicted.</param>
    /// <param name="expected">The etag the caller expected.</param>
    public WorkflowConflictException(WorkflowRunId runId, WorkflowEtag expected)
        : base($"The checkpoint for workflow run '{runId}' was modified concurrently (expected etag '{expected}'). Reload and retry.")
    {
        this.RunId = runId;
        this.Expected = expected;
    }

    /// <summary>Gets the run whose save conflicted.</summary>
    public WorkflowRunId RunId { get; }

    /// <summary>Gets the etag the caller expected when the conflict occurred.</summary>
    public WorkflowEtag Expected { get; }
}