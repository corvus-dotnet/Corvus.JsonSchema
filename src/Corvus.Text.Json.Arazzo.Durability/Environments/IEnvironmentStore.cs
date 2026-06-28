// <copyright file="IEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Durable storage for deployment environments (design §7.7): a first-class, governed, reach-scoped resource a workflow
/// version is made available in and whose source credentials form a per-environment set. A environment is keyed by its
/// <c>name</c>; administration is a separate concern (the environment-administrator store), exactly as a workflow's
/// administrators are stored apart from its catalog versions.
/// </summary>
/// <remarks>
/// <para><strong>Reach (§14.2).</strong> Management reads/writes are reach-filtered by each environment's
/// <see cref="Environment.ManagementTagsValue"/> via the caller's <see cref="AccessContext"/> — an environment outside
/// the caller's reach is reported as absent (non-disclosing). The deployment stamps the creator's internal tenant tag
/// onto the draft before <see cref="AddAsync"/>, so the new environment is owned by the creator's slice of the shell;
/// because the name is unique only within a reach slice, two reach-isolated environments may share a name and the store
/// disambiguates by the management-tag discriminator.</para>
/// <para><strong>Concurrency.</strong> Update/delete take an expected <see cref="WorkflowEtag"/> for optimistic
/// concurrency (pass <see cref="WorkflowEtag.None"/> to act unconditionally); a stale etag throws
/// <see cref="EnvironmentConflictException"/>.</para>
/// <para><strong>Ownership.</strong> Read/return methods hand back <strong>pooled documents whose lifetime the caller
/// owns</strong>: dispose the returned <see cref="ParsedJsonDocument{T}"/> / <see cref="EnvironmentPage"/> once read.
/// The draft passed to <see cref="AddAsync"/>/<see cref="UpdateAsync"/> is read synchronously (bytes-to-bytes), so a
/// pooled draft may be disposed once the call returns.</para>
/// </remarks>
public interface IEnvironmentStore
{
    /// <summary>Creates an environment, stamping createdBy/createdAt/etag. The draft carries the name + resolved
    /// management tags (the deployment stamps the creator's tenant tag before calling). Throws if an environment with the
    /// same (name, management-tags) already exists. The store reads the draft's content bytes-to-bytes.</summary>
    /// <param name="draft">The draft environment (name + display/description + resolved management tags) — typically built
    /// by <see cref="Environment.Draft(in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/> or from a
    /// request body; read synchronously, so a pooled draft may be disposed once the call returns.</param>
    /// <param name="actor">The authenticated identity creating the environment (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created environment, as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the environment named <paramref name="name"/> that the caller's read reach admits, or
    /// <see langword="null"/> if none is visible (an environment outside reach is reported as absent — non-disclosing).</summary>
    /// <param name="name">The environment name.</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The environment as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Lists one keyset page of the environments the caller's read reach admits, ascending by <c>name</c>. Reach
    /// is a per-row predicate evaluated in keyset order; the store seeks past <paramref name="pageToken"/> and streams
    /// rows applying reach until <paramref name="limit"/> visible environments accumulate (or the data is exhausted),
    /// emitting a <see cref="EnvironmentPage.NextPageToken"/> when more remain. The token is opaque and backend-scoped.</summary>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="limit">The maximum number of environments to return (the store treats a non-positive value as 1).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="EnvironmentPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (visible environments + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken);

    /// <summary>Updates the environment named <paramref name="name"/> the caller's write reach admits, under optimistic
    /// concurrency. The <c>name</c>, the management tags, and the created-* audit fields are immutable; only the display
    /// name and description are replaced (carried bytes-to-bytes from the draft).</summary>
    /// <param name="name">The environment name.</param>
    /// <param name="draft">The new content as a draft (its name and tags are ignored — immutable, carried forward); read
    /// synchronously, so a pooled draft may be disposed once the call returns.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the environment (for audit).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated environment as a pooled document the caller must dispose, or <see langword="null"/> if no environment the caller may write exists for that name.</returns>
    /// <exception cref="EnvironmentConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Deletes the environment named <paramref name="name"/> the caller's write reach admits, under optimistic
    /// concurrency.</summary>
    /// <param name="name">The environment name.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if an environment the caller may write was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="EnvironmentConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken);
}