// <copyright file="SecurityBindingPaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared in-memory keyset paging of security bindings, backing the default
/// <see cref="ISecurityPolicyStore.ListBindingsAsync(int, JsonString, JsonString, System.Threading.CancellationToken)"/>
/// over a store's unpaged full read (design §14.2). A backend overrides that method with a native keyset query so neither
/// the full read nor this re-parse happens — this is the correct-everywhere fallback that keeps the API paged before each
/// backend is optimised. It is <b>bytes-native</b>: the keyset order and cursor compare the order (an int) and the id's
/// persisted UTF-8 (no id string), and the optional <c>q</c> filter transcodes only to a pooled UTF-16 scratch.
/// </summary>
internal static class SecurityBindingPaging
{
    /// <summary>The page size used when a caller passes a non-positive limit.</summary>
    internal const int DefaultPageSize = 50;

    /// <summary>Pages <paramref name="all"/> (a full read) in memory: bindings ordered by <c>(order, id)</c> (order
    /// ascending, id ordinal UTF-8), resuming strictly after the cursor decoded from <paramref name="pageToken"/>
    /// (undefined = first page), optionally filtered by <paramref name="q"/> (a case-insensitive substring of the claim
    /// type, claim value, or description), bounded to <paramref name="limit"/>. Each page row is re-parsed into the returned
    /// (owned) page so the caller may dispose <paramref name="all"/>.</summary>
    /// <param name="all">The store's full binding read (the caller disposes it).</param>
    /// <param name="limit">The maximum bindings in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque continuation token (its JSON value) to resume after, or undefined for the first page.</param>
    /// <param name="q">An optional case-insensitive substring filter over claim type/value/description, or undefined for no filter.</param>
    /// <returns>One keyset page, owning its pooled documents (and token buffer when more remain).</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    internal static SecurityBindingPage PageInMemory(PooledDocumentList<SecurityBindingDocument> all, int limit, JsonString pageToken, JsonString q)
    {
        int pageSize = limit > 0 ? limit : DefaultPageSize;

        // The stable total order every backend pages by ((order, id), id unique), materialised here without a DB index.
        var ordered = new List<SecurityBindingDocument>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            ordered.Add(all[i]);
        }

        ordered.Sort(static (x, y) =>
        {
            int byOrder = x.OrderValue.CompareTo(y.OrderValue);
            if (byOrder != 0)
            {
                return byOrder;
            }

            using UnescapedUtf8JsonString xi = x.Id.GetUtf8String();
            using UnescapedUtf8JsonString yi = y.Id.GetUtf8String();
            return xi.Span.SequenceCompareTo(yi.Span);
        });

        byte[]? cursorBuffer = null;
        char[]? qBuffer = null;
        char[]? fieldBuffer = null;
        var page = new PooledDocumentList<SecurityBindingDocument>(Math.Min(pageSize, ordered.Count));
        try
        {
            // The cursor decodes (bytes-native) into a pooled buffer; the id stays a span into it for the scan.
            int cursorOrder = 0;
            ReadOnlySpan<byte> cursorId = default;
            bool hasCursor = false;
            if (pageToken.IsNotUndefined())
            {
                using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
                cursorBuffer = ArrayPool<byte>.Shared.Rent(SecurityBindingContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
                hasCursor = SecurityBindingContinuationToken.TryDecode(tokenUtf8.Span, cursorBuffer, out cursorOrder, out cursorId);
            }

            ReadOnlySpan<char> qChars = default;
            bool hasQ = false;
            if (q.IsNotUndefined())
            {
                using UnescapedUtf8JsonString qUtf8 = q.GetUtf8String();
                qBuffer = ArrayPool<char>.Shared.Rent(Encoding.UTF8.GetMaxCharCount(qUtf8.Span.Length));
                qChars = qBuffer.AsSpan(0, Encoding.UTF8.GetChars(qUtf8.Span, qBuffer));
                hasQ = true;
            }

            bool hasMore = false;
            foreach (SecurityBindingDocument binding in ordered)
            {
                if (hasCursor && !After(binding, cursorOrder, cursorId))
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                if (hasQ && !Matches(binding, qChars, ref fieldBuffer))
                {
                    continue;
                }

                if (page.Count == pageSize)
                {
                    hasMore = true; // a further match exists → there is a next page after the last included row
                    break;
                }

                // Re-parse into the page so it is independent of `all` (which the caller disposes). A native backend
                // query returns only the page, so it never pays this copy.
                page.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(JsonMarshal.GetRawUtf8Value(binding).Memory.Span));
            }

            if (!hasMore)
            {
                return SecurityBindingPage.Create(page);
            }

            SecurityBindingDocument last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return SecurityBindingPage.Create(page, last.OrderValue, lastId.Span);
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

            if (qBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(qBuffer);
            }

            if (fieldBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(fieldBuffer);
            }
        }
    }

    // True when (binding.order, binding.id) sorts strictly after the cursor in the (order asc, id ordinal asc) order.
    private static bool After(SecurityBindingDocument binding, int cursorOrder, ReadOnlySpan<byte> cursorId)
    {
        int order = binding.OrderValue;
        if (order != cursorOrder)
        {
            return order > cursorOrder;
        }

        using UnescapedUtf8JsonString id = binding.Id.GetUtf8String();
        return id.Span.SequenceCompareTo(cursorId) > 0;
    }

    // True when q is a case-insensitive substring of the binding's claim type, claim value, or description. Each field is
    // transcoded into the reused pooled UTF-16 buffer (grown on demand); no managed field string. Optional fields are
    // gated on Undefined (never realised to null).
    private static bool Matches(SecurityBindingDocument binding, ReadOnlySpan<char> qChars, ref char[]? fieldBuffer)
    {
        using (UnescapedUtf8JsonString claimType = binding.ClaimType.GetUtf8String())
        {
            if (SecurityPagingText.ContainsIgnoreCase(claimType.Span, qChars, ref fieldBuffer))
            {
                return true;
            }
        }

        if (binding.ClaimValue.IsNotUndefined())
        {
            using UnescapedUtf8JsonString claimValue = binding.ClaimValue.GetUtf8String();
            if (SecurityPagingText.ContainsIgnoreCase(claimValue.Span, qChars, ref fieldBuffer))
            {
                return true;
            }
        }

        if (binding.Description.IsNotUndefined())
        {
            using UnescapedUtf8JsonString description = binding.Description.GetUtf8String();
            return SecurityPagingText.ContainsIgnoreCase(description.Span, qChars, ref fieldBuffer);
        }

        return false;
    }
}