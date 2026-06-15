// <copyright file="WorkflowAdministrationConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Thrown when an optimistic-concurrency change to a workflow's administration record (design §13/§14.2) fails because the
/// caller's expected <see cref="WorkflowEtag"/> no longer matches the stored state — a concurrent administrator change won the
/// race, so the caller must reload and retry. Maps to HTTP 409 at the control-plane surface.
/// </summary>
public sealed class WorkflowAdministrationConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowAdministrationConflictException"/> class.</summary>
    /// <param name="baseWorkflowId">The base workflow id whose administration change conflicted.</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public WorkflowAdministrationConflictException(string baseWorkflowId, WorkflowEtag expected)
        : base($"Concurrency conflict changing administration of workflow '{baseWorkflowId}': expected etag {expected} no longer matches.")
    {
        this.BaseWorkflowId = baseWorkflowId;
        this.Expected = expected;
    }

    /// <summary>Gets the base workflow id whose administration change conflicted.</summary>
    public string BaseWorkflowId { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}