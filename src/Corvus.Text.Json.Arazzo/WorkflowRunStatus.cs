// <copyright file="WorkflowRunStatus.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The lifecycle status of a workflow run (see <c>docs/ArazzoWorkflowEnginePlan.md</c> §10).
/// </summary>
public enum WorkflowRunStatus
{
    /// <summary>Created but not yet started.</summary>
    Pending,

    /// <summary>Currently executing.</summary>
    Running,

    /// <summary>Awaiting an external event (a durable timer or a correlated message).</summary>
    Suspended,

    /// <summary>Finished successfully.</summary>
    Completed,

    /// <summary>Terminated by operator request.</summary>
    Cancelled,

    /// <summary>Terminal-but-recoverable failure awaiting remediation.</summary>
    Faulted,
}