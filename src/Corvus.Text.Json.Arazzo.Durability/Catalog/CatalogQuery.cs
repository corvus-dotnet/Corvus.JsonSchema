// <copyright file="CatalogQuery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A search over the catalog: free-text over title/description, plus base-id, tag (AND-matched), status and owner
/// filters. Keyset-paged by (base workflow id, version number) so every backend pages identically.
/// </summary>
/// <param name="Text">Free-text matched (case-insensitive contains) against each version's title and description, if set.</param>
/// <param name="BaseWorkflowId">Restrict to versions of this base workflow id (exact), if set.</param>
/// <param name="WorkflowIdPrefix">Restrict to versions whose versioned workflow id starts with this prefix
/// (case-insensitive), if set. An anchored, index-friendly prefix match for type-ahead — because the versioned
/// id begins with the base id, a base-name prefix matches all of that workflow's versions.</param>
/// <param name="Tags">Restrict to versions carrying every one of these tags (AND), if set.</param>
/// <param name="Status">Restrict to versions in this status, if set.</param>
/// <param name="Owner">Restrict to versions whose owner name or email contains this value (case-insensitive), if set.</param>
/// <param name="Limit">The maximum number of versions to return in this page.</param>
/// <param name="ContinuationToken">The opaque token from a previous page's <see cref="CatalogPage.NextPageToken"/> as its
/// JSON value, or undefined for the first page. Carried bytes-native: the backend decodes it straight from the request
/// UTF-8 (no managed token string).</param>
/// <param name="Security">A row-authorization filter restricting results to versions whose security tags satisfy the principal's rule(s) (§14.2); <see langword="null"/> is unrestricted.</param>
public readonly record struct CatalogQuery(
    string? Text = null,
    string? BaseWorkflowId = null,
    string? WorkflowIdPrefix = null,
    TagSet Tags = default,
    CatalogStatus? Status = null,
    string? Owner = null,
    int Limit = 100,
    JsonString ContinuationToken = default,
    SecurityFilter? Security = null);