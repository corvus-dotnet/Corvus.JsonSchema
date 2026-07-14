// <copyright file="DirectoryMembershipExpander.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// The shared per-user membership cache + fan-out that a fetch-based <see cref="IPrincipalDirectory"/> adapter (Keycloak,
/// Okta, Google, Entra) uses to resolve a page of people to their <strong>full membership-expanded identity</strong> (design
/// §16.5.4). Each person's group names are fetched by a membership key (its directory id, or its email where that is the
/// group-lookup key), cached per key with a short TTL, and the fold into <c>sys:group</c> is delegated to the projector.
/// Inline adapters (LDAP, SCIM) carry memberships on the record already and call
/// <see cref="DirectoryPrincipalProjector.EnrichWithMemberships"/> directly, so they do not need this.
/// </summary>
/// <remarks>
/// A short TTL is safe: the directory-resolved identity feeds the grantee picker and the effective-access read view only,
/// never a live request's authorization (that is always re-derived from the caller's own fresh token), so a stale cache can
/// at worst show a slightly stale membership and never over-grants. Fetches for a page run concurrently, so a page of N
/// people costs one round-trip of latency, not N. The adapter provides the fetch delegate (its own HTTP call + parse), with
/// its per-search auth token captured; this owns only the cache and the concurrency.
/// </remarks>
public sealed class DirectoryMembershipExpander
{
    // The cache is pruned of expired entries only once it grows past this many distinct users, bounding its size for a large
    // directory without paying an O(n) sweep on the common (small, warm) path.
    private const int PruneThreshold = 1024;

    private readonly ConcurrentDictionary<string, CachedGroups> cache = new(StringComparer.Ordinal);
    private readonly TimeSpan cacheTtl;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="DirectoryMembershipExpander"/> class.</summary>
    /// <param name="cacheTtl">How long a fetched membership set is cached before a re-fetch; <see cref="TimeSpan.Zero"/> disables the cache (every search re-fetches).</param>
    /// <param name="timeProvider">The time source for cache expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public DirectoryMembershipExpander(TimeSpan cacheTtl, TimeProvider? timeProvider = null)
    {
        this.cacheTtl = cacheTtl;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Expands a page of resolved people to their full membership identity: fetch each person's group names by its
    /// membership key (cached per key, all concurrent), then fold a <c>sys:group</c> per group into the identity through the
    /// projector. A person whose key is empty gets an empty membership set and is returned unexpanded.
    /// </summary>
    /// <param name="people">The resolved people to expand.</param>
    /// <param name="membershipKeys">The membership-lookup key for each person, parallel to <paramref name="people"/> (the directory id, or the email where that is the group-lookup key).</param>
    /// <param name="projector">The projector that folds each membership name in through the deployment mapper.</param>
    /// <param name="fetchGroupNames">The adapter's group-name fetch for one membership key (its HTTP call + parse), with its per-search auth token captured.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The people with their memberships' identity tags unioned in.</returns>
    public async Task<IReadOnlyList<ResolvedPrincipal>> ExpandAsync(
        IReadOnlyList<ResolvedPrincipal> people,
        IReadOnlyList<string> membershipKeys,
        DirectoryPrincipalProjector projector,
        Func<string, CancellationToken, Task<IReadOnlyList<string>>> fetchGroupNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(membershipKeys);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(fetchGroupNames);
        if (people.Count == 0)
        {
            return people;
        }

        var fetches = new Task<IReadOnlyList<string>>[people.Count];
        for (int i = 0; i < people.Count; i++)
        {
            fetches[i] = this.GetGroupNamesAsync(membershipKeys[i], fetchGroupNames, cancellationToken);
        }

        await Task.WhenAll(fetches).ConfigureAwait(false);

        var expanded = new ResolvedPrincipal[people.Count];
        for (int i = 0; i < people.Count; i++)
        {
            expanded[i] = projector.EnrichWithMemberships(people[i], fetches[i].Result, null);
        }

        return expanded;
    }

    // A membership set, served from the TTL cache when warm (a volatile dictionary read) or fetched once and cached otherwise.
    private async Task<IReadOnlyList<string>> GetGroupNamesAsync(string key, Func<string, CancellationToken, Task<IReadOnlyList<string>>> fetch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key))
        {
            return [];
        }

        if (this.cache.TryGetValue(key, out CachedGroups cached) && this.timeProvider.GetUtcNow() < cached.ExpiresAt)
        {
            return cached.Names;
        }

        IReadOnlyList<string> names = await fetch(key, cancellationToken).ConfigureAwait(false);

        if (this.cacheTtl > TimeSpan.Zero)
        {
            this.cache[key] = new CachedGroups(names, this.timeProvider.GetUtcNow() + this.cacheTtl);
            this.PruneExpired();
        }

        return names;
    }

    // Bounds the cache: once it exceeds the prune threshold, drop the entries that have already expired (the warm path never
    // pays this — the check short-circuits on Count). TTL drains the rest.
    private void PruneExpired()
    {
        if (this.cache.Count <= PruneThreshold)
        {
            return;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        foreach (KeyValuePair<string, CachedGroups> entry in this.cache)
        {
            if (now >= entry.Value.ExpiresAt)
            {
                this.cache.TryRemove(entry.Key, out _);
            }
        }
    }

    // A membership set + when the entry goes stale. A struct so a cache hit is a value read with no per-entry heap
    // allocation (the names list is shared, not copied).
    private readonly record struct CachedGroups(IReadOnlyList<string> Names, DateTimeOffset ExpiresAt);
}