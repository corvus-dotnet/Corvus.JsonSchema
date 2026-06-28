// <copyright file="IEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for the explicit administration record of a deployment environment (design §7.7): the mutable set of
/// administrator identities entitled to govern the environment — manage it and approve promotions into it. Keyed by
/// <c>environmentName</c>. Mirrors <see cref="IWorkflowAdministratorStore"/>.
/// </summary>
/// <remarks>
/// <para>Unlike a workflow's administration (which can default to version 1's identity), an environment's administration
/// record is materialized eagerly when the environment is created — creating one grants the creator administration — via
/// <see cref="PutAsync"/> against <see cref="WorkflowEtag.None"/>. Add / transfer / remove update it.</para>
/// <para>Authorization (the caller must currently be an administrator; the last administrator cannot be removed) is the
/// governing service's concern, not this store's — the store is a CAS key/value persistence seam, like the
/// security-policy and source-credential stores, and takes no <see cref="AccessContext"/>. <see cref="PutAsync"/>
/// enforces optimistic concurrency against the expected etag: <see cref="WorkflowEtag.None"/> expects <em>no</em> existing
/// record (initial materialization); any other value expects a record with exactly that etag. A mismatch throws
/// <see cref="EnvironmentAdministrationConflictException"/>.</para>
/// <para>Read/return methods hand back a <strong>pooled document whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> once read.</para>
/// </remarks>
public interface IEnvironmentAdministratorStore
{
    /// <summary>Gets the administration record for <paramref name="environmentName"/>, or <see langword="null"/> if none
    /// exists (an unknown environment, or one created before administration was tracked).</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the administration record for <paramref name="environmentName"/> under optimistic
    /// concurrency, setting its administrator identities to <paramref name="administrators"/>.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="administrators">The new administrator identities (at least one — the caller guarantees non-empty); each
    /// carries its own <c>tags</c> plus the optional resolved <c>kind</c>/<c>label</c>, persisted verbatim.</param>
    /// <param name="expectedEtag">The expected current etag: <see cref="WorkflowEtag.None"/> to create (no record may
    /// exist), or the record's current etag to replace it.</param>
    /// <param name="actor">The authenticated identity changing the administrator set (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted record (with its new etag) as a pooled document the caller must dispose.</returns>
    /// <exception cref="EnvironmentAdministrationConflictException">The expected etag no longer matches the stored state.</exception>
    ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Removes the administration record for <paramref name="environmentName"/> (when the environment is deleted).
    /// A missing record is a no-op.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the record is gone.</returns>
    ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken);
}