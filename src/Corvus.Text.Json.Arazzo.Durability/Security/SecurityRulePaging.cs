// <copyright file="SecurityRulePaging.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared in-memory keyset paging of security rules, backing the default
/// <see cref="ISecurityPolicyStore.ListRulesAsync(int, JsonString, string?, System.Threading.CancellationToken)"/> over
/// a store's unpaged full read (design §14.2). A backend overrides that method with a native keyset query so neither the
/// full read nor this re-parse happens — this is the correct-everywhere fallback that keeps the API paged before each
/// backend is optimised.
/// </summary>
internal static class SecurityRulePaging
{
    /// <summary>The page size used when a caller passes a non-positive limit.</summary>
    internal const int DefaultPageSize = 50;

    /// <summary>Pages <paramref name="all"/> (a full read) in memory: rules ordered by <c>name</c> (ordinal), resuming
    /// strictly after <paramref name="cursor"/> (<see langword="null"/> = first page), optionally filtered by
    /// <paramref name="q"/> (a case-insensitive substring of the name or expression), bounded to <paramref name="limit"/>.
    /// Each page row is re-parsed into the returned (owned) page so the caller may dispose <paramref name="all"/>.</summary>
    /// <param name="all">The store's full rule read (the caller disposes it).</param>
    /// <param name="limit">The maximum rules in the page (non-positive uses <see cref="DefaultPageSize"/>).</param>
    /// <param name="cursor">The rule name to resume strictly after, or <see langword="null"/> for the first page.</param>
    /// <param name="q">An optional case-insensitive substring filter over name/expression.</param>
    /// <returns>One keyset page, owning its pooled documents (and token buffer when more remain).</returns>
    internal static SecurityRulePage PageInMemory(PooledDocumentList<SecurityRuleDocument> all, int limit, string? cursor, string? q)
    {
        int pageSize = limit > 0 ? limit : DefaultPageSize;

        // The stable total order every backend pages by (name is unique), materialised here without a DB index.
        var ordered = new List<SecurityRuleDocument>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            ordered.Add(all[i]);
        }

        ordered.Sort(static (x, y) => string.CompareOrdinal(x.NameValue, y.NameValue));

        var page = new PooledDocumentList<SecurityRuleDocument>(Math.Min(pageSize, ordered.Count));
        bool hasMore = false;
        string lastName = string.Empty;
        try
        {
            foreach (SecurityRuleDocument rule in ordered)
            {
                string name = rule.NameValue;
                if (cursor is not null && string.CompareOrdinal(name, cursor) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                if (q is not null && !Matches(rule, q))
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
                lastName = name;
            }

            return hasMore ? SecurityRulePage.Create(page, lastName) : SecurityRulePage.Create(page);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    private static bool Matches(SecurityRuleDocument rule, string q)
        => rule.NameValue.Contains(q, StringComparison.OrdinalIgnoreCase)
        || rule.ExpressionValue.Contains(q, StringComparison.OrdinalIgnoreCase);
}