// <copyright file="EnvironmentStoreSealKeyResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Resolves an environment's checkpoint seal-key registration (ADR 0065) from the environment store, for the
/// <see cref="EnvironmentCheckpointProtector"/>'s save-side routing: the registered <c>checkpointKey</c> is read as
/// <see cref="AccessContext.System"/> (sealing is a platform mechanism, not a caller-reach concern) and cached for a
/// short TTL so a checkpoint save does not cost a store round-trip; a rotation is visible within the TTL. An unknown
/// or unsealed environment resolves to <see langword="null"/> (the baseline posture); a store failure propagates, so
/// a save never silently falls back to the baseline when the environment might be sealed.
/// </summary>
public sealed class EnvironmentStoreSealKeyResolver
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IEnvironmentStore store;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan cacheTtl;
    private readonly Dictionary<string, (DateTimeOffset Expires, EnvironmentSealKey? Value)> cache = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Initializes a new instance of the <see cref="EnvironmentStoreSealKeyResolver"/> class.</summary>
    /// <param name="store">The environment store the registrations are read from.</param>
    /// <param name="cacheTtl">How long a resolved registration (or its absence) is served from cache; defaults to
    /// 30 seconds. A rotation registered on the environment is picked up within this window.</param>
    /// <param name="timeProvider">The time source for cache expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public EnvironmentStoreSealKeyResolver(IEnvironmentStore store, TimeSpan? cacheTtl = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
        this.cacheTtl = cacheTtl ?? DefaultCacheTtl;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Resolves the environment's current registration; the <see cref="EnvironmentSealKeyResolver"/> shape.</summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The registration, or <see langword="null"/> for an unknown or unsealed environment.</returns>
    public async ValueTask<EnvironmentSealKey?> ResolveAsync(string environment, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (this.cache.TryGetValue(environment, out (DateTimeOffset Expires, EnvironmentSealKey? Value) cached) && cached.Expires > now)
            {
                return cached.Value;
            }
        }

        EnvironmentSealKey? resolved = null;
        using (ParsedJsonDocument<Environment>? document = await this.store.GetAsync(environment, AccessContext.System, cancellationToken).ConfigureAwait(false))
        {
            if (document is { } found)
            {
                Environment.EnvironmentCheckpointKeyInfo registration = found.RootElement.CheckpointKey;
                if (((JsonElement)registration).ValueKind == JsonValueKind.Object)
                {
                    // Realised at the leaf: the key id and the decoded seal key are the values the sealer imports.
                    resolved = new EnvironmentSealKey((string)registration.KeyId, Convert.FromBase64String((string)registration.SealKey));
                }
            }
        }

        lock (this.gate)
        {
            this.cache[environment] = (now + this.cacheTtl, resolved);
        }

        return resolved;
    }
}