// <copyright file="WorkflowContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Encodes and decodes the opaque continuation token used to page <see cref="WorkflowQuery"/> results. Stores
/// page by ascending run id (keyset paging), so the token is just the last run id of a page, base64url-encoded
/// to keep it opaque and URL-safe. Centralised here so every backend pages identically.
/// </summary>
public static class WorkflowContinuationToken
{
    /// <summary>Encodes the last run id of a page into a continuation token.</summary>
    /// <param name="lastRunId">The last run id returned in the page.</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string lastRunId)
    {
        ArgumentNullException.ThrowIfNull(lastRunId);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(lastRunId));
    }

    /// <summary>
    /// Builds a page from rows a store fetched with <c>limit + 1</c> ordered by ascending run id: if more than
    /// <paramref name="limit"/> rows came back there is a next page, so the extra row is dropped and a
    /// continuation token is emitted from the last kept row.
    /// </summary>
    /// <param name="rows">The fetched rows (at most <paramref name="limit"/> + 1), ascending by run id. Mutated in place.</param>
    /// <param name="limit">The page size requested by the query.</param>
    /// <returns>The page, with a continuation token when a next page exists.</returns>
    public static WorkflowRunPage Paginate(List<WorkflowRunListing> rows, int limit)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            return new WorkflowRunPage(rows, Encode(rows[^1].Id.Value));
        }

        return new WorkflowRunPage(rows);
    }

    /// <summary>Decodes a continuation token back to the run id to resume after.</summary>
    /// <param name="token">A token from a previous page, or <see langword="null"/> for the first page.</param>
    /// <returns>The run id to page after (exclusive), or <see langword="null"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static string? Decode(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException ex)
        {
            throw new FormatException("The continuation token is malformed.", ex);
        }
    }
}