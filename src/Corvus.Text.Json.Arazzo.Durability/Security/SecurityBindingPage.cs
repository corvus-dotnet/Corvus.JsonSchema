// <copyright file="SecurityBindingPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of security bindings (design §14.2): the bindings for the page, ordered by <c>(order, id)</c>, plus an
/// opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The
/// <see cref="Bindings"/> are pooled documents the caller owns, and the token (when present) is held in a pooled buffer —
/// <see cref="Dispose"/> the page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{SecurityBindingDocument}, int, string)"/>
/// Base64URL-encodes the last row's <c>(order, id)</c> straight into a pooled buffer (no token string), exposed as
/// <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response. The buffer outlives the synchronous
/// response build and is returned on <see cref="Dispose"/>.
/// </remarks>
public sealed class SecurityBindingPage : IDisposable
{
    /// <summary>The page size used when a caller passes a non-positive limit — the store contract's default, shared by the
    /// in-memory pager and every backend's native keyset query so they page identically.</summary>
    public const int DefaultPageSize = 50;

    private byte[]? rentedToken;

    private SecurityBindingPage(PooledDocumentList<SecurityBindingDocument> bindings, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Bindings = bindings;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's bindings, ordered by <c>(order, id)</c>.</summary>
    public PooledDocumentList<SecurityBindingDocument> Bindings { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="bindings">The page's bindings.</param>
    /// <returns>The page.</returns>
    public static SecurityBindingPage Create(PooledDocumentList<SecurityBindingDocument> bindings)
        => new(bindings, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(order, id)</c> straight into a pooled
    /// buffer (no intermediate token string, no id string).</summary>
    /// <param name="bindings">The page's bindings.</param>
    /// <param name="lastOrder">The last row's order (the first keyset cursor part the next page resumes after).</param>
    /// <param name="lastIdUtf8">The last row's id as UTF-8 (the keyset tie-breaker the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static SecurityBindingPage Create(PooledDocumentList<SecurityBindingDocument> bindings, int lastOrder, ReadOnlySpan<byte> lastIdUtf8)
    {
        int maxLength = SecurityBindingContinuationToken.GetMaxEncodedLength(lastIdUtf8.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = SecurityBindingContinuationToken.EncodeToUtf8(lastOrder, lastIdUtf8, buffer);
            return new SecurityBindingPage(bindings, buffer.AsMemory(0, written), buffer);
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