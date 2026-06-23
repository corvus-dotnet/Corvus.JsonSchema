// <copyright file="ISecuredWorkflowManagement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The control plane over a durability store (plan §11): see and act on runs — list/inspect them, resume a
/// faulted run, cancel a run, delete a single run, and reap terminal ones. It is the workflow-level analogue
/// of dead-letter queue inspection and redelivery; all mutations take a single-owner lease and use the store's
/// optimistic concurrency so concurrent operators (or an operator and a worker) cannot conflict.
/// </summary>
public interface ISecuredWorkflowManagement
{
    /// <summary>
    /// Starts a new run of a workflow: creates a fresh <see cref="WorkflowRunStatus.Pending"/> run with the
    /// supplied inputs and enqueues it (the store is the queue) for a hosting runner to claim and execute. The
    /// run executes asynchronously and durably; observe it via <see cref="GetAsync"/>/<see cref="ListAsync"/>.
    /// </summary>
    /// <param name="workflowId">The versioned workflow id (<c>{base}-v{n}</c>) to run.</param>
    /// <param name="inputs">The workflow inputs.</param>
    /// <param name="correlationId">An optional telemetry correlation id (a W3C trace id); a new one is captured when omitted.</param>
    /// <param name="tags">Optional free-form tags to attach to the run.</param>
    /// <param name="securityTags">Optional security tags (KVP labels) for row authorization (§14.2), typically inherited from the workflow version; distinct from <paramref name="tags"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The id of the newly created pending run.</returns>
    ValueTask<WorkflowRunId> StartAsync(string workflowId, JsonElement inputs, string? correlationId, TagSet tags, SecurityTagSet securityTags, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a run idempotently: the run id is derived deterministically from
    /// (<paramref name="workflowId"/>, <paramref name="idempotencyKey"/>), so re-invoking with the same key
    /// (for example on broker message redelivery, or a duplicate schedule fire) is a no-op that returns the
    /// existing run rather than creating a duplicate.
    /// </summary>
    /// <param name="workflowId">The versioned workflow id (<c>{base}-v{n}</c>) to run.</param>
    /// <param name="inputs">The workflow inputs (used only when the run is first created).</param>
    /// <param name="idempotencyKey">A stable key identifying the logical start (e.g. a message id or a scheduled-slot timestamp).</param>
    /// <param name="correlationId">An optional telemetry correlation id; a new one is captured when omitted.</param>
    /// <param name="tags">Optional free-form tags to attach to the run.</param>
    /// <param name="securityTags">Optional security tags (KVP labels) for row authorization (§14.2), typically inherited from the workflow version; distinct from <paramref name="tags"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The id of the run for this key (newly created, or the pre-existing one).</returns>
    ValueTask<WorkflowRunId> StartIdempotentAsync(string workflowId, JsonElement inputs, string idempotencyKey, string? correlationId = null, TagSet tags = default, SecurityTagSet securityTags = default, CancellationToken cancellationToken = default);

    /// <summary>Lists runs matching a visibility query (filter by status / workflow id, paged), scoped to the
    /// caller's read reach (§14.2).</summary>
    /// <param name="query">The visibility query.</param>
    /// <param name="context">The caller's access grant; the listing is restricted to its read reach. Use
    /// <see cref="AccessContext.System"/> for the trusted system path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of matching runs the caller may read.</returns>
    ValueTask<WorkflowRunPage> ListAsync(WorkflowQuery query, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Gets a run's current detail (status, cursor, wait/fault, etag) from its authoritative checkpoint,
    /// if the caller's read reach admits it (§14.2).</summary>
    /// <param name="id">The run id.</param>
    /// <param name="context">The caller's access grant. A run outside its read reach is reported as
    /// <see langword="null"/> — indistinguishable from absent (non-disclosing). Use <see cref="AccessContext.System"/>
    /// for the trusted system path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run detail, or <see langword="null"/> if no run with that id exists or it is not within read reach.</returns>
    ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Resumes a faulted run, re-executing it from its last checkpoint per the chosen <see cref="ResumeMode"/>.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="options">How to resume — retry the faulted step, rewind to an earlier cursor, skip the faulted step, or apply a state patch first.</param>
    /// <param name="context">The caller's access grant; the run must be within its write reach (§14.2).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the run was resumed; <see langword="false"/> if it was not faulted, was outside the caller's write reach, was held by another owner, was changed concurrently, or no longer exists.</returns>
    ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Cancels a non-terminal run, marking it <see cref="WorkflowRunStatus.Cancelled"/>.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="reason">An operator-supplied reason for the cancellation.</param>
    /// <param name="context">The caller's access grant; the run must be within its write reach (§14.2).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the run was cancelled; <see langword="false"/> if it was already terminal, was outside the caller's write reach, was held by another owner, or no longer exists.</returns>
    ValueTask<bool> CancelAsync(WorkflowRunId id, string reason, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Reaps terminal runs (completed or cancelled) older than a cutoff, scoped to the caller's purge reach (§14.2).</summary>
    /// <param name="query">Which terminal runs to reap.</param>
    /// <param name="context">The caller's access grant; only runs within its purge reach are reaped. Use
    /// <see cref="AccessContext.System"/> for the trusted system path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs deleted.</returns>
    ValueTask<int> PurgeAsync(WorkflowPurgeQuery query, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Deletes a single run, regardless of status. Unlike <see cref="PurgeAsync"/> (which reaps only old
    /// terminal runs), this removes exactly one run by id — the operator's explicit "remove this" action.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="context">The caller's access grant; the run must be within its write reach (§14.2).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a run was deleted; <see langword="false"/> if no run with that id existed, it was outside the caller's write reach, or it was held by another owner.</returns>
    ValueTask<bool> DeleteAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken);
}

/// <summary>A run's management-relevant detail, read from its authoritative checkpoint.</summary>
/// <param name="Id">The run id.</param>
/// <param name="WorkflowId">The id of the workflow the run executes.</param>
/// <param name="Status">The run's lifecycle status.</param>
/// <param name="Cursor">The cursor (state-machine index of the next step to run).</param>
/// <param name="CreatedAt">When the run was first created.</param>
/// <param name="Wait">Why the run is suspended, if it is.</param>
/// <param name="Fault">The fault record if the run is (or was) faulted.</param>
/// <param name="Etag">The etag the checkpoint was read at.</param>
/// <param name="CorrelationId">The run-wide telemetry correlation id (the W3C trace id) set at creation, if any.</param>
/// <param name="Tags">The free-form tags applied to the run at creation, if any.</param>
/// <param name="SecurityTags">The security tags (KVP labels) applied to the run at creation, if any (§14.2), distinct from the free-form <paramref name="Tags"/>.</param>
public readonly record struct WorkflowRunDetail(
    WorkflowRunId Id,
    string WorkflowId,
    WorkflowRunStatus Status,
    int Cursor,
    DateTimeOffset CreatedAt,
    WorkflowWait? Wait,
    WorkflowFault? Fault,
    WorkflowEtag Etag,
    string? CorrelationId = null,
    TagSet Tags = default,
    SecurityTagSet SecurityTags = default);

/// <summary>How to resume a faulted run (plan §11). Each mode loads the checkpoint, mutates status/cursor/state
/// under optimistic concurrency, then re-enters the executor.</summary>
public enum ResumeMode
{
    /// <summary>Re-execute from the last checkpoint — the faulted step. The common case.</summary>
    RetryFaultedStep,

    /// <summary>Rewind the cursor to an earlier step (<see cref="ResumeOptions.TargetCursor"/>) and re-run forward
    /// from there, overwriting the outputs of the re-executed steps. (cf. Temporal <em>reset</em> / Durable
    /// Functions <em>rewind</em>.)</summary>
    Rewind,

    /// <summary>Skip the faulted step — advance the cursor past it (to <see cref="ResumeOptions.TargetCursor"/>, or the
    /// next index by default), optionally recording operator-supplied outputs (<see cref="ResumeOptions.SkipOutputs"/>)
    /// for the skipped step so downstream references resolve. Only safe when downstream does not need its real
    /// outputs.</summary>
    Skip,

    /// <summary>Apply an RFC 6902 JSON Patch (<see cref="ResumeOptions.Patch"/>) to the run's persisted context —
    /// an object <c>{ "inputs": …, "stepOutputs": { … } }</c> — to fix a bad input or output, then retry the
    /// faulted step.</summary>
    StatePatch,
}

/// <summary>Options controlling how a run is resumed.</summary>
/// <param name="Mode">The resume strategy.</param>
/// <param name="TargetCursor">For <see cref="ResumeMode.Rewind"/>, the cursor to rewind to (required). For
/// <see cref="ResumeMode.Skip"/>, the cursor to resume at (defaults to the faulted cursor + 1). Ignored otherwise.</param>
/// <param name="SkipOutputs">For <see cref="ResumeMode.Skip"/>, the operator-supplied outputs to record for the
/// skipped step (an undefined element records nothing). Ignored otherwise.</param>
/// <param name="Patch">For <see cref="ResumeMode.StatePatch"/>, the RFC 6902 JSON Patch array to apply to the run's
/// persisted context. Ignored otherwise.</param>
public readonly record struct ResumeOptions(
    ResumeMode Mode = ResumeMode.RetryFaultedStep,
    int? TargetCursor = null,
    JsonElement SkipOutputs = default,
    JsonElement Patch = default)
{
    /// <summary>Gets options that retry the faulted step (re-execute from the last checkpoint).</summary>
    public static ResumeOptions RetryFaultedStep => new(ResumeMode.RetryFaultedStep);

    /// <summary>Creates options that rewind the cursor to an earlier step and re-run forward from there.</summary>
    /// <param name="targetCursor">The cursor to rewind to.</param>
    /// <returns>The resume options.</returns>
    public static ResumeOptions Rewind(int targetCursor) => new(ResumeMode.Rewind, TargetCursor: targetCursor);

    /// <summary>Creates options that skip the faulted step, advancing past it.</summary>
    /// <param name="outputs">The operator-supplied outputs to record for the skipped step (optional).</param>
    /// <param name="targetCursor">The cursor to resume at (defaults to the faulted cursor + 1).</param>
    /// <returns>The resume options.</returns>
    public static ResumeOptions Skip(JsonElement outputs = default, int? targetCursor = null) => new(ResumeMode.Skip, TargetCursor: targetCursor, SkipOutputs: outputs);

    /// <summary>Creates options that apply a JSON Patch to the persisted context, then retry the faulted step.</summary>
    /// <param name="patch">The RFC 6902 JSON Patch array.</param>
    /// <returns>The resume options.</returns>
    public static ResumeOptions StatePatch(JsonElement patch) => new(ResumeMode.StatePatch, Patch: patch);
}

/// <summary>Selects terminal runs to reap. The purge reach comes from the <see cref="AccessContext"/> passed to
/// <see cref="ISecuredWorkflowManagement.PurgeAsync"/> (§14.2), not from the query.</summary>
/// <param name="OlderThan">Reap completed/cancelled runs last updated strictly before this instant.</param>
/// <param name="Limit">The maximum number of runs to delete in one call.</param>
public readonly record struct WorkflowPurgeQuery(DateTimeOffset OlderThan, int Limit = 1000);