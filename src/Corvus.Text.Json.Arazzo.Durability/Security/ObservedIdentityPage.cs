// <copyright file="ObservedIdentityPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of observed identities (design §16.5.4): the prefix-matched identities for the page, ordered by
/// <c>(subjectValue, subjectKind)</c>, plus an opaque <see cref="NextPageToken"/> to fetch the next page (empty when this
/// is the last page). The <see cref="Identities"/> are pooled documents the caller owns, and the token (when present) is
/// held in a pooled buffer — <see cref="Dispose"/> the page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{ObservedIdentity}, string, string)"/>
/// Base64URL-encodes the <c>(subjectValue, subjectKind)</c> cursor straight into a pooled buffer (no token string),
/// exposed as <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response. The buffer outlives the
/// synchronous response build and is returned on <see cref="Dispose"/>.
/// </remarks>
public sealed class ObservedIdentityPage : IDisposable
{
    private byte[]? rentedToken;

    private ObservedIdentityPage(PooledDocumentList<ObservedIdentity> identities, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Identities = identities;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's identities, ordered by <c>(subjectValue, subjectKind)</c>.</summary>
    public PooledDocumentList<ObservedIdentity> Identities { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="identities">The page's prefix-matched identities.</param>
    /// <returns>The page.</returns>
    public static ObservedIdentityPage Create(PooledDocumentList<ObservedIdentity> identities)
        => new(identities, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(subjectValue, subjectKind)</c> cursor
    /// straight into a pooled buffer (no intermediate token string).</summary>
    /// <param name="identities">The page's prefix-matched identities.</param>
    /// <param name="subjectValue">The last row's subject value.</param>
    /// <param name="subjectKind">The last row's subject kind token (the tie-breaker).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static ObservedIdentityPage Create(PooledDocumentList<ObservedIdentity> identities, string subjectValue, string subjectKind)
    {
        int maxLength = ObservedIdentityContinuationToken.GetMaxEncodedLength(subjectValue, subjectKind);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = ObservedIdentityContinuationToken.EncodeToUtf8(subjectValue, subjectKind, buffer);
            return new ObservedIdentityPage(identities, buffer.AsMemory(0, written), buffer);
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
        this.Identities.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}