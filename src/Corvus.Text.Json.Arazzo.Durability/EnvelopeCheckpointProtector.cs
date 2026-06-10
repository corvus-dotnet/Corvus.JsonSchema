// <copyright file="EnvelopeCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A base <see cref="ICheckpointProtector"/> implementing envelope encryption: each checkpoint is encrypted
/// with a fresh random data key (AES-GCM, the run id as additional authenticated data), and that data key is
/// wrapped by a key-management service via the abstract <see cref="WrapAsync"/>/<see cref="UnwrapAsync"/>.
/// Concrete subclasses (Azure Key Vault, AWS KMS) supply only the wrap/unwrap calls.
/// </summary>
/// <remarks>
/// The wire format is <c>int32-BE wrappedKeyLength || wrappedKey || nonce(12) || tag(16) || ciphertext</c>.
/// A fresh data key and nonce are generated per checkpoint, and the plaintext data key is zeroed after use.
/// Because the checkpoint is bound to the run id as AES-GCM additional authenticated data, a stored blob cannot
/// be tampered with or moved to another run without decryption failing closed.
/// </remarks>
public abstract class EnvelopeCheckpointProtector : ICheckpointProtector
{
    private const int LengthPrefixSize = 4;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int DataKeySize = 32; // AES-256

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>> ProtectAsync(ReadOnlyMemory<byte> plaintext, WorkflowRunId id, CancellationToken cancellationToken)
    {
        byte[] dataKey = new byte[DataKeySize];
        RandomNumberGenerator.Fill(dataKey);
        try
        {
            ReadOnlyMemory<byte> wrapped = await this.WrapAsync(dataKey, id, cancellationToken).ConfigureAwait(false);

            ReadOnlySpan<byte> source = plaintext.Span;
            int wrappedLength = wrapped.Length;
            byte[] output = new byte[LengthPrefixSize + wrappedLength + NonceSize + TagSize + source.Length];
            BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(0, LengthPrefixSize), wrappedLength);
            wrapped.Span.CopyTo(output.AsSpan(LengthPrefixSize, wrappedLength));

            int offset = LengthPrefixSize + wrappedLength;
            Span<byte> nonce = output.AsSpan(offset, NonceSize);
            Span<byte> tag = output.AsSpan(offset + NonceSize, TagSize);
            Span<byte> ciphertext = output.AsSpan(offset + NonceSize + TagSize);

            RandomNumberGenerator.Fill(nonce);
            byte[] associatedData = Encoding.UTF8.GetBytes(id.Value);
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Encrypt(nonce, source, ciphertext, tag, associatedData);
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>> UnprotectAsync(ReadOnlyMemory<byte> ciphertext, WorkflowRunId id, CancellationToken cancellationToken)
    {
        if (ciphertext.Length < LengthPrefixSize)
        {
            throw new CryptographicException("The protected checkpoint is too short to be valid.");
        }

        int wrappedLength = BinaryPrimitives.ReadInt32BigEndian(ciphertext.Span[..LengthPrefixSize]);
        if (wrappedLength <= 0 || ciphertext.Length < LengthPrefixSize + wrappedLength + NonceSize + TagSize)
        {
            throw new CryptographicException("The protected checkpoint is malformed.");
        }

        ReadOnlyMemory<byte> wrapped = ciphertext.Slice(LengthPrefixSize, wrappedLength);
        byte[] dataKey = (await this.UnwrapAsync(wrapped, id, cancellationToken).ConfigureAwait(false)).ToArray();
        try
        {
            int offset = LengthPrefixSize + wrappedLength;
            ReadOnlySpan<byte> source = ciphertext.Span;
            ReadOnlySpan<byte> nonce = source.Slice(offset, NonceSize);
            ReadOnlySpan<byte> tag = source.Slice(offset + NonceSize, TagSize);
            ReadOnlySpan<byte> data = source[(offset + NonceSize + TagSize)..];

            byte[] plaintext = new byte[data.Length];
            byte[] associatedData = Encoding.UTF8.GetBytes(id.Value);
            using var aes = new AesGcm(dataKey, TagSize);

            // Fails closed (CryptographicException) on any tamper / wrong-run / wrong-data-key failure.
            aes.Decrypt(nonce, data, tag, plaintext, associatedData);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>Wraps (encrypts) a freshly generated data key with the key-management service's key.</summary>
    /// <param name="dataKey">The plaintext data key to protect.</param>
    /// <param name="id">The run the checkpoint belongs to (may be bound as extra context).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The wrapped (encrypted) data key to store alongside the ciphertext.</returns>
    protected abstract ValueTask<ReadOnlyMemory<byte>> WrapAsync(ReadOnlyMemory<byte> dataKey, WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>Unwraps (decrypts) a data key previously produced by <see cref="WrapAsync"/>.</summary>
    /// <param name="wrappedDataKey">The wrapped data key read back from storage.</param>
    /// <param name="id">The run the checkpoint belongs to (must match the wrap context).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The plaintext data key.</returns>
    protected abstract ValueTask<ReadOnlyMemory<byte>> UnwrapAsync(ReadOnlyMemory<byte> wrappedDataKey, WorkflowRunId id, CancellationToken cancellationToken);
}