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
    ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, CancellationToken cancellationToken);

    /// <summary>Adds a workflow version with security tags (KVP labels for row authorization, §14.2).</summary>
    /// <param name="packageUtf8">The package envelope as UTF-8 JSON.</param>
    /// <param name="owner">The accountable governance owner.</param>
    /// <param name="tags">Free-form tags, if any.</param>
    /// <param name="securityTags">Security tags (KVP labels) for row authorization, distinct from the free-form <paramref name="tags"/>, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The added version's metadata.</returns>
    /// <exception cref="ArgumentException">The package is malformed, or its workflow id already carries a <c>-vN</c> suffix.</exception>
    ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, SecurityTagSet securityTags, CancellationToken cancellationToken);

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
    ValueTask<CatalogVersion?> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, TagSet? tags, CatalogStatus? status, AccessContext context, CancellationToken cancellationToken);

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

    /// <summary>
    /// Gets the current administrator identities of a base workflow id (design §15): the workflow's explicit
    /// administrator record if one has been materialized, otherwise the single identity that stamped version 1. Each
    /// identity is a set of unforgeable, deployment-stamped <c>sys:</c> tags; <em>any</em> administrator may publish
    /// further versions and administer it. An unknown base id yields an empty list.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administrator identities, or empty if the base id is unknown.</returns>
    ValueTask<IReadOnlyList<SecurityTagSet>> GetAdministratorsAsync(string baseWorkflowId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds <paramref name="newAdministrator"/> to a base workflow id's administrator set (design §15), authorized by a
    /// current administrator. Idempotent: if it is already an administrator the set is unchanged. The first change
    /// materializes the explicit administrator record from the version-1-derived default.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="newAdministrator">The identity to add (the unforgeable, deployment-stamped tags of the new administrator).</param>
    /// <param name="callerIdentity">The caller's own stamped identity; the caller must currently be an administrator.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administrator identities after the change.</returns>
    /// <exception cref="NotSupportedException">No administrator store is configured.</exception>
    /// <exception cref="WorkflowAdministrationException">The caller is not a current administrator (or the base id is unknown).</exception>
    ValueTask<IReadOnlyList<SecurityTagSet>> AddAdministratorAsync(string baseWorkflowId, SecurityTagSet newAdministrator, SecurityTagSet callerIdentity, CancellationToken cancellationToken);

    /// <summary>
    /// Removes <paramref name="administrator"/> from a base workflow id's administrator set (design §15), authorized by
    /// a current administrator. Idempotent: removing a non-administrator leaves the set unchanged. Refuses to remove the
    /// <em>last</em> administrator — a workflow can never be orphaned.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="administrator">The identity to remove.</param>
    /// <param name="callerIdentity">The caller's own stamped identity; the caller must currently be an administrator.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administrator identities after the change.</returns>
    /// <exception cref="NotSupportedException">No administrator store is configured.</exception>
    /// <exception cref="WorkflowAdministrationException">The caller is not a current administrator (or the base id is unknown).</exception>
    /// <exception cref="ArgumentException">Removing <paramref name="administrator"/> would leave the workflow with no administrators.</exception>
    ValueTask<IReadOnlyList<SecurityTagSet>> RemoveAdministratorAsync(string baseWorkflowId, SecurityTagSet administrator, SecurityTagSet callerIdentity, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a base workflow id's entire administrator set with <paramref name="newAdministrators"/> (design §15) —
    /// the reassignment / hand-off operation — authorized by a current administrator. The caller need not appear in
    /// <paramref name="newAdministrators"/> (an administrator may transfer administration away from itself).
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="newAdministrators">The replacement identities (at least one; duplicates are coalesced).</param>
    /// <param name="callerIdentity">The caller's own stamped identity; the caller must currently be an administrator.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administrator identities after the change.</returns>
    /// <exception cref="NotSupportedException">No administrator store is configured.</exception>
    /// <exception cref="WorkflowAdministrationException">The caller is not a current administrator (or the base id is unknown).</exception>
    /// <exception cref="ArgumentException"><paramref name="newAdministrators"/> is empty.</exception>
    ValueTask<IReadOnlyList<SecurityTagSet>> TransferAdministrationAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> newAdministrators, SecurityTagSet callerIdentity, CancellationToken cancellationToken);
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