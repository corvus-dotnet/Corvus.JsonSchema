// <copyright file="IWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for the explicit administration record of a base workflow id (design §13/§14.2): the mutable set of
/// administrator identities entitled to publish further versions and to manage administration. Keyed by <c>baseWorkflowId</c>.
/// </summary>
/// <remarks>
/// <para>The record is materialized lazily — a workflow whose administration has never been mutated has <strong>no</strong>
/// record here, and its administration defaults to the administrator identity that stamped version 1 (the
/// <see cref="SecuredWorkflowCatalog"/> applies that fallback). The first transfer / add-administrator writes the explicit
/// record via <see cref="PutAsync"/>.</para>
/// <para>Authorization (caller must currently be an administrator; the last administrator cannot be removed) is the
/// <see cref="SecuredWorkflowCatalog"/>'s concern, not this store's — the store is a CAS key/value persistence seam, like
/// the security-policy and source-credential stores, and takes no <see cref="AccessContext"/>. <see cref="PutAsync"/>
/// enforces optimistic concurrency against the expected etag: <see cref="WorkflowEtag.None"/> expects
/// <em>no</em> existing record (initial materialization); any other value expects a record with exactly that etag. A
/// mismatch throws <see cref="WorkflowAdministrationConflictException"/>.</para>
/// <para>Read/return methods hand back a <strong>pooled document whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> once read.</para>
/// </remarks>
public interface IWorkflowAdministratorStore
{
    /// <summary>Gets the explicit administration record for <paramref name="baseWorkflowId"/>, or <see langword="null"/> if
    /// none has been materialized (administration still defaults to version 1's administrator identity).</summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the administration record for <paramref name="baseWorkflowId"/> under optimistic
    /// concurrency, setting its administrator identities to <paramref name="administrators"/>.</summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="administrators">The new administrator identities (at least one — the caller guarantees non-empty); each
    /// carries its own <c>tags</c> plus the optional resolved <c>kind</c>/<c>label</c>, persisted verbatim.</param>
    /// <param name="expectedEtag">The expected current etag: <see cref="WorkflowEtag.None"/> to create (no record may
    /// exist), or the record's current etag to replace it.</param>
    /// <param name="actor">The authenticated identity changing the administrator set (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted record (with its new etag) as a pooled document the caller must dispose.</returns>
    /// <exception cref="WorkflowAdministrationConflictException">The expected etag no longer matches the stored state.</exception>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Lists the base workflow ids the given administrator identity administers — the reverse administration index
    /// (design §15.4) that powers the approver inbox. Because administration membership is exact, order-independent set
    /// equality, and <see cref="SecurityIdentityDigest"/> is canonical (two identities are set-equal iff their digests are
    /// equal), the reverse lookup is an indexed digest seek, never a scan: a workflow appears here iff
    /// <paramref name="adminDigest"/> is the digest of one of its administrator identities.</summary>
    /// <param name="adminDigest">The administrator identity's collision-probe digest (<see cref="SecurityIdentityDigest.Compute(SecurityTagSet)"/>
    /// of the caller's resolved identity). The empty identity has no digest and administers nothing.</param>
    /// <param name="limit">The maximum base ids to return (a non-positive value uses <see cref="WorkflowAdministeredPage.DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="WorkflowAdministeredPage.NextPageToken"/>,
    /// or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page of administered base ids (ordered by <c>baseWorkflowId</c>), as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    /// <exception cref="NotSupportedException">The store does not maintain the reverse administration index.</exception>
    ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("This administrator store does not maintain the reverse administration index (design §15.4).");
}