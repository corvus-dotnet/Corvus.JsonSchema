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
    private readonly ConcurrentDictionary<(string SourceName, string Environment), Entry> entries = new();
    private readonly ConcurrentDictionary<(string SourceName, string Environment), SemaphoreSlim> buildGates = new();
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

    /// <summary>Gets the HTTP authentication provider for a source/environment, or <see langword="null"/> when no
    /// binding exists (the source is unauthenticated). The warm path is synchronous and allocation-free.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached provider, or <see langword="null"/> if the source has no credential binding.</returns>
    public ValueTask<IHttpAuthenticationProvider?> GetAsync(string sourceName, string environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);

        (string, string) key = (sourceName, environment);
        if (this.entries.TryGetValue(key, out Entry? warm) && this.timeProvider.GetUtcNow() < warm.ExpiresAt)
        {
            // Warm path: a reference read of the published, immutable entry. No store I/O, no allocation.
            return new ValueTask<IHttpAuthenticationProvider?>(warm.Provider);
        }

        return this.GetSlowAsync(key, cancellationToken);
    }

    /// <summary>Explicitly invalidates a cached entry (e.g. on a control-plane rotation), disposing its provider.</summary>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    public void Invalidate(string sourceName, string environment)
    {
        if (this.entries.TryRemove((sourceName, environment), out Entry? removed))
        {
            DisposeProvider(removed.Provider);
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

    private async ValueTask<IHttpAuthenticationProvider?> GetSlowAsync((string SourceName, string Environment) key, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = this.buildGates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have filled the entry while we waited on the gate.
            if (this.entries.TryGetValue(key, out Entry? current) && this.timeProvider.GetUtcNow() < current.ExpiresAt)
            {
                return current.Provider;
            }

            using ParsedJsonDocument<SourceCredentialBinding>? document = await this.store.GetAsync(key.SourceName, key.Environment, cancellationToken).ConfigureAwait(false);
            DateTimeOffset expiresAt = this.timeProvider.GetUtcNow() + this.ttl;

            if (document is null)
            {
                // Negative cache: remember "no binding" for the TTL so an unauthenticated source costs no repeat I/O.
                this.Publish(key, current, new Entry(null, expiresAt, WorkflowEtag.None));
                return null;
            }

            WorkflowEtag etag = document.RootElement.EtagValue;
            if (current is { Provider: not null } && current.Etag == etag)
            {
                // Unchanged binding: keep the existing provider (no secret re-resolve), just extend the warm window.
                this.Publish(key, current, new Entry(current.Provider, expiresAt, etag), disposePrevious: false);
                return current.Provider;
            }

            IHttpAuthenticationProvider provider = await this.factory.CreateAsync(document.RootElement, cancellationToken).ConfigureAwait(false);
            this.Publish(key, current, new Entry(provider, expiresAt, etag));
            return provider;
        }
        finally
        {
            gate.Release();
        }
    }

    private void Publish((string SourceName, string Environment) key, Entry? previous, Entry next, bool disposePrevious = true)
    {
        this.entries[key] = next;
        if (disposePrevious && previous is not null && !ReferenceEquals(previous.Provider, next.Provider))
        {
            DisposeProvider(previous.Provider);
        }
    }

    // Immutable once published, so the lock-free warm-path read sees a consistent snapshot (no torn DateTimeOffset).
    private sealed class Entry(IHttpAuthenticationProvider? provider, DateTimeOffset expiresAt, WorkflowEtag etag)
    {
        public IHttpAuthenticationProvider? Provider { get; } = provider;

        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public WorkflowEtag Etag { get; } = etag;
    }
}