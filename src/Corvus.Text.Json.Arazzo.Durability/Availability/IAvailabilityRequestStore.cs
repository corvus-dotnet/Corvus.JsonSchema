// <copyright file="IAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// Durable storage for availability requests (design §7.8): a principal's request to make a workflow version available in
/// an environment, its decision state, and audit metadata. This is the persistence layer behind the control-plane
/// availability-request API; the approval service reads a pending request, gates it on the target environment's
/// administration, makes the version available, and records the decision here. Mirrors
/// <see cref="Security.IAccessRequestStore"/>, parameterised by environment.
/// </summary>
/// <remarks>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> once read (clone any value that must
/// outlive the dispose). <see cref="DecideAsync"/> takes an expected <see cref="WorkflowEtag"/> for optimistic concurrency
/// (pass <see cref="WorkflowEtag.None"/> to apply unconditionally); a stale etag throws
/// <see cref="AvailabilityRequestConflictException"/> — so two administrators cannot double-decide the same request.</para>
/// </remarks>
public interface IAvailabilityRequestStore
{
    /// <summary>Creates a new <see cref="AvailabilityRequestStatus.Pending"/> request, assigning it an id.</summary>
    /// <param name="draft">The draft request carrying the create-content as JSON values; the store stamps the id/etag/created metadata and the Pending status. Build one via <see cref="AvailabilityRequest.Draft(string, int, string, string)"/>.</param>
    /// <param name="actor">The authenticated identity (the requester) creating the request (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created request (with its assigned id), as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a request by id, or <see langword="null"/> if absent.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The request as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<AvailabilityRequest>?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Lists requests matching a filter, oldest first (creation order). The full filtered read used by the default
    /// keyset pager; the paged <see cref="ListAsync(AvailabilityRequestQuery, int, JsonString, CancellationToken)"/> is the
    /// API list seam.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching requests, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken);

    /// <summary>Lists requests as a keyset page (design §7.8): the <paramref name="query"/>-filtered requests oldest-first by
    /// <c>(createdAt, id)</c>, bounded to <paramref name="limit"/>, resuming strictly after <paramref name="pageToken"/>. The
    /// default implementation pages over <see cref="ListAsync(AvailabilityRequestQuery, CancellationToken)"/> in memory; a
    /// backend overrides it with a native keyset query so the read itself is bounded.</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="limit">The maximum requests to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="AvailabilityRequestPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<AvailabilityRequestPage> ListAsync(AvailabilityRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        using PooledDocumentList<AvailabilityRequest> filtered = await this.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return AvailabilityRequestPaging.PageInMemory(filtered, limit, pageToken);
    }

    /// <summary>Applies a terminal decision to a request under optimistic concurrency.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to apply unconditionally).</param>
    /// <param name="actor">The authenticated identity deciding the request (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The decided request as a pooled document the caller must dispose, or <see langword="null"/> if no request with that id exists.</returns>
    /// <exception cref="AvailabilityRequestConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<AvailabilityRequest>?> DecideAsync(string id, AvailabilityRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);
}