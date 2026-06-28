// <copyright file="ISourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// Durable storage for registered sources (design §7.6): a first-class, reach-scoped resource a workflow references by
/// name in its <c>sourceDescriptions</c>, carrying the source's OpenAPI/AsyncAPI document verbatim. A source is keyed by
/// its <c>name</c>; the per-environment credentials that authenticate calls to it are a separate concern (the
/// source-credential store), and there is no administrator set — reach membership is the management gate.
/// </summary>
/// <remarks>
/// <para><strong>Reach (§14.2).</strong> Management reads/writes are reach-filtered by each source's
/// <see cref="RegisteredSource.ManagementTagsValue"/> via the caller's <see cref="AccessContext"/> — a source outside the caller's
/// reach is reported as absent (non-disclosing). The deployment stamps the creator's internal tenant tag onto the draft
/// before <see cref="AddAsync"/>, so the new source is owned by the creator's slice of the shell; because the name is
/// unique only within a reach slice, two reach-isolated sources may share a name and the store disambiguates by the
/// management-tag discriminator.</para>
/// <para><strong>Concurrency.</strong> Update/delete take an expected <see cref="WorkflowEtag"/> for optimistic
/// concurrency (pass <see cref="WorkflowEtag.None"/> to act unconditionally); a stale etag throws
/// <see cref="SourceConflictException"/>.</para>
/// <para><strong>Ownership.</strong> Read/return methods hand back <strong>pooled documents whose lifetime the caller
/// owns</strong>: dispose the returned <see cref="ParsedJsonDocument{T}"/> / <see cref="SourcePage"/> once read. Each
/// returned source is the <em>full</em> stored document (the registered document included); the list returns full
/// sources too, and the control-plane handler field-selects the document-less summary. The draft passed to
/// <see cref="AddAsync"/>/<see cref="UpdateAsync"/> is read synchronously (bytes-to-bytes), so a pooled draft may be
/// disposed once the call returns.</para>
/// </remarks>
public interface ISourceStore
{
    /// <summary>Registers a source, stamping createdBy/createdAt/etag. The draft carries the name, type, document, and
    /// resolved management tags (the deployment stamps the creator's tenant tag before calling). Throws if a source with
    /// the same (name, management-tags) already exists. The store reads the draft's content bytes-to-bytes (the document
    /// included).</summary>
    /// <param name="draft">The draft source (name + type + document + display/description + resolved management tags) —
    /// typically built by <see cref="RegisteredSource.Draft(in JsonElement, in JsonElement, in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/>
    /// or from a request body; read synchronously, so a pooled draft may be disposed once the call returns.</param>
    /// <param name="actor">The authenticated identity registering the source (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The registered source, as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the source named <paramref name="name"/> that the caller's read reach admits, or
    /// <see langword="null"/> if none is visible (a source outside reach is reported as absent — non-disclosing).</summary>
    /// <param name="name">The source name.</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The source as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Lists one keyset page of the sources the caller's read reach admits, ascending by <c>name</c>. Reach is a
    /// per-row predicate evaluated in keyset order; the store seeks past <paramref name="pageToken"/> and streams rows
    /// applying reach until <paramref name="limit"/> visible sources accumulate (or the data is exhausted), emitting a
    /// <see cref="SourcePage.NextPageToken"/> when more remain. The token is opaque and backend-scoped.</summary>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="limit">The maximum number of sources to return (the store treats a non-positive value as 1).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="SourcePage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (visible sources + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<SourcePage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken);

    /// <summary>Updates the source named <paramref name="name"/> the caller's write reach admits, under optimistic
    /// concurrency. The <c>name</c>, the <c>type</c>, the management tags, and the created-* audit fields are immutable;
    /// the display name and description are replaced, and the document is rotated when the draft supplies one (otherwise
    /// carried forward) — all bytes-to-bytes from the draft / the stored source.</summary>
    /// <param name="name">The source name.</param>
    /// <param name="draft">The new content as a draft (its name and type and tags are ignored — immutable, carried
    /// forward; an undefined document keeps the stored one); read synchronously, so a pooled draft may be disposed once
    /// the call returns.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the source (for audit).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated source as a pooled document the caller must dispose, or <see langword="null"/> if no source the caller may write exists for that name.</returns>
    /// <exception cref="SourceConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Deletes the source named <paramref name="name"/> the caller's write reach admits, under optimistic
    /// concurrency. The per-environment credentials bound to the source are a separate resource and are not removed here.</summary>
    /// <param name="name">The source name.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a source the caller may write was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SourceConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken);
}