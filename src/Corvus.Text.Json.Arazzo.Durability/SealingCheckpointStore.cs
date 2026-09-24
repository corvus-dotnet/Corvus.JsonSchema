// <copyright file="SealingCheckpointStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The runner's side of checkpoint confidentiality and integrity (ADR 0065 decisions 4, 5 and 10): a decorator over
/// the runner's checkpoint store that, for an environment its <see cref="RunnerKeyRing"/> holds, encrypts the payload
/// of every clear row it saves (<see cref="CheckpointPayloadCipher"/>), seals the row with the unified MAC
/// (<see cref="CheckpointIntegrity"/>), and on every load verifies the MAC, decrypts the payload and hands back a
/// clear row. A load whose MAC does not verify, that names a generation the ring does not hold, or whose payload does
/// not decrypt faults before any of its envelope is trusted; a clear row for an environment the runner serves sealed
/// is refused the same way, other than the genesis row. The run itself stays crypto-free: it sees clear rows go out
/// and come back exactly as before, and nothing above this store ever holds a key.
/// </summary>
/// <remarks>
/// The store encrypts clear rows only. A row that comes back through it for a control-plane-region write (the
/// serverless checkpoint coordinator faulting a run on its budget over a row it loaded here) is a clear row by then,
/// and is encrypted again under a fresh salt, which is what the salt-per-operation rule of decision 5 requires. A
/// row that already names a key generation is not one this store re-seals: it refuses rather than encrypt twice.
/// </remarks>
public sealed class SealingCheckpointStore : IWorkflowCheckpointStore, IWorkflowCheckpointFlush
{
    private const int MaxKeyIdUtf8Length = 3 * 256;

    private readonly IWorkflowCheckpointStore inner;
    private readonly RunnerKeyRing ring;

    /// <summary>Initializes a new instance of the <see cref="SealingCheckpointStore"/> class.</summary>
    /// <param name="inner">The store the sealed rows go to and the rows to open come from.</param>
    /// <param name="ring">The runner's keys.</param>
    public SealingCheckpointStore(IWorkflowCheckpointStore inner, RunnerKeyRing ring)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(ring);
        this.inner = inner;
        this.ring = ring;
    }

    /// <summary>
    /// Seals a clear row: encrypts its payload under a data key derived for this one operation and writes the
    /// encrypted row with its key id and MAC. The runner region and the control-plane region are carried as they are.
    /// </summary>
    /// <param name="clearRow">The clear row, as <see cref="WorkflowCheckpointSerializer.Serialize"/> writes it.</param>
    /// <param name="address">The run's address: the environment and run id the payload is bound to.</param>
    /// <param name="keys">The environment's keys.</param>
    /// <returns>The encrypted, MAC'd row.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row.</exception>
    /// <exception cref="InvalidOperationException">The row is not a clear row.</exception>
    public static byte[] Seal(ReadOnlyMemory<byte> clearRow, in WorkflowRunAddress address, in RunnerEnvironmentKeys keys)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(clearRow.Span);
        if (layout.Algorithm != CheckpointAlgorithm.Clear || CheckpointIntegrity.KeyIdOf(clearRow.Span) is not null)
        {
            throw ThrowHelper.GetCheckpointSealRefusedException(address);
        }

        if (!WorkflowCheckpointSerializer.TryReadSequence(clearRow, out long sequence))
        {
            ThrowHelper.ThrowCheckpointRowMalformed();
        }

        ReadOnlySpan<byte> row = clearRow.Span;
        ReadOnlySpan<byte> payload = row[layout.Payload];
        Span<byte> keyIdUtf8 = stackalloc byte[MaxKeyIdUtf8Length];
        keyIdUtf8 = keyIdUtf8[..Encoding.UTF8.GetBytes(keys.KeyId, keyIdUtf8)];
        Span<byte> salt = stackalloc byte[CheckpointPayloadCipher.SaltLength];
        Span<byte> nonce = stackalloc byte[CheckpointPayloadCipher.NonceLength];
        Span<byte> tag = stackalloc byte[CheckpointPayloadCipher.TagLength];
        Span<byte> mac = stackalloc byte[CheckpointIntegrity.MacLength];
        byte[] rented = ArrayPool<byte>.Shared.Rent(payload.Length);
        try
        {
            Span<byte> ciphertext = rented.AsSpan(0, payload.Length);
            CheckpointPayloadCipher.Encrypt(keys.PayloadKey, address.Environment, keys.KeyId, address.RunId.Value, (ulong)sequence, payload, salt, nonce, tag, ciphertext);
            CheckpointIntegrity.Compute(CheckpointAlgorithm.Aes256Gcm, keyIdUtf8, row[layout.RunnerRegion], ciphertext, keys.EnvelopeMac, mac);
            return CheckpointRow.WriteSealed(row[layout.RunnerRegion], keyIdUtf8, salt, nonce, tag, ciphertext, mac, row[layout.ControlPlaneRegion]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Opens an encrypted row: verifies its MAC, decrypts its payload and returns the clear row, with the runner
    /// region and the control-plane region as they were. A MAC that does not verify, a generation other than the one
    /// held, and a payload that does not authenticate are one and the same refusal.
    /// </summary>
    /// <param name="sealedRow">The encrypted row.</param>
    /// <param name="address">The run's address.</param>
    /// <param name="keys">The environment's keys.</param>
    /// <returns>The clear row.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row.</exception>
    /// <exception cref="CryptographicException">The row does not verify or its payload does not decrypt under these keys.</exception>
    public static byte[] Open(ReadOnlyMemory<byte> sealedRow, in WorkflowRunAddress address, in RunnerEnvironmentKeys keys)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(sealedRow.Span);
        if (layout.Algorithm != CheckpointAlgorithm.Aes256Gcm
            || !string.Equals(keys.KeyId, CheckpointIntegrity.KeyIdOf(sealedRow.Span), StringComparison.Ordinal)
            || !CheckpointIntegrity.Verify(sealedRow.Span, keys.EnvelopeMac)
            || !WorkflowCheckpointSerializer.TryReadSequence(sealedRow, out long sequence))
        {
            throw ThrowHelper.GetCheckpointIntegrityException(address);
        }

        ReadOnlySpan<byte> row = sealedRow.Span;
        ReadOnlySpan<byte> ciphertext = row[layout.Payload];
        byte[] rented = ArrayPool<byte>.Shared.Rent(ciphertext.Length);
        Span<byte> plaintext = rented.AsSpan(0, ciphertext.Length);
        try
        {
            CheckpointPayloadCipher.Decrypt(keys.PayloadKey, address.Environment, keys.KeyId, address.RunId.Value, (ulong)sequence, row[layout.Salt], row[layout.Nonce], row[layout.Tag], ciphertext, plaintext);
            return CheckpointRow.WriteClear(row[layout.RunnerRegion], plaintext, row[layout.ControlPlaneRegion]);
        }
        catch (CryptographicException)
        {
            throw ThrowHelper.GetCheckpointIntegrityException(address);
        }
        finally
        {
            plaintext.Clear();
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointRow, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        if (!this.ring.TryGet(address.Environment, out RunnerEnvironmentKeys keys))
        {
            return this.inner.SaveAsync(address, checkpointRow, index, expected, cancellationToken);
        }

        byte[] sealedRow = Seal(checkpointRow, address, keys);
        return this.inner.SaveAsync(address, sealedRow, index, expected, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? loaded = await this.inner.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (loaded is not { } checkpoint)
        {
            return null;
        }

        string? keyId = CheckpointIntegrity.KeyIdOf(checkpoint.Row.Span);
        if (keyId is null)
        {
            // A clear row. Fine for an environment the runner serves clear. For one it serves sealed, the only clear
            // row a runner may open is the genesis row (ADR 0065 decisions 4 and 6): the control plane writes a run's
            // first row before any runner has claimed it, holding no key and no lease, so it carries no lease epoch. A
            // clear row that carries an epoch was written under a runner's grant and has no MAC, so it is one the
            // control plane, a backup or a peer rewrote without the key, and nothing in it is trusted. A whole row
            // substituted by a clear genesis row is a rollback to the start, which is the anchor's to catch.
            if (this.ring.IsSealed(address.Environment)
                && (!WorkflowCheckpointSerializer.TryProject(checkpoint.Row, out CheckpointProjection projection) || projection.Epoch is not null))
            {
                throw ThrowHelper.GetCheckpointCleartextRefusedException(address);
            }

            return checkpoint;
        }

        if (!this.ring.TryGet(address.Environment, out RunnerEnvironmentKeys keys)
            || !string.Equals(keys.KeyId, keyId, StringComparison.Ordinal)
            || !CheckpointIntegrity.Verify(checkpoint.Row.Span, keys.EnvelopeMac))
        {
            // A generation this runner does not hold and a MAC that does not verify are the same refusal: the row is
            // not one this runner can vouch for, and saying which would be an oracle on the key ring.
            throw ThrowHelper.GetCheckpointIntegrityException(address);
        }

        if (CheckpointRow.Parse(checkpoint.Row.Span).Algorithm == CheckpointAlgorithm.Clear)
        {
            // A MAC'd row whose payload is clear. This store never writes one, so for a sealed environment it is a
            // row that left the runner's boundary in the clear, whoever signed it; an environment held but not marked
            // sealed tolerates it, as it tolerates any clear row.
            if (this.ring.IsSealed(address.Environment))
            {
                throw ThrowHelper.GetCheckpointCleartextRefusedException(address);
            }

            return checkpoint;
        }

        return new WorkflowCheckpoint(Open(checkpoint.Row, address, keys), checkpoint.Etag);
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
        => this.inner is IWorkflowCheckpointFlush flush ? flush.FlushAsync(cancellationToken) : default;
}