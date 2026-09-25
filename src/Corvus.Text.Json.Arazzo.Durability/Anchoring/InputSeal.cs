// <copyright file="InputSeal.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The public-key seal a run's start inputs are wrapped to (ADR 0065 decision 9): RFC 9180 HPKE in base mode, with
/// DHKEM(P-256, HKDF-SHA256), HKDF-SHA256 and AES-256-GCM, single shot. The recipient key is the environment's
/// registered seal key, whose public half the control plane publishes and whose private half the runner alone holds,
/// so the initiator seals and only the runner opens. The construction is the RFC's, byte for byte: the KEM's labeled
/// extract-and-expand over the Diffie-Hellman secret and the two serialized points, the key schedule over the mode,
/// the empty PSK id and the info, and the AEAD under the derived key and base nonce at sequence zero. The RFC's
/// published vectors for this KEM and KDF hold against it.
/// </summary>
/// <remarks>
/// A seal gives confidentiality and binds the associated data; it gives no authenticity, since the public key is
/// public. The initiator's signature over the seal (<see cref="SealedStartSignature"/>) is what says who sealed.
/// </remarks>
public static class InputSeal
{
    /// <summary>The encapsulated key's length: an uncompressed P-256 point.</summary>
    public const int EncLength = 1 + (2 * CoordinateLength);

    /// <summary>The AEAD tag's length, which a sealed value carries after its ciphertext.</summary>
    public const int TagLength = 16;

    private const int CoordinateLength = 32;
    private const int HashLength = 32;
    private const int NonceLength = 12;
    private const int SecretLength = 32;
    private const byte ModeBase = 0;
    private const byte UncompressedPoint = 4;
    private const ushort KemId = 0x0010;
    private const ushort KdfId = 0x0001;
    private const int MaxStackInput = 512;

    /// <summary>AES-256-GCM, the AEAD every seal in this platform uses.</summary>
    internal const ushort AeadAes256Gcm = 0x0002;

    /// <summary>AES-128-GCM, which the RFC's P-256 vectors are given for; a conformance test's AEAD, never a seal's.</summary>
    internal const ushort AeadAes128Gcm = 0x0001;

    /// <summary>The sealed length for a plaintext: the ciphertext and its tag.</summary>
    /// <param name="plaintextLength">The plaintext's length.</param>
    /// <returns>The sealed value's length.</returns>
    public static int SealedLength(int plaintextLength) => checked(plaintextLength + TagLength);

    /// <summary>Seals a value to a recipient's public key.</summary>
    /// <param name="recipientPublicKey">The recipient's P-256 public key, as SubjectPublicKeyInfo.</param>
    /// <param name="info">The application info the key schedule binds.</param>
    /// <param name="associatedData">The associated data the AEAD binds.</param>
    /// <param name="plaintext">The value to seal.</param>
    /// <param name="enc">Receives the encapsulated key (<see cref="EncLength"/> bytes).</param>
    /// <param name="sealedValue">Receives the ciphertext and tag (<see cref="SealedLength"/> of the plaintext's length).</param>
    /// <exception cref="CryptographicException">The recipient key is not a P-256 key.</exception>
    public static void Seal(ReadOnlySpan<byte> recipientPublicKey, ReadOnlySpan<byte> info, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext, Span<byte> enc, Span<byte> sealedValue)
    {
        using ECDiffieHellman recipient = ECDiffieHellman.Create();
        recipient.ImportSubjectPublicKeyInfo(recipientPublicKey, out _);
        using ECDiffieHellman ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        Seal(recipient, ephemeral, info, associatedData, plaintext, enc, sealedValue, AeadAes256Gcm);
    }

    /// <summary>Opens a sealed value with the recipient's private key.</summary>
    /// <param name="recipientPrivateKey">The recipient's P-256 private key, as PKCS#8.</param>
    /// <param name="enc">The encapsulated key.</param>
    /// <param name="info">The application info the seal was made under.</param>
    /// <param name="associatedData">The associated data the seal was made under.</param>
    /// <param name="sealedValue">The ciphertext and tag.</param>
    /// <param name="plaintext">Receives the plaintext (the sealed length less <see cref="TagLength"/>).</param>
    /// <exception cref="CryptographicException">The value does not open: a wrong key, other associated data, other info, or a tampered ciphertext, all alike.</exception>
    public static void Open(ReadOnlySpan<byte> recipientPrivateKey, ReadOnlySpan<byte> enc, ReadOnlySpan<byte> info, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> sealedValue, Span<byte> plaintext)
    {
        using ECDiffieHellman recipient = ECDiffieHellman.Create();
        recipient.ImportPkcs8PrivateKey(recipientPrivateKey, out _);
        Open(recipient, enc, info, associatedData, sealedValue, plaintext, AeadAes256Gcm);
    }

    /// <summary>The seal with every input explicit, which is what a vector check needs and a caller never does.</summary>
    internal static void Seal(ECDiffieHellman recipient, ECDiffieHellman ephemeral, ReadOnlySpan<byte> info, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext, Span<byte> enc, Span<byte> sealedValue, ushort aeadId)
    {
        RequireP256(recipient);
        RequireP256(ephemeral);
        ArgumentOutOfRangeException.ThrowIfNotEqual(enc.Length, EncLength, nameof(enc));
        ArgumentOutOfRangeException.ThrowIfNotEqual(sealedValue.Length, SealedLength(plaintext.Length), nameof(sealedValue));
        Span<byte> recipientPoint = stackalloc byte[EncLength];
        SerializePoint(recipient, recipientPoint);
        SerializePoint(ephemeral, enc);
        Span<byte> key = stackalloc byte[KeyLength(aeadId)];
        Span<byte> nonce = stackalloc byte[NonceLength];
        try
        {
            byte[] dh = ephemeral.DeriveRawSecretAgreement(recipient.PublicKey);
            try
            {
                Schedule(dh, enc, recipientPoint, info, aeadId, key, nonce);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dh);
            }

            using var aead = new AesGcm(key, TagLength);
            aead.Encrypt(nonce, plaintext, sealedValue[..plaintext.Length], sealedValue[plaintext.Length..], associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>The open with every input explicit.</summary>
    internal static void Open(ECDiffieHellman recipient, ReadOnlySpan<byte> enc, ReadOnlySpan<byte> info, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> sealedValue, Span<byte> plaintext, ushort aeadId)
    {
        RequireP256(recipient);
        if (enc.Length != EncLength || enc[0] != UncompressedPoint || sealedValue.Length < TagLength || plaintext.Length != sealedValue.Length - TagLength)
        {
            throw new CryptographicException("The sealed value is malformed.");
        }

        Span<byte> recipientPoint = stackalloc byte[EncLength];
        SerializePoint(recipient, recipientPoint);
        Span<byte> key = stackalloc byte[KeyLength(aeadId)];
        Span<byte> nonce = stackalloc byte[NonceLength];
        try
        {
            using ECDiffieHellman ephemeral = ImportPoint(enc);
            byte[] dh = recipient.DeriveRawSecretAgreement(ephemeral.PublicKey);
            try
            {
                Schedule(dh, enc, recipientPoint, info, aeadId, key, nonce);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dh);
            }

            using var aead = new AesGcm(key, TagLength);
            aead.Decrypt(nonce, sealedValue[..plaintext.Length], sealedValue[plaintext.Length..], plaintext, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // DHKEM's ExtractAndExpand, then the base-mode key schedule (RFC 9180 sections 4.1 and 5.1): the shared secret
    // from the Diffie-Hellman value and both serialized points, the context from the mode and the hashed (empty) PSK
    // id and info, and the AEAD key and base nonce expanded from the secret under the ciphersuite's own suite id.
    private static void Schedule(ReadOnlySpan<byte> dh, ReadOnlySpan<byte> enc, ReadOnlySpan<byte> recipientPoint, ReadOnlySpan<byte> info, ushort aeadId, Span<byte> key, Span<byte> nonce)
    {
        Span<byte> kemSuite = stackalloc byte[3 + sizeof(ushort)];
        "KEM"u8.CopyTo(kemSuite);
        BinaryPrimitives.WriteUInt16BigEndian(kemSuite[3..], KemId);
        Span<byte> kemContext = stackalloc byte[2 * EncLength];
        enc.CopyTo(kemContext);
        recipientPoint.CopyTo(kemContext[EncLength..]);
        Span<byte> eaePrk = stackalloc byte[HashLength];
        Span<byte> sharedSecret = stackalloc byte[SecretLength];
        Span<byte> hpkeSuite = stackalloc byte[4 + (3 * sizeof(ushort))];
        Span<byte> pskIdHash = stackalloc byte[HashLength];
        Span<byte> infoHash = stackalloc byte[HashLength];
        Span<byte> context = stackalloc byte[1 + (2 * HashLength)];
        Span<byte> secret = stackalloc byte[HashLength];
        try
        {
            LabeledExtract(kemSuite, default, "eae_prk"u8, dh, eaePrk);
            LabeledExpand(kemSuite, eaePrk, "shared_secret"u8, kemContext, sharedSecret);

            "HPKE"u8.CopyTo(hpkeSuite);
            BinaryPrimitives.WriteUInt16BigEndian(hpkeSuite[4..], KemId);
            BinaryPrimitives.WriteUInt16BigEndian(hpkeSuite[6..], KdfId);
            BinaryPrimitives.WriteUInt16BigEndian(hpkeSuite[8..], aeadId);
            LabeledExtract(hpkeSuite, default, "psk_id_hash"u8, default, pskIdHash);
            LabeledExtract(hpkeSuite, default, "info_hash"u8, info, infoHash);
            context[0] = ModeBase;
            pskIdHash.CopyTo(context[1..]);
            infoHash.CopyTo(context[(1 + HashLength)..]);
            LabeledExtract(hpkeSuite, sharedSecret, "secret"u8, default, secret);
            LabeledExpand(hpkeSuite, secret, "key"u8, context, key);
            LabeledExpand(hpkeSuite, secret, "base_nonce"u8, context, nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(eaePrk);
            CryptographicOperations.ZeroMemory(sharedSecret);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    // LabeledExtract(salt, label, ikm) = Extract(salt, "HPKE-v1" || suite_id || label || ikm).
    private static void LabeledExtract(ReadOnlySpan<byte> suite, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> label, ReadOnlySpan<byte> ikm, Span<byte> prk)
    {
        int length = 7 + suite.Length + label.Length + ikm.Length;
        byte[]? rented = length > MaxStackInput ? System.Buffers.ArrayPool<byte>.Shared.Rent(length) : null;
        Span<byte> labeled = rented ?? stackalloc byte[MaxStackInput];
        labeled = labeled[..length];
        try
        {
            int at = 0;
            "HPKE-v1"u8.CopyTo(labeled);
            at += 7;
            suite.CopyTo(labeled[at..]);
            at += suite.Length;
            label.CopyTo(labeled[at..]);
            at += label.Length;
            ikm.CopyTo(labeled[at..]);
            HKDF.Extract(HashAlgorithmName.SHA256, labeled, salt, prk);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(labeled);
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    // LabeledExpand(prk, label, info, L) = Expand(prk, I2OSP(L, 2) || "HPKE-v1" || suite_id || label || info, L).
    private static void LabeledExpand(ReadOnlySpan<byte> suite, ReadOnlySpan<byte> prk, ReadOnlySpan<byte> label, ReadOnlySpan<byte> info, Span<byte> output)
    {
        int length = sizeof(ushort) + 7 + suite.Length + label.Length + info.Length;
        byte[]? rented = length > MaxStackInput ? System.Buffers.ArrayPool<byte>.Shared.Rent(length) : null;
        Span<byte> labeled = rented ?? stackalloc byte[MaxStackInput];
        labeled = labeled[..length];
        try
        {
            int at = 0;
            BinaryPrimitives.WriteUInt16BigEndian(labeled, (ushort)output.Length);
            at += sizeof(ushort);
            "HPKE-v1"u8.CopyTo(labeled[at..]);
            at += 7;
            suite.CopyTo(labeled[at..]);
            at += suite.Length;
            label.CopyTo(labeled[at..]);
            at += label.Length;
            info.CopyTo(labeled[at..]);
            HKDF.Expand(HashAlgorithmName.SHA256, prk, output, labeled);
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static int KeyLength(ushort aeadId) => aeadId switch
    {
        AeadAes256Gcm => 32,
        AeadAes128Gcm => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(aeadId)),
    };

    private static void RequireP256(ECDiffieHellman key)
    {
        if (key.KeySize != 256)
        {
            throw new CryptographicException("The seal key is not a P-256 key.");
        }
    }

    private static void SerializePoint(ECDiffieHellman key, Span<byte> point)
    {
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        if (parameters.Q.X is not { Length: CoordinateLength } x || parameters.Q.Y is not { Length: CoordinateLength } y)
        {
            throw new CryptographicException("The seal key is not a P-256 key.");
        }

        point[0] = UncompressedPoint;
        x.CopyTo(point[1..]);
        y.CopyTo(point[(1 + CoordinateLength)..]);
    }

    private static ECDiffieHellman ImportPoint(ReadOnlySpan<byte> point)
    {
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..(1 + CoordinateLength)].ToArray(), Y = point[(1 + CoordinateLength)..].ToArray() },
        };
        return ECDiffieHellman.Create(parameters);
    }
}