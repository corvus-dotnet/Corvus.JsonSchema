// <copyright file="AccessRequestPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of access requests (design §16.5): the requests for the page, oldest-first by <c>(createdAt, id)</c>,
/// plus an opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The
/// <see cref="Requests"/> are pooled documents the caller owns, and the token (when present) is held in a pooled buffer —
/// <see cref="Dispose"/> the page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{AccessRequest}, long, ReadOnlySpan{byte})"/>
/// Base64URL-encodes the last row's <c>(createdAt, id)</c> straight into a pooled buffer (no token string), exposed as
/// <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response. The buffer outlives the synchronous
/// response build and is returned on <see cref="Dispose"/>.
/// </remarks>
public sealed class AccessRequestPage : IDisposable
{
    private byte[]? rentedToken;

    private AccessRequestPage(PooledDocumentList<AccessRequest> requests, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Requests = requests;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's access requests, oldest-first by <c>(createdAt, id)</c>.</summary>
    public PooledDocumentList<AccessRequest> Requests { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="requests">The page's requests.</param>
    /// <returns>The page.</returns>
    public static AccessRequestPage Create(PooledDocumentList<AccessRequest> requests)
        => new(requests, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(createdAt, id)</c> straight into a
    /// pooled buffer (no intermediate token string, no id string).</summary>
    /// <param name="requests">The page's requests.</param>
    /// <param name="lastUtcTicks">The last row's creation instant as UTC ticks (the first keyset cursor part).</param>
    /// <param name="lastIdUtf8">The last row's id as UTF-8 (the keyset tie-breaker the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static AccessRequestPage Create(PooledDocumentList<AccessRequest> requests, long lastUtcTicks, ReadOnlySpan<byte> lastIdUtf8)
    {
        int maxLength = AccessRequestContinuationToken.GetMaxEncodedLength(lastIdUtf8.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = AccessRequestContinuationToken.EncodeToUtf8(lastUtcTicks, lastIdUtf8, buffer);
            return new AccessRequestPage(requests, buffer.AsMemory(0, written), buffer);
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
        this.Requests.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}