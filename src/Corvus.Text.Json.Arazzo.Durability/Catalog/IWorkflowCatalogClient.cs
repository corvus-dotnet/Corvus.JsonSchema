// <copyright file="IWorkflowCatalogClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The catalog control surface: add/search/inspect/govern workflow document packages, with referential
/// integrity against workflow runs. It is the catalog analogue of <see cref="IWorkflowManagementClient"/> —
/// it coordinates the catalog store (<see cref="IWorkflowCatalogStore"/>) with the run store for delete/purge
/// safety and emits the audit telemetry, so the REST handler stays a thin translation layer.
/// </summary>
public interface IWorkflowCatalogClient
{
    /// <summary>
    /// Adds a new immutable version: validates the submitted workflow id has no <c>-vN</c> suffix, then has the
    /// store assign the version, rewrite the id, hash the package and persist it.
    /// </summary>
    /// <param name="packageUtf8">The package envelope as UTF-8 JSON.</param>
    /// <param name="owner">The accountable governance owner.</param>
    /// <param name="tags">Free-form tags, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The added version's metadata.</returns>
    /// <exception cref="ArgumentException">The package is malformed, or its workflow id already carries a <c>-vN</c> suffix.</exception>
    ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, IReadOnlyList<string>? tags, CancellationToken cancellationToken);

    /// <summary>Adds a workflow version with security tags (KVP labels for row authorization, §14.2).</summary>
    /// <param name="packageUtf8">The package envelope as UTF-8 JSON.</param>
    /// <param name="owner">The accountable governance owner.</param>
    /// <param name="tags">Free-form tags, if any.</param>
    /// <param name="securityTags">Security tags (KVP labels) for row authorization, distinct from the free-form <paramref name="tags"/>, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The added version's metadata.</returns>
    /// <exception cref="ArgumentException">The package is malformed, or its workflow id already carries a <c>-vN</c> suffix.</exception>
    ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, IReadOnlyList<string>? tags, IReadOnlyList<SecurityTag>? securityTags, CancellationToken cancellationToken);

    /// <summary>Searches the catalog.</summary>
    /// <param name="query">The search.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of matching version metadata.</returns>
    ValueTask<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken cancellationToken);

    /// <summary>Gets a version's metadata.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The version, or <see langword="null"/> if it does not exist.</returns>
    ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>Gets the whole canonical package envelope for a version.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The package bytes, or <see langword="null"/> if the version does not exist.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>Gets a single document from a version's package (the workflow, or one named source).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="documentName">The document name (<see cref="CatalogPackage.WorkflowDocumentName"/> or a source name).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The document bytes, or <see langword="null"/> if the version or named document does not exist.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken);

    /// <summary>Updates a version's mutable governance metadata.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="owner">A replacement owner, if changing it.</param>
    /// <param name="tags">A replacement tag set, if changing it.</param>
    /// <param name="status">A new status, if changing it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated version, or <see langword="null"/> if it does not exist.</returns>
    ValueTask<CatalogVersion?> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, IReadOnlyList<string>? tags, CatalogStatus? status, CancellationToken cancellationToken);

    /// <summary>Deletes a single version, refusing while any workflow run references its versioned workflow id.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome — deleted, not found, or refused because runs still reference it.</returns>
    ValueTask<CatalogDeleteOutcome> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>Bulk-deletes obsolete versions that have no referencing workflow runs.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of versions purged.</returns>
    ValueTask<int> PurgeAsync(CancellationToken cancellationToken);
}

/// <summary>The outcome of a single-version delete.</summary>
public enum CatalogDeleteOutcome
{
    /// <summary>The version was deleted.</summary>
    Deleted,

    /// <summary>No version with that base id and number exists.</summary>
    NotFound,

    /// <summary>The version was not deleted because one or more workflow runs still reference it.</summary>
    Referenced,
}