// <copyright file="ExecutorNotAdmittedException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A run's executor was loaded and verified against the platform's signature, and the runner's own executor policy
/// for the run's environment did not admit it (ADR 0065 phase C). Not an executor fault: the run is not ended, it is
/// handed back, and it advances once the tenant operator countersigns the version for the environment.
/// </summary>
public sealed class ExecutorNotAdmittedException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="ExecutorNotAdmittedException"/> class.</summary>
    /// <param name="environment">The environment the run belongs to.</param>
    /// <param name="workflowId">The versioned workflow id whose executor was refused.</param>
    /// <param name="admission">Why the executor was not admitted.</param>
    /// <param name="message">The message.</param>
    public ExecutorNotAdmittedException(string environment, string workflowId, ExecutorAdmission admission, string message)
        : base(message)
    {
        this.Environment = environment;
        this.WorkflowId = workflowId;
        this.Admission = admission;
    }

    /// <summary>Gets the environment the run belongs to.</summary>
    public string Environment { get; }

    /// <summary>Gets the versioned workflow id whose executor was refused.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets why the executor was not admitted.</summary>
    public ExecutorAdmission Admission { get; }
}