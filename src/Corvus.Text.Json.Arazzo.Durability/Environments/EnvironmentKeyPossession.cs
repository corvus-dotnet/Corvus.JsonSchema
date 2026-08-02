// <copyright file="EnvironmentKeyPossession.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>Why a key registration was refused, or that it was accepted (ADR 0065).</summary>
public enum EnvironmentKeyPossessionResult
{
    /// <summary>The registrant proved possession of the private seal half.</summary>
    Verified,

    /// <summary>The presented public key does not parse as an SPKI key on the declared algorithm's curve.</summary>
    KeyUnreadable,

    /// <summary>The declared algorithm is not one this deployment accepts.</summary>
    AlgorithmUnsupported,

    /// <summary>The signature does not verify over the framed tuple.</summary>
    SignatureInvalid,

    /// <summary>The signing instant falls outside the freshness window.</summary>
    NotFresh,

    /// <summary>An identifier exceeds the bound the signed tuple's stack allocation is sized from.</summary>
    IdentifierTooLong,
}

/// <summary>
/// Verifies the proof of possession on an environment key registration (ADR 0065). The registrant signs a framed
/// tuple with the private half of the seal key it presents, and this verifies that signature against the presented
/// key.
/// </summary>
/// <remarks>
/// <para>
/// What this establishes is bounded, and the bound matters. It proves the registrant controls the seal pair being
/// registered. It does <em>not</em> prove the environment's symmetric payload key exists, which is unprovable to a
/// party that must never hold it. A missing payload key is caught at runtime instead, by decision 10's rule that a
/// runner bound to a sealed environment faults rather than writing cleartext. What the proof removes is the weaker
/// failure: without it, "this environment has a registered key" is satisfiable by typing a string, and the tenancy
/// invariant would pass on a deployment where nothing whatever is protected.
/// </para>
/// <para>
/// There is no server-issued challenge, because the signed tuple names the environment, the key id, the key, and the
/// signing instant, so it fully determines the effect of accepting it. Replaying a captured registration re-registers
/// the identical generation and changes nothing, which makes registration idempotent by construction rather than by a
/// nonce store the control plane would have to keep and expire.
/// </para>
/// </remarks>
public static class EnvironmentKeyPossession
{
    /// <summary>The algorithm identifier for ECDSA over P-256 with SHA-256, matching the executor-package signer.</summary>
    public const string EcdsaP256Sha256 = "ES256";

    /// <summary>Gets <see cref="EcdsaP256Sha256"/> pre-encoded, so the request's UTF-8 is matched span-to-span.</summary>
    public static ReadOnlySpan<byte> EcdsaP256Sha256Utf8 => "ES256"u8;

    /// <summary>How far in the past a signing instant may sit and still be accepted.</summary>
    public static readonly TimeSpan DefaultFreshnessWindow = TimeSpan.FromMinutes(5);

    /// <summary>How far ahead of the server's clock a signing instant may sit, absorbing ordinary clock skew.</summary>
    public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(1);

    private const int MaxIdentifierLength = 256;

    /// <summary>Verifies a registration's proof of possession.</summary>
    /// <param name="environment">The environment the generation is being registered for.</param>
    /// <param name="keyId">The generation's id.</param>
    /// <param name="algorithm">The declared signature algorithm.</param>
    /// <param name="sealPublicKey">The presented public seal key, SPKI-encoded.</param>
    /// <param name="notBefore">The instant the registrant signed.</param>
    /// <param name="signature">The signature over the framed tuple, IEEE P1363 fixed-field.</param>
    /// <param name="now">The server's current instant.</param>
    /// <param name="freshnessWindow">How far in the past <paramref name="notBefore"/> may sit.</param>
    /// <param name="clockSkew">How far ahead of <paramref name="now"/> it may sit.</param>
    /// <returns>Whether the proof verified, and if not, which check failed.</returns>
    public static EnvironmentKeyPossessionResult Verify(
        string environment,
        string keyId,
        ReadOnlySpan<byte> algorithm,
        ReadOnlySpan<byte> sealPublicKey,
        DateTimeOffset notBefore,
        ReadOnlySpan<byte> signature,
        DateTimeOffset now,
        TimeSpan? freshnessWindow = null,
        TimeSpan? clockSkew = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(keyId);

        // Compared span-to-span against the pre-encoded name. The algorithm arrives as UTF-8 in the request body and is
        // matched against a fixed literal, so realizing it as a managed string to call string.Equals would put an
        // allocation between two byte ends for a comparison neither end needs.
        if (!algorithm.SequenceEqual(EcdsaP256Sha256Utf8))
        {
            return EnvironmentKeyPossessionResult.AlgorithmUnsupported;
        }

        // Freshness is checked before the signature so a captured registration cannot be replayed indefinitely, and
        // the future bound absorbs ordinary skew rather than admitting an arbitrarily post-dated registration.
        TimeSpan window = freshnessWindow ?? DefaultFreshnessWindow;
        TimeSpan skew = clockSkew ?? DefaultClockSkew;
        if (notBefore < now - window || notBefore > now + skew)
        {
            return EnvironmentKeyPossessionResult.NotFresh;
        }

        if (environment.Length > MaxIdentifierLength || keyId.Length > MaxIdentifierLength)
        {
            return EnvironmentKeyPossessionResult.IdentifierTooLong;
        }

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(sealPublicKey, out _);
        }
        catch (CryptographicException)
        {
            return EnvironmentKeyPossessionResult.KeyUnreadable;
        }

        if (ecdsa.KeySize != 256)
        {
            return EnvironmentKeyPossessionResult.KeyUnreadable;
        }

        Span<byte> tuple = stackalloc byte[MaxTupleLength(environment, keyId, sealPublicKey.Length)];
        int written = WriteSignedTuple(tuple, environment, keyId, sealPublicKey, notBefore);

        return ecdsa.VerifyData(tuple[..written], signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            ? EnvironmentKeyPossessionResult.Verified
            : EnvironmentKeyPossessionResult.SignatureInvalid;
    }

    /// <summary>
    /// Writes the tuple a registrant signs. Every field carries a 4-byte big-endian length, so a registration signed
    /// for one environment cannot be replayed against another whose name and key id happen to concatenate the same
    /// way. This is the same framing discipline the checkpoint derivation uses, and for the same reason.
    /// </summary>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="keyId">The generation's id.</param>
    /// <param name="sealPublicKey">The public seal key, SPKI-encoded.</param>
    /// <param name="notBefore">The signing instant.</param>
    /// <returns>The number of bytes written.</returns>
    public static int WriteSignedTuple(Span<byte> destination, string environment, string keyId, ReadOnlySpan<byte> sealPublicKey, DateTimeOffset notBefore)
    {
        int written = WriteField(destination, "environment-key-registration"u8);
        written += WriteField(destination[written..], environment);
        written += WriteField(destination[written..], keyId);
        written += WriteField(destination[written..], sealPublicKey);

        // The instant is framed as its UTC tick count, not as formatted text: two encodings of one moment would
        // otherwise be two different signing inputs, and a verifier reformatting the value would reject a valid proof.
        BinaryPrimitives.WriteInt64BigEndian(destination[written..], notBefore.UtcTicks);
        return written + sizeof(long);
    }

    /// <summary>Gets an upper bound on the signed tuple's length.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="keyId">The generation's id.</param>
    /// <param name="sealPublicKeyLength">The key's encoded length.</param>
    /// <returns>The maximum number of bytes <see cref="WriteSignedTuple"/> writes.</returns>
    public static int MaxTupleLength(string environment, string keyId, int sealPublicKeyLength)
        => (4 * (sizeof(int) + 28))
        + Encoding.UTF8.GetMaxByteCount(environment.Length + keyId.Length)
        + sealPublicKeyLength
        + sizeof(long);

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