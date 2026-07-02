// <copyright file="ISecuredWorkflowCatalog.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using AdministratorIdentity = Corvus.Text.Json.Arazzo.Durability.Security.WorkflowAdministrators.AdministratorIdentity;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The catalog control surface: add/search/inspect/govern workflow document packages, with referential
/// integrity against workflow runs. It is the catalog analogue of <see cref="ISecuredWorkflowManagement"/> —
/// it coordinates the catalog store (<see cref="IWorkflowCatalogStore"/>) with the run store for delete/purge
/// safety and emits the audit telemetry, so the REST handler stays a thin translation layer.
/// </summary>
public interface ISecuredWorkflowCatalog
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
    ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, CancellationToken cancellationToken);

    /// <summary>Adds a workflow version with security tags (KVP labels for row authorization, §14.2).</summary>
    /// <param name="packageUtf8">The package envelope as UTF-8 JSON.</param>
    /// <param name="owner">The accountable governance owner.</param>
    /// <param name="tags">Free-form tags, if any.</param>
    /// <param name="securityTags">Security tags (KVP labels) for row authorization, distinct from the free-form <paramref name="tags"/>, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The added version's metadata.</returns>
    /// <exception cref="ArgumentException">The package is malformed, or its workflow id already carries a <c>-vN</c> suffix.</exception>
    ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, SecurityTagSet securityTags, CancellationToken cancellationToken);

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
    ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken);

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
    /// <param name="securityTags">The caller's new <em>non-internal</em> security tags, if re-tagging the version
    /// (§14.2); <see langword="null"/> leaves the security tags unchanged. The wrapper preserves the version's existing
    /// deployment-internal tags and persists the merged result — internal tags are never dropped or user-editable.</param>
    /// <param name="internalTagPrefix">The reserved internal-tag key prefix, used to preserve the internal tags on
    /// re-tag; ignored when <paramref name="securityTags"/> is <see langword="null"/>.</param>
    /// <param name="context">The caller's access grant; the version must be within its write reach.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on <see cref="CatalogUpdateOutcome.Updated"/>, the updated version document (which the
    /// caller owns): <see cref="CatalogUpdateOutcome.NotFound"/> if it is absent or outside read reach (non-disclosing),
    /// <see cref="CatalogUpdateOutcome.Forbidden"/> if readable but outside write reach.</returns>
    ValueTask<CatalogUpdateResult> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, TagSet? tags, CatalogStatus? status, SecurityTagSet? securityTags, string? internalTagPrefix, AccessContext context, CancellationToken cancellationToken);

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
    /// Gets the current administration record of a base workflow id (design §15): the workflow's explicit administrator
    /// record if one has been materialized, otherwise a synthetic (display-only) record carrying the single identity that
    /// stamped version 1. Each administrator identity carries its unforgeable, deployment-stamped <c>sys:</c> tags plus the
    /// resolved grantee <c>kind</c>/<c>label</c> (when known); <em>any</em> administrator may publish further versions and
    /// administer it. An unknown base id yields <see langword="null"/>.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administration record as a pooled document the caller must dispose, or <see langword="null"/> if the base id is unknown.</returns>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAdministratorsAsync(string baseWorkflowId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the base workflow ids the given caller identity administers — the approver inbox's workflow set (design
    /// §15.4/§16.5), resolved from the reverse administration index by the caller's identity digest. Returns an empty list
    /// when the caller administers nothing (or no administrator store is configured). The set is bounded by the caller's
    /// administrative scope and is materialized in full, since the inbox then lists access requests across it.
    /// </summary>
    /// <param name="callerIdentity">The caller's resolved <c>sys:</c> identity (its digest is the reverse-index key).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The base workflow ids the caller administers (possibly empty), ordered by base workflow id.</returns>
    ValueTask<IReadOnlyList<string>> ListAdministeredWorkflowsAsync(SecurityTagSet callerIdentity, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a resolved administrator identity to a base workflow id's administrator set (design §15), authorized by a
    /// current administrator. Idempotent: if its identity is already an administrator the set is unchanged. The first change
    /// materializes the explicit administrator record from the version-1-derived default.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix) — the admin-store/catalog key (a string leaf).</param>
    /// <param name="identity">The resolved identity to add (the unforgeable, deployment-stamped <c>sys:</c> tags of the new administrator).</param>
    /// <param name="kind">The resolved grantee kind (person/team/role/workflow), written when <paramref name="hasKind"/>.</param>
    /// <param name="hasKind">Whether <paramref name="kind"/> is present.</param>
    /// <param name="label">The display label, written when <paramref name="hasLabel"/>.</param>
    /// <param name="hasLabel">Whether <paramref name="label"/> is present.</param>
    /// <param name="callerIdentity">The caller's own stamped identity; the caller must currently be an administrator.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administration record after the change, as a pooled document the caller must dispose.</returns>
    /// <exception cref="NotSupportedException">No administrator store is configured.</exception>
    /// <exception cref="WorkflowAdministrationException">The caller is not a current administrator (or the base id is unknown).</exception>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>> AddAdministratorAsync(string baseWorkflowId, SecurityTagSet identity, AdministratorIdentity.KindEntity kind, bool hasKind, JsonString label, bool hasLabel, SecurityTagSet callerIdentity, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the administrator whose resolved-identity digest equals <paramref name="digest"/> from a base workflow id's
    /// administrator set (design §15), authorized by a current administrator. Idempotent: an unmatched digest leaves the set
    /// unchanged. Refuses to remove the <em>last</em> administrator — a workflow can never be orphaned.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="digest">The lower-case hex SHA-256 identity digest of the administrator to remove (see <see cref="SecurityIdentityDigest"/>).</param>
    /// <param name="callerIdentity">The caller's own stamped identity; the caller must currently be an administrator.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administration record after the change, as a pooled document the caller must dispose.</returns>
    /// <exception cref="NotSupportedException">No administrator store is configured.</exception>
    /// <exception cref="WorkflowAdministrationException">The caller is not a current administrator (or the base id is unknown).</exception>
    /// <exception cref="ArgumentException">Removing the matched administrator would leave the workflow with no administrators.</exception>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>> RemoveAdministratorAsync(string baseWorkflowId, string digest, SecurityTagSet callerIdentity, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a base workflow id's entire administrator set with <paramref name="newAdministrators"/> (design §15) —
    /// the reassignment / hand-off operation — authorized by a current administrator. The caller need not appear in
    /// <paramref name="newAdministrators"/> (an administrator may transfer administration away from itself).
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id (no <c>-vN</c> suffix).</param>
    /// <param name="newAdministrators">The replacement resolved identities (at least one; duplicates are coalesced by identity).
    /// Each is a resolved <c>sys:</c> identity (tags); the bulk hand-off form carries no per-administrator kind/label.</param>
    /// <param name="callerIdentity">The caller's own stamped identity; the caller must currently be an administrator.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The administration record after the change, as a pooled document the caller must dispose.</returns>
    /// <exception cref="NotSupportedException">No administrator store is configured.</exception>
    /// <exception cref="WorkflowAdministrationException">The caller is not a current administrator (or the base id is unknown).</exception>
    /// <exception cref="ArgumentException"><paramref name="newAdministrators"/> is empty.</exception>
    ValueTask<ParsedJsonDocument<WorkflowAdministrators>> TransferAdministrationAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> newAdministrators, SecurityTagSet callerIdentity, CancellationToken cancellationToken);
}

/// <summary>The outcome of a single-version delete.</summary>
public enum CatalogDeleteOutcome
{
    /// <summary>The version was deleted.</summary>
    Deleted,

    /// <summary>No version with that base id and number exists, or it is outside the caller's read reach (non-disclosing).</summary>
    NotFound,

    /// <summary>The version was not deleted because one or more workflow runs still reference it.</summary>
    Referenced,

    /// <summary>The version is within the caller's read reach but outside its write reach.</summary>
    Forbidden,
}

/// <summary>The outcome of a version metadata update.</summary>
public enum CatalogUpdateOutcome
{
    /// <summary>The version was updated; the result carries the updated document.</summary>
    Updated,

    /// <summary>No version with that base id and number exists, or it is outside the caller's read reach (non-disclosing).</summary>
    NotFound,

    /// <summary>The version is within the caller's read reach but outside its write reach.</summary>
    Forbidden,
}

/// <summary>
/// The result of a version metadata update: the outcome and, when <see cref="CatalogUpdateOutcome.Updated"/>, the updated
/// document. The caller owns <see cref="Document"/> on the <c>Updated</c> outcome and must dispose it (or hand it to a
/// workspace); it is <see langword="null"/> for the <c>NotFound</c>/<c>Forbidden</c> outcomes.
/// </summary>
/// <param name="Outcome">The update outcome.</param>
/// <param name="Document">The updated version document (only on <see cref="CatalogUpdateOutcome.Updated"/>).</param>
public readonly record struct CatalogUpdateResult(CatalogUpdateOutcome Outcome, ParsedJsonDocument<CatalogVersion>? Document);