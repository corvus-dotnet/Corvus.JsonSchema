// <copyright file="ISourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for source credential bindings (design §13): the operator-managed <em>references</em> to secret
/// material — plus non-sensitive auth metadata — that a runner resolves to live secrets at transport-bind time. A
/// binding is keyed by (<c>sourceName</c>, <c>environment</c>); the runner caches the resolved provider against that
/// same key.
/// </summary>
/// <remarks>
/// <para><strong>Trust boundary (§13).</strong> This store holds <em>references only</em> — every persisted
/// <see cref="SourceCredentialBinding"/> is a <see cref="SecretRef"/> plus metadata, never secret material. Resolving a
/// reference to a live secret is the runner's <c>ISecretResolver</c>'s job and happens nowhere near this store; the
/// control plane manages references/status with no secret-read capability. Rotation is by changing the reference
/// (typically its version), so a rotated secret is picked up without the store ever seeing it.</para>
/// <para>Like the security-policy store, access is gated by the control-plane's capability scopes, not by row-level
/// reach, so it takes no <see cref="AccessContext"/>. Update/delete take an expected <see cref="WorkflowEtag"/> for
/// optimistic concurrency (pass <see cref="WorkflowEtag.None"/> to act unconditionally); a stale etag throws
/// <see cref="SourceCredentialConflictException"/>.</para>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> once read (clone any value that
/// must outlive the dispose).</para>
/// </remarks>
public interface ISourceCredentialStore
{
    /// <summary>Creates a binding for (<paramref name="definition"/>'s sourceName, environment), assigning it an id.
    /// Throws if a binding for that (sourceName, environment) already exists.</summary>
    /// <param name="definition">The binding content (references + non-secret metadata only).</param>
    /// <param name="actor">The authenticated identity creating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created binding (with its assigned id), as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the binding for (<paramref name="sourceName"/>, <paramref name="environment"/>), or <see langword="null"/> if absent.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The binding as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, CancellationToken cancellationToken);

    /// <summary>Lists all bindings (ascending by sourceName then environment).</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>All bindings, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<SourceCredentialBinding>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Updates a binding's content under optimistic concurrency. The (sourceName, environment) identity and the
    /// created-* audit fields are immutable; only the references and non-secret metadata are replaced.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="definition">The new content.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated binding as a pooled document the caller must dispose, or <see langword="null"/> if no binding for that key exists.</returns>
    /// <exception cref="SourceCredentialConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Deletes a binding under optimistic concurrency.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a binding was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SourceCredentialConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, CancellationToken cancellationToken);
}