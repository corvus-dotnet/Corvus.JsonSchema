// <copyright file="RunnerAuthorizationBindings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The <see cref="IRunnerEnvironmentBindings"/> a deployment that governs its runners uses: the environments a machine
/// principal may execute runs for are the ones an administrator has authorized it for (ADR 0027), read from the
/// authorization records rather than declared in configuration.
/// </summary>
/// <remarks>
/// <para>
/// Only an <c>Authorized</c> record binds. Pending, Quarantined, and Revoked are all excluded, which is what makes the
/// revocation fence take effect here rather than depending on a runner standing down when told to.
/// </para>
/// <para>
/// Resolution is cached for a bounded window — thirty seconds at most, per ADR 0065 decision 2 — because the window is
/// the fence's latency. Revoking a runner stops it being offered work within it, so the bound is the property rather
/// than a tuning knob, and a caller asking for longer gets thirty seconds.
/// </para>
/// </remarks>
public sealed class RunnerAuthorizationBindings : IRunnerEnvironmentBindings
{
    /// <summary>The longest a resolution may be cached. The cache window is the runner-revocation fence's latency, so it
    /// is capped here rather than left to a deployment to choose badly.</summary>
    public static readonly TimeSpan MaximumCacheWindow = TimeSpan.FromSeconds(30);

    private static readonly string[] None = [];

    private readonly IEnvironmentRunnerAuthorizationStore authorizations;
    private readonly IEnvironmentStore environments;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan cacheWindow;
    private readonly int cacheCapacity;
    private readonly ConcurrentDictionary<string, Entry> cache = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="RunnerAuthorizationBindings"/> class.</summary>
    /// <param name="authorizations">The runner-authorization records.</param>
    /// <param name="environments">The environment records, read to tell a tenant environment from the platform's own.</param>
    /// <param name="cacheWindow">How long a resolution may be reused, bounded by <see cref="MaximumCacheWindow"/>. Pass
    /// <see cref="TimeSpan.Zero"/> to resolve on every request.</param>
    /// <param name="cacheCapacity">The most principals to cache. A principal is authenticated, so the population is
    /// bounded in practice; the cap is there so it is bounded by construction too.</param>
    /// <param name="timeProvider">The time source for cache expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public RunnerAuthorizationBindings(
        IEnvironmentRunnerAuthorizationStore authorizations,
        IEnvironmentStore environments,
        TimeSpan? cacheWindow = null,
        int cacheCapacity = 1024,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(authorizations);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheCapacity);

        this.authorizations = authorizations;
        this.environments = environments;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.cacheCapacity = cacheCapacity;

        TimeSpan requested = cacheWindow ?? MaximumCacheWindow;
        this.cacheWindow = requested < TimeSpan.Zero || requested > MaximumCacheWindow ? MaximumCacheWindow : requested;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>> ResolveAsync(string principal, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        if (this.cache.TryGetValue(principal, out Entry cached) && cached.ExpiresAt > now)
        {
            return cached.Environments;
        }

        IReadOnlyList<string> resolved = await this.ReadBindingsAsync(principal, cancellationToken).ConfigureAwait(false);

        // Evict wholesale rather than by age: entries all share one window, so a full cache is a cache about to expire
        // anyway, and tracking per-entry recency to remove one of them would cost more than it saves.
        if (this.cache.Count >= this.cacheCapacity)
        {
            this.cache.Clear();
        }

        this.cache[principal] = new Entry(resolved, now + this.cacheWindow);
        return resolved;
    }

    private async ValueTask<IReadOnlyList<string>> ReadBindingsAsync(string principal, CancellationToken cancellationToken)
    {
        // The unpaged read, deliberately. This query is scoped to one principal, so its result is the environments a
        // single runner serves — bounded by the deployment's environment count rather than by its runner or run count,
        // and the paged overload's own default implementation reads exactly this before slicing it.
        var query = new RunnerAuthorizationQuery(RunnerAuthorizationStatus.Authorized, Principal: principal);
        List<string>? bound = null;

        using (PooledDocumentList<EnvironmentRunnerAuthorization> authorized = await this.authorizations.ListAsync(query, cancellationToken).ConfigureAwait(false))
        {
            for (int i = 0; i < authorized.Count; ++i)
            {
                (bound ??= []).Add(authorized[i].EnvironmentValue);
            }
        }

        if (bound is null)
        {
            return None;
        }

        return await this.SeparatedAsync(bound, cancellationToken).ConfigureAwait(false);
    }

    // Applies ADR 0065 decision 2's exclusivity rule and drops a binding whose environment no longer exists.
    private async ValueTask<IReadOnlyList<string>> SeparatedAsync(List<string> bound, CancellationToken cancellationToken)
    {
        List<string>? live = null;
        bool holdsPlatform = false;
        bool holdsTenant = false;

        foreach (string environment in bound)
        {
            using ParsedJsonDocument<Environments.Environment>? record = await this.environments.GetAsync(environment, AccessContext.System, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                // The authorization outlived its environment. A runner cannot serve one that is not there, and treating
                // the binding as live would be trusting a name nothing backs.
                continue;
            }

            if (TenantEnvironmentSealing.IsPlatform(record.RootElement))
            {
                holdsPlatform = true;
            }
            else
            {
                holdsTenant = true;
            }

            (live ??= []).Add(environment);
        }

        // A principal holding both is a laundering route out of a sealed environment into the never-sealed platform one,
        // so it resolves to nothing at all. Picking a side would leave the route open in whichever direction was kept,
        // and the rule is re-checked here per request precisely because a binding written before it was enforced, or
        // written concurrently, would otherwise pass unexamined.
        if (holdsPlatform && holdsTenant)
        {
            return None;
        }

        return live ?? (IReadOnlyList<string>)None;
    }

    private readonly record struct Entry(IReadOnlyList<string> Environments, DateTimeOffset ExpiresAt);
}