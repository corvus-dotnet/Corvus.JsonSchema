// <copyright file="EnvironmentRunnerAuthorizationPaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>
/// Shared in-memory keyset paging of environment-runner authorizations, backing the default
/// <see cref="IEnvironmentRunnerAuthorizationStore.ListAsync(RunnerAuthorizationQuery, int, JsonString, System.Threading.CancellationToken)"/>
/// over a store's existing filtered read (design §5.5). The filter (status / environment / administered set) is applied by
/// <see cref="IEnvironmentRunnerAuthorizationStore.ListAsync(RunnerAuthorizationQuery, System.Threading.CancellationToken)"/>;
/// this only orders the result by <c>(environment, runnerId)</c>, seeks strictly past the cursor, and bounds to the limit. A
/// backend overrides the paged method with a native keyset query so neither the full read nor this re-parse happens. It is
/// <b>bytes-native</b>: both key parts compare on their persisted UTF-8 spans. Mirrors
/// <see cref="Availability.AvailabilityRequestPaging"/>.
/// </summary>
internal static class EnvironmentRunnerAuthorizationPaging
{
    /// <summary>The page size used when a caller passes a non-positive limit (the public store-contract default).</summary>
    internal const int DefaultPageSize = EnvironmentRunnerAuthorizationPage.DefaultPageSize;

    /// <summary>Pages <paramref name="filtered"/> (an already-filtered read) in memory: ordered by
    /// <c>(environment, runnerId)</c>, resuming strictly after the cursor decoded from <paramref name="pageToken"/>
    /// (undefined = first page), bounded to <paramref name="limit"/>. Each page row is re-parsed into the returned (owned)
    /// page so the caller may dispose <paramref name="filtered"/>.</summary>
    /// <param name="filtered">The store's filtered read (the caller disposes it).</param>
    /// <param name="limit">The maximum authorizations in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque continuation token (its JSON value) to resume after, or undefined for the first page.</param>
    /// <returns>One keyset page, owning its pooled documents (and token buffer when more remain).</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    internal static EnvironmentRunnerAuthorizationPage PageInMemory(PooledDocumentList<EnvironmentRunnerAuthorization> filtered, int limit, JsonString pageToken)
    {
        int pageSize = limit > 0 ? limit : DefaultPageSize;

        // The stable total order every backend pages by ((environment, runnerId) is unique), materialised here without a DB
        // index. Both parts compare on their persisted UTF-8 (ordinal byte order == ordinal code-point order; the token is
        // backend-scoped so only self-consistency matters).
        var ordered = new List<EnvironmentRunnerAuthorization>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
        {
            ordered.Add(filtered[i]);
        }

        ordered.Sort(static (x, y) =>
        {
            using UnescapedUtf8JsonString xe = x.Environment.GetUtf8String();
            using UnescapedUtf8JsonString ye = y.Environment.GetUtf8String();
            int byEnvironment = xe.Span.SequenceCompareTo(ye.Span);
            if (byEnvironment != 0)
            {
                return byEnvironment;
            }

            using UnescapedUtf8JsonString xr = x.RunnerId.GetUtf8String();
            using UnescapedUtf8JsonString yr = y.RunnerId.GetUtf8String();
            return xr.Span.SequenceCompareTo(yr.Span);
        });

        byte[]? cursorBuffer = null;
        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(Math.Min(pageSize, ordered.Count));
        try
        {
            // The cursor decodes (bytes-native) into a pooled buffer; both parts stay spans into it for the scan.
            ReadOnlySpan<byte> cursorEnvironment = default;
            ReadOnlySpan<byte> cursorRunnerId = default;
            bool hasCursor = false;
            if (pageToken.IsNotUndefined())
            {
                using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
                cursorBuffer = ArrayPool<byte>.Shared.Rent(EnvironmentRunnerAuthorizationContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
                hasCursor = EnvironmentRunnerAuthorizationContinuationToken.TryDecode(tokenUtf8.Span, cursorBuffer, out cursorEnvironment, out cursorRunnerId);
            }

            bool hasMore = false;
            foreach (EnvironmentRunnerAuthorization authorization in ordered)
            {
                if (hasCursor && !After(authorization, cursorEnvironment, cursorRunnerId))
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
                page.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(JsonMarshal.GetRawUtf8Value(authorization).Memory.Span));
            }

            if (!hasMore)
            {
                return EnvironmentRunnerAuthorizationPage.Create(page);
            }

            EnvironmentRunnerAuthorization last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastEnvironment = last.Environment.GetUtf8String();
            using UnescapedUtf8JsonString lastRunnerId = last.RunnerId.GetUtf8String();
            return EnvironmentRunnerAuthorizationPage.Create(page, lastEnvironment.Span, lastRunnerId.Span);
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

    // True when (authorization.environment, authorization.runnerId) sorts strictly after the cursor in the
    // (environment ordinal asc, runnerId ordinal asc) order.
    private static bool After(EnvironmentRunnerAuthorization authorization, ReadOnlySpan<byte> cursorEnvironment, ReadOnlySpan<byte> cursorRunnerId)
    {
        using UnescapedUtf8JsonString environment = authorization.Environment.GetUtf8String();
        int byEnvironment = environment.Span.SequenceCompareTo(cursorEnvironment);
        if (byEnvironment != 0)
        {
            return byEnvironment > 0;
        }

        using UnescapedUtf8JsonString runnerId = authorization.RunnerId.GetUtf8String();
        return runnerId.Span.SequenceCompareTo(cursorRunnerId) > 0;
    }
}