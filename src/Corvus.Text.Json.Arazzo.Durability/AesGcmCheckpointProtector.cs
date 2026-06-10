// <copyright file="AesGcmCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An <see cref="ICheckpointProtector"/> that encrypts checkpoints with AES-GCM (authenticated encryption)
/// under a single symmetric key. The run id is bound as additional authenticated data, so a checkpoint cannot
/// be moved between runs, and any tampering, wrong key, or wrong run fails authentication on decrypt.
/// </summary>
/// <remarks>
/// The wire format is <c>nonce(12) || tag(16) || ciphertext</c>, with a fresh random nonce per encryption. The
/// supplied key must be 16, 24, or 32 bytes (AES-128/192/256). For managed key custody and rotation, supply the
/// key from a KMS/Key Vault, or implement <see cref="ICheckpointProtector"/> directly over an envelope scheme
/// (a per-checkpoint data key wrapped by a Key Vault key). A new <see cref="AesGcm"/> is created per call, so
/// the type is safe to use concurrently.
/// </remarks>
public sealed class AesGcmCheckpointProtector : ICheckpointProtector, IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] key;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="AesGcmCheckpointProtector"/> class.</summary>
    /// <param name="key">A 16-, 24-, or 32-byte AES key. The bytes are copied.</param>
    public AesGcmCheckpointProtector(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException("The AES key must be 16, 24, or 32 bytes (AES-128/192/256).", nameof(key));
        }

        this.key = key.ToArray();
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ProtectAsync(ReadOnlyMemory<byte> plaintext, WorkflowRunId id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> source = plaintext.Span;
        byte[] output = new byte[NonceSize + TagSize + source.Length];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        Span<byte> tag = output.AsSpan(NonceSize, TagSize);
        Span<byte> ciphertext = output.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        byte[] associatedData = Encoding.UTF8.GetBytes(id.Value);
        using var aes = new AesGcm(this.key, TagSize);
        aes.Encrypt(nonce, source, ciphertext, tag, associatedData);
        return new ValueTask<ReadOnlyMemory<byte>>(output);
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> UnprotectAsync(ReadOnlyMemory<byte> ciphertext, WorkflowRunId id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> source = ciphertext.Span;
        if (source.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The protected checkpoint is too short to be valid.");
        }

        ReadOnlySpan<byte> nonce = source[..NonceSize];
        ReadOnlySpan<byte> tag = source.Slice(NonceSize, TagSize);
        ReadOnlySpan<byte> data = source[(NonceSize + TagSize)..];

        byte[] plaintext = new byte[data.Length];
        byte[] associatedData = Encoding.UTF8.GetBytes(id.Value);
        using var aes = new AesGcm(this.key, TagSize);

        // AesGcm.Decrypt throws AuthenticationTagMismatchException (a CryptographicException) on any
        // tamper / wrong-key / wrong-run failure — i.e. it fails closed.
        aes.Decrypt(nonce, data, tag, plaintext, associatedData);
        return new ValueTask<ReadOnlyMemory<byte>>((ReadOnlyMemory<byte>)plaintext);
    }

    /// <summary>Clears the in-memory copy of the key.</summary>
    public void Dispose()
    {
        if (!this.disposed)
        {
            CryptographicOperations.ZeroMemory(this.key);
            this.disposed = true;
        }
    }
}