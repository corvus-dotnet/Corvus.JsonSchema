// <copyright file="SecurityLabelQueryResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Resolves a <see cref="SecurityLabelQuery"/> against a backend's <see cref="ISecurityLabelIndex"/> into the
/// candidate row ids a reach-filtered query need consider (design §14.4). Shared by every backend that narrows
/// through a label index, so the set algebra is written once.
/// </summary>
public static class SecurityLabelQueryResolver
{
    /// <summary>
    /// Resolves the plan to the candidate row ids.
    /// </summary>
    /// <param name="query">The plan, from <see cref="SecurityLabelQueryEmitter"/>.</param>
    /// <param name="index">The backend's label index.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The candidate ids, a <b>superset</b> of the rows the rule admits, so the caller must still evaluate
    /// <see cref="SecurityFilter"/> against each row it loads. <see langword="null"/> means the index could not
    /// narrow at all and the caller should fall back to its unnarrowed query. An <b>empty set is not null</b>:
    /// it means no row qualifies and the caller should return nothing without querying.
    /// </returns>
    public static async ValueTask<IReadOnlySet<string>?> ResolveAsync(
        SecurityLabelQuery query,
        ISecurityLabelIndex index,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(index);

        switch (query.Kind)
        {
            case SecurityLabelQuery.QueryKind.Universe:
                return null;

            case SecurityLabelQuery.QueryKind.Empty:
                return EmptySet;

            case SecurityLabelQuery.QueryKind.Label:
                return ToSet(await index.LookupLabelAsync(query.Key, query.Value, cancellationToken).ConfigureAwait(false));

            case SecurityLabelQuery.QueryKind.AnyValueOfKey:
                return ToSet(await index.LookupKeyAsync(query.Key, cancellationToken).ConfigureAwait(false));

            case SecurityLabelQuery.QueryKind.Union:
                return await ResolveUnionAsync(query, index, cancellationToken).ConfigureAwait(false);

            default:
                return await ResolveIntersectAsync(query, index, cancellationToken).ConfigureAwait(false);
        }
    }

    private static HashSet<string> EmptySet => [];

    private static async ValueTask<IReadOnlySet<string>?> ResolveUnionAsync(
        SecurityLabelQuery query,
        ISecurityLabelIndex index,
        CancellationToken cancellationToken)
    {
        // One un-narrowable branch makes the whole union un-narrowable, and it does so without the other
        // branches being looked up at all.
        HashSet<string>? accumulated = null;
        foreach (SecurityLabelQuery child in query.Children)
        {
            IReadOnlySet<string>? resolved = await ResolveAsync(child, index, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                return null;
            }

            if (accumulated is null)
            {
                accumulated = [.. resolved];
            }
            else
            {
                accumulated.UnionWith(resolved);
            }
        }

        return accumulated ?? EmptySet;
    }

    private static async ValueTask<IReadOnlySet<string>?> ResolveIntersectAsync(
        SecurityLabelQuery query,
        ISecurityLabelIndex index,
        CancellationToken cancellationToken)
    {
        // An un-narrowable branch contributes no constraint and is simply skipped, which is what lets a rule the
        // index cannot express still be bounded by a sibling it can — the wrapper-rule case (§14.3).
        HashSet<string>? accumulated = null;
        foreach (SecurityLabelQuery child in query.Children)
        {
            IReadOnlySet<string>? resolved = await ResolveAsync(child, index, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                continue;
            }

            if (accumulated is null)
            {
                accumulated = [.. resolved];
            }
            else
            {
                accumulated.IntersectWith(resolved);
            }

            if (accumulated.Count == 0)
            {
                // Nothing can re-enter an intersection, so stop before issuing further index lookups.
                return accumulated;
            }
        }

        return accumulated;
    }

    private static HashSet<string> ToSet(IReadOnlyCollection<string> ids)
        => new(ids, StringComparer.Ordinal);
}