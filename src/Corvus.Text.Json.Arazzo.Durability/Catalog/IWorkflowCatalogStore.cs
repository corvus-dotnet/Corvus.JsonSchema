// <copyright file="IWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An immutable, content-hashed, versioned store of workflow document packages (an Arazzo workflow plus the
/// OpenAPI/AsyncAPI sources it references). Versions are grouped under a <em>base workflow id</em>; the store
/// assigns the version number, rewrites the workflow id to the versioned form, hashes the canonical package, and
/// projects the searchable/governance metadata — the same authoritative-store-with-index pattern the run store
/// (<see cref="IWorkflowStateStore"/>) uses, so a backend stays a thin adapter.
/// </summary>
/// <remarks>
/// Referential integrity (a version may not be deleted while runs reference its <see cref="CatalogVersion.WorkflowId"/>)
/// and audit telemetry are the management client's concern; the store is the persistence primitive.
/// </remarks>
public interface IWorkflowCatalogStore
{
    /// <summary>
    /// Adds a version: assigns the next version number for <paramref name="baseWorkflowId"/>, rewrites the
    /// package's workflow id to <c>{baseWorkflowId}-v{n}</c>, hashes the canonical package, persists it (so its
    /// documents stay individually retrievable), and returns the projected metadata.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (must not already carry a <c>-vN</c> suffix; the caller validates this).</param>
    /// <param name="packageUtf8">The submitted package envelope (<c>{ workflow, sources }</c>) as UTF-8 JSON.</param>
    /// <param name="metadata">The governance metadata (owner, tags) and the adding actor.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The added version's metadata.</returns>
    ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken);

    /// <summary>Gets a version's metadata (no documents).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The version, or <see langword="null"/> if no such version exists.</returns>
    ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>Gets the whole stored canonical package envelope for a version.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The canonical package bytes, or <see langword="null"/> if no such version exists.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a single, individually-addressable document from a version's package: the name
    /// <see cref="CatalogPackage.WorkflowDocumentName"/> returns the Arazzo workflow document, any other name
    /// returns the matching <c>sourceDescriptions</c> document.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="documentName">The document name (<see cref="CatalogPackage.WorkflowDocumentName"/> or a source name).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The document's JSON bytes, or <see langword="null"/> if the version or named document does not exist.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken);

    /// <summary>Searches the catalog, returning a page of version metadata (no documents).</summary>
    /// <param name="query">The search.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of matching versions.</returns>
    ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken);

    /// <summary>Updates a version's mutable governance metadata (owner, tags, status), stamping the audit fields.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="patch">The metadata changes and the updating actor.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated version, or <see langword="null"/> if no such version exists.</returns>
    ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken);

    /// <summary>Deletes a single version. The caller is responsible for the referential-integrity check first.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a version was deleted; <see langword="false"/> if no such version existed.</returns>
    ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);

    /// <summary>Lists the references of every obsolete version, for a purge's referential-integrity sweep.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The obsolete versions' references.</returns>
    ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken);

    /// <summary>Deletes many versions by reference (the unreferenced obsolete versions a purge selected).</summary>
    /// <param name="versions">The versions to delete.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the versions are deleted.</returns>
    ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken);
}