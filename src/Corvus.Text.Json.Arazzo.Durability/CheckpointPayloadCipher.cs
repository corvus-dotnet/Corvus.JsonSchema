// <copyright file="CheckpointPayloadCipher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The payload encryption of ADR 0065 decision 5, as built: AES-256-GCM under a data key derived for the one
/// encryption (<see cref="CheckpointDerivation.DeriveDataKey"/>) from the environment payload key, the environment,
/// the key generation, the run, the checkpoint sequence and a fresh 32-byte salt. The nonce is the counter's first
/// value, twelve zero bytes, which is sound only because a data key is used for exactly one encryption; that is what
/// the fresh salt per operation guarantees. The associated data is exactly <c>(runId, environmentId, keyId,
/// uint64(sequence))</c>, length-framed, and nothing else: the algorithm id and the key id are authenticated by the
/// row's MAC (decision 4), not here, so an unsupported algorithm and a failed authentication fault identically.
/// </summary>
public static class CheckpointPayloadCipher
{
    /// <summary>The salt's length: what <see cref="CheckpointDerivation.DeriveDataKey"/> takes, fresh per operation.</summary>
    public const int SaltLength = 32;

    /// <summary>The nonce's length: AES-GCM's standard 96 bits.</summary>
    public const int NonceLength = 12;

    /// <summary>The tag's length: AES-GCM's full 128 bits.</summary>
    public const int TagLength = 16;

    private const int KeyLength = 32;
    private const int LengthPrefix = sizeof(uint);
    private const int MaxIdentifierLength = 256;

    /// <summary>
    /// Encrypts one checkpoint payload. The salt is drawn fresh here, never supplied, so no caller can reuse one
    /// across a retry; the nonce is written for the row to carry, so the row is self-describing under the framing.
    /// </summary>
    /// <param name="payloadKey">The environment payload key.</param>
    /// <param name="environmentId">The environment.</param>
    /// <param name="keyId">The key generation.</param>
    /// <param name="runId">The run.</param>
    /// <param name="sequence">The checkpoint sequence the payload is saved at.</param>
    /// <param name="plaintext">The payload plaintext.</param>
    /// <param name="salt">Receives the fresh salt (<see cref="SaltLength"/> bytes).</param>
    /// <param name="nonce">Receives the nonce (<see cref="NonceLength"/> bytes).</param>
    /// <param name="tag">Receives the tag (<see cref="TagLength"/> bytes).</param>
    /// <param name="ciphertext">Receives the ciphertext, the same length as <paramref name="plaintext"/>.</param>
    public static void Encrypt(ReadOnlySpan<byte> payloadKey, string environmentId, string keyId, string runId, ulong sequence, ReadOnlySpan<byte> plaintext, Span<byte> salt, Span<byte> nonce, Span<byte> tag, Span<byte> ciphertext)
    {
        CheckLengths(salt, nonce, tag);
        ArgumentOutOfRangeException.ThrowIfNotEqual(ciphertext.Length, plaintext.Length, nameof(ciphertext));

        RandomNumberGenerator.Fill(salt);
        nonce.Clear();
        Span<byte> dataKey = stackalloc byte[KeyLength];
        Span<byte> associatedData = stackalloc byte[MaxAssociatedDataLength(environmentId, keyId, runId)];
        try
        {
            CheckpointDerivation.DeriveDataKey(payloadKey, environmentId, keyId, runId, sequence, salt, dataKey);
            int written = WriteAssociatedData(associatedData, environmentId, keyId, runId, sequence);
            using var aes = new AesGcm(dataKey, TagLength);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData[..written]);
        }
        finally
        {
            dataKey.Clear();
        }
    }

    /// <summary>
    /// Decrypts one checkpoint payload, failing closed: a wrong key, a payload moved to another run, environment,
    /// generation or sequence, and a tampered ciphertext or tag all fault the same way.
    /// </summary>
    /// <param name="payloadKey">The environment payload key.</param>
    /// <param name="environmentId">The environment.</param>
    /// <param name="keyId">The key generation.</param>
    /// <param name="runId">The run.</param>
    /// <param name="sequence">The checkpoint sequence the payload was saved at.</param>
    /// <param name="salt">The salt the row carries.</param>
    /// <param name="nonce">The nonce the row carries.</param>
    /// <param name="tag">The tag the row carries.</param>
    /// <param name="ciphertext">The ciphertext the row carries.</param>
    /// <param name="plaintext">Receives the plaintext, the same length as <paramref name="ciphertext"/>.</param>
    /// <exception cref="CryptographicException">The payload does not authenticate under these inputs.</exception>
    public static void Decrypt(ReadOnlySpan<byte> payloadKey, string environmentId, string keyId, string runId, ulong sequence, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        CheckLengths(salt, nonce, tag);
        ArgumentOutOfRangeException.ThrowIfNotEqual(plaintext.Length, ciphertext.Length, nameof(plaintext));

        Span<byte> dataKey = stackalloc byte[KeyLength];
        Span<byte> associatedData = stackalloc byte[MaxAssociatedDataLength(environmentId, keyId, runId)];
        try
        {
            CheckpointDerivation.DeriveDataKey(payloadKey, environmentId, keyId, runId, sequence, salt, dataKey);
            int written = WriteAssociatedData(associatedData, environmentId, keyId, runId, sequence);
            using var aes = new AesGcm(dataKey, TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData[..written]);
        }
        catch (CryptographicException)
        {
            // Nothing of a failed decryption leaves this method: a partial plaintext is not a plaintext.
            plaintext.Clear();
            throw;
        }
        finally
        {
            dataKey.Clear();
        }
    }

    private static void CheckLengths(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(salt.Length, SaltLength, nameof(salt));
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, NonceLength, nameof(nonce));
        ArgumentOutOfRangeException.ThrowIfNotEqual(tag.Length, TagLength, nameof(tag));
    }

    // Identifiers are bounded the way the derivation bounds them, so the stack allocation above is bounded too; the
    // derivation itself refuses a longer one before anything is encrypted.
    private static int MaxAssociatedDataLength(string environmentId, string keyId, string runId)
    {
        if (environmentId.Length > MaxIdentifierLength || keyId.Length > MaxIdentifierLength || runId.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"Derivation identifiers are limited to {MaxIdentifierLength} characters.");
        }

        return (3 * LengthPrefix) + Encoding.UTF8.GetMaxByteCount(environmentId.Length + keyId.Length + runId.Length) + sizeof(ulong);
    }

    // AAD = len‖runId ‖ len‖environmentId ‖ len‖keyId ‖ uint64BE(sequence). Framed, so two splits of the same bytes
    // are two different associated data, and the sequence is fixed-width so it cannot be confused with an identifier.
    private static int WriteAssociatedData(Span<byte> destination, string environmentId, string keyId, string runId, ulong sequence)
    {
        int written = WriteField(destination, runId);
        written += WriteField(destination[written..], environmentId);
        written += WriteField(destination[written..], keyId);
        BinaryPrimitives.WriteUInt64BigEndian(destination[written..], sequence);
        return written + sizeof(ulong);
    }

    private static int WriteField(Span<byte> destination, string value)
    {
        int length = Encoding.UTF8.GetBytes(value, destination[LengthPrefix..]);
        BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)length);
        return LengthPrefix + length;
    }
}