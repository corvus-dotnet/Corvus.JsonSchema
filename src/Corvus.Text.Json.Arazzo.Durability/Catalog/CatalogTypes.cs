// <copyright file="CatalogTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>A catalog version's lifecycle status.</summary>
public enum CatalogStatus
{
    /// <summary>The version is current and usable.</summary>
    Active,

    /// <summary>The version has been retired; it remains addressable (and referenced runs keep working) until purged.</summary>
    Obsolete,
}

/// <summary>The accountable governance owner of a cataloged workflow, for integration with governance tooling.</summary>
/// <param name="Name">The owning person or team's display name.</param>
/// <param name="Email">A contact email (an <c>idn-email</c>).</param>
/// <param name="Team">An optional owning team/group.</param>
/// <param name="Url">An optional link (e.g. a runbook or owning-team page), an <c>iri</c>.</param>
public readonly record struct CatalogOwner(string Name, string Email, string? Team = null, string? Url = null);

/// <summary>A reference to one source document within a version's package, surfaced in the metadata so a client
/// knows which documents are individually addressable.</summary>
/// <param name="Name">The source name (the workflow's <c>sourceDescriptions</c> name), used to address the document.</param>
/// <param name="Type">The source document kind (e.g. <c>openapi</c>, <c>asyncapi</c>), if the workflow declared it.</param>
public readonly record struct CatalogSourceRef(string Name, string? Type = null);

/// <summary>The minimal identity of a catalog version — its base id, version number and the versioned workflow id
/// runs execute under. Used for purge ref-checks.</summary>
/// <param name="BaseWorkflowId">The base workflow id (no version suffix).</param>
/// <param name="VersionNumber">The 1-based version number under the base id.</param>
/// <param name="WorkflowId">The versioned workflow id (<c>{baseWorkflowId}-v{versionNumber}</c>).</param>
public readonly record struct CatalogVersionRef(string BaseWorkflowId, int VersionNumber, string WorkflowId);

/// <summary>The governance metadata supplied when a version is added (the actor and the mutable governance fields
/// set at creation). The immutable identity, hash, title/description and sources are derived from the package by
/// the store.</summary>
/// <param name="Owner">The accountable governance owner.</param>
/// <param name="CreatedBy">The authenticated actor adding the version (recorded for governance + audit).</param>
/// <param name="Tags">Free-form tags for display and filtering (AND-matched on search), if any.</param>
public readonly record struct CatalogMetadata(CatalogOwner Owner, string CreatedBy, IReadOnlyList<string>? Tags = null);

/// <summary>A partial update of a version's mutable governance metadata; an unset field is left unchanged. Setting
/// <see cref="Status"/> to <see cref="CatalogStatus.Obsolete"/> records the obsoletion as a distinct governance event.</summary>
/// <param name="UpdatedBy">The authenticated actor performing the update (recorded as <c>lastUpdatedBy</c>, and as
/// <c>obsoletedBy</c> when this update obsoletes the version).</param>
/// <param name="Owner">A replacement owner, if set.</param>
/// <param name="Tags">A replacement tag set, if set.</param>
/// <param name="Status">A new status, if set.</param>
public readonly record struct CatalogMetadataPatch(
    string UpdatedBy,
    CatalogOwner? Owner = null,
    IReadOnlyList<string>? Tags = null,
    CatalogStatus? Status = null);

/// <summary>
/// A catalog version's metadata — the immutable identity/hash/title/description/sources derived from the package,
/// plus the mutable governance fields and audit attribution. The package documents themselves are never embedded
/// here; they are fetched via <see cref="IWorkflowCatalogStore.GetPackageAsync"/> / <see cref="IWorkflowCatalogStore.GetDocumentAsync"/>.
/// </summary>
/// <param name="BaseWorkflowId">The base workflow id (no version suffix).</param>
/// <param name="VersionNumber">The 1-based version number assigned by the store.</param>
/// <param name="WorkflowId">The versioned workflow id (<c>{baseWorkflowId}-v{versionNumber}</c>) runs execute under.</param>
/// <param name="Title">The title, extracted from the workflow's <c>info.title</c>.</param>
/// <param name="Description">The description, from the workflow's <c>info.description</c> (falling back to <c>info.summary</c>), if any.</param>
/// <param name="Status">The lifecycle status.</param>
/// <param name="Tags">The free-form tags.</param>
/// <param name="Owner">The accountable governance owner.</param>
/// <param name="Sources">The source documents in the package (name + type), addressable individually.</param>
/// <param name="Hash">The SHA-256 of the canonical package, hex-encoded.</param>
/// <param name="CreatedBy">The actor that added the version.</param>
/// <param name="CreatedAt">When the version was added.</param>
/// <param name="LastUpdatedBy">The actor of the last metadata change, if any.</param>
/// <param name="LastUpdatedAt">When the metadata was last changed, if ever.</param>
/// <param name="ObsoletedBy">The actor that marked the version obsolete, if it is.</param>
/// <param name="ObsoletedAt">When the version was marked obsolete, if it is.</param>
/// <param name="Runnable">Whether the package carries a compiled workflow executor assembly an execution host can run.</param>
public sealed record CatalogVersion(
    string BaseWorkflowId,
    int VersionNumber,
    string WorkflowId,
    string Title,
    string? Description,
    CatalogStatus Status,
    IReadOnlyList<string> Tags,
    CatalogOwner Owner,
    IReadOnlyList<CatalogSourceRef> Sources,
    string Hash,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? LastUpdatedBy = null,
    DateTimeOffset? LastUpdatedAt = null,
    string? ObsoletedBy = null,
    DateTimeOffset? ObsoletedAt = null,
    bool Runnable = false)
{
    /// <summary>Gets the minimal identity reference for this version.</summary>
    public CatalogVersionRef Ref => new(this.BaseWorkflowId, this.VersionNumber, this.WorkflowId);
}