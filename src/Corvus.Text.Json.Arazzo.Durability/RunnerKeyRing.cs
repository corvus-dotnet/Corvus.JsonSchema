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

            // The seal key and the pinned initiators come together or not at all (decision 9): a seal key with no
            // initiator would open any seal, whoever made it, and a pinned initiator with no seal key opens nothing.
            IReadOnlyList<string> initiators = entry.Initiators ?? [];
            if ((entry.SealKey is null) != (initiators.Count == 0))
            {
                throw ThrowHelper.GetSealKeyNeedsInitiatorsException(entry.Environment);
            }

            byte[]? sealPrivateKey = null;
            List<byte[]>? initiatorKeys = null;
            if (entry.SealKey is { } sealKeyRef)
            {
                using (SecretMaterial material = await secrets.ResolveAsync(sealKeyRef, cancellationToken).ConfigureAwait(false))
                {
                    sealPrivateKey = DecodeSealKey(material.Reveal(), entry.Environment, entry.KeyId);
                }

                initiatorKeys = new List<byte[]>(initiators.Count);
                foreach (string initiator in initiators)
                {
                    initiatorKeys.Add(DecodeInitiatorKey(initiator, entry.Environment));
                }
            }

            built[entry.Environment] = new RunnerEnvironmentKeys(entry.KeyId, payloadKey, envelopeMac, entry.Sealed, sealPrivateKey, initiatorKeys);
        }

        return new RunnerKeyRing(built.ToFrozenDictionary(StringComparer.Ordinal));
    }

    // The seal private key is base64 PKCS#8, which is what a secret store holds for a key; it has to import as a
    // P-256 key the input seal can open with, or the runner does not start.
    private static byte[] DecodeSealKey(string material, string environment, string keyId)
    {
        byte[] pkcs8;
        try
        {
            pkcs8 = Convert.FromBase64String(material);
            using var key = System.Security.Cryptography.ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(pkcs8, out int consumed);
            if (consumed != pkcs8.Length || key.KeySize != 256)
            {
                throw ThrowHelper.GetSealKeyNotAKeyException(environment, keyId);
            }
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            throw ThrowHelper.GetSealKeyNotAKeyException(environment, keyId);
        }

        return pkcs8;
    }

    private static byte[] DecodeInitiatorKey(string material, string environment)
    {
        byte[] spki;
        try
        {
            spki = Convert.FromBase64String(material);
        }
        catch (FormatException)
        {
            throw ThrowHelper.GetInitiatorKeyNotAKeyException(environment);
        }

        if (!SealedStartSignature.IsP256PublicKey(spki))
        {
            throw ThrowHelper.GetInitiatorKeyNotAKeyException(environment);
        }

        return spki;
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
/// <param name="SealKey">Where the runner reads the private half of the environment's seal key for this generation (ADR 0065 decision 9): a reference into its own secret store, holding the key as base64 PKCS#8. With one, the runner opens sealed starts for the environment; without one it faults them.</param>
/// <param name="Initiators">The initiator public keys the runner pins (decision 9), each as base64 SubjectPublicKeyInfo of a P-256 key: a sealed start opens only under a signature one of them verifies. Required with <paramref name="SealKey"/>, and meaningless without it.</param>
public sealed record RunnerKeyRingEntry(string Environment, string KeyId, SecretRef PayloadKey, bool Sealed, SecretRef? SealKey = null, IReadOnlyList<string>? Initiators = null);

/// <summary>The keys a runner holds for one environment.</summary>
/// <param name="KeyId">The key generation.</param>
/// <param name="PayloadKey">The environment payload key (ADR 0065 decision 5): the derivation key every checkpoint's data key comes from.</param>
/// <param name="EnvelopeMac">The <c>envelope-mac</c> subkey, derived once.</param>
/// <param name="Sealed">Whether the environment is sealed for this runner.</param>
/// <param name="SealPrivateKey">The private half of the environment's seal key for this generation, as PKCS#8 (ADR 0065 decision 9), or <see langword="null"/> for a runner that opens no sealed starts there.</param>
/// <param name="InitiatorKeys">The pinned initiator public keys, each as SubjectPublicKeyInfo (decision 9); a sealed start opens only under a signature one of them verifies.</param>
public readonly record struct RunnerEnvironmentKeys(string KeyId, byte[] PayloadKey, byte[] EnvelopeMac, bool Sealed, byte[]? SealPrivateKey = null, IReadOnlyList<byte[]>? InitiatorKeys = null)
{
    /// <summary>Gets a value indicating whether these keys can open a sealed start: a seal key and at least one pinned initiator.</summary>
    public bool OpensSealedStarts => this.SealPrivateKey is not null && this.InitiatorKeys is { Count: > 0 };
}