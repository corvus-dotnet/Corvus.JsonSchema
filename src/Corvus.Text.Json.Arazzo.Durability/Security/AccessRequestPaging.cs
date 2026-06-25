// <copyright file="AccessRequestPaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared in-memory keyset paging of access requests, backing the default
/// <see cref="IAccessRequestStore.ListAsync(AccessRequestQuery, int, JsonString, System.Threading.CancellationToken)"/>
/// over a store's existing filtered read (design §16.5). The filter (status / base workflow / subject) is applied by
/// <see cref="IAccessRequestStore.ListAsync(AccessRequestQuery, System.Threading.CancellationToken)"/>; this only orders
/// the result oldest-first by <c>(createdAt, id)</c>, seeks strictly past the cursor, and bounds to the limit. A backend
/// overrides the paged method with a native keyset query so neither the full read nor this re-parse happens. It is
/// <b>bytes-native</b>: createdAt is an instant value (no string) and the id compares on its persisted UTF-8 span.
/// </summary>
internal static class AccessRequestPaging
{
    /// <summary>The page size used when a caller passes a non-positive limit.</summary>
    internal const int DefaultPageSize = 50;

    /// <summary>Pages <paramref name="filtered"/> (an already-filtered read) in memory: oldest-first by
    /// <c>(createdAt, id)</c>, resuming strictly after the cursor decoded from <paramref name="pageToken"/> (undefined =
    /// first page), bounded to <paramref name="limit"/>. Each page row is re-parsed into the returned (owned) page so the
    /// caller may dispose <paramref name="filtered"/>.</summary>
    /// <param name="filtered">The store's filtered read (the caller disposes it).</param>
    /// <param name="limit">The maximum requests in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque continuation token (its JSON value) to resume after, or undefined for the first page.</param>
    /// <returns>One keyset page, owning its pooled documents (and token buffer when more remain).</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    internal static AccessRequestPage PageInMemory(PooledDocumentList<AccessRequest> filtered, int limit, JsonString pageToken)
    {
        int pageSize = limit > 0 ? limit : DefaultPageSize;

        // The stable total order every backend pages by (id is unique), materialised here without a DB index. createdAt
        // compares as an instant value (no string); the id compares on its persisted UTF-8 (ordinal byte order == ordinal
        // code-point order; the token is backend-scoped so only self-consistency matters).
        var ordered = new List<AccessRequest>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
        {
            ordered.Add(filtered[i]);
        }

        ordered.Sort(static (x, y) =>
        {
            int byTime = x.CreatedAtValue.CompareTo(y.CreatedAtValue);
            if (byTime != 0)
            {
                return byTime;
            }

            using UnescapedUtf8JsonString xi = x.Id.GetUtf8String();
            using UnescapedUtf8JsonString yi = y.Id.GetUtf8String();
            return xi.Span.SequenceCompareTo(yi.Span);
        });

        byte[]? cursorBuffer = null;
        var page = new PooledDocumentList<AccessRequest>(Math.Min(pageSize, ordered.Count));
        try
        {
            // The cursor decodes (bytes-native) into a pooled buffer; the id stays a span into it for the scan.
            long cursorTicks = 0;
            ReadOnlySpan<byte> cursorId = default;
            bool hasCursor = false;
            if (pageToken.IsNotUndefined())
            {
                using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
                cursorBuffer = ArrayPool<byte>.Shared.Rent(AccessRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
                hasCursor = AccessRequestContinuationToken.TryDecode(tokenUtf8.Span, cursorBuffer, out cursorTicks, out cursorId);
            }

            bool hasMore = false;
            foreach (AccessRequest request in ordered)
            {
                if (hasCursor && !After(request, cursorTicks, cursorId))
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                if (page.Count == pageSize)
                {
                    hasMore = true; // a further row exists → there is a next page after the last included row
                    break;
                }

                // Re-parse into the page so it is independent of `filtered` (which the caller disposes). A native backend
                // query returns only the page, so it never pays this copy.
                page.Add(PersistedJson.ToPooledDocument<AccessRequest>(JsonMarshal.GetRawUtf8Value(request).Memory.Span));
            }

            if (!hasMore)
            {
                return AccessRequestPage.Create(page);
            }

            AccessRequest last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return AccessRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
        finally
        {
            if (cursorBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(cursorBuffer);
            }
        }
    }

    // True when (request.createdAt, request.id) sorts strictly after the cursor in the (createdAt asc, id ordinal asc) order.
    private static bool After(AccessRequest request, long cursorTicks, ReadOnlySpan<byte> cursorId)
    {
        long ticks = request.CreatedAtValue.UtcTicks;
        if (ticks != cursorTicks)
        {
            return ticks > cursorTicks;
        }

        using UnescapedUtf8JsonString id = request.Id.GetUtf8String();
        return id.Span.SequenceCompareTo(cursorId) > 0;
    }
}