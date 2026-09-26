// <copyright file="RunnerKeyRing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
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
/// not name gets the runner nothing. A keyed entry names the key generations the runner holds, oldest first (decision
/// 12): every row under any of them at or above the entry's minimum generation opens, and the runner writes under
/// one of them, the <b>write generation</b>. That is the newest one to begin with, and a runner that checks the
/// control plane's advertised generations selects the newest it holds that is active and reaches the pinned seal-key
/// fingerprint along verified rotation links, so a rotation the tenant registers is followed without a restart. A
/// clear entry names the environment alone and is served clear. The per-generation subkeys are derived once here and
/// held for the runner's life, so no checkpoint pays for one; the payload keys are held too, since every checkpoint's
/// data key is derived from one afresh.
/// </summary>
public sealed class RunnerKeyRing
{
    private const int PayloadKeyLength = 32;

    private readonly FrozenDictionary<string, RunnerEnvironmentKeys> keys;
    private readonly FrozenSet<string> admitted;
    private readonly ConcurrentDictionary<string, string> writing = new(StringComparer.Ordinal);

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
    /// with no pinned seal-key fingerprint, or a minimum generation the entry does not hold, fails the build: a runner
    /// never starts serving an environment it cannot serve as configured.
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

            IReadOnlyList<RunnerKeyGeneration> configured = entry.HeldGenerations;
            if (configured.Count == 0)
            {
                // A clear entry: the environment is served, and served clear. A sealed one has to hold its key.
                if (entry.Sealed || entry.KeyId is not null || entry.PayloadKey is not null || entry.SealKey is not null || entry.Initiators is { Count: > 0 } || entry.SealKeyFingerprint is not null || entry.MinimumKeyId is not null)
                {
                    throw ThrowHelper.GetAllowlistSealedEntryNeedsKeyException(entry.Environment);
                }

                continue;
            }

            if (secrets is null)
            {
                throw ThrowHelper.GetAllowlistNeedsSecretsException(entry.Environment);
            }

            // The pin is the runner's own record of a seal key the tenant registered (decision 10); an entry with a
            // key and no pin would take whatever key the control plane advertised, which is the substitution the pin
            // exists to catch. With more than one generation the pin may be any of them: a later one reaches it along
            // the rotation links (decision 12).
            if (string.IsNullOrEmpty(entry.SealKeyFingerprint))
            {
                throw ThrowHelper.GetAllowlistSealKeyFingerprintRequiredException(entry.Environment, configured[^1].KeyId);
            }

            // The seal keys and the pinned initiators come together or not at all (decision 9): a seal key with no
            // initiator would open any seal, whoever made it, and a pinned initiator with no seal key opens nothing.
            IReadOnlyList<string> initiators = entry.Initiators ?? [];
            bool anySealKey = false;
            foreach (RunnerKeyGeneration generation in configured)
            {
                anySealKey |= generation.SealKey is not null;
            }

            if (anySealKey != initiators.Count > 0)
            {
                throw ThrowHelper.GetSealKeyNeedsInitiatorsException(entry.Environment);
            }

            var held = new List<RunnerGenerationKeys>(configured.Count);
            var ids = new HashSet<string>(configured.Count, StringComparer.Ordinal);
            foreach (RunnerKeyGeneration generation in configured)
            {
                ArgumentException.ThrowIfNullOrEmpty(generation.KeyId);
                if (!ids.Add(generation.KeyId))
                {
                    throw ThrowHelper.GetAllowlistGenerationDuplicateException(entry.Environment, generation.KeyId);
                }

                byte[] payloadKey;
                using (SecretMaterial material = await secrets.ResolveAsync(generation.PayloadKey, cancellationToken).ConfigureAwait(false))
                {
                    // The payload key is stored as base64 of its 32 bytes, which is what a secret store holds for a binary key.
                    payloadKey = new byte[PayloadKeyLength];
                    if (!Convert.TryFromBase64Chars(material.Reveal(), payloadKey, out int written) || written != PayloadKeyLength)
                    {
                        throw ThrowHelper.GetPayloadKeyNotAKeyException(entry.Environment, generation.KeyId);
                    }
                }

                // The payload key itself stays on the ring: every encryption derives its data key from it (decision 5),
                // so it is held for the runner's life alongside the subkey derived once here.
                byte[] envelopeMac = new byte[PayloadKeyLength];
                CheckpointDerivation.DeriveSubkey(payloadKey, CheckpointSubkey.EnvelopeMac, entry.Environment, generation.KeyId, envelopeMac);

                byte[]? sealPrivateKey = null;
                if (generation.SealKey is { } sealKeyRef)
                {
                    using SecretMaterial material = await secrets.ResolveAsync(sealKeyRef, cancellationToken).ConfigureAwait(false);
                    sealPrivateKey = DecodeSealKey(material.Reveal(), entry.Environment, generation.KeyId);
                }

                held.Add(new RunnerGenerationKeys(generation.KeyId, payloadKey, envelopeMac, sealPrivateKey));
            }

            // The minimum generation (decision 10) is the oldest generation the runner accepts a row under; it has to
            // be one the runner holds, and is the oldest held by default.
            if (entry.MinimumKeyId is { } minimum && !ids.Contains(minimum))
            {
                throw ThrowHelper.GetAllowlistMinimumGenerationNotHeldException(entry.Environment, minimum);
            }

            List<byte[]>? initiatorKeys = null;
            if (initiators.Count > 0)
            {
                initiatorKeys = new List<byte[]>(initiators.Count);
                foreach (string initiator in initiators)
                {
                    initiatorKeys.Add(DecodeInitiatorKey(initiator, entry.Environment));
                }
            }

            built[entry.Environment] = RunnerEnvironmentKeys.Holding(held, entry.Sealed, initiatorKeys, entry.SealKeyFingerprint, entry.MinimumKeyId);
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

    /// <summary>Looks up the keys for an environment, projected onto the generation the runner currently writes under.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="keys">The keys, when the ring holds a key for the environment.</param>
    /// <returns><see langword="true"/> when the ring holds a key for the environment.</returns>
    public bool TryGet(string environment, out RunnerEnvironmentKeys keys)
    {
        if (!this.keys.TryGetValue(environment, out RunnerEnvironmentKeys held))
        {
            keys = default;
            return false;
        }

        keys = this.writing.TryGetValue(environment, out string? selected) ? held.WritingUnder(selected) : held;
        return true;
    }

    /// <summary>
    /// Selects the generation the runner writes under for an environment (decision 12): one it holds at or above its
    /// minimum. A runner that checks the control plane's advertised generations calls this with the newest held
    /// generation that is active and reaches the pin; until then the newest held generation is the one written under.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <param name="keyId">The generation to write under.</param>
    /// <returns><see langword="true"/> when the selection changed the write generation.</returns>
    /// <exception cref="ArgumentException">The ring does not hold the generation for the environment, or it is below the minimum.</exception>
    public bool SelectWriteGeneration(string environment, string keyId)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        if (!this.keys.TryGetValue(environment, out RunnerEnvironmentKeys held) || !held.Accepts(keyId))
        {
            throw ThrowHelper.GetAllowlistGenerationNotHeldException(environment, keyId);
        }

        string previous = this.writing.TryGetValue(environment, out string? selected) ? selected : held.KeyId;
        this.writing[environment] = keyId;
        return !string.Equals(previous, keyId, StringComparison.Ordinal);
    }

    /// <summary>The generation the runner currently writes under for an environment, or <see langword="null"/> for one it holds no key for.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The write generation.</returns>
    public string? WriteGenerationOf(string environment)
        => this.TryGet(environment, out RunnerEnvironmentKeys keys) ? keys.KeyId : null;

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
/// <param name="KeyId">The one key generation the runner holds, when it holds one; the id the control plane's registration carries. Absent on a clear entry, and absent when <paramref name="Generations"/> lists them instead.</param>
/// <param name="PayloadKey">Where the runner reads the environment payload key for <paramref name="KeyId"/>: a reference into its own secret store, holding the key's 32 bytes as base64.</param>
/// <param name="SealKeyFingerprint">The pinned fingerprint of a seal key the tenant registered: the base64 SHA-256 of its SubjectPublicKeyInfo. Required with a key; the runner writes only under a generation whose advertised key is this one or reaches it along verified rotation links (decision 12), and suspends the environment otherwise.</param>
/// <param name="SealKey">Where the runner reads the private half of the environment's seal key for <paramref name="KeyId"/> (decision 9): a reference into its own secret store, holding the key as base64 PKCS#8. With one, the runner opens sealed starts under that generation; without one it faults them.</param>
/// <param name="Initiators">The initiator public keys the runner pins (decision 9), each as base64 SubjectPublicKeyInfo of a P-256 key: a sealed start opens only under a signature one of them verifies. Required with any seal key, and meaningless without one.</param>
/// <param name="MinimumKeyId">The oldest generation the runner accepts a row under (decision 10): one it holds, the oldest held by default. Rows under an older held generation are refused, which is how an operator retires one in two steps, first the minimum, then the secret.</param>
/// <param name="Generations">The key generations the runner holds for the environment, oldest first (decision 12), in place of the single <paramref name="KeyId"/>, <paramref name="PayloadKey"/> and <paramref name="SealKey"/>. The runner writes under the newest of them the control plane holds active and that reaches the pin, and opens rows under any of them at or above the minimum.</param>
public sealed record RunnerKeyRingEntry(
    string Environment,
    bool Sealed = false,
    string? KeyId = null,
    SecretRef? PayloadKey = null,
    string? SealKeyFingerprint = null,
    SecretRef? SealKey = null,
    IReadOnlyList<string>? Initiators = null,
    string? MinimumKeyId = null,
    IReadOnlyList<RunnerKeyGeneration>? Generations = null)
{
    /// <summary>A clear entry: the environment is served, with no key.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The entry.</returns>
    public static RunnerKeyRingEntry Clear(string environment) => new(environment);

    /// <summary>
    /// Gets the generations the entry holds, oldest first: <see cref="Generations"/> when given, else the single
    /// generation named by <see cref="KeyId"/> and <see cref="PayloadKey"/>, else none. An entry naming both forms
    /// does not build.
    /// </summary>
    public IReadOnlyList<RunnerKeyGeneration> HeldGenerations
    {
        get
        {
            if (this.Generations is { Count: > 0 } listed)
            {
                if (this.KeyId is not null || this.PayloadKey is not null || this.SealKey is not null)
                {
                    throw ThrowHelper.GetAllowlistGenerationsAndShorthandException(this.Environment);
                }

                return listed;
            }

            if (this.KeyId is null || this.PayloadKey is not { } payloadKey)
            {
                return [];
            }

            return [new RunnerKeyGeneration(this.KeyId, payloadKey, this.SealKey)];
        }
    }
}

/// <summary>One key generation a runner holds for an environment, as configured (ADR 0065 decision 12).</summary>
/// <param name="KeyId">The generation's id; the id the control plane's registration carries.</param>
/// <param name="PayloadKey">Where the runner reads the generation's payload key: a reference into its own secret store, holding the key's 32 bytes as base64.</param>
/// <param name="SealKey">Where the runner reads the private half of the generation's seal key (decision 9), as base64 PKCS#8, or <see langword="null"/> when this runner opens no sealed starts under it.</param>
public sealed record RunnerKeyGeneration(string KeyId, SecretRef PayloadKey, SecretRef? SealKey = null);

/// <summary>The keys a runner holds for one generation of one environment (ADR 0065 decision 12).</summary>
/// <param name="KeyId">The key generation.</param>
/// <param name="PayloadKey">The generation's payload key (decision 5): the derivation key every checkpoint's data key comes from.</param>
/// <param name="EnvelopeMac">The <c>envelope-mac</c> subkey, derived once.</param>
/// <param name="SealPrivateKey">The private half of the generation's seal key, as PKCS#8 (decision 9), or <see langword="null"/> for a runner that opens no sealed starts under it.</param>
public readonly record struct RunnerGenerationKeys(string KeyId, byte[] PayloadKey, byte[] EnvelopeMac, byte[]? SealPrivateKey = null);

/// <summary>
/// The keys a runner holds for one environment: the generation it writes under, in the positional members, and every
/// generation it holds in <see cref="Generations"/>, oldest first (ADR 0065 decision 12). Keys built with the
/// positional members alone hold that one generation.
/// </summary>
/// <param name="KeyId">The key generation written under.</param>
/// <param name="PayloadKey">Its payload key (ADR 0065 decision 5): the derivation key every checkpoint's data key comes from.</param>
/// <param name="EnvelopeMac">Its <c>envelope-mac</c> subkey, derived once.</param>
/// <param name="Sealed">Whether the environment is sealed for this runner.</param>
/// <param name="SealPrivateKey">The private half of its seal key, as PKCS#8 (ADR 0065 decision 9), or <see langword="null"/> for a runner that opens no sealed starts under it.</param>
/// <param name="InitiatorKeys">The pinned initiator public keys, each as SubjectPublicKeyInfo (decision 9); a sealed start opens only under a signature one of them verifies.</param>
/// <param name="SealKeyFingerprint">The pinned fingerprint of a registered seal key (decision 10): the base64 SHA-256 of its SubjectPublicKeyInfo, or <see langword="null"/> for keys built without a pin (a listener that holds its own).</param>
/// <param name="Generations">Every generation held, oldest first, or <see langword="null"/> when the positional generation is the only one.</param>
/// <param name="MinimumKeyId">The oldest generation accepted on open, or <see langword="null"/> for the oldest held.</param>
public readonly record struct RunnerEnvironmentKeys(
    string KeyId,
    byte[] PayloadKey,
    byte[] EnvelopeMac,
    bool Sealed,
    byte[]? SealPrivateKey = null,
    IReadOnlyList<byte[]>? InitiatorKeys = null,
    string? SealKeyFingerprint = null,
    IReadOnlyList<RunnerGenerationKeys>? Generations = null,
    string? MinimumKeyId = null)
{
    /// <summary>Keys holding several generations, writing under the newest.</summary>
    /// <param name="generations">The generations, oldest first; at least one.</param>
    /// <param name="sealed">Whether the environment is sealed for this runner.</param>
    /// <param name="initiatorKeys">The pinned initiator public keys.</param>
    /// <param name="sealKeyFingerprint">The pinned seal-key fingerprint.</param>
    /// <param name="minimumKeyId">The oldest generation accepted on open, or <see langword="null"/> for the oldest held.</param>
    /// <returns>The keys.</returns>
    public static RunnerEnvironmentKeys Holding(IReadOnlyList<RunnerGenerationKeys> generations, bool @sealed, IReadOnlyList<byte[]>? initiatorKeys = null, string? sealKeyFingerprint = null, string? minimumKeyId = null)
    {
        ArgumentNullException.ThrowIfNull(generations);
        ArgumentOutOfRangeException.ThrowIfZero(generations.Count);
        RunnerGenerationKeys newest = generations[^1];
        return new RunnerEnvironmentKeys(newest.KeyId, newest.PayloadKey, newest.EnvelopeMac, @sealed, newest.SealPrivateKey, initiatorKeys, sealKeyFingerprint, generations, minimumKeyId);
    }

    /// <summary>Gets a value indicating whether these keys can open a sealed start: a seal key under some held generation and at least one pinned initiator.</summary>
    public bool OpensSealedStarts
    {
        get
        {
            if (this.InitiatorKeys is not { Count: > 0 })
            {
                return false;
            }

            if (this.Generations is not { } generations)
            {
                return this.SealPrivateKey is not null;
            }

            foreach (RunnerGenerationKeys generation in generations)
            {
                if (generation.SealPrivateKey is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets the generation written under, as its own keys.</summary>
    public RunnerGenerationKeys WriteGeneration => new(this.KeyId, this.PayloadKey, this.EnvelopeMac, this.SealPrivateKey);

    /// <summary>Gets the number of generations held.</summary>
    public int GenerationCount => this.Generations?.Count ?? 1;

    /// <summary>Whether an advertised seal public key is the pinned one.</summary>
    /// <param name="sealPublicKey">The advertised key, as SubjectPublicKeyInfo.</param>
    /// <returns><see langword="true"/> when its fingerprint is the pinned fingerprint; <see langword="false"/> when it is not, or nothing is pinned.</returns>
    public bool Pins(ReadOnlySpan<byte> sealPublicKey)
        => this.SealKeyFingerprint is { } pinned && string.Equals(pinned, RunStartInitiator.SealKeyFingerprint(sealPublicKey), StringComparison.Ordinal);

    /// <summary>Whether a row under a generation is accepted on open (decisions 10 and 12): held, and at or above the minimum.</summary>
    /// <param name="keyId">The generation the row names.</param>
    /// <returns><see langword="true"/> when the generation is held and accepted.</returns>
    public bool Accepts(string keyId)
    {
        if (this.Generations is not { } generations)
        {
            return string.Equals(keyId, this.KeyId, StringComparison.Ordinal);
        }

        int minimum = 0;
        int index = -1;
        for (int i = 0; i < generations.Count; i++)
        {
            string held = generations[i].KeyId;
            if (this.MinimumKeyId is { } minimumKeyId && string.Equals(held, minimumKeyId, StringComparison.Ordinal))
            {
                minimum = i;
            }

            if (string.Equals(held, keyId, StringComparison.Ordinal))
            {
                index = i;
            }
        }

        return index >= minimum;
    }

    /// <summary>Looks up the keys of a held generation accepted on open.</summary>
    /// <param name="keyId">The generation.</param>
    /// <param name="generation">Its keys.</param>
    /// <returns><see langword="true"/> when the generation is held and accepted.</returns>
    public bool TryGetGeneration(string keyId, out RunnerGenerationKeys generation)
    {
        if (!this.Accepts(keyId))
        {
            generation = default;
            return false;
        }

        if (this.Generations is not { } generations)
        {
            generation = this.WriteGeneration;
            return true;
        }

        foreach (RunnerGenerationKeys held in generations)
        {
            if (string.Equals(held.KeyId, keyId, StringComparison.Ordinal))
            {
                generation = held;
                return true;
            }
        }

        generation = default;
        return false;
    }

    /// <summary>Enumerates the generations accepted on open, oldest first: what a delivery queries the wait index under (decision 4).</summary>
    /// <returns>The accepted generations.</returns>
    public IEnumerable<RunnerGenerationKeys> AcceptedGenerations()
    {
        if (this.Generations is not { } generations)
        {
            yield return this.WriteGeneration;
            yield break;
        }

        foreach (RunnerGenerationKeys held in generations)
        {
            if (this.Accepts(held.KeyId))
            {
                yield return held;
            }
        }
    }

    /// <summary>These keys, writing under another held generation.</summary>
    /// <param name="keyId">The generation to write under; held and accepted.</param>
    /// <returns>The keys, projected.</returns>
    public RunnerEnvironmentKeys WritingUnder(string keyId)
    {
        if (!this.TryGetGeneration(keyId, out RunnerGenerationKeys generation))
        {
            throw new ArgumentException($"Generation '{keyId}' is not held, or is below the minimum generation.", nameof(keyId));
        }

        return this with { KeyId = generation.KeyId, PayloadKey = generation.PayloadKey, EnvelopeMac = generation.EnvelopeMac, SealPrivateKey = generation.SealPrivateKey };
    }
}