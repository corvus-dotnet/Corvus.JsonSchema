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

/// <summary>How sensitive a catalog version's step-output payloads are for disclosure (design §14), distinct from
/// row-visibility security tags: it classifies the step-output PAYLOADS above who may see the run.</summary>
public enum OutputsSensitivity
{
    /// <summary>The default (also the meaning of an absent classification): the step journal is readable at the
    /// <c>runs:outputs:read</c> tier.</summary>
    Standard,

    /// <summary>The step journal is redacted from callers who hold only <c>runs:outputs:read</c>; reading it requires the
    /// stronger grant (write reach on the run).</summary>
    Sensitive,
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
/// <param name="SecurityTags">Security tags (KVP labels) — the input to tag-based row authorization (§14.2), distinct from the free-form <paramref name="Tags"/> — if any.</param>
public readonly record struct CatalogMetadata(CatalogOwner Owner, string CreatedBy, TagSet Tags = default, SecurityTagSet SecurityTags = default);

/// <summary>A partial update of a version's mutable governance metadata; an unset field is left unchanged. Setting
/// <see cref="Status"/> to <see cref="CatalogStatus.Obsolete"/> records the obsoletion as a distinct governance event.</summary>
/// <param name="UpdatedBy">The authenticated actor performing the update (recorded as <c>lastUpdatedBy</c>, and as
/// <c>obsoletedBy</c> when this update obsoletes the version).</param>
/// <param name="Owner">A replacement owner, if set.</param>
/// <param name="Tags">A replacement tag set, if set.</param>
/// <param name="Status">A new status, if set.</param>
/// <param name="SecurityTags">The <em>effective</em> replacement security-tag set, if the version is being re-tagged
/// (design §14.2) — <see langword="null"/> leaves the version's security tags unchanged. The set is already the final
/// tags to persist: the security wrapper has merged the caller's new non-internal labels with the version's preserved
/// deployment-internal tags, so a store simply replaces its stored/queryable representation with it.</param>
/// <param name="OutputsSensitivity">A reclassification of the version's step-output sensitivity (design §14), if set —
/// <see langword="null"/> leaves it unchanged. <c>Standard</c> clears the marker (the default); <c>Sensitive</c> redacts
/// the whole step journal from callers below the stronger grant.</param>
public readonly record struct CatalogMetadataPatch(
    string UpdatedBy,
    CatalogOwner? Owner = null,
    TagSet? Tags = null,
    CatalogStatus? Status = null,
    SecurityTagSet? SecurityTags = null,
    OutputsSensitivity? OutputsSensitivity = null);

// The catalog version's persisted metadata is the generated Corvus.Text.Json type CatalogVersion
// (see CatalogVersion.cs + Schemas/CatalogVersion.json) — the entity stores hold as JSON.