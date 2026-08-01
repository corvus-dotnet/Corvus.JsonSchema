// <copyright file="SealedCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The <see cref="ICheckpointProtector"/> that seals checkpoints to an environment (ADR 0065): an
/// <see cref="EnvelopeCheckpointProtector"/> whose per-checkpoint data key is wrapped asymmetrically to the
/// environment's <b>seal (public) key</b>, so any writer holding the seal key can produce a checkpoint — the
/// control plane sealing the initial run document at start, the environment's runners sealing every
/// checkpoint — but only a holder of the environment's <b>open (private) key</b> can read one back. The open
/// key stays in the tenant's own custody and is resolved runner-side like every other environment credential
/// (ADR 0059); a control plane constructed with <see cref="ForSealing"/> can never open what it wrote.
/// </summary>
/// <remarks>
/// The wrapped-key blob is <c>keyIdLen(1) || keyId || epkLen(2 BE) || ephemeral public key (SubjectPublicKeyInfo)
/// || nonce(12) || tag(16) || wrapped data key</c>. Sealing generates an ephemeral P-256 key, derives the key-
/// encryption key from the ECDH agreement with the seal key via HKDF-SHA256 (the key id bound in the info), and
/// wraps the data key with AES-GCM, the run id as additional authenticated data — so a wrapped key cannot be
/// moved between runs or re-targeted to another key id without failing closed. The key id names the environment
/// keypair the checkpoint was sealed to, so rotation is a registration event: an opener holding a different key
/// refuses with both ids named. Key material is imported per call and zeroed where the platform allows, so the
/// type is safe to use concurrently.
/// </remarks>
public sealed class SealedCheckpointProtector : EnvelopeCheckpointProtector, IDisposable
{
    private const int KeyIdLengthPrefixSize = 1;
    private const int EpkLengthPrefixSize = 2;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] KekInfoPrefix = Encoding.UTF8.GetBytes("arazzo-sealed-checkpoint-v1:");

    private readonly byte[] keyIdUtf8;
    private readonly string keyId;
    private readonly byte[] sealKeySpki;
    private readonly byte[]? openKeyPkcs8;
    private bool disposed;

    private SealedCheckpointProtector(string keyId, byte[] sealKeySpki, byte[]? openKeyPkcs8)
    {
        this.keyId = keyId;
        this.keyIdUtf8 = Encoding.UTF8.GetBytes(keyId);
        this.sealKeySpki = sealKeySpki;
        this.openKeyPkcs8 = openKeyPkcs8;

        if (this.keyIdUtf8.Length is 0 or > byte.MaxValue)
        {
            throw new ArgumentException("The key id must be 1 to 255 UTF-8 bytes.", nameof(keyId));
        }
    }

    /// <summary>
    /// Creates a seal-only protector from the environment's registered seal (public) key — the control plane's
    /// posture: it can write the initial run document but can never read a checkpoint back (ADR 0065).
    /// </summary>
    /// <param name="keyId">The environment checkpoint key id the seal key is registered under.</param>
    /// <param name="sealKey">The seal key (a P-256 <c>SubjectPublicKeyInfo</c>). The bytes are copied.</param>
    /// <returns>The protector.</returns>
    public static SealedCheckpointProtector ForSealing(string keyId, ReadOnlySpan<byte> sealKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyId);

        // Validate the key up front so a bad registration fails at construction, not at the first checkpoint.
        using var ecdh = ECDiffieHellman.Create();
        ecdh.ImportSubjectPublicKeyInfo(sealKey, out _);
        return new SealedCheckpointProtector(keyId, sealKey.ToArray(), null);
    }

    /// <summary>
    /// Creates an opening (and sealing) protector from the environment's open (private) key — the runner's
    /// posture, the key resolved from the tenant's own custody (ADR 0059/0065).
    /// </summary>
    /// <param name="keyId">The environment checkpoint key id the keypair is registered under.</param>
    /// <param name="openKey">The open key (a P-256 PKCS#8 private key). The bytes are copied.</param>
    /// <returns>The protector.</returns>
    public static SealedCheckpointProtector ForOpening(string keyId, ReadOnlySpan<byte> openKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyId);

        using var ecdh = ECDiffieHellman.Create();
        ecdh.ImportPkcs8PrivateKey(openKey, out _);
        return new SealedCheckpointProtector(keyId, ecdh.ExportSubjectPublicKeyInfo(), openKey.ToArray());
    }

    /// <summary>
    /// Generates a fresh environment checkpoint keypair (P-256): the seal key to register on the environment
    /// record, the open key for the tenant's own secret store. The platform never sees the open key again.
    /// </summary>
    /// <returns>The keypair.</returns>
    public static SealedCheckpointKeyPair GenerateKeyPair()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return new SealedCheckpointKeyPair(ecdh.ExportSubjectPublicKeyInfo(), ecdh.ExportPkcs8PrivateKey());
    }

    /// <summary>
    /// Reads the key id out of a protected checkpoint blob when (and only when) it is structurally a sealed
    /// envelope this protector produced: the envelope framing's wrapped-key blob must satisfy every length
    /// invariant of the sealed layout exactly. A blob from any other protector fails the structural check, so a
    /// router can distinguish sealed state (routed by key id) from baseline-protected state (handed to the
    /// deployment's fallback protector).
    /// </summary>
    /// <param name="protectedCheckpoint">The protected checkpoint blob as stored.</param>
    /// <param name="keyId">On return, the key id the envelope names, when the blob is a sealed envelope.</param>
    /// <returns>Whether the blob is structurally a sealed envelope.</returns>
    public static bool TryReadSealedKeyId(ReadOnlySpan<byte> protectedCheckpoint, out string keyId)
    {
        keyId = string.Empty;

        // The base envelope framing: int32-BE wrappedLength || wrapped || nonce(12) || tag(16) || ciphertext.
        if (protectedCheckpoint.Length < 4)
        {
            return false;
        }

        int wrappedLength = BinaryPrimitives.ReadInt32BigEndian(protectedCheckpoint[..4]);
        if (wrappedLength <= 0 || protectedCheckpoint.Length < 4 + wrappedLength + NonceSize + TagSize)
        {
            return false;
        }

        // The sealed wrapped-key layout: keyIdLen(1) || keyId || epkLen(2 BE) || epk || nonce(12) || tag(16) ||
        // wrapped 32-byte data key — every length must agree exactly.
        ReadOnlySpan<byte> wrapped = protectedCheckpoint.Slice(4, wrappedLength);
        int keyIdLength = wrapped[0];
        if (keyIdLength == 0 || wrapped.Length < KeyIdLengthPrefixSize + keyIdLength + EpkLengthPrefixSize)
        {
            return false;
        }

        int epkLength = BinaryPrimitives.ReadUInt16BigEndian(wrapped.Slice(KeyIdLengthPrefixSize + keyIdLength, EpkLengthPrefixSize));
        if (epkLength == 0
            || wrapped.Length != KeyIdLengthPrefixSize + keyIdLength + EpkLengthPrefixSize + epkLength + NonceSize + TagSize + 32)
        {
            return false;
        }

        keyId = Encoding.UTF8.GetString(wrapped.Slice(KeyIdLengthPrefixSize, keyIdLength));
        return true;
    }

    /// <summary>Clears the in-memory copy of the open key.</summary>
    public void Dispose()
    {
        if (!this.disposed)
        {
            if (this.openKeyPkcs8 is { } key)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            this.disposed = true;
        }
    }

    /// <inheritdoc/>
    protected override ValueTask<ReadOnlyMemory<byte>> WrapAsync(ReadOnlyMemory<byte> dataKey, WorkflowRunId id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var sealKey = ECDiffieHellman.Create();
        sealKey.ImportSubjectPublicKeyInfo(this.sealKeySpki, out _);
        using ECDiffieHellmanPublicKey sealPublic = sealKey.PublicKey;

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[] shared = ephemeral.DeriveRawSecretAgreement(sealPublic);
        byte[] kek = new byte[32];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, kek, salt: null, info: this.KekInfo());

            byte[] epk = ephemeral.ExportSubjectPublicKeyInfo();
            byte[] wrapped = new byte[
                KeyIdLengthPrefixSize + this.keyIdUtf8.Length
                + EpkLengthPrefixSize + epk.Length
                + NonceSize + TagSize + dataKey.Length];

            int offset = 0;
            wrapped[offset++] = (byte)this.keyIdUtf8.Length;
            this.keyIdUtf8.CopyTo(wrapped.AsSpan(offset));
            offset += this.keyIdUtf8.Length;
            BinaryPrimitives.WriteUInt16BigEndian(wrapped.AsSpan(offset, EpkLengthPrefixSize), (ushort)epk.Length);
            offset += EpkLengthPrefixSize;
            epk.CopyTo(wrapped.AsSpan(offset));
            offset += epk.Length;

            Span<byte> nonce = wrapped.AsSpan(offset, NonceSize);
            Span<byte> tag = wrapped.AsSpan(offset + NonceSize, TagSize);
            Span<byte> wrappedKey = wrapped.AsSpan(offset + NonceSize + TagSize);

            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(kek, TagSize);
            aes.Encrypt(nonce, dataKey.Span, wrappedKey, tag, Encoding.UTF8.GetBytes(id.Value));
            return new ValueTask<ReadOnlyMemory<byte>>(wrapped);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <inheritdoc/>
    protected override ValueTask<ReadOnlyMemory<byte>> UnwrapAsync(ReadOnlyMemory<byte> wrappedDataKey, WorkflowRunId id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.openKeyPkcs8 is not { } openKeyBytes)
        {
            ThrowHelper.ThrowSealedCheckpointSealOnly();
            return default; // Unreachable; satisfies definite assignment.
        }

        ReadOnlySpan<byte> source = wrappedDataKey.Span;
        if (source.Length < KeyIdLengthPrefixSize)
        {
            ThrowHelper.ThrowCheckpointTooShort();
        }

        int keyIdLength = source[0];
        if (keyIdLength == 0 || source.Length < KeyIdLengthPrefixSize + keyIdLength + EpkLengthPrefixSize)
        {
            ThrowHelper.ThrowCheckpointMalformed();
        }

        ReadOnlySpan<byte> sealedKeyId = source.Slice(KeyIdLengthPrefixSize, keyIdLength);
        if (!sealedKeyId.SequenceEqual(this.keyIdUtf8))
        {
            ThrowHelper.ThrowSealedCheckpointKeyIdMismatch(Encoding.UTF8.GetString(sealedKeyId), this.keyId);
        }

        int offset = KeyIdLengthPrefixSize + keyIdLength;
        int epkLength = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, EpkLengthPrefixSize));
        offset += EpkLengthPrefixSize;
        if (epkLength == 0 || source.Length < offset + epkLength + NonceSize + TagSize)
        {
            ThrowHelper.ThrowCheckpointMalformed();
        }

        using var ephemeral = ECDiffieHellman.Create();
        try
        {
            ephemeral.ImportSubjectPublicKeyInfo(source.Slice(offset, epkLength), out _);
        }
        catch (CryptographicException)
        {
            ThrowHelper.ThrowCheckpointMalformed();
        }

        offset += epkLength;
        using ECDiffieHellmanPublicKey ephemeralPublic = ephemeral.PublicKey;
        using var openKey = ECDiffieHellman.Create();
        openKey.ImportPkcs8PrivateKey(openKeyBytes, out _);

        byte[] shared = openKey.DeriveRawSecretAgreement(ephemeralPublic);
        byte[] kek = new byte[32];
        byte[] dataKey = new byte[source.Length - offset - NonceSize - TagSize];
        try
        {
            HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, kek, salt: null, info: this.KekInfo());

            ReadOnlySpan<byte> nonce = source.Slice(offset, NonceSize);
            ReadOnlySpan<byte> tag = source.Slice(offset + NonceSize, TagSize);
            ReadOnlySpan<byte> wrappedKey = source[(offset + NonceSize + TagSize)..];

            using var aes = new AesGcm(kek, TagSize);

            // Fails closed (CryptographicException) on tamper, a foreign open key, or a wrong run id.
            aes.Decrypt(nonce, wrappedKey, tag, dataKey, Encoding.UTF8.GetBytes(id.Value));
            return new ValueTask<ReadOnlyMemory<byte>>(dataKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    // The HKDF info binds the derivation to this protocol version and the key id, so a key-encryption key derived
    // for one environment keypair can never be replayed for another.
    private byte[] KekInfo()
    {
        byte[] info = new byte[KekInfoPrefix.Length + this.keyIdUtf8.Length];
        KekInfoPrefix.CopyTo(info, 0);
        this.keyIdUtf8.CopyTo(info, KekInfoPrefix.Length);
        return info;
    }
}

/// <summary>
/// A freshly generated environment checkpoint keypair (ADR 0065): the seal key is registered on the environment
/// record for every writer; the open key goes to the tenant's own secret store and never crosses the platform
/// boundary again.
/// </summary>
/// <param name="SealKey">The public seal key (P-256 <c>SubjectPublicKeyInfo</c>).</param>
/// <param name="OpenKey">The private open key (P-256 PKCS#8).</param>
public readonly record struct SealedCheckpointKeyPair(byte[] SealKey, byte[] OpenKey);