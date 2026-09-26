// <copyright file="EnvironmentKeyRotation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>Why a rotation link was refused, or that it verified (ADR 0065 decision 12).</summary>
public enum EnvironmentKeyRotationResult
{
    /// <summary>The predecessor's private seal half signed the successor's registration.</summary>
    Verified,

    /// <summary>The predecessor's public key does not parse as a P-256 SPKI key.</summary>
    PredecessorKeyUnreadable,

    /// <summary>The signature does not verify under the predecessor's key over the framed rotation tuple.</summary>
    SignatureInvalid,

    /// <summary>An identifier exceeds the bound the signed tuple's stack allocation is sized from.</summary>
    IdentifierTooLong,
}

/// <summary>
/// The predecessor-signed rotation link of ADR 0065 decision 12. A generation registered into an environment that
/// already holds one names its predecessor and carries that predecessor's ES256 signature over the framed tuple
/// <c>("environment-key-rotation", environment, predecessorKeyId, keyId, sealPublicKey)</c>, made with the private half
/// of the predecessor's seal key. The link is a permanent fact of the generation, stored beside it and advertised with
/// it, and every party that pinned an earlier generation's fingerprint re-verifies it for itself: a runner advances
/// the generation it writes under, and an initiator seals to a successor, only along links that verify all the way
/// back to the fingerprint they pinned (<see cref="EnvironmentKeyChain"/>). An unsigned change is therefore a change
/// nobody follows, whoever made it.
/// </summary>
/// <remarks>
/// The tuple carries no instant. The registration that presents the link is fresh by its own proof of possession,
/// and the link itself is re-verified for the life of the generation by parties that hold no clock the registrant
/// signed against; a replayed link re-registers the identical successor, which changes nothing.
/// </remarks>
public static class EnvironmentKeyRotation
{
    private const int MaxIdentifierLength = 256;

    /// <summary>Verifies a rotation link.</summary>
    /// <param name="environment">The environment the successor is registered for.</param>
    /// <param name="predecessorKeyId">The predecessor generation's id.</param>
    /// <param name="predecessorPublicKey">The predecessor's public seal key, SPKI-encoded, as the environment recorded it.</param>
    /// <param name="keyId">The successor generation's id.</param>
    /// <param name="sealPublicKey">The successor's public seal key, SPKI-encoded.</param>
    /// <param name="signature">The predecessor's signature over the framed tuple, IEEE P1363 fixed-field.</param>
    /// <returns>Whether the link verified, and if not, which check failed.</returns>
    public static EnvironmentKeyRotationResult Verify(
        string environment,
        string predecessorKeyId,
        ReadOnlySpan<byte> predecessorPublicKey,
        string keyId,
        ReadOnlySpan<byte> sealPublicKey,
        ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(predecessorKeyId);
        ArgumentNullException.ThrowIfNull(keyId);

        if (environment.Length > MaxIdentifierLength || predecessorKeyId.Length > MaxIdentifierLength || keyId.Length > MaxIdentifierLength)
        {
            return EnvironmentKeyRotationResult.IdentifierTooLong;
        }

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(predecessorPublicKey, out _);
        }
        catch (CryptographicException)
        {
            return EnvironmentKeyRotationResult.PredecessorKeyUnreadable;
        }

        if (ecdsa.KeySize != 256)
        {
            return EnvironmentKeyRotationResult.PredecessorKeyUnreadable;
        }

        Span<byte> tuple = stackalloc byte[MaxTupleLength(environment, predecessorKeyId, keyId, sealPublicKey.Length)];
        int written = WriteSignedTuple(tuple, environment, predecessorKeyId, keyId, sealPublicKey);
        return ecdsa.VerifyData(tuple[..written], signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            ? EnvironmentKeyRotationResult.Verified
            : EnvironmentKeyRotationResult.SignatureInvalid;
    }

    /// <summary>
    /// Writes the tuple a predecessor signs to hand over to a successor. Every field carries a 4-byte big-endian
    /// length, as the registration tuple does, so a link signed for one environment or one successor cannot be
    /// re-framed as another.
    /// </summary>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="predecessorKeyId">The predecessor generation's id.</param>
    /// <param name="keyId">The successor generation's id.</param>
    /// <param name="sealPublicKey">The successor's public seal key, SPKI-encoded.</param>
    /// <returns>The number of bytes written.</returns>
    public static int WriteSignedTuple(Span<byte> destination, string environment, string predecessorKeyId, string keyId, ReadOnlySpan<byte> sealPublicKey)
    {
        int written = WriteField(destination, "environment-key-rotation"u8);
        written += WriteField(destination[written..], environment);
        written += WriteField(destination[written..], predecessorKeyId);
        written += WriteField(destination[written..], keyId);
        written += WriteField(destination[written..], sealPublicKey);
        return written;
    }

    /// <summary>Gets an upper bound on the signed tuple's length.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="predecessorKeyId">The predecessor generation's id.</param>
    /// <param name="keyId">The successor generation's id.</param>
    /// <param name="sealPublicKeyLength">The successor key's encoded length.</param>
    /// <returns>The maximum number of bytes <see cref="WriteSignedTuple"/> writes.</returns>
    public static int MaxTupleLength(string environment, string predecessorKeyId, string keyId, int sealPublicKeyLength)
        => (5 * (sizeof(int) + 24))
        + Encoding.UTF8.GetMaxByteCount(environment.Length + predecessorKeyId.Length + keyId.Length)
        + sealPublicKeyLength;

    /// <summary>Signs a rotation link as the predecessor: what an operator holding the outgoing private seal half hands to the registration.</summary>
    /// <param name="predecessor">The predecessor's private seal key.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="predecessorKeyId">The predecessor generation's id.</param>
    /// <param name="keyId">The successor generation's id.</param>
    /// <param name="sealPublicKey">The successor's public seal key, SPKI-encoded.</param>
    /// <returns>The signature, IEEE P1363 fixed-field.</returns>
    public static byte[] Sign(ECDsa predecessor, string environment, string predecessorKeyId, string keyId, ReadOnlySpan<byte> sealPublicKey)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(predecessorKeyId);
        ArgumentNullException.ThrowIfNull(keyId);
        Span<byte> tuple = stackalloc byte[MaxTupleLength(environment, predecessorKeyId, keyId, sealPublicKey.Length)];
        int written = WriteSignedTuple(tuple, environment, predecessorKeyId, keyId, sealPublicKey);
        return predecessor.SignData(tuple[..written], HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static int WriteField(Span<byte> destination, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, value.Length);
        value.CopyTo(destination[sizeof(int)..]);
        return sizeof(int) + value.Length;
    }

    private static int WriteField(Span<byte> destination, string value)
    {
        int length = Encoding.UTF8.GetBytes(value, destination[sizeof(int)..]);
        BinaryPrimitives.WriteInt32BigEndian(destination, length);
        return sizeof(int) + length;
    }
}

/// <summary>One generation as an environment advertises it, for chain verification (ADR 0065 decision 12).</summary>
/// <param name="KeyId">The generation's id.</param>
/// <param name="SealPublicKey">Its public seal key, SPKI-encoded.</param>
/// <param name="Active">Whether the control plane holds it active.</param>
/// <param name="PredecessorKeyId">The generation it was rotated from, or <see langword="null"/> for a first registration.</param>
/// <param name="RotationSignature">The predecessor's signature over the rotation tuple, or <see langword="null"/> with no predecessor.</param>
public readonly record struct AdvertisedKeyGeneration(string KeyId, byte[] SealPublicKey, bool Active, string? PredecessorKeyId = null, byte[]? RotationSignature = null);

/// <summary>
/// Walks the rotation links from a candidate generation back to a pinned fingerprint (ADR 0065 decisions 10 and 12):
/// the candidate is the pinned key itself, or its predecessor's signature verifies over its registration and the
/// predecessor in turn reaches the pin. Nothing the control plane says about a generation is taken on trust, only what
/// a private key the tenant held signed; a generation with no link, a link that does not verify, a predecessor the
/// environment does not advertise, a cycle, or a chain longer than any rotation history should be, all fail to reach.
/// </summary>
public static class EnvironmentKeyChain
{
    /// <summary>The most links a chain is followed for; a rotation history longer than this is treated as unreachable rather than walked.</summary>
    public const int MaxDepth = 64;

    /// <summary>Whether a candidate generation reaches the pinned fingerprint along verified rotation links.</summary>
    /// <param name="environment">The environment the generations belong to; inside every signed link.</param>
    /// <param name="pinnedFingerprint">The pinned fingerprint: the base64 SHA-256 of a registered seal key's SubjectPublicKeyInfo.</param>
    /// <param name="keyId">The candidate generation.</param>
    /// <param name="generations">Every generation the environment advertises.</param>
    /// <returns><see langword="true"/> when the candidate is the pinned key or chains to it.</returns>
    public static bool Reaches(string environment, string pinnedFingerprint, string keyId, IReadOnlyList<AdvertisedKeyGeneration> generations)
        => Distance(environment, pinnedFingerprint, keyId, generations) >= 0;

    /// <summary>
    /// The number of verified links from a candidate generation back to the pinned fingerprint: 0 when the candidate
    /// is the pinned key itself, -1 when it does not reach it. A party choosing among several reachable generations
    /// takes the farthest, which is the latest successor.
    /// </summary>
    /// <param name="environment">The environment the generations belong to.</param>
    /// <param name="pinnedFingerprint">The pinned fingerprint.</param>
    /// <param name="keyId">The candidate generation.</param>
    /// <param name="generations">Every generation the environment advertises.</param>
    /// <returns>The link count, or -1.</returns>
    public static int Distance(string environment, string pinnedFingerprint, string keyId, IReadOnlyList<AdvertisedKeyGeneration> generations)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(pinnedFingerprint);
        ArgumentNullException.ThrowIfNull(keyId);
        ArgumentNullException.ThrowIfNull(generations);

        string current = keyId;
        for (int depth = 0; depth <= MaxDepth; depth++)
        {
            if (!TryFind(generations, current, out AdvertisedKeyGeneration generation))
            {
                return -1;
            }

            if (string.Equals(Anchoring.RunStartInitiator.SealKeyFingerprint(generation.SealPublicKey), pinnedFingerprint, StringComparison.Ordinal))
            {
                return depth;
            }

            if (generation.PredecessorKeyId is not { } predecessorKeyId
                || generation.RotationSignature is not { } signature
                || string.Equals(predecessorKeyId, generation.KeyId, StringComparison.Ordinal)
                || !TryFind(generations, predecessorKeyId, out AdvertisedKeyGeneration predecessor)
                || EnvironmentKeyRotation.Verify(environment, predecessorKeyId, predecessor.SealPublicKey, generation.KeyId, generation.SealPublicKey, signature) != EnvironmentKeyRotationResult.Verified)
            {
                return -1;
            }

            current = predecessorKeyId;
        }

        return -1;
    }

    private static bool TryFind(IReadOnlyList<AdvertisedKeyGeneration> generations, string keyId, out AdvertisedKeyGeneration found)
    {
        foreach (AdvertisedKeyGeneration generation in generations)
        {
            if (string.Equals(generation.KeyId, keyId, StringComparison.Ordinal))
            {
                found = generation;
                return true;
            }
        }

        found = default;
        return false;
    }
}