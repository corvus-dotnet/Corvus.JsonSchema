// <copyright file="SecurityRulePaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared in-memory keyset paging of security rules, backing the default
/// <see cref="ISecurityPolicyStore.ListRulesAsync(int, JsonString, JsonString, System.Threading.CancellationToken)"/> over
/// a store's unpaged full read (design §14.2). A backend overrides that method with a native keyset query so neither the
/// full read nor this re-parse happens — this is the correct-everywhere fallback that keeps the API paged before each
/// backend is optimised. It is <b>bytes-native</b>: the keyset order and cursor compare on the persisted UTF-8 (no name
/// string), and the optional <c>q</c> filter transcodes only to a pooled UTF-16 scratch for the case-insensitive match.
/// </summary>
internal static class SecurityRulePaging
{
    /// <summary>The page size used when a caller passes a non-positive limit.</summary>
    internal const int DefaultPageSize = 50;

    /// <summary>Pages <paramref name="all"/> (a full read) in memory: rules ordered by <c>name</c> (ordinal UTF-8),
    /// resuming strictly after the cursor decoded from <paramref name="pageToken"/> (undefined = first page), optionally
    /// filtered by <paramref name="q"/> (a case-insensitive substring of the name or expression), bounded to
    /// <paramref name="limit"/>. Each page row is re-parsed into the returned (owned) page so the caller may dispose
    /// <paramref name="all"/>.</summary>
    /// <param name="all">The store's full rule read (the caller disposes it).</param>
    /// <param name="limit">The maximum rules in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="pageToken">The opaque continuation token (its JSON value) to resume after, or undefined for the first page.</param>
    /// <param name="q">An optional case-insensitive substring filter over name/expression, or undefined for no filter.</param>
    /// <returns>One keyset page, owning its pooled documents (and token buffer when more remain).</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    internal static SecurityRulePage PageInMemory(PooledDocumentList<SecurityRuleDocument> all, int limit, JsonString pageToken, JsonString q)
    {
        int pageSize = limit > 0 ? limit : DefaultPageSize;

        // The stable total order every backend pages by (name is unique), materialised here without a DB index. The
        // comparison reads each row's persisted UTF-8 directly (ordinal byte order == ordinal code-point order; the token
        // is backend-scoped so only self-consistency matters) — no per-row name string.
        var ordered = new List<SecurityRuleDocument>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            ordered.Add(all[i]);
        }

        ordered.Sort(static (x, y) =>
        {
            using UnescapedUtf8JsonString xn = x.Name.GetUtf8String();
            using UnescapedUtf8JsonString yn = y.Name.GetUtf8String();
            return xn.Span.SequenceCompareTo(yn.Span);
        });

        byte[]? cursorBuffer = null;
        char[]? qBuffer = null;
        char[]? fieldBuffer = null;
        var page = new PooledDocumentList<SecurityRuleDocument>(Math.Min(pageSize, ordered.Count));
        try
        {
            // The cursor decodes (bytes-native) into a pooled buffer; the cursor stays a span into it for the scan.
            ReadOnlySpan<byte> cursor = default;
            bool hasCursor = false;
            if (pageToken.IsNotUndefined())
            {
                using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
                cursorBuffer = ArrayPool<byte>.Shared.Rent(SecurityRuleContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
                hasCursor = SecurityRuleContinuationToken.TryDecode(tokenUtf8.Span, cursorBuffer, out cursor);
            }

            // Transcode q to UTF-16 once; each candidate field is transcoded into a reused pooled buffer and matched
            // OrdinalIgnoreCase — the case-insensitive substring with no managed string.
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
            foreach (SecurityRuleDocument rule in ordered)
            {
                bool atOrBeforeCursor;
                using (UnescapedUtf8JsonString name = rule.Name.GetUtf8String())
                {
                    atOrBeforeCursor = hasCursor && name.Span.SequenceCompareTo(cursor) <= 0;
                }

                if (atOrBeforeCursor)
                {
                    continue; // already returned in an earlier page
                }

                if (hasQ && !Matches(rule, qChars, ref fieldBuffer))
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
                page.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(JsonMarshal.GetRawUtf8Value(rule).Memory.Span));
            }

            if (!hasMore)
            {
                return SecurityRulePage.Create(page);
            }

            using UnescapedUtf8JsonString lastName = page[page.Count - 1].Name.GetUtf8String();
            return SecurityRulePage.Create(page, lastName.Span);
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

    // True when q is a case-insensitive substring of the rule's name or expression. Each field is transcoded into the
    // reused pooled UTF-16 buffer (grown on demand); no managed field string.
    private static bool Matches(SecurityRuleDocument rule, ReadOnlySpan<char> qChars, ref char[]? fieldBuffer)
    {
        using (UnescapedUtf8JsonString name = rule.Name.GetUtf8String())
        {
            if (SecurityPagingText.ContainsIgnoreCase(name.Span, qChars, ref fieldBuffer))
            {
                return true;
            }
        }

        using UnescapedUtf8JsonString expression = rule.Expression.GetUtf8String();
        return SecurityPagingText.ContainsIgnoreCase(expression.Span, qChars, ref fieldBuffer);
    }
}