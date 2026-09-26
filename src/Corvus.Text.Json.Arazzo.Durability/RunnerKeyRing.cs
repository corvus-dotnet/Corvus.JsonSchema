// <copyright file="RunnerKeyRing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The runner's allowlist (ADR 0065 decision 10): the environments a runner serves at all, and for each the keys it
/// serves them with. One entry per environment, read from the runner's own configuration and its own secret store;
/// nothing on it comes from the control plane. An environment with no entry is not served: a claim for it is handed
/// back, and a checkpoint for it is refused, so a binding the control plane writes for an environment the tenant did
/// not name gets the runner nothing. A sealed entry names the key generation the runner writes under, the payload key
/// (decision 5) and the fingerprint of the seal key the tenant registered, which the runner pins and checks against
/// what the control plane advertises; a clear entry names the environment alone and is served clear. The per-generation
/// subkeys are derived once here and held for the runner's life, so no checkpoint pays for one; the payload key is
/// held too, since every checkpoint's data key is derived from it afresh.
/// </summary>
public sealed class RunnerKeyRing
{
    private const int PayloadKeyLength = 32;

    private readonly FrozenDictionary<string, RunnerEnvironmentKeys> keys;
    private readonly FrozenSet<string> admitted;

    private RunnerKeyRing(FrozenDictionary<string, RunnerEnvironmentKeys> keys, FrozenSet<string> admitted)
    {
        this.keys = keys;
        this.admitted = admitted;
    }

    /// <summary>An empty ring: a runner that admits no environment and so serves nothing.</summary>
    public static RunnerKeyRing Empty { get; } = new(FrozenDictionary<string, RunnerEnvironmentKeys>.Empty, FrozenSet<string>.Empty);

    /// <summary>Gets a value indicating whether the ring admits no environment.</summary>
    public bool IsEmpty => this.admitted.Count == 0;

    /// <summary>
    /// Builds the ring: resolves each keyed entry's keys through the runner's secret resolver and derives its subkeys.
    /// An entry whose key cannot be resolved, or is not a 32-byte key, or a sealed entry with no key, or a keyed entry
    /// with no pinned seal-key fingerprint, fails the build: a runner never starts serving an environment it cannot
    /// serve as configured.
    /// </summary>
    /// <param name="entries">The environments the runner serves, and their keys.</param>
    /// <param name="secrets">The runner's secret resolver; may be <see langword="null"/> when no entry names a secret.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ring.</returns>
    public static async ValueTask<RunnerKeyRing> BuildAsync(IReadOnlyList<RunnerKeyRingEntry> entries, ISecretResolver? secrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var built = new Dictionary<string, RunnerEnvironmentKeys>(entries.Count, StringComparer.Ordinal);
        var admitted = new HashSet<string>(entries.Count, StringComparer.Ordinal);
        foreach (RunnerKeyRingEntry entry in entries)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.Environment);
            if (!admitted.Add(entry.Environment))
            {
                throw ThrowHelper.GetAllowlistEntryDuplicateException(entry.Environment);
            }

            if (entry.KeyId is null || entry.PayloadKey is not { } payloadKeyRef)
            {
                // A clear entry: the environment is served, and served clear. A sealed one has to hold its key.
                if (entry.Sealed || entry.KeyId is not null || entry.PayloadKey is not null || entry.SealKey is not null || entry.Initiators is { Count: > 0 } || entry.SealKeyFingerprint is not null || entry.MinimumKeyId is not null)
                {
                    throw ThrowHelper.GetAllowlistSealedEntryNeedsKeyException(entry.Environment);
                }

                continue;
            }

            ArgumentException.ThrowIfNullOrEmpty(entry.KeyId);
            if (secrets is null)
            {
                throw ThrowHelper.GetAllowlistNeedsSecretsException(entry.Environment);
            }

            // The pin is the runner's own record of the seal key the tenant registered (decision 10); an entry with a
            // key and no pin would take whatever key the control plane advertised, which is the substitution the pin
            // exists to catch.
            if (string.IsNullOrEmpty(entry.SealKeyFingerprint))
            {
                throw ThrowHelper.GetAllowlistSealKeyFingerprintRequiredException(entry.Environment, entry.KeyId);
            }

            // The minimum generation (decision 10) is the oldest generation the runner accepts. A ring holds one
            // generation per environment, so the minimum is that generation and a row under any other is refused
            // already; a minimum naming another generation waits on a ring that can hold more than one (decision 12).
            if (entry.MinimumKeyId is { } minimum && !string.Equals(minimum, entry.KeyId, StringComparison.Ordinal))
            {
                throw ThrowHelper.GetAllowlistMinimumGenerationNotHeldException(entry.Environment, minimum, entry.KeyId);
            }

            byte[] payloadKey;
            using (SecretMaterial material = await secrets.ResolveAsync(payloadKeyRef, cancellationToken).ConfigureAwait(false))
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

            built[entry.Environment] = new RunnerEnvironmentKeys(entry.KeyId, payloadKey, envelopeMac, entry.Sealed, sealPrivateKey, initiatorKeys, entry.SealKeyFingerprint);
        }

        return new RunnerKeyRing(built.ToFrozenDictionary(StringComparer.Ordinal), admitted.ToFrozenSet(StringComparer.Ordinal));
    }

    /// <summary>Builds a ring from keys already in hand, for a host that holds them itself (tests, and a listener that unwraps its own). Every keyed environment is admitted; <paramref name="clearEnvironments"/> are admitted and served clear.</summary>
    /// <param name="keys">The per-environment keys.</param>
    /// <param name="clearEnvironments">Environments admitted with no key.</param>
    /// <returns>The ring.</returns>
    public static RunnerKeyRing From(IReadOnlyDictionary<string, RunnerEnvironmentKeys> keys, params string[] clearEnvironments)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var admitted = new HashSet<string>(keys.Keys, StringComparer.Ordinal);
        foreach (string environment in clearEnvironments)
        {
            ArgumentException.ThrowIfNullOrEmpty(environment);
            admitted.Add(environment);
        }

        return new RunnerKeyRing(keys.ToFrozenDictionary(StringComparer.Ordinal), admitted.ToFrozenSet(StringComparer.Ordinal));
    }

    /// <summary>A ring that admits the named environments and serves each clear: the allowlist of a runner with no keys.</summary>
    /// <param name="environments">The environments served clear.</param>
    /// <returns>The ring.</returns>
    public static RunnerKeyRing Admitting(params string[] environments)
        => From(FrozenDictionary<string, RunnerEnvironmentKeys>.Empty, environments);

    /// <summary>Whether the ring admits an environment at all (decision 10): served, clear or sealed. Anything else is not the runner's to serve.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> when the environment has an entry.</returns>
    public bool Admits(string environment)
        => this.admitted.Contains(environment);

    /// <summary>Looks up the keys for an environment.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="keys">The keys, when the ring holds a key for the environment.</param>
    /// <returns><see langword="true"/> when the ring holds a key for the environment.</returns>
    public bool TryGet(string environment, out RunnerEnvironmentKeys keys)
        => this.keys.TryGetValue(environment, out keys);

    /// <summary>Whether the runner is configured to serve an environment sealed: it refuses a clear row on load, other than the genesis row.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> when the environment's entry is marked sealed.</returns>
    public bool IsSealed(string environment)
        => this.keys.TryGetValue(environment, out RunnerEnvironmentKeys keys) && keys.Sealed;

    /// <summary>Gets every environment the ring holds a key for.</summary>
    public IEnumerable<string> Environments => this.keys.Keys;

    /// <summary>Gets every environment the ring admits, keyed or clear.</summary>
    public IEnumerable<string> AdmittedEnvironments => this.admitted;

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

    // The seal private key is base64 PKCS#8, which is what a secret store holds for a key; it has to import as a
    // P-256 key the input seal can open with, or the runner does not start.
    private static byte[] DecodeSealKey(string material, string environment, string keyId)
    {
        byte[] pkcs8;
        try
        {
            pkcs8 = Convert.FromBase64String(material);
            using var key = ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(pkcs8, out int consumed);
            if (consumed != pkcs8.Length || key.KeySize != 256)
            {
                throw ThrowHelper.GetSealKeyNotAKeyException(environment, keyId);
            }
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
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
}

/// <summary>One environment's entry in a runner's allowlist (ADR 0065 decision 10).</summary>
/// <param name="Environment">The environment the runner serves.</param>
/// <param name="Sealed">Whether the environment is sealed for this runner: it refuses a clear row on open, other than the genesis row. A sealed entry holds a key.</param>
/// <param name="KeyId">The key generation the runner writes under; the id the control plane's registration carries. Absent on a clear entry.</param>
/// <param name="PayloadKey">Where the runner reads the environment payload key: a reference into its own secret store, holding the key's 32 bytes as base64. Absent on a clear entry.</param>
/// <param name="SealKeyFingerprint">The pinned fingerprint of the seal key the tenant registered for <paramref name="KeyId"/>: the base64 SHA-256 of its SubjectPublicKeyInfo. Required with a key; the runner checks it against what the control plane advertises and suspends the environment on any other.</param>
/// <param name="SealKey">Where the runner reads the private half of the environment's seal key for this generation (decision 9): a reference into its own secret store, holding the key as base64 PKCS#8. With one, the runner opens sealed starts for the environment; without one it faults them.</param>
/// <param name="Initiators">The initiator public keys the runner pins (decision 9), each as base64 SubjectPublicKeyInfo of a P-256 key: a sealed start opens only under a signature one of them verifies. Required with <paramref name="SealKey"/>, and meaningless without it.</param>
/// <param name="MinimumKeyId">The oldest generation the runner accepts a row under (decision 10). With one generation held it is <paramref name="KeyId"/>, which is the default; another value needs a ring holding more than one generation (decision 12) and does not build until then.</param>
public sealed record RunnerKeyRingEntry(
    string Environment,
    bool Sealed = false,
    string? KeyId = null,
    SecretRef? PayloadKey = null,
    string? SealKeyFingerprint = null,
    SecretRef? SealKey = null,
    IReadOnlyList<string>? Initiators = null,
    string? MinimumKeyId = null)
{
    /// <summary>A clear entry: the environment is served, with no key.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The entry.</returns>
    public static RunnerKeyRingEntry Clear(string environment) => new(environment);
}

/// <summary>The keys a runner holds for one environment.</summary>
/// <param name="KeyId">The key generation.</param>
/// <param name="PayloadKey">The environment payload key (ADR 0065 decision 5): the derivation key every checkpoint's data key comes from.</param>
/// <param name="EnvelopeMac">The <c>envelope-mac</c> subkey, derived once.</param>
/// <param name="Sealed">Whether the environment is sealed for this runner.</param>
/// <param name="SealPrivateKey">The private half of the environment's seal key for this generation, as PKCS#8 (ADR 0065 decision 9), or <see langword="null"/> for a runner that opens no sealed starts there.</param>
/// <param name="InitiatorKeys">The pinned initiator public keys, each as SubjectPublicKeyInfo (decision 9); a sealed start opens only under a signature one of them verifies.</param>
/// <param name="SealKeyFingerprint">The pinned fingerprint of the registered seal key (decision 10): the base64 SHA-256 of its SubjectPublicKeyInfo, or <see langword="null"/> for keys built without a pin (a listener that holds its own).</param>
public readonly record struct RunnerEnvironmentKeys(string KeyId, byte[] PayloadKey, byte[] EnvelopeMac, bool Sealed, byte[]? SealPrivateKey = null, IReadOnlyList<byte[]>? InitiatorKeys = null, string? SealKeyFingerprint = null)
{
    /// <summary>Gets a value indicating whether these keys can open a sealed start: a seal key and at least one pinned initiator.</summary>
    public bool OpensSealedStarts => this.SealPrivateKey is not null && this.InitiatorKeys is { Count: > 0 };

    /// <summary>Whether an advertised seal public key is the pinned one.</summary>
    /// <param name="sealPublicKey">The advertised key, as SubjectPublicKeyInfo.</param>
    /// <returns><see langword="true"/> when its fingerprint is the pinned fingerprint; <see langword="false"/> when it is not, or nothing is pinned.</returns>
    public bool Pins(ReadOnlySpan<byte> sealPublicKey)
        => this.SealKeyFingerprint is { } pinned && string.Equals(pinned, RunStartInitiator.SealKeyFingerprint(sealPublicKey), StringComparison.Ordinal);
}