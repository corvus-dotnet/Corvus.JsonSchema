// <copyright file="WorkflowRunIndexEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The handful of projected fields a store indexes alongside the opaque checkpoint bytes. The runtime
/// projects this entry on every save; a backend never parses the checkpoint — it stores the bytes by id and
/// indexes these fields — which is what keeps each backend a thin adapter.
/// </summary>
/// <remarks>
/// The same projection serves the Tier-2 wait index (find runs that are <em>due</em> or <em>awaiting</em> a
/// correlation) and the control-plane visibility queries (find runs by status / workflow / error), so one
/// index answers timers, message wakeups, and operator queries alike. For Tier 1 (crash recovery) the entry
/// is stored but not queried.
/// </remarks>
/// <param name="WorkflowId">The id of the workflow this run executes.</param>
/// <param name="Status">The run's lifecycle status.</param>
/// <param name="CreatedAt">When the run was first created.</param>
/// <param name="UpdatedAt">When this checkpoint was written.</param>
/// <param name="DueAt">When a suspended run's durable timer fires, if it is waiting on one (Tier 2).</param>
/// <param name="AwaitingChannel">The channel a suspended run is awaiting a message on, if any (Tier 2).</param>
/// <param name="AwaitingCorrelationId">The correlation id a suspended run is awaiting, if any (Tier 2).</param>
/// <param name="ErrorType">The error type of a faulted run, if any.</param>
/// <param name="CorrelationId">The run-wide telemetry correlation id (the W3C trace id) set at creation, if any.</param>
/// <param name="Tags">The free-form tags applied to the run at creation, if any.</param>
/// <param name="SecurityTags">The security tags (KVP labels) applied to the run at creation, if any — the input to tag-based row authorization (§14.2), distinct from the free-form <paramref name="Tags"/>.</param>
/// <param name="Environment">The deployment environment the run is pinned to (design §5.5), if any — the store indexes it so dispatch can constrain a run to runners serving its environment; absent on a run created before run→environment pinning.</param>
/// <param name="ResumeRequestedAt">When a <em>paused</em> (or faulted) run was marked resume-claimable through
/// <see cref="WorkflowRun.RequestResumeAsync"/> by the control plane (design §18), if it has been. A run carrying
/// this marker is surfaced by <see cref="IWorkflowDispatchIndex.QueryClaimableAsync(IReadOnlyCollection{string}, string?, DateTimeOffset, CancellationToken)"/>
/// so a <em>separate</em> runner can claim and advance it, while the run stays <see cref="WorkflowRunStatus.Suspended"/>
/// (never transitioned to <see cref="WorkflowRunStatus.Pending"/>, so it is not mistaken for a fresh un-started run).
/// The marker is cleared on the runner's next checkpoint, so an advancing run is not perpetually re-claimable.</param>
public readonly record struct WorkflowRunIndexEntry(
    string WorkflowId,
    WorkflowRunStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DueAt = null,
    string? AwaitingChannel = null,
    string? AwaitingCorrelationId = null,
    string? ErrorType = null,
    string? CorrelationId = null,
    TagSet Tags = default,
    SecurityTagSet SecurityTags = default,
    string? Environment = null,
    DateTimeOffset? ResumeRequestedAt = null)
{
    /// <summary>
    /// Projects the index entry from a run's constituent state. This is the single home for the wait-kind and
    /// fault mapping (a timer wait yields <see cref="DueAt"/>; a message wait yields <see cref="AwaitingChannel"/>
    /// and <see cref="AwaitingCorrelationId"/>; a fault yields <see cref="ErrorType"/>), so the two producers of
    /// the entry agree by construction: <see cref="WorkflowRun"/> projects it from its live fields on every save,
    /// and the runner's checkpoint surface re-projects it from the opaque bytes a serverless function checks in
    /// (<see cref="WorkflowCheckpointSerializer.ProjectIndex(ReadOnlyMemory{byte})"/>). Every index field is a
    /// top-level checkpoint scalar or tag set, so the re-projection never needs the run's heavy working state
    /// (step outputs, retry counters, journal); keep it that way so the lean re-projection stays complete.
    /// </summary>
    /// <param name="workflowId">The id of the workflow the run executes.</param>
    /// <param name="status">The run's lifecycle status.</param>
    /// <param name="createdAt">When the run was first created.</param>
    /// <param name="updatedAt">When this checkpoint was written.</param>
    /// <param name="wait">The wait the run is suspended on, if any (Tier 2).</param>
    /// <param name="fault">The fault the run faulted with, if any.</param>
    /// <param name="correlationId">The run-wide telemetry correlation id, if any.</param>
    /// <param name="tags">The free-form tags applied at creation, if any.</param>
    /// <param name="securityTags">The security tags applied at creation, if any (§14.2).</param>
    /// <param name="environment">The deployment environment the run is pinned to, if any (§5.5).</param>
    /// <param name="resumeRequestedAt">When the run was marked resume-claimable (§18), if it has been.</param>
    /// <returns>The projected index entry.</returns>
    public static WorkflowRunIndexEntry Project(
        string workflowId,
        WorkflowRunStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        WorkflowWait? wait,
        WorkflowFault? fault,
        string? correlationId,
        TagSet tags,
        SecurityTagSet securityTags,
        string? environment,
        DateTimeOffset? resumeRequestedAt)
        => new(
            workflowId,
            status,
            createdAt,
            updatedAt,
            DueAt: wait is { Kind: WorkflowWaitKind.Timer } timer ? timer.DueAt : null,
            AwaitingChannel: wait is { Kind: WorkflowWaitKind.Message } message ? message.Channel : null,
            AwaitingCorrelationId: wait is { Kind: WorkflowWaitKind.Message } messageCorrelation ? messageCorrelation.CorrelationId : null,
            ErrorType: fault?.Error,
            CorrelationId: correlationId,
            Tags: tags,
            SecurityTags: securityTags,
            Environment: environment,
            ResumeRequestedAt: resumeRequestedAt);
}