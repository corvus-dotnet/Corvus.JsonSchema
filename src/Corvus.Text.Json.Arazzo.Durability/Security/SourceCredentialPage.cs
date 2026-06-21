// <copyright file="SourceCredentialPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of source credential bindings (design §13): the reach-visible bindings for the page, ordered by
/// <c>(sourceName, environment)</c>, plus an opaque <see cref="NextPageToken"/> to fetch the next page (empty when this
/// is the last page). The <see cref="Bindings"/> are pooled documents the caller owns, and the token (when present) is
/// held in a pooled buffer — <see cref="Dispose"/> the page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{SourceCredentialBinding}, string, string, string)"/>
/// Base64URL-encodes the cursor straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/>
/// UTF-8 the handler writes verbatim into the response. The buffer outlives the synchronous response build and is
/// returned on <see cref="Dispose"/>.
/// </remarks>
public sealed class SourceCredentialPage : IDisposable
{
    private byte[]? rentedToken;

    private SourceCredentialPage(PooledDocumentList<SourceCredentialBinding> bindings, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Bindings = bindings;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's reach-visible bindings, ordered by <c>(sourceName, environment)</c>.</summary>
    public PooledDocumentList<SourceCredentialBinding> Bindings { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="bindings">The page's reach-visible bindings.</param>
    /// <returns>The page.</returns>
    public static SourceCredentialPage Create(PooledDocumentList<SourceCredentialBinding> bindings)
        => new(bindings, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(sourceName, environment, tie-breaker)</c>
    /// cursor straight into a pooled buffer (no intermediate token string).</summary>
    /// <param name="bindings">The page's reach-visible bindings.</param>
    /// <param name="sourceName">The last row's source name.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (whatever the store orders on).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static SourceCredentialPage Create(PooledDocumentList<SourceCredentialBinding> bindings, string sourceName, string environment, string tieBreaker)
    {
        int maxLength = SourceCredentialContinuationToken.GetMaxEncodedLength(sourceName, environment, tieBreaker);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = SourceCredentialContinuationToken.EncodeToUtf8(sourceName, environment, tieBreaker, buffer);
            return new SourceCredentialPage(bindings, buffer.AsMemory(0, written), buffer);
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
        this.Bindings.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}