// <copyright file="SourcePage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// One keyset page of sources (design §7.6): the reach-visible sources for the page, ordered by <c>name</c>, plus an
/// opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The <see cref="Sources"/>
/// are pooled documents the caller owns (each the full stored source, including its document — the handler field-selects
/// the summary), and the token (when present) is held in a pooled buffer — <see cref="Dispose"/> the page once read to
/// return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{RegisteredSource}, string, string)"/> Base64URL-encodes
/// the cursor straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/> UTF-8 the handler
/// writes verbatim into the response. The buffer outlives the synchronous response build and is returned on
/// <see cref="Dispose"/>. A carrier that owns a rented buffer is a <see langword="sealed"/> <see langword="class"/>,
/// never a record struct (a value-copy would double-return the rent).
/// </remarks>
public sealed class SourcePage : IDisposable
{
    private byte[]? rentedToken;

    private SourcePage(PooledDocumentList<RegisteredSource> sources, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Sources = sources;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's reach-visible sources, ordered by <c>name</c>.</summary>
    public PooledDocumentList<RegisteredSource> Sources { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="sources">The page's reach-visible sources.</param>
    /// <returns>The page.</returns>
    public static SourcePage Create(PooledDocumentList<RegisteredSource> sources)
        => new(sources, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(name, tie-breaker)</c> cursor
    /// straight into a pooled buffer (no intermediate token string).</summary>
    /// <param name="sources">The page's reach-visible sources.</param>
    /// <param name="name">The last row's source name.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (the tag discriminator).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static SourcePage Create(PooledDocumentList<RegisteredSource> sources, string name, string tieBreaker)
    {
        int maxLength = SourceContinuationToken.GetMaxEncodedLength(name, tieBreaker);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = SourceContinuationToken.EncodeToUtf8(name, tieBreaker, buffer);
            return new SourcePage(sources, buffer.AsMemory(0, written), buffer);
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
        this.Sources.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}