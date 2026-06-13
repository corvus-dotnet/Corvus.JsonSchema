// <copyright file="RowSecurityFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Marks a run- or catalog-store implementation that <b>honours</b> a row-security reach filter
/// (<see cref="WorkflowQuery.Security"/> / <see cref="CatalogQuery.Security"/>) inside its query — i.e. it pushes
/// the filter down so a multi-row list/search/purge returns only rows the caller may see (§14.2/§14.4).
/// </summary>
/// <remarks>
/// A store that does <em>not</em> implement the marker would silently ignore the filter and leak rows across the
/// authorization boundary, so the management/catalog clients refuse a filtered query against an unmarked store
/// (see <see cref="RowSecurityPushdown.EnsureSupported"/>) rather than run it. As each backend implements
/// indexed reach-filtering it adds this marker; until then it fails loud. The in-memory reference stores already
/// filter and carry the marker.
/// </remarks>
public interface ISupportsRowSecurityFilter;

/// <summary>Guards against a row-security reach filter being silently ignored by a store that does not push it down.</summary>
internal static class RowSecurityPushdown
{
    /// <summary>
    /// Throws if a non-<see langword="null"/> reach filter is about to be handed to a store that does not
    /// implement <see cref="ISupportsRowSecurityFilter"/> — which would otherwise drop the filter and leak rows.
    /// </summary>
    /// <param name="security">The reach filter the query carries (<see langword="null"/> is unrestricted and always allowed).</param>
    /// <param name="store">The store the query is about to run against.</param>
    /// <exception cref="NotSupportedException">The filter is set but the store does not push row-security down.</exception>
    public static void EnsureSupported(SecurityFilter? security, object store)
    {
        if (security is not null && store is not ISupportsRowSecurityFilter)
        {
            throw new NotSupportedException(
                $"The store '{store.GetType().Name}' does not yet implement row-security (reach) filtering in its query (§14.2/§14.4); " +
                "a reach filter would otherwise be silently ignored, leaking rows across the authorization boundary. " +
                "Use a store that implements ISupportsRowSecurityFilter, or run without row security.");
        }
    }
}