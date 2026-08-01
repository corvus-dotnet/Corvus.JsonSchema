// <copyright file="CheckpointDerivation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>The closed set of subkeys derived from an environment's payload key (ADR 0065 decision 5).</summary>
public enum CheckpointSubkey
{
    /// <summary>Per-encryption: the AES-GCM key for one checkpoint payload.</summary>
    DataKey,

    /// <summary>Per key generation: the HMAC key over the runner-authored region.</summary>
    EnvelopeMac,

    /// <summary>Per key generation: the HMAC key for blind wait-index entries.</summary>
    WaitIndex,

    /// <summary>Per key generation: the secret backing the ADR 0062 checkpoint-callback token.</summary>
    CheckpointToken,
}

/// <summary>
/// The normative key-derivation table of ADR 0065 decision 5. Every derivation input is length-framed and every
/// label is distinct, and <c>environmentId</c> is present in all four — these are not stylistic choices.
/// </summary>
/// <remarks>
/// <para>Without framing, <c>(keyId "k1", runId "0abc")</c> and <c>(keyId "k10", runId "abc")</c> derive the
/// <em>same</em> key; with a counter nonce that is a repeated <c>(key, nonce)</c> pair over different
/// plaintexts, which yields the GCM authentication subkey and hence payload forgery. Without
/// <c>environmentId</c>, one tenant registering the same <c>keyId</c> in two environments derives one
/// wait-index subkey, and a message delivered to one environment wakes a run parked in the other — defeating
/// environment pinning. Both were real defects found in review; the conformance suite now holds them closed.</para>
/// </remarks>
public static class CheckpointDerivation
{
    private const int KeySize = 32;

    // keyId is tenant-registered and runId is control-plane-issued, so neither is ours to trust with a stack
    // allocation. An identifier long enough to matter here is already malformed, and overflowing the stack is an
    // uncatchable process kill rather than a rejected write.
    private const int MaxIdentifierLength = 256;

    /// <summary>Derives one of the three per-generation subkeys.</summary>
    /// <param name="payloadKey">The environment's payload key.</param>
    /// <param name="subkey">Which subkey. <see cref="CheckpointSubkey.DataKey"/> is per encryption and uses <see cref="DeriveDataKey"/>.</param>
    /// <param name="environmentId">The environment the key belongs to.</param>
    /// <param name="keyId">The key generation.</param>
    /// <param name="destination">Receives 32 bytes.</param>
    public static void DeriveSubkey(ReadOnlySpan<byte> payloadKey, CheckpointSubkey subkey, string environmentId, string keyId, Span<byte> destination)
    {
        if (subkey == CheckpointSubkey.DataKey)
        {
            throw new ArgumentException("A data key is per encryption; use DeriveDataKey, which additionally frames the run, sequence, and salt.", nameof(subkey));
        }

        CheckIdentifiers(environmentId, keyId, string.Empty);

        Span<byte> info = stackalloc byte[MaxInfoLength(environmentId, keyId, string.Empty, 0)];
        int written = WriteFramed(info, Label(subkey), environmentId, keyId);
        HKDF.Expand(HashAlgorithmName.SHA256, payloadKey, destination[..KeySize], info[..written]);
    }

    /// <summary>
    /// Derives the data key for one encryption. A fresh salt is required for every encryption <em>operation</em>,
    /// never per logical checkpoint and never per retry: re-encrypting after a <c>409</c> with a cached salt
    /// restarts the counter nonce under the same derived key.
    /// </summary>
    /// <param name="payloadKey">The environment's payload key.</param>
    /// <param name="environmentId">The environment.</param>
    /// <param name="keyId">The key generation.</param>
    /// <param name="runId">The run.</param>
    /// <param name="sequence">The checkpoint sequence.</param>
    /// <param name="salt">A fresh 32-byte salt.</param>
    /// <param name="destination">Receives 32 bytes.</param>
    public static void DeriveDataKey(ReadOnlySpan<byte> payloadKey, string environmentId, string keyId, string runId, ulong sequence, ReadOnlySpan<byte> salt, Span<byte> destination)
    {
        CheckIdentifiers(environmentId, keyId, runId);

        Span<byte> info = stackalloc byte[MaxInfoLength(environmentId, keyId, runId, salt.Length)];
        int written = WriteFramed(info, Label(CheckpointSubkey.DataKey), environmentId, keyId, runId);

        BinaryPrimitives.WriteUInt64BigEndian(info[written..], sequence);
        written += sizeof(ulong);
        salt.CopyTo(info[written..]);
        written += salt.Length;

        HKDF.Expand(HashAlgorithmName.SHA256, payloadKey, destination[..KeySize], info[..written]);
    }

    private static ReadOnlySpan<byte> Label(CheckpointSubkey subkey) => subkey switch
    {
        CheckpointSubkey.DataKey => "data-key"u8,
        CheckpointSubkey.EnvelopeMac => "envelope-mac"u8,
        CheckpointSubkey.WaitIndex => "wait-index"u8,
        CheckpointSubkey.CheckpointToken => "checkpoint-token"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(subkey)),
    };

    private static void CheckIdentifiers(string environmentId, string keyId, string runId)
    {
        if (environmentId.Length > MaxIdentifierLength || keyId.Length > MaxIdentifierLength || runId.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"Derivation identifiers are limited to {MaxIdentifierLength} characters.");
        }
    }

    private static int MaxInfoLength(string environmentId, string keyId, string runId, int saltLength)
        => (4 * (sizeof(int) + 16))
        + Encoding.UTF8.GetMaxByteCount(environmentId.Length + keyId.Length + runId.Length)
        + sizeof(ulong)
        + saltLength;

    // Each variable-length field carries a 4-byte big-endian length. That is the whole defence against two
    // different field splits producing one info string, and therefore one key.
    private static int WriteFramed(Span<byte> destination, ReadOnlySpan<byte> label, string environmentId, string keyId, string? runId = null)
    {
        int written = WriteField(destination, label);
        written += WriteField(destination[written..], environmentId);
        written += WriteField(destination[written..], keyId);
        if (runId is not null)
        {
            written += WriteField(destination[written..], runId);
        }

        return written;
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

/// <summary>
/// The checkpoint digest of ADR 0065 decision 6 — a checkpoint's identity, and therefore a byte-exact contract
/// between the five checkpoint writers, the genesis writer, and every open that compares one.
/// </summary>
public static class CheckpointDigest
{
    /// <summary>
    /// The digest over a checkpoint's submitted bytes: the header, runner region, salt, nonce, tag, ciphertext,
    /// and MAC, in stored order. It never covers the server-owned control-plane region, which is joined at read
    /// time and is not the runner's to commit to.
    /// </summary>
    /// <param name="submittedBytes">Everything the runner submits, in stored order.</param>
    /// <returns>The digest.</returns>
    public static AnchorDigest ForSubmitted(ReadOnlySpan<byte> submittedBytes)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "checkpoint-digest"u8);
        AppendField(hash, submittedBytes);
        hash.GetHashAndReset(digest);
        return new AnchorDigest(digest);
    }

    /// <summary>
    /// The digest of the genesis row, which has no runner region and cannot have a runner MAC: it is written by
    /// the control plane from the initiator's sealed, signed inputs, and the initiator signature is the only
    /// tenant-side authenticator that exists at sequence 0. The two fields are framed independently, because an
    /// unframed pair would let a different split of the same bytes produce the same digest once a second
    /// signature algorithm made the signature length vary.
    /// </summary>
    /// <param name="sealedInputs">The sealed-inputs ciphertext.</param>
    /// <param name="initiatorSignature">The initiator's signature over the seal.</param>
    /// <returns>The digest.</returns>
    public static AnchorDigest ForGenesis(ReadOnlySpan<byte> sealedInputs, ReadOnlySpan<byte> initiatorSignature)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "checkpoint-digest-genesis"u8);
        AppendField(hash, sealedInputs);
        AppendField(hash, initiatorSignature);
        hash.GetHashAndReset(digest);
        return new AnchorDigest(digest);
    }

    private static void AppendField(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}