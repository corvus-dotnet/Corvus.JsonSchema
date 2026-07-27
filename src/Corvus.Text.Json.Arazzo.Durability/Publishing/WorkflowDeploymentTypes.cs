// <copyright file="WorkflowDeploymentTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>The lifecycle state of a <see cref="WorkflowDeployment"/> (ADR 0055).</summary>
public enum WorkflowDeploymentStatus
{
    /// <summary>Enqueued; awaiting a worker to claim it.</summary>
    Queued,

    /// <summary>Claimed by a worker and deploying.</summary>
    Deploying,

    /// <summary>The deployment completed successfully; the function is available at its invoke URL.</summary>
    Deployed,

    /// <summary>The deployment failed; see the failure reason.</summary>
    Failed,
}

/// <summary>A completion applied to a deployment (a terminal transition from <see cref="WorkflowDeploymentStatus.Deploying"/>).</summary>
/// <param name="Status">The terminal status — <see cref="WorkflowDeploymentStatus.Deployed"/> or <see cref="WorkflowDeploymentStatus.Failed"/>.</param>
/// <param name="FunctionUrl">The deployed function's invoke URL; set for <see cref="WorkflowDeploymentStatus.Deployed"/>, otherwise <see langword="null"/>.</param>
/// <param name="FailureReason">The reason the deployment failed; set for <see cref="WorkflowDeploymentStatus.Failed"/>, otherwise <see langword="null"/>.</param>
public readonly record struct WorkflowDeploymentCompletion(
    WorkflowDeploymentStatus Status,
    string? FunctionUrl = null,
    string? FailureReason = null);

/// <summary>A filter over deployments (all criteria optional; an absent criterion matches anything).</summary>
/// <param name="Status">Only deployments in this state. <see langword="null"/> matches anything.</param>
/// <param name="BaseWorkflowId">Only deployments targeting this base workflow id. <see langword="null"/> matches anything.</param>
/// <param name="VersionNumber">Only deployments targeting this version number. <see langword="null"/> matches anything.</param>
/// <param name="Environment">Only deployments targeting this environment. <see langword="null"/> matches anything.</param>
/// <param name="RuntimeIdentifier">Only deployments targeting this runtime identifier. <see langword="null"/> matches anything.</param>
public readonly record struct WorkflowDeploymentQuery(
    WorkflowDeploymentStatus? Status = null,
    string? BaseWorkflowId = null,
    int? VersionNumber = null,
    string? Environment = null,
    string? RuntimeIdentifier = null)
{
    /// <summary>Whether a row passes this filter. String-free for the string criteria — the candidate strings' bytes are
    /// compared against the row's JSON values (no field is realised from the document per row); the version number compares
    /// as an integer value. The shared client-side membership test for stores that filter in memory (the in-memory store
    /// and the KV/table backends, which scan their keyset index and apply this per row).</summary>
    /// <param name="deployment">The candidate row.</param>
    /// <returns><see langword="true"/> if the row is admitted by the filter.</returns>
    public bool Matches(in WorkflowDeployment deployment)
    {
        if (this.Status is { } status && !deployment.HasStatus(status))
        {
            return false;
        }

        if (this.BaseWorkflowId is { } baseWorkflowId && !deployment.BaseWorkflowIdEquals(baseWorkflowId))
        {
            return false;
        }

        if (this.VersionNumber is { } versionNumber && !deployment.VersionNumberEquals(versionNumber))
        {
            return false;
        }

        if (this.Environment is { } environment && !deployment.EnvironmentEquals(environment))
        {
            return false;
        }

        return this.RuntimeIdentifier is not { } runtimeIdentifier || deployment.RuntimeIdentifierEquals(runtimeIdentifier);
    }
}

/// <summary>The wire (persisted) names for <see cref="WorkflowDeploymentStatus"/>.</summary>
public static class WorkflowDeploymentStatusNames
{
    /// <summary>The persisted name for <see cref="WorkflowDeploymentStatus.Queued"/>.</summary>
    public const string Queued = "Queued";

    /// <summary>The persisted name for <see cref="WorkflowDeploymentStatus.Deploying"/>.</summary>
    public const string Deploying = "Deploying";

    /// <summary>The persisted name for <see cref="WorkflowDeploymentStatus.Deployed"/>.</summary>
    public const string Deployed = "Deployed";

    /// <summary>The persisted name for <see cref="WorkflowDeploymentStatus.Failed"/>.</summary>
    public const string Failed = "Failed";

    /// <summary>Gets the persisted wire name for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(WorkflowDeploymentStatus status) => status switch
    {
        WorkflowDeploymentStatus.Queued => Queued,
        WorkflowDeploymentStatus.Deploying => Deploying,
        WorkflowDeploymentStatus.Deployed => Deployed,
        WorkflowDeploymentStatus.Failed => Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

/// <summary>Thrown when a deployment cannot be acted on as asked — a wrong-state transition (e.g. completing a deployment
/// that is not deploying).</summary>
public sealed class WorkflowDeploymentStateException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowDeploymentStateException"/> class.</summary>
    /// <param name="deploymentId">The deployment id the operation concerned.</param>
    /// <param name="message">The reason the operation is not permitted in the deployment's current state.</param>
    public WorkflowDeploymentStateException(string deploymentId, string message)
        : base(message)
        => this.DeploymentId = deploymentId;

    /// <summary>Gets the deployment id the operation concerned.</summary>
    public string DeploymentId { get; }
}

/// <summary>Thrown when a deployment's expected etag no longer matches (an optimistic-concurrency conflict).</summary>
public sealed class WorkflowDeploymentConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowDeploymentConflictException"/> class.</summary>
    /// <param name="deploymentId">The conflicting deployment id.</param>
    /// <param name="expectedEtag">The caller's expected etag.</param>
    public WorkflowDeploymentConflictException(string deploymentId, WorkflowEtag expectedEtag)
        : base($"The workflow deployment '{deploymentId}' was modified concurrently (expected etag '{expectedEtag.Value}').")
    {
        this.DeploymentId = deploymentId;
        this.ExpectedEtag = expectedEtag;
    }

    /// <summary>Gets the conflicting deployment id.</summary>
    public string DeploymentId { get; }

    /// <summary>Gets the caller's expected etag.</summary>
    public WorkflowEtag ExpectedEtag { get; }
}