// <copyright file="WorkflowOwnershipException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when adding a catalog version is refused because the base workflow id is already owned by a different
/// security identity (design §13/§14.2): a base id's ownership is established by its first version, and only that owner
/// may publish further versions, so a workflow's identity (<c>sys:workflow</c>) cannot be squatted by another submitter.
/// Maps to HTTP 409 at the control-plane surface.
/// </summary>
public sealed class WorkflowOwnershipException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowOwnershipException"/> class.</summary>
    /// <param name="baseWorkflowId">The base workflow id whose ownership the submitter does not hold.</param>
    public WorkflowOwnershipException(string baseWorkflowId)
        : base($"The workflow id '{baseWorkflowId}' is already owned by a different identity; only its owner may publish further versions.")
        => this.BaseWorkflowId = baseWorkflowId;

    /// <summary>Gets the base workflow id whose ownership the submitter does not hold.</summary>
    public string BaseWorkflowId { get; }
}