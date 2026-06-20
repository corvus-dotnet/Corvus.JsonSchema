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
    /// <summary>Creates a binding (identity = sourceName, environment, and the immutable security tags), assigning it an
    /// id. Throws if a binding with the same (sourceName, environment, security-tags) already exists. The control plane
    /// stamps the principal's internal tenant tag onto the <paramref name="draft"/> before calling, so the new binding
    /// is owned by the creator's slice of the security shell (§14.2). The store reads the draft's content bytes-to-bytes
    /// and stamps id/createdBy/createdAt/etag.</summary>
    /// <param name="draft">The draft binding (references + non-secret metadata + resolved security tags) — typically built
    /// by <see cref="SourceCredentialBinding.Draft(SourceCredentialDefinition)"/> or from a request body; the store reads
    /// it synchronously, so a pooled draft may be disposed once the call returns.</param>
    /// <param name="actor">The authenticated identity creating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created binding (with its assigned id), as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the binding for (<paramref name="sourceName"/>, <paramref name="environment"/>) that the caller's
    /// read reach admits, or <see langword="null"/> if none is visible. A binding outside the caller's reach is reported
    /// as absent (non-disclosing, §14.2).</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The binding as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Lists one keyset page of the bindings the caller's read reach admits, ascending by sourceName then environment.
    /// Reach is a per-row predicate evaluated in-memory, so the store seeks past <paramref name="pageToken"/> in
    /// keyset order and streams rows applying reach until <paramref name="limit"/> visible bindings accumulate (or the
    /// data is exhausted), emitting a <see cref="SourceCredentialPage.NextPageToken"/> when more remain — it never
    /// materialises the whole table per page. The token is opaque and backend-scoped.
    /// </summary>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="limit">The maximum number of bindings to return in the page (the store treats a non-positive value as 1).</param>
    /// <param name="pageToken">An opaque token from a previous page's <see cref="SourceCredentialPage.NextPageToken"/>, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (visible bindings + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, string? pageToken, CancellationToken cancellationToken);

    /// <summary>Updates the binding for (<paramref name="sourceName"/>, <paramref name="environment"/>) the caller's
    /// write reach admits, under optimistic concurrency. The (sourceName, environment) identity, the security tags, and
    /// the created-* audit fields are immutable; only the references and non-secret metadata are replaced.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="draft">The new content as a draft (its identity and security tags are ignored — those are immutable
    /// and carried forward from the stored binding); the store reads it synchronously, so a pooled draft may be disposed
    /// once the call returns.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the binding (for audit).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated binding as a pooled document the caller must dispose, or <see langword="null"/> if no binding the caller may write exists for that key.</returns>
    /// <exception cref="SourceCredentialConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Deletes the binding for (<paramref name="sourceName"/>, <paramref name="environment"/>) the caller's
    /// write reach admits, under optimistic concurrency.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a binding the caller may write was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SourceCredentialConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Resolves the credential binding a run carrying <paramref name="runTags"/> is entitled to use for
    /// (<paramref name="sourceName"/>, <paramref name="environment"/>) (design §13/§14.2): the binding the run's tags
    /// satisfy by label-superset (<see cref="SourceCredentialBinding.IsUsableBy"/>), or <see langword="null"/> if the run
    /// is entitled to none. This is the <strong>usage</strong> path — distinct from the management reach above — and
    /// fails closed: an unentitled or absent binding yields no credential, never another scope's secret.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="runTags">The run's own security tags.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The entitled binding as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken);

    /// <summary>Evaluates whether <paramref name="tags"/> are entitled to use the credential bindings configured for
    /// <paramref name="sourceName"/> across <strong>all</strong> environments (design §13) — the catalog-time usage
    /// check that gates declaring the source before any run. Returns <see cref="CredentialSourceAccess.Granted"/> if any
    /// binding for the source is usable by the tags (label-superset), <see cref="CredentialSourceAccess.Denied"/> if the
    /// source has bindings but none is usable, and <see cref="CredentialSourceAccess.Unconfigured"/> if it has none.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="tags">The security tags to evaluate (typically the catalogued workflow version's tags, which its runs inherit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The access evaluation.</returns>
    ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken);
}