// <copyright file="INativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>
/// Durable storage for Native-AOT build jobs (ADR 0055): the state of an asynchronous build of a workflow version's
/// serverless binary for one runtime target in one environment, its lifecycle state, and audit metadata. This is the
/// persistence layer a background build worker drives Queued -> Building -> Ready | Failed. A job's identity is its target
/// tuple (<c>baseWorkflowId</c>, <c>versionNumber</c>, <c>environment</c>, <c>runtimeIdentifier</c>), from which the id is
/// derived deterministically (<see cref="NativeBuildJob.DeriveId"/>), so <see cref="EnqueueAsync"/> is idempotent per
/// target. Mirrors the availability-request store, keyed by target tuple rather than a random id.
/// </summary>
/// <remarks>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> once read (clone any value that must
/// outlive the dispose). <see cref="CompleteAsync"/> takes an expected <see cref="WorkflowEtag"/> for optimistic concurrency
/// (pass <see cref="WorkflowEtag.None"/> to apply unconditionally); a stale etag throws
/// <see cref="NativeBuildJobConflictException"/> and a wrong-state transition throws
/// <see cref="NativeBuildJobStateException"/> — so a build cannot be completed twice or from a non-building state.</para>
/// </remarks>
public interface INativeBuildJobStore
{
    /// <summary>Enqueues a build for a target, idempotently: the job for the target tuple is (re)set to
    /// <see cref="NativeBuildJobStatus.Queued"/>, resetting any existing job's <c>startedAt</c>/<c>completedAt</c>/
    /// <c>failureReason</c>/<c>claimedBy</c> (a rebuild). The id is derived from the draft's target tuple, so a repeated
    /// enqueue for the same target overwrites the same job rather than creating a duplicate.</summary>
    /// <param name="draft">The draft job carrying the target-content (the tuple + optional build label) as JSON values; the store stamps the id/etag/created metadata and the Queued status. Build one via <see cref="NativeBuildJob.Draft(string, int, string, string, string)"/>.</param>
    /// <param name="actor">The authenticated identity requesting the build (for audit); this entity carries no created-by field, so it is accepted for parity and validated but not persisted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The enqueued (Queued) job, as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a job by id, or <see langword="null"/> if absent.</summary>
    /// <param name="id">The job id (derive it with <see cref="NativeBuildJob.DeriveId"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The job as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Atomically claims the oldest claimable job (oldest-first by <c>(createdAt, id)</c>) and transitions it to
    /// <see cref="NativeBuildJobStatus.Building"/>, stamping <c>claimedBy</c>/<c>startedAt</c> and an advisory lease
    /// (<c>leaseExpiresAt</c> = now + <paramref name="leaseTtl"/>) — the build worker's claim primitive. A job is claimable
    /// when it is <see cref="NativeBuildJobStatus.Queued"/> or it is <see cref="NativeBuildJobStatus.Building"/> with no live
    /// lease (an orphan of a crashed worker, reclaimed here — ADR 0056); a reclaim resets <c>startedAt</c> and the lease and
    /// bumps the etag, superseding the orphaned worker's completion. Returns <see langword="null"/> when nothing is
    /// claimable.</summary>
    /// <param name="claimedBy">The worker claiming the job (for audit and lease ownership).</param>
    /// <param name="leaseTtl">How long the claiming worker's lease is held before another worker may reclaim the job; the worker renews it on a heartbeat while it builds.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed (Building) job as a pooled document the caller must dispose, or <see langword="null"/> if nothing was claimable.</returns>
    ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken);

    /// <summary>Completes a building job under optimistic concurrency: transitions it from
    /// <see cref="NativeBuildJobStatus.Building"/> to <see cref="NativeBuildJobStatus.Ready"/> or
    /// <see cref="NativeBuildJobStatus.Failed"/>, stamping <c>completedAt</c> and (for a failure) <c>failureReason</c>.</summary>
    /// <param name="id">The job id.</param>
    /// <param name="completion">The completion to apply (terminal status + optional failure reason).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to apply unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The completed job as a pooled document the caller must dispose, or <see langword="null"/> if no job with that id exists.</returns>
    /// <exception cref="NativeBuildJobConflictException">The expected etag no longer matches.</exception>
    /// <exception cref="NativeBuildJobStateException">The job is not in the <see cref="NativeBuildJobStatus.Building"/> state.</exception>
    ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken);

    /// <summary>Renews the advisory lease on a building job under optimistic concurrency — the heartbeat a worker calls while
    /// its compile runs so no other worker reclaims the job as an orphan (ADR 0056). Extends <c>leaseExpiresAt</c> to
    /// now + <paramref name="leaseTtl"/> and bumps the etag; the returned job carries the new etag the worker renews and
    /// completes with next. The etag is the ownership token: if the job was reclaimed, <paramref name="expectedEtag"/> no
    /// longer matches and the renewal conflicts, which is how a superseded worker learns it lost the lease.</summary>
    /// <param name="id">The job id.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to renew unconditionally).</param>
    /// <param name="leaseTtl">How long from now the renewed lease is held.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The renewed job (carrying the new etag) as a pooled document the caller must dispose, or <see langword="null"/> if no job with that id exists.</returns>
    /// <exception cref="NativeBuildJobConflictException">The expected etag no longer matches (the job was reclaimed).</exception>
    /// <exception cref="NativeBuildJobStateException">The job is not in the <see cref="NativeBuildJobStatus.Building"/> state.</exception>
    ValueTask<ParsedJsonDocument<NativeBuildJob>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken);

    /// <summary>Lists jobs matching a filter, oldest first (creation order). The full filtered read used by the default
    /// keyset pager; the paged <see cref="ListAsync(NativeBuildJobQuery, int, JsonString, CancellationToken)"/> is the API
    /// list seam.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching jobs, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken);

    /// <summary>Determines whether the build for the given target tuple is <see cref="NativeBuildJobStatus.Ready"/> — the
    /// serverless binary is available. The predicate a dispatch/deploy path gates on before routing to a target's binary.</summary>
    /// <param name="baseWorkflowId">The base workflow id of the target.</param>
    /// <param name="versionNumber">The version number of the target.</param>
    /// <param name="environment">The target environment.</param>
    /// <param name="runtimeIdentifier">The runtime identifier of the target.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the target tuple's job exists and is Ready.</returns>
    /// <remarks>
    /// The default filters <see cref="ListAsync(NativeBuildJobQuery, CancellationToken)"/> by the target tuple and a Ready
    /// status — the documented full-read scan seam. A backend overrides it with a native indexed lookup on the derived id
    /// (the target tuple maps to a unique id), so the read itself is a point read rather than a scan.
    /// </remarks>
    async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        var query = new NativeBuildJobQuery(NativeBuildJobStatus.Ready, baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using PooledDocumentList<NativeBuildJob> matches = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return matches.Count > 0;
    }

    /// <summary>Lists jobs as a keyset page (ADR 0055): the <paramref name="query"/>-filtered jobs oldest-first by
    /// <c>(createdAt, id)</c>, bounded to <paramref name="limit"/>, resuming strictly after <paramref name="pageToken"/>. The
    /// default implementation pages over <see cref="ListAsync(NativeBuildJobQuery, CancellationToken)"/> in memory; a backend
    /// overrides it with a native keyset query so the read itself is bounded.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="limit">The maximum jobs to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="NativeBuildJobPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        using PooledDocumentList<NativeBuildJob> filtered = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return NativeBuildJobPaging.PageInMemory(filtered, limit, pageToken);
    }

    /// <summary>Counts jobs matching a filter, bounded by <paramref name="cap"/>: the same <paramref name="query"/> filter as
    /// the paged list, but returning only a bounded total, never rows. The default implementation counts over
    /// <see cref="ListAsync(NativeBuildJobQuery, CancellationToken)"/>; a backend overrides it with a native <c>COUNT</c>
    /// capped at <paramref name="cap"/> + 1 so the read itself is bounded (allocation-free — a number, no row
    /// materialisation).</summary>
    /// <param name="query">The filter (all criteria optional) — the same query the list uses.</param>
    /// <param name="cap">The maximum count to report; when the true total exceeds it, <c>Capped</c> is <see langword="true"/> and <c>Count</c> is <paramref name="cap"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded count, and whether the cap was hit (so the caller renders e.g. <c>100+</c>).</returns>
    async ValueTask<(int Count, bool Capped)> CountAsync(NativeBuildJobQuery query, int cap, CancellationToken cancellationToken)
    {
        using PooledDocumentList<NativeBuildJob> filtered = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        int total = filtered.Count;
        return total > cap ? (cap, true) : (total, false);
    }
}