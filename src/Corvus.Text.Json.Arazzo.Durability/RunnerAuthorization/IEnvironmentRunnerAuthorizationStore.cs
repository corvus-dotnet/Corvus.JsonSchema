// <copyright file="IEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>
/// Durable storage for environment-runner authorizations (design §5.5): a runner's authorization to serve a deployment
/// environment, its decision state, and audit metadata. This is the persistence layer behind the control-plane
/// runner-authorization API; a runner registering for an environment enters the <c>Pending</c> state and is not dispatchable
/// until an administrator of that environment (§15.1) authorizes it. Keyed by <c>(environment, runnerId)</c> and governed by
/// the target environment's administrators, exactly like <see cref="Availability.IAvailabilityRequestStore"/>.
/// </summary>
/// <remarks>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> once read (clone any value that must
/// outlive the dispose). <see cref="DecideAsync"/> takes an expected <see cref="WorkflowEtag"/> for optimistic concurrency
/// (pass <see cref="WorkflowEtag.None"/> to apply unconditionally); a stale etag throws
/// <see cref="RunnerAuthorizationConflictException"/> — so two administrators cannot double-decide the same
/// authorization.</para>
/// </remarks>
public interface IEnvironmentRunnerAuthorizationStore
{
    /// <summary>Ensures a <see cref="RunnerAuthorizationStatus.Pending"/> authorization exists for <c>(environment,
    /// runnerId)</c>: idempotent — returns the existing record unchanged if one already exists (whatever its status), else
    /// creates a Pending one. A runner re-registering for an environment it is already authorized for therefore stays
    /// Authorized.</summary>
    /// <param name="environment">The environment the runner asks to serve.</param>
    /// <param name="runnerId">The runner the authorization applies to.</param>
    /// <param name="actor">The authenticated identity (the runner) creating the record (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The existing or newly-created authorization, as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the authorization for <c>(environment, runnerId)</c>, or <see langword="null"/> if absent.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="runnerId">The runner id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The authorization as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken);

    /// <summary>Lists authorizations matching a filter, ordered by <c>(environment, runnerId)</c>. The full filtered read used
    /// by the default keyset pager; the paged
    /// <see cref="ListAsync(RunnerAuthorizationQuery, int, JsonString, CancellationToken)"/> is the API list seam.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching authorizations, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken);

    /// <summary>Lists authorizations as a keyset page (design §5.5): the <paramref name="query"/>-filtered authorizations
    /// ordered by <c>(environment, runnerId)</c>, bounded to <paramref name="limit"/>, resuming strictly after
    /// <paramref name="pageToken"/>. The default implementation pages over
    /// <see cref="ListAsync(RunnerAuthorizationQuery, CancellationToken)"/> in memory; a backend overrides it with a native
    /// keyset query so the read itself is bounded.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="limit">The maximum authorizations to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="EnvironmentRunnerAuthorizationPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        using PooledDocumentList<EnvironmentRunnerAuthorization> filtered = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return EnvironmentRunnerAuthorizationPaging.PageInMemory(filtered, limit, pageToken);
    }

    /// <summary>Applies a decision to an authorization under optimistic concurrency.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="runnerId">The runner id.</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to apply unconditionally).</param>
    /// <param name="actor">The authenticated identity (an environment administrator) deciding the authorization (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The decided authorization as a pooled document the caller must dispose, or <see langword="null"/> if no authorization exists for <c>(environment, runnerId)</c>.</returns>
    /// <exception cref="RunnerAuthorizationConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);
}