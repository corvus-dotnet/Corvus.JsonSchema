// <copyright file="RunnerKeyRing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The keys a runner holds for the environments it serves (ADR 0065 decisions 5 and 10): one entry per environment,
/// naming the key generation and the environment payload key, read from the runner's own configuration and its own
/// secret store. Nothing about keys ever comes from the control plane. The per-generation subkeys are derived once
/// here and held for the runner's life, so no checkpoint pays for one; the payload key is held too, since every
/// checkpoint's data key is derived from it afresh. Every environment on the ring is one the runner encrypts for:
/// there is no MAC-only entry.
/// </summary>
public sealed class RunnerKeyRing
{
    private const int PayloadKeyLength = 32;

    private readonly FrozenDictionary<string, RunnerEnvironmentKeys> keys;

    private RunnerKeyRing(FrozenDictionary<string, RunnerEnvironmentKeys> keys)
    {
        this.keys = keys;
    }

    /// <summary>An empty ring: a runner that seals nothing and verifies nothing.</summary>
    public static RunnerKeyRing Empty { get; } = new(FrozenDictionary<string, RunnerEnvironmentKeys>.Empty);

    /// <summary>Gets a value indicating whether the ring holds no keys.</summary>
    public bool IsEmpty => this.keys.Count == 0;

    /// <summary>
    /// Builds the ring: resolves each entry's payload key through the runner's secret resolver and derives its
    /// subkeys. An entry whose key cannot be resolved, or is not a 32-byte key, fails the build: a runner never starts
    /// with a sealed environment it cannot seal.
    /// </summary>
    /// <param name="entries">The environments and their keys.</param>
    /// <param name="secrets">The runner's secret resolver.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ring.</returns>
    public static async ValueTask<RunnerKeyRing> BuildAsync(IReadOnlyList<RunnerKeyRingEntry> entries, ISecretResolver secrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(secrets);

        var built = new Dictionary<string, RunnerEnvironmentKeys>(entries.Count, StringComparer.Ordinal);
        foreach (RunnerKeyRingEntry entry in entries)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.Environment);
            ArgumentException.ThrowIfNullOrEmpty(entry.KeyId);

            byte[] payloadKey;
            using (SecretMaterial material = await secrets.ResolveAsync(entry.PayloadKey, cancellationToken).ConfigureAwait(false))
            {
                // The payload key is stored as base64 of its 32 bytes, which is what a secret store holds for a binary key.
                payloadKey = new byte[PayloadKeyLength];
                if (!Convert.TryFromBase64Chars(material.Reveal(), payloadKey, out int written) || written != PayloadKeyLength)
                {
                    throw ThrowHelper.GetPayloadKeyNotAKeyException(entry.Environment, entry.KeyId);
                }
            }

            // The payload key itself stays on the ring: every encryption derives its data key from it (decision 5),
            // so it is held for the runner's life alongside the subkey derived once here.
            byte[] envelopeMac = new byte[PayloadKeyLength];
            CheckpointDerivation.DeriveSubkey(payloadKey, CheckpointSubkey.EnvelopeMac, entry.Environment, entry.KeyId, envelopeMac);
            built[entry.Environment] = new RunnerEnvironmentKeys(entry.KeyId, payloadKey, envelopeMac, entry.Sealed);
        }

        return new RunnerKeyRing(built.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>Builds a ring from keys already in hand, for a host that holds them itself (tests, and a listener that unwraps its own).</summary>
    /// <param name="keys">The per-environment keys.</param>
    /// <returns>The ring.</returns>
    public static RunnerKeyRing From(IReadOnlyDictionary<string, RunnerEnvironmentKeys> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return new RunnerKeyRing(keys.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>Looks up the keys for an environment.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="keys">The keys, when the ring holds the environment.</param>
    /// <returns><see langword="true"/> when the ring holds the environment.</returns>
    public bool TryGet(string environment, out RunnerEnvironmentKeys keys)
        => this.keys.TryGetValue(environment, out keys);

    /// <summary>Whether the runner is configured to serve an environment sealed: it refuses a clear row on load, other than the genesis row.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> when the environment's entry is marked sealed.</returns>
    public bool IsSealed(string environment)
        => this.keys.TryGetValue(environment, out RunnerEnvironmentKeys keys) && keys.Sealed;

    /// <summary>Gets every environment on the ring.</summary>
    public IEnumerable<string> Environments => this.keys.Keys;

    /// <summary>Gets the environments on the ring that are marked sealed.</summary>
    public IEnumerable<string> SealedEnvironments
    {
        get
        {
            foreach (KeyValuePair<string, RunnerEnvironmentKeys> entry in this.keys)
            {
                if (entry.Value.Sealed)
                {
                    yield return entry.Key;
                }
            }
        }
    }
}

/// <summary>One environment's entry in a runner's key ring configuration.</summary>
/// <param name="Environment">The environment the runner serves.</param>
/// <param name="KeyId">The key generation the runner writes under; the id the control plane's registration carries.</param>
/// <param name="PayloadKey">Where the runner reads the environment payload key: a reference into its own secret store, holding the key's 32 bytes as base64.</param>
/// <param name="Sealed">Whether the environment is sealed for this runner: it refuses a clear row on open, other than the genesis row. The runner encrypts and MACs what it writes for the environment either way.</param>
public sealed record RunnerKeyRingEntry(string Environment, string KeyId, SecretRef PayloadKey, bool Sealed);

/// <summary>The keys a runner holds for one environment.</summary>
/// <param name="KeyId">The key generation.</param>
/// <param name="PayloadKey">The environment payload key (ADR 0065 decision 5): the derivation key every checkpoint's data key comes from.</param>
/// <param name="EnvelopeMac">The <c>envelope-mac</c> subkey, derived once.</param>
/// <param name="Sealed">Whether the environment is sealed for this runner.</param>
public readonly record struct RunnerEnvironmentKeys(string KeyId, byte[] PayloadKey, byte[] EnvelopeMac, bool Sealed);