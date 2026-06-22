// <copyright file="CatalogPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A page of catalog versions matching a <see cref="CatalogQuery"/> (metadata only — no documents): the matching versions
/// ordered by (base workflow id, version number), plus an opaque <see cref="NextPageToken"/> to fetch the next page (empty
/// when this is the last page). The <see cref="Versions"/> are pooled documents the caller owns, and the token (when
/// present) is held in a pooled buffer — <see cref="Dispose"/> the page once the response has been projected to return
/// both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{CatalogVersion}, string)"/> Base64URL-encodes the
/// last version's <c>(baseWorkflowId, versionNumber)</c> sort-key cursor straight into a pooled buffer (no token string),
/// exposed as <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response. The buffer outlives the
/// synchronous response build and is returned on <see cref="Dispose"/>. This is a class, not a record struct: it owns a
/// rented buffer, which a value-copy of a struct would double-return on dispose.
/// </remarks>
public sealed class CatalogPage : IDisposable
{
    private byte[]? rentedToken;

    private CatalogPage(PooledDocumentList<CatalogVersion> versions, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Versions = versions;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the matching versions (at most <see cref="CatalogQuery.Limit"/>), ordered by (base workflow id, version number).</summary>
    public PooledDocumentList<CatalogVersion> Versions { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="versions">The matching versions.</param>
    /// <returns>The page.</returns>
    public static CatalogPage Create(PooledDocumentList<CatalogVersion> versions)
        => new(versions, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last version's keyset sort-key cursor straight into
    /// a pooled buffer (no intermediate token string).</summary>
    /// <param name="versions">The matching versions.</param>
    /// <param name="sortKey">The last version's <c>(baseWorkflowId, versionNumber)</c> sort key (the keyset cursor the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static CatalogPage Create(PooledDocumentList<CatalogVersion> versions, string sortKey)
    {
        int maxLength = WorkflowContinuationToken.GetMaxEncodedLength(sortKey);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = WorkflowContinuationToken.EncodeToUtf8(sortKey, buffer);
            return new CatalogPage(versions, buffer.AsMemory(0, written), buffer);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Versions.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}