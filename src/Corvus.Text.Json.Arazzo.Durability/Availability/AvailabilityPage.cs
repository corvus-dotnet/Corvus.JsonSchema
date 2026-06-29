// <copyright file="AvailabilityPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// One keyset page of availability entries (design §7.8): the entries for the page (the environments a version is
/// available in, or the versions available in an environment) plus an opaque <see cref="NextPageToken"/> to fetch the
/// next page (empty when this is the last page). The <see cref="Entries"/> are pooled documents the caller owns, and the
/// token (when present) is held in a pooled buffer — <see cref="Dispose"/> the page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{AvailabilityEntry}, string, int, string)"/>
/// Base64URL-encodes the last row's full key straight into a pooled buffer (no token string), exposed as
/// <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response. A carrier that owns a rented buffer
/// is a <see langword="sealed"/> <see langword="class"/>, never a record struct (a value-copy would double-return the rent).
/// </remarks>
public sealed class AvailabilityPage : IDisposable
{
    /// <summary>The default page size when a non-positive limit is supplied.</summary>
    public const int DefaultPageSize = 50;

    private byte[]? rentedToken;

    private AvailabilityPage(PooledDocumentList<AvailabilityEntry> entries, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Entries = entries;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's availability entries.</summary>
    public PooledDocumentList<AvailabilityEntry> Entries { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="entries">The page's availability entries.</param>
    /// <returns>The page.</returns>
    public static AvailabilityPage Create(PooledDocumentList<AvailabilityEntry> entries)
        => new(entries, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's full key straight into a pooled buffer.</summary>
    /// <param name="entries">The page's availability entries.</param>
    /// <param name="baseWorkflowId">The last row's base workflow id.</param>
    /// <param name="versionNumber">The last row's version number.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static AvailabilityPage Create(PooledDocumentList<AvailabilityEntry> entries, string baseWorkflowId, int versionNumber, string environment)
    {
        int maxLength = AvailabilityContinuationToken.GetMaxEncodedLength(baseWorkflowId, environment);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = AvailabilityContinuationToken.EncodeToUtf8(baseWorkflowId, versionNumber, environment, buffer);
            return new AvailabilityPage(entries, buffer.AsMemory(0, written), buffer);
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
        this.Entries.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}