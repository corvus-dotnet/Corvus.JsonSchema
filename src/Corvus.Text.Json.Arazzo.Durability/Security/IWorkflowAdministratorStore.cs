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
/// <see cref="WorkflowCatalogClient"/> applies that fallback). The first transfer / add-administrator writes the explicit
/// record via <see cref="PutAsync"/>.</para>
/// <para>Authorization (caller must currently be an administrator; the last administrator cannot be removed) is the
/// <see cref="WorkflowCatalogClient"/>'s concern, not this store's — the store is a CAS key/value persistence seam, like
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
    /// <param name="administrators">The new administrator identities (at least one — the caller guarantees non-empty).</param>
    /// <param name="expectedEtag">The expected current etag: <see cref="WorkflowEtag.None"/> to create (no record may
    /// exist), or the record's current etag to replace it.</param>
    /// <param name="actor">The authenticated identity changing the administrator set (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted record (with its new etag) as a pooled document the caller must dispose.</returns>
    /// <exception cref="WorkflowAdministrationConflictException">The expected etag no longer matches the stored state.</exception>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);
}