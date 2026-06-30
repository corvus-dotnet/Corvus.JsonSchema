// <copyright file="RunnerRegistryPaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Shared in-memory keyset paging of registered runners, backing the default
/// <see cref="IRunnerRegistry.ListAsync(int, JsonString, System.Threading.CancellationToken)"/> over a registry's full
/// read. A backend overrides that method with a native keyset query so neither the full read nor this sort happens — this
/// is the correct-everywhere fallback that keeps the API paged before each backend is optimised. It is <b>bytes-native</b>:
/// the keyset order and cursor compare on the runner id's persisted UTF-8 (no id string), and the page rows are the
/// detached registrations themselves (no re-parse — <see cref="IRunnerRegistry.ListAsync(System.Threading.CancellationToken)"/>
/// already detaches each from any store-side buffer).
/// </summary>
internal static class RunnerRegistryPaging
{
    /// <summary>The page size used when a caller passes a non-positive limit (the public store-contract default).</summary>
    internal const int DefaultPageSize = RunnerRegistryPage.DefaultPageSize;

    /// <summary>Pages <paramref name="all"/> (a full read) in memory after dropping every runner outside the caller's
    /// read reach (§14.2): a runner is visible only when <paramref name="context"/> admits its <c>reachTags</c> for
    /// <see cref="AccessVerb.Read"/>, so a tenant sees only the runners serving its environments. An unrestricted read
    /// reach skips the per-row filter entirely. The surviving runners are then keyset-paged exactly as the unscoped
    /// overload.</summary>
    /// <param name="all">The registry's full read (detached registrations).</param>
    /// <param name="context">The caller's resolved row-access grant; its read reach selects the visible runners.</param>
    /// <param name="limit">The maximum runners in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque continuation token (its JSON value) to resume after, or undefined for the first page.</param>
    /// <returns>One keyset page over the reach-visible runners (owning its pooled token buffer when more remain).</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    internal static RunnerRegistryPage PageInMemory(IReadOnlyList<RunnerRegistration> all, AccessContext context, int limit, JsonString pageToken)
    {
        if (context.ReadReach is null)
        {
            // Unrestricted read reach (e.g. the trusted system path) — no row is filtered, so avoid the per-row tag copy.
            return PageInMemory(all, limit, pageToken);
        }

        var visible = new List<RunnerRegistration>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            RunnerRegistration runner = all[i];

            // reachTags is absent on a runner serving an unscoped environment; an empty tag set fails a scoped reach
            // (fail-closed, as for every reach-scoped row), so such a runner is invisible to a tenant-scoped caller.
            SecurityTagSet tags = runner.ReachTags.IsNotUndefined()
                ? SecurityTagSet.CopyFrom(runner.ReachTags)
                : SecurityTagSet.Empty;
            if (context.Admits(AccessVerb.Read, tags))
            {
                visible.Add(runner);
            }
        }

        return PageInMemory(visible, limit, pageToken);
    }

    /// <summary>Pages <paramref name="all"/> (a full read) in memory: runners ordered by <c>runnerId</c> (ordinal UTF-8),
    /// resuming strictly after the cursor decoded from <paramref name="pageToken"/> (undefined = first page), bounded to
    /// <paramref name="limit"/>.</summary>
    /// <param name="all">The registry's full read (detached registrations).</param>
    /// <param name="limit">The maximum runners in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque continuation token (its JSON value) to resume after, or undefined for the first page.</param>
    /// <returns>One keyset page (owning its pooled token buffer when more remain).</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    internal static RunnerRegistryPage PageInMemory(IReadOnlyList<RunnerRegistration> all, int limit, JsonString pageToken)
    {
        int pageSize = limit > 0 ? limit : DefaultPageSize;

        // The stable total order every backend pages by (runnerId is unique), materialised here without a DB index. The
        // comparison reads each row's persisted UTF-8 directly (ordinal byte order == ordinal code-point order; the token
        // is backend-scoped so only self-consistency matters) — no per-row id string.
        var ordered = new List<RunnerRegistration>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            ordered.Add(all[i]);
        }

        ordered.Sort(static (x, y) =>
        {
            using UnescapedUtf8JsonString xi = x.RunnerId.GetUtf8String();
            using UnescapedUtf8JsonString yi = y.RunnerId.GetUtf8String();
            return xi.Span.SequenceCompareTo(yi.Span);
        });

        byte[]? cursorBuffer = null;
        try
        {
            ReadOnlySpan<byte> cursor = default;
            bool hasCursor = false;
            if (pageToken.IsNotUndefined())
            {
                using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
                cursorBuffer = ArrayPool<byte>.Shared.Rent(RunnerRegistryContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
                hasCursor = RunnerRegistryContinuationToken.TryDecode(tokenUtf8.Span, cursorBuffer, out cursor);
            }

            var page = new List<RunnerRegistration>(Math.Min(pageSize, ordered.Count));
            bool hasMore = false;
            foreach (RunnerRegistration runner in ordered)
            {
                if (hasCursor)
                {
                    bool atOrBeforeCursor;
                    using (UnescapedUtf8JsonString id = runner.RunnerId.GetUtf8String())
                    {
                        atOrBeforeCursor = id.Span.SequenceCompareTo(cursor) <= 0;
                    }

                    if (atOrBeforeCursor)
                    {
                        continue; // already returned in an earlier page
                    }
                }

                if (page.Count == pageSize)
                {
                    hasMore = true; // a further row exists → there is a next page after the last included row
                    break;
                }

                // The registration is already detached, so it is added by value — no re-parse, and it stays valid after
                // `all`/`ordered` are released. A native backend query returns only the page, so it never pays this sort.
                page.Add(runner);
            }

            if (!hasMore)
            {
                return RunnerRegistryPage.Create(page);
            }

            using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
            return RunnerRegistryPage.Create(page, lastId.Span);
        }
        finally
        {
            if (cursorBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(cursorBuffer);
            }
        }
    }
}