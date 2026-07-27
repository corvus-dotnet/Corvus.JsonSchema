// <copyright file="IWorkflowDeploymentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>
/// Durable storage for workflow deployments (ADR 0055): the state of an asynchronous deployment of a workflow version's
/// signed native binary to a function platform for one runtime target in one environment, the resulting function invoke URL,
/// its lifecycle state, and audit metadata. This is the persistence layer a background deploy worker drives Queued ->
/// Deploying -> Deployed | Failed. A deployment's identity is its target tuple (<c>baseWorkflowId</c>, <c>versionNumber</c>,
/// <c>environment</c>, <c>runtimeIdentifier</c>), from which the id is derived deterministically
/// (<see cref="WorkflowDeployment.DeriveId"/>), so <see cref="EnqueueAsync"/> is idempotent per target. Mirrors the
/// availability-request store, keyed by target tuple rather than a random id.
/// </summary>
/// <remarks>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> once read (clone any value that must
/// outlive the dispose). <see cref="CompleteAsync"/> takes an expected <see cref="WorkflowEtag"/> for optimistic concurrency
/// (pass <see cref="WorkflowEtag.None"/> to apply unconditionally); a stale etag throws
/// <see cref="WorkflowDeploymentConflictException"/> and a wrong-state transition throws
/// <see cref="WorkflowDeploymentStateException"/> — so a deployment cannot be completed twice or from a non-deploying state.</para>
/// </remarks>
public interface IWorkflowDeploymentStore
{
    /// <summary>Enqueues a deployment for a target, idempotently: the deployment for the target tuple is (re)set to
    /// <see cref="WorkflowDeploymentStatus.Queued"/>, resetting any existing deployment's <c>startedAt</c>/<c>completedAt</c>/
    /// <c>failureReason</c>/<c>functionUrl</c>/<c>claimedBy</c> (a redeploy). The id is derived from the draft's target tuple,
    /// so a repeated enqueue for the same target overwrites the same deployment rather than creating a duplicate.</summary>
    /// <param name="draft">The draft deployment carrying the target-content (the tuple) as JSON values; the store stamps the id/etag/created metadata and the Queued status. Build one via <see cref="WorkflowDeployment.Draft(string, int, string, string)"/>.</param>
    /// <param name="actor">The authenticated identity requesting the deployment (for audit); this entity carries no created-by field, so it is accepted for parity and validated but not persisted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The enqueued (Queued) deployment, as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(WorkflowDeployment draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a deployment by id, or <see langword="null"/> if absent.</summary>
    /// <param name="id">The deployment id (derive it with <see cref="WorkflowDeployment.DeriveId"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deployment as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<WorkflowDeployment>?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Atomically claims the oldest claimable deployment (oldest-first by <c>(createdAt, id)</c>) and transitions it
    /// to <see cref="WorkflowDeploymentStatus.Deploying"/>, stamping <c>claimedBy</c>/<c>startedAt</c> and an advisory lease
    /// (<c>leaseExpiresAt</c> = now + <paramref name="leaseTtl"/>) — the deploy worker's claim primitive. A deployment is
    /// claimable when it is <see cref="WorkflowDeploymentStatus.Queued"/> or it is
    /// <see cref="WorkflowDeploymentStatus.Deploying"/> with no live lease (an orphan of a crashed worker, reclaimed here —
    /// ADR 0056); a reclaim resets <c>startedAt</c> and the lease and bumps the etag, superseding the orphaned worker's
    /// completion. Returns <see langword="null"/> when nothing is claimable.</summary>
    /// <param name="claimedBy">The worker claiming the deployment (for audit and lease ownership).</param>
    /// <param name="leaseTtl">How long the claiming worker's lease is held before another worker may reclaim the deployment; the worker renews it on a heartbeat while it deploys.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed (Deploying) deployment as a pooled document the caller must dispose, or <see langword="null"/> if nothing was claimable.</returns>
    ValueTask<ParsedJsonDocument<WorkflowDeployment>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken);

    /// <summary>Completes a deploying deployment under optimistic concurrency: transitions it from
    /// <see cref="WorkflowDeploymentStatus.Deploying"/> to <see cref="WorkflowDeploymentStatus.Deployed"/> or
    /// <see cref="WorkflowDeploymentStatus.Failed"/>, stamping <c>completedAt</c> and (for a success) <c>functionUrl</c> or
    /// (for a failure) <c>failureReason</c>.</summary>
    /// <param name="id">The deployment id.</param>
    /// <param name="completion">The completion to apply (terminal status + optional function URL + optional failure reason).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to apply unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The completed deployment as a pooled document the caller must dispose, or <see langword="null"/> if no deployment with that id exists.</returns>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches.</exception>
    /// <exception cref="WorkflowDeploymentStateException">The deployment is not in the <see cref="WorkflowDeploymentStatus.Deploying"/> state.</exception>
    ValueTask<ParsedJsonDocument<WorkflowDeployment>?> CompleteAsync(string id, WorkflowDeploymentCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken);

    /// <summary>Renews the advisory lease on a deploying deployment under optimistic concurrency — the heartbeat a worker
    /// calls while its deploy runs so no other worker reclaims the deployment as an orphan (ADR 0056). Extends
    /// <c>leaseExpiresAt</c> to now + <paramref name="leaseTtl"/> and bumps the etag; the returned deployment carries the new
    /// etag the worker renews and completes with next. The etag is the ownership token: if the deployment was reclaimed,
    /// <paramref name="expectedEtag"/> no longer matches and the renewal conflicts, which is how a superseded worker learns it
    /// lost the lease.</summary>
    /// <param name="id">The deployment id.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to renew unconditionally).</param>
    /// <param name="leaseTtl">How long from now the renewed lease is held.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The renewed deployment (carrying the new etag) as a pooled document the caller must dispose, or <see langword="null"/> if no deployment with that id exists.</returns>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches (the deployment was reclaimed).</exception>
    /// <exception cref="WorkflowDeploymentStateException">The deployment is not in the <see cref="WorkflowDeploymentStatus.Deploying"/> state.</exception>
    ValueTask<ParsedJsonDocument<WorkflowDeployment>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken);

    /// <summary>Lists deployments matching a filter, oldest first (creation order). The full filtered read used by the default
    /// keyset pager; the paged <see cref="ListAsync(WorkflowDeploymentQuery, int, JsonString, CancellationToken)"/> is the API
    /// list seam.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching deployments, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<WorkflowDeployment>> ListAsync(WorkflowDeploymentQuery query, CancellationToken cancellationToken);

    /// <summary>Determines whether the deployment for the given target tuple is
    /// <see cref="WorkflowDeploymentStatus.Deployed"/> — the function is available at its invoke URL. The predicate a
    /// dispatch/routing path gates on before routing to a target's deployed function.</summary>
    /// <param name="baseWorkflowId">The base workflow id of the target.</param>
    /// <param name="versionNumber">The version number of the target.</param>
    /// <param name="environment">The target environment.</param>
    /// <param name="runtimeIdentifier">The runtime identifier of the target.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the target tuple's deployment exists and is Deployed.</returns>
    /// <remarks>
    /// The default filters <see cref="ListAsync(WorkflowDeploymentQuery, CancellationToken)"/> by the target tuple and a
    /// Deployed status — the documented full-read scan seam. A backend overrides it with a native indexed lookup on the
    /// derived id (the target tuple maps to a unique id), so the read itself is a point read rather than a scan.
    /// </remarks>
    async ValueTask<bool> IsDeployedAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        var query = new WorkflowDeploymentQuery(WorkflowDeploymentStatus.Deployed, baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using PooledDocumentList<WorkflowDeployment> matches = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return matches.Count > 0;
    }

    /// <summary>Lists deployments as a keyset page (ADR 0055): the <paramref name="query"/>-filtered deployments oldest-first
    /// by <c>(createdAt, id)</c>, bounded to <paramref name="limit"/>, resuming strictly after <paramref name="pageToken"/>.
    /// The default implementation pages over <see cref="ListAsync(WorkflowDeploymentQuery, CancellationToken)"/> in memory; a
    /// backend overrides it with a native keyset query so the read itself is bounded.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="limit">The maximum deployments to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="WorkflowDeploymentPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<WorkflowDeploymentPage> ListAsync(WorkflowDeploymentQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        using PooledDocumentList<WorkflowDeployment> filtered = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return WorkflowDeploymentPaging.PageInMemory(filtered, limit, pageToken);
    }

    /// <summary>Counts deployments matching a filter, bounded by <paramref name="cap"/>: the same <paramref name="query"/>
    /// filter as the paged list, but returning only a bounded total, never rows. The default implementation counts over
    /// <see cref="ListAsync(WorkflowDeploymentQuery, CancellationToken)"/>; a backend overrides it with a native <c>COUNT</c>
    /// capped at <paramref name="cap"/> + 1 so the read itself is bounded (allocation-free — a number, no row
    /// materialisation).</summary>
    /// <param name="query">The filter (all criteria optional) — the same query the list uses.</param>
    /// <param name="cap">The maximum count to report; when the true total exceeds it, <c>Capped</c> is <see langword="true"/> and <c>Count</c> is <paramref name="cap"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded count, and whether the cap was hit (so the caller renders e.g. <c>100+</c>).</returns>
    async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowDeploymentQuery query, int cap, CancellationToken cancellationToken)
    {
        using PooledDocumentList<WorkflowDeployment> filtered = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        int total = filtered.Count;
        return total > cap ? (cap, true) : (total, false);
    }
}