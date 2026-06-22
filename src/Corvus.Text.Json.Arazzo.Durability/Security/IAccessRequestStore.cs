// <copyright file="IAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for access requests (design §16.5): a principal's request for elevated capability on a workflow,
/// its decision state, and audit metadata. This is the persistence layer behind the control-plane access-request API;
/// the approval service reads a pending request, gates it on §15 administration, writes the entitlement, and records
/// the decision here.
/// </summary>
/// <remarks>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> once read (clone any value that
/// must outlive the dispose). <see cref="DecideAsync"/> takes an expected <see cref="WorkflowEtag"/> for optimistic
/// concurrency (pass <see cref="WorkflowEtag.None"/> to apply unconditionally); a stale etag throws
/// <see cref="AccessRequestConflictException"/> — so two administrators cannot double-decide the same request.</para>
/// </remarks>
public interface IAccessRequestStore
{
    /// <summary>Creates a new <see cref="AccessRequestStatus.Pending"/> request, assigning it an id.</summary>
    /// <param name="draft">The draft request carrying the create-content as JSON values; the store stamps the id/etag/created metadata and the Pending status. Build one programmatically via <see cref="AccessRequest.Draft(string, System.Collections.Generic.IReadOnlyList{string}, string, string, string, string, long?)"/>.</param>
    /// <param name="actor">The authenticated identity (the requester) creating the request (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created request (with its assigned id), as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a request by id, or <see langword="null"/> if absent.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The request as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Lists requests matching a filter, oldest first (creation order).</summary>
    /// <param name="query">The filter (all criteria optional).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching requests, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken);

    /// <summary>Applies a terminal decision to a request under optimistic concurrency.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to apply unconditionally).</param>
    /// <param name="actor">The authenticated identity deciding the request (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The decided request as a pooled document the caller must dispose, or <see langword="null"/> if no request with that id exists.</returns>
    /// <exception cref="AccessRequestConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);
}