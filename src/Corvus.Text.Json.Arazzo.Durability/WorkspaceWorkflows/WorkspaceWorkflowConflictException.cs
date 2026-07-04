// <copyright file="WorkspaceWorkflowConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

/// <summary>
/// Thrown when an optimistic-concurrency update/delete against an <see cref="IWorkspaceWorkflowStore"/> fails because the caller's
/// expected <see cref="WorkflowEtag"/> no longer matches the stored working copy — a collaborator saved first; the caller must reload and reconcile. Maps to
/// HTTP 409 at the control-plane surface.
/// </summary>
public sealed class WorkspaceWorkflowConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkspaceWorkflowConflictException"/> class.</summary>
    /// <param name="id">The working-copy id.</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public WorkspaceWorkflowConflictException(string id, WorkflowEtag expected)
        : base($"Concurrency conflict saving working copy '{id}': expected etag {expected} no longer matches.")
    {
        this.Id = id;
        this.Expected = expected;
    }

    /// <summary>Gets the working-copy id.</summary>
    public string Id { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}