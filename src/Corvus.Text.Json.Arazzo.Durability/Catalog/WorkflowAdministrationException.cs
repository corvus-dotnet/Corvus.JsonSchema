// <copyright file="WorkflowAdministrationException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when an operation on a base workflow id is refused because the caller is not one of its administrators
/// (design §13/§14.2): a base id's administration is established by its first version's stamped identity (and
/// thereafter by its explicit administrator set), and only an administrator may publish further versions or administer
/// it, so a workflow's identity (<c>sys:workflow</c>) cannot be squatted by another submitter. Maps to HTTP 409 at the
/// control-plane surface.
/// </summary>
public sealed class WorkflowAdministrationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowAdministrationException"/> class.</summary>
    /// <param name="baseWorkflowId">The base workflow id the caller does not administer.</param>
    public WorkflowAdministrationException(string baseWorkflowId)
        : base($"The workflow id '{baseWorkflowId}' is administered by a different identity; only an administrator may publish further versions or administer it.")
        => this.BaseWorkflowId = baseWorkflowId;

    /// <summary>Gets the base workflow id the caller does not administer.</summary>
    public string BaseWorkflowId { get; }
}