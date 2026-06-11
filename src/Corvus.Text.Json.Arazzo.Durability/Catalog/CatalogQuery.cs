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
/// <param name="Tags">Restrict to versions carrying every one of these tags (AND), if set.</param>
/// <param name="Status">Restrict to versions in this status, if set.</param>
/// <param name="Owner">Restrict to versions whose owner name or email contains this value (case-insensitive), if set.</param>
/// <param name="Limit">The maximum number of versions to return in this page.</param>
/// <param name="ContinuationToken">The opaque token from a previous page's <see cref="CatalogPage.ContinuationToken"/>, or
/// <see langword="null"/> for the first page.</param>
public readonly record struct CatalogQuery(
    string? Text = null,
    string? BaseWorkflowId = null,
    IReadOnlyList<string>? Tags = null,
    CatalogStatus? Status = null,
    string? Owner = null,
    int Limit = 100,
    string? ContinuationToken = null);

/// <summary>A page of catalog versions matching a <see cref="CatalogQuery"/> (metadata only — no documents).</summary>
/// <param name="Versions">The matching versions (at most <see cref="CatalogQuery.Limit"/>), ordered by (base workflow id, version number).</param>
/// <param name="ContinuationToken">The opaque token to pass as the next query's <see cref="CatalogQuery.ContinuationToken"/>,
/// or <see langword="null"/> when this is the last page.</param>
public readonly record struct CatalogPage(IReadOnlyList<CatalogVersion> Versions, string? ContinuationToken = null);