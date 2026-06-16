// <copyright file="SourceCredentialCache.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// The runner-side credential cache (design §13.4): the cornerstone that makes secure-by-default ~free on the warm
/// path. It caches the <strong>built <see cref="IHttpAuthenticationProvider"/></strong> — not the raw secret — keyed by
/// (<c>sourceName</c>, <c>environment</c>), with a <strong>fairly short TTL</strong> plus etag-based rotation
/// invalidation. A warm hit does <strong>zero secret-store I/O and zero allocation</strong>; a miss fetches the binding
/// once, resolves the secret once, and builds the provider once, single-flight per key (concurrent callers await one
/// build — no thundering herd).
/// </summary>
/// <remarks>
/// <para>On refresh, the binding's etag is compared: an unchanged binding keeps the existing provider (no secret
/// re-resolve) and just extends the TTL; a changed binding rebuilds and disposes the superseded provider, so a rotation
/// in the secret store is picked up within the TTL without an explicit invalidation (or immediately via
/// <see cref="Invalidate"/>).</para>
/// <para>The cache holds only the derived provider and the binding etag — never secret material; the
/// <see cref="SourceCredentialProviderFactory"/> scrubs the resolved secret as soon as the provider is built.</para>
/// </remarks>
public sealed class SourceCredentialCache : IDisposable
{
    private readonly ISourceCredentialStore store;
    private readonly SourceCredentialProviderFactory factory;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan ttl;
    private readonly ConcurrentDictionary<(string SourceName, string Environment, string RunTags), Entry> entries = new();
    private readonly ConcurrentDictionary<(string SourceName, string Environment, string RunTags), SemaphoreSlim> buildGates = new();
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SourceCredentialCache"/> class.</summary>
    /// <param name="store">The store the binding (reference + metadata) is fetched from on a miss.</param>
    /// <param name="factory">The factory that resolves the secret and builds the provider.</param>
    /// <param name="timeProvider">The time source for TTL; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="ttl">The cache time-to-live (the warm window); defaults to a fairly short 5 minutes.</param>
    public SourceCredentialCache(ISourceCredentialStore store, SourceCredentialProviderFactory factory, TimeProvider? timeProvider = null, TimeSpan? ttl = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(factory);
        this.store = store;
        this.factory = factory;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>Gets the HTTP authentication provider a run carrying <paramref name="runTags"/> is entitled to use for
    /// the source/environment, or <see langword="null"/> when the run is entitled to no binding (the source is left
    /// unauthenticated). The warm path is synchronous and allocation-free; the entry is keyed by the run's tags too, so
    /// different tenants cache (and resolve) independently.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="runTags">The run's own security tags (§14.2), used to entitle the binding (label-superset).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached provider, or <see langword="null"/> if the run is entitled to no credential binding.</returns>
    public ValueTask<IHttpAuthenticationProvider?> GetAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken = default)
        => this.GetAsync(sourceName, environment, SourceCredentialKey.CanonicalTags(runTags), runTags, cancellationToken);

    /// <summary>As <see cref="GetAsync(string, string, SecurityTagSet, CancellationToken)"/>, but with the run-tags key
    /// supplied pre-computed (<see cref="SourceCredentialKey.CanonicalTags"/>) — the run's tags are fixed at
    /// transport-bind time, so a per-source provider computes the key once and the per-request warm path stays
    /// <strong>allocation-free</strong> (§13.4). The <paramref name="runTags"/> are still needed for a cache miss.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="runTagsKey">The pre-computed canonical run-tags key (the cache discriminator).</param>
    /// <param name="runTags">The run's own security tags (used only on a miss, to entitle the binding).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached provider, or <see langword="null"/> if the run is entitled to no credential binding.</returns>
    public ValueTask<IHttpAuthenticationProvider?> GetAsync(string sourceName, string environment, string runTagsKey, SecurityTagSet runTags, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runTagsKey);

        (string, string, string) key = (sourceName, environment, runTagsKey);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        if (this.entries.TryGetValue(key, out Entry? warm) && now < warm.ExpiresAt)
        {
            // Warm path: a reference read of the published, immutable entry over a pre-computed key. No store I/O, no
            // allocation. Enforcing credential expiry (§13.2) costs exactly one nullable comparison on the success path;
            // the throw is the exceptional branch, so the hot path is unchanged.
            if (warm.CredentialExpiresAt is { } credentialExpiry && now >= credentialExpiry)
            {
                throw new SourceCredentialExpiredException(sourceName);
            }

            return new ValueTask<IHttpAuthenticationProvider?>(warm.Provider);
        }

        return this.GetSlowAsync(key, runTags, cancellationToken);
    }

    /// <summary>Explicitly invalidates the cached entries for a source/environment (e.g. on a control-plane rotation),
    /// disposing their providers — across every run-tag scope, since a rotation in the store affects them all.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    public void Invalidate(string sourceName, string environment)
    {
        foreach ((string SourceName, string Environment, string RunTags) key in this.entries.Keys)
        {
            if (string.Equals(key.SourceName, sourceName, StringComparison.Ordinal) &&
                string.Equals(key.Environment, environment, StringComparison.Ordinal) &&
                this.entries.TryRemove(key, out Entry? removed))
            {
                DisposeProvider(removed.Provider);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        foreach (Entry entry in this.entries.Values)
        {
            DisposeProvider(entry.Provider);
        }

        this.entries.Clear();
        foreach (SemaphoreSlim gate in this.buildGates.Values)
        {
            gate.Dispose();
        }

        this.buildGates.Clear();
    }

    private static void DisposeProvider(IHttpAuthenticationProvider? provider)
    {
        if (provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async ValueTask<IHttpAuthenticationProvider?> GetSlowAsync((string SourceName, string Environment, string RunTags) key, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = this.buildGates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have filled the entry while we waited on the gate.
            if (this.entries.TryGetValue(key, out Entry? current) && this.timeProvider.GetUtcNow() < current.ExpiresAt)
            {
                if (current.CredentialExpiresAt is { } cachedExpiry && this.timeProvider.GetUtcNow() >= cachedExpiry)
                {
                    throw new SourceCredentialExpiredException(key.SourceName);
                }

                return current.Provider;
            }

            using ParsedJsonDocument<SourceCredentialBinding>? document = await this.store.ResolveForUsageAsync(key.SourceName, key.Environment, runTags, cancellationToken).ConfigureAwait(false);
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            DateTimeOffset cacheExpiresAt = now + this.ttl;

            if (document is null)
            {
                // Negative cache: remember "no binding" for the TTL so an unauthenticated source costs no repeat I/O.
                this.Publish(key, current, new Entry(null, cacheExpiresAt, WorkflowEtag.None, credentialExpiresAt: null));
                return null;
            }

            // §13.2: a run binds only a credential it is entitled to use; if that binding has expired, fault the bind so
            // the durable executor records a typed credentials-expired fault (resumable after rotation). Cache the
            // expired state so repeat reads in the window throw without re-resolving; a rotation (new etag, or an
            // explicit Invalidate) refreshes it.
            DateTimeOffset? credentialExpiresAt = document.RootElement.ExpiresAtOrNull;
            WorkflowEtag etag = document.RootElement.EtagValue;
            if (credentialExpiresAt is { } expiry && now >= expiry)
            {
                this.Publish(key, current, new Entry(null, cacheExpiresAt, etag, credentialExpiresAt));
                throw new SourceCredentialExpiredException(key.SourceName);
            }

            if (current is { Provider: not null } && current.Etag == etag)
            {
                // Unchanged binding: keep the existing provider (no secret re-resolve), just extend the warm window.
                this.Publish(key, current, new Entry(current.Provider, cacheExpiresAt, etag, credentialExpiresAt), disposePrevious: false);
                return current.Provider;
            }

            IHttpAuthenticationProvider provider = await this.factory.CreateAsync(document.RootElement, cancellationToken).ConfigureAwait(false);
            this.Publish(key, current, new Entry(provider, cacheExpiresAt, etag, credentialExpiresAt));
            return provider;
        }
        finally
        {
            gate.Release();
        }
    }

    private void Publish((string SourceName, string Environment, string RunTags) key, Entry? previous, Entry next, bool disposePrevious = true)
    {
        this.entries[key] = next;
        if (disposePrevious && previous is not null && !ReferenceEquals(previous.Provider, next.Provider))
        {
            DisposeProvider(previous.Provider);
        }
    }

    // Immutable once published, so the lock-free warm-path read sees a consistent snapshot (no torn DateTimeOffset).
    private sealed class Entry(IHttpAuthenticationProvider? provider, DateTimeOffset expiresAt, WorkflowEtag etag, DateTimeOffset? credentialExpiresAt)
    {
        public IHttpAuthenticationProvider? Provider { get; } = provider;

        // The cache TTL window (when to refresh) — distinct from CredentialExpiresAt (when the secret itself expires).
        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public WorkflowEtag Etag { get; } = etag;

        // The bound credential's own expiry (§13.2), or null when it has no known expiry (non-expiring).
        public DateTimeOffset? CredentialExpiresAt { get; } = credentialExpiresAt;
    }
}