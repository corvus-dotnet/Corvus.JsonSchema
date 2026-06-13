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

    /// <summary>Searches the catalog, scoped to the caller's read reach (§14.2).</summary>
    /// <param name="query">The search.</param>
    /// <param name="context">The caller's access grant; results are restricted to its read reach. Use <see cref="AccessContext.System"/> for the trusted system path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of matching version metadata the caller may read.</returns>
    ValueTask<CatalogPage> SearchAsync(CatalogQuery query, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Gets a version's metadata, if the caller's read reach admits it (§14.2).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="context">The caller's access grant. A version outside its read reach is reported as <see langword="null"/> — indistinguishable from absent (non-disclosing).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The version, or <see langword="null"/> if it does not exist or is not within read reach.</returns>
    ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Gets the whole canonical package envelope for a version, if the caller's read reach admits it (§14.2).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="context">The caller's access grant.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The package bytes, or <see langword="null"/> if the version does not exist or is not within read reach.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Gets a single document from a version's package, if the caller's read reach admits the owning version (§14.2).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="documentName">The document name (<see cref="CatalogPackage.WorkflowDocumentName"/> or a source name).</param>
    /// <param name="context">The caller's access grant.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The document bytes, or <see langword="null"/> if the version/document does not exist or is not within read reach.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Updates a version's mutable governance metadata, if the caller's write reach admits it (§14.2).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="owner">A replacement owner, if changing it.</param>
    /// <param name="tags">A replacement tag set, if changing it.</param>
    /// <param name="status">A new status, if changing it.</param>
    /// <param name="context">The caller's access grant; the version must be within its write reach.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated version, or <see langword="null"/> if it does not exist or is not within write reach.</returns>
    ValueTask<CatalogVersion?> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, IReadOnlyList<string>? tags, CatalogStatus? status, AccessContext context, CancellationToken cancellationToken);

    /// <summary>Deletes a single version, refusing while any workflow run references its versioned workflow id, if the caller's write reach admits it (§14.2).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="context">The caller's access grant; the version must be within its write reach.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome — deleted, not found (absent or outside read reach), or refused because runs still reference it.</returns>
    ValueTask<CatalogDeleteOutcome> DeleteAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Bulk-deletes obsolete, unreferenced versions within the caller's purge reach (§14.2). As with run purge,
    /// the capability to purge is orthogonal to reach — a tenant admin reaps only their tenant's obsolete versions,
    /// a service operator (<see cref="AccessContext.System"/>) reaps across tenants.
    /// </summary>
    /// <param name="context">The caller's access grant; only versions within its purge reach are reaped.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of versions purged.</returns>
    ValueTask<int> PurgeAsync(AccessContext context, CancellationToken cancellationToken);
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