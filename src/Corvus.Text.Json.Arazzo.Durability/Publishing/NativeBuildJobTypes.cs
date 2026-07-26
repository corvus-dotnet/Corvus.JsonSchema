// <copyright file="NativeBuildJobTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>The lifecycle state of a <see cref="NativeBuildJob"/> (ADR 0055).</summary>
public enum NativeBuildJobStatus
{
    /// <summary>Enqueued; awaiting a worker to claim it.</summary>
    Queued,

    /// <summary>Claimed by a worker and building.</summary>
    Building,

    /// <summary>The build completed successfully; the serverless binary is available.</summary>
    Ready,

    /// <summary>The build failed; see the failure reason.</summary>
    Failed,
}

/// <summary>A completion applied to a build job (a terminal transition from <see cref="NativeBuildJobStatus.Building"/>).</summary>
/// <param name="Status">The terminal status — <see cref="NativeBuildJobStatus.Ready"/> or <see cref="NativeBuildJobStatus.Failed"/>.</param>
/// <param name="FailureReason">The reason the build failed; set for <see cref="NativeBuildJobStatus.Failed"/>, otherwise <see langword="null"/>.</param>
public readonly record struct NativeBuildJobCompletion(
    NativeBuildJobStatus Status,
    string? FailureReason = null);

/// <summary>A filter over build jobs (all criteria optional; an absent criterion matches anything).</summary>
/// <param name="Status">Only jobs in this state. <see langword="null"/> matches anything.</param>
/// <param name="BaseWorkflowId">Only jobs targeting this base workflow id. <see langword="null"/> matches anything.</param>
/// <param name="VersionNumber">Only jobs targeting this version number. <see langword="null"/> matches anything.</param>
/// <param name="Environment">Only jobs targeting this environment. <see langword="null"/> matches anything.</param>
/// <param name="RuntimeIdentifier">Only jobs targeting this runtime identifier. <see langword="null"/> matches anything.</param>
public readonly record struct NativeBuildJobQuery(
    NativeBuildJobStatus? Status = null,
    string? BaseWorkflowId = null,
    int? VersionNumber = null,
    string? Environment = null,
    string? RuntimeIdentifier = null)
{
    /// <summary>Whether a row passes this filter. String-free for the string criteria — the candidate strings' bytes are
    /// compared against the row's JSON values (no field is realised from the document per row); the version number compares
    /// as an integer value. The shared client-side membership test for stores that filter in memory (the in-memory store
    /// and the KV/table backends, which scan their keyset index and apply this per row).</summary>
    /// <param name="job">The candidate row.</param>
    /// <returns><see langword="true"/> if the row is admitted by the filter.</returns>
    public bool Matches(in NativeBuildJob job)
    {
        if (this.Status is { } status && !job.HasStatus(status))
        {
            return false;
        }

        if (this.BaseWorkflowId is { } baseWorkflowId && !job.BaseWorkflowIdEquals(baseWorkflowId))
        {
            return false;
        }

        if (this.VersionNumber is { } versionNumber && !job.VersionNumberEquals(versionNumber))
        {
            return false;
        }

        if (this.Environment is { } environment && !job.EnvironmentEquals(environment))
        {
            return false;
        }

        return this.RuntimeIdentifier is not { } runtimeIdentifier || job.RuntimeIdentifierEquals(runtimeIdentifier);
    }
}

/// <summary>The wire (persisted) names for <see cref="NativeBuildJobStatus"/>.</summary>
public static class NativeBuildJobStatusNames
{
    /// <summary>The persisted name for <see cref="NativeBuildJobStatus.Queued"/>.</summary>
    public const string Queued = "Queued";

    /// <summary>The persisted name for <see cref="NativeBuildJobStatus.Building"/>.</summary>
    public const string Building = "Building";

    /// <summary>The persisted name for <see cref="NativeBuildJobStatus.Ready"/>.</summary>
    public const string Ready = "Ready";

    /// <summary>The persisted name for <see cref="NativeBuildJobStatus.Failed"/>.</summary>
    public const string Failed = "Failed";

    /// <summary>Gets the persisted wire name for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(NativeBuildJobStatus status) => status switch
    {
        NativeBuildJobStatus.Queued => Queued,
        NativeBuildJobStatus.Building => Building,
        NativeBuildJobStatus.Ready => Ready,
        NativeBuildJobStatus.Failed => Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

/// <summary>Thrown when a build job cannot be acted on as asked — a wrong-state transition (e.g. completing a job that is
/// not building).</summary>
public sealed class NativeBuildJobStateException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NativeBuildJobStateException"/> class.</summary>
    /// <param name="jobId">The job id the operation concerned.</param>
    /// <param name="message">The reason the operation is not permitted in the job's current state.</param>
    public NativeBuildJobStateException(string jobId, string message)
        : base(message)
        => this.JobId = jobId;

    /// <summary>Gets the job id the operation concerned.</summary>
    public string JobId { get; }
}

/// <summary>Thrown when a build job's expected etag no longer matches (an optimistic-concurrency conflict).</summary>
public sealed class NativeBuildJobConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NativeBuildJobConflictException"/> class.</summary>
    /// <param name="jobId">The conflicting job id.</param>
    /// <param name="expectedEtag">The caller's expected etag.</param>
    public NativeBuildJobConflictException(string jobId, WorkflowEtag expectedEtag)
        : base($"The native build job '{jobId}' was modified concurrently (expected etag '{expectedEtag.Value}').")
    {
        this.JobId = jobId;
        this.ExpectedEtag = expectedEtag;
    }

    /// <summary>Gets the conflicting job id.</summary>
    public string JobId { get; }

    /// <summary>Gets the caller's expected etag.</summary>
    public WorkflowEtag ExpectedEtag { get; }
}