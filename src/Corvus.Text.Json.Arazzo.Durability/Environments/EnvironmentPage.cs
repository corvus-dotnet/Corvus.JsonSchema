// <copyright file="EnvironmentPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// One keyset page of environments (design §7.7): the reach-visible environments for the page, ordered by <c>name</c>,
/// plus an opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The
/// <see cref="Environments"/> are pooled documents the caller owns, and the token (when present) is held in a pooled
/// buffer — <see cref="Dispose"/> the page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{Environment}, string, string)"/>
/// Base64URL-encodes the cursor straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/>
/// UTF-8 the handler writes verbatim into the response. The buffer outlives the synchronous response build and is
/// returned on <see cref="Dispose"/>. A carrier that owns a rented buffer is a <see langword="sealed"/>
/// <see langword="class"/>, never a record struct (a value-copy would double-return the rent).
/// </remarks>
public sealed class EnvironmentPage : IDisposable
{
    private byte[]? rentedToken;

    private EnvironmentPage(PooledDocumentList<Environment> environments, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Environments = environments;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's reach-visible environments, ordered by <c>name</c>.</summary>
    public PooledDocumentList<Environment> Environments { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="environments">The page's reach-visible environments.</param>
    /// <returns>The page.</returns>
    public static EnvironmentPage Create(PooledDocumentList<Environment> environments)
        => new(environments, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(name, tie-breaker)</c> cursor
    /// straight into a pooled buffer (no intermediate token string).</summary>
    /// <param name="environments">The page's reach-visible environments.</param>
    /// <param name="name">The last row's environment name.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (the tag discriminator).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static EnvironmentPage Create(PooledDocumentList<Environment> environments, string name, string tieBreaker)
    {
        int maxLength = EnvironmentContinuationToken.GetMaxEncodedLength(name, tieBreaker);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = EnvironmentContinuationToken.EncodeToUtf8(name, tieBreaker, buffer);
            return new EnvironmentPage(environments, buffer.AsMemory(0, written), buffer);
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
        this.Environments.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}