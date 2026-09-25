// <copyright file="SealingCheckpointStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;

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
/// <para>
/// With a tenant anchor store (decision 6), every environment on the ring is anchored as well: each load evaluates
/// the anchor decision table over the row the store holds before a byte of it is trusted, and each save stages its
/// mark with the tenant before it is dispatched, so a rollback, a substitution or a replay by the control plane is a
/// fault at the next open rather than an advance. The genesis row, the clear row the control plane writes at
/// sequence 0 before any runner has claimed, is the origin the anchor commits to. A load whose MAC does not verify
/// is, for an anchored environment, the table's unreadable row rather than a bare integrity fault. Without an anchor
/// store the environment is sealed but not anchored, which is the listener's posture (decision 11) and never a
/// lease-holding runner's: the runner client refuses that combination.
/// </para>
/// <para>
/// A sealed start's genesis row (decision 9) is opened here too: the seal key generation has to be the one the ring
/// holds, the initiator's signature has to verify under a pinned initiator key, and the inputs are opened under the
/// binding re-derived from the run's own address and the envelope's workflow id, never from anything else in the row.
/// A start that does not open is a <see cref="SealedStartException"/> carrying the row, so the runner can fault the
/// run at its start rather than claim it again and again; a runner with no seal key for the environment opens none.
/// </para>
/// <para>
/// The store encrypts clear rows only. A row that comes back through it for a control-plane-region write (the
/// serverless checkpoint coordinator faulting a run on its budget over a row it loaded here) is a clear row by then,
/// and is encrypted again under a fresh salt, which is what the salt-per-operation rule of decision 5 requires. A
/// row that already names a key generation is not one this store re-seals: it refuses rather than encrypt twice.
/// </para>
/// </remarks>
public sealed class SealingCheckpointStore : IWorkflowCheckpointStore, IWorkflowCheckpointFlush
{
    private const int MaxKeyIdUtf8Length = 3 * 256;

    private readonly IWorkflowCheckpointStore inner;
    private readonly RunnerKeyRing ring;
    private readonly CheckpointAnchoring? anchoring;

    /// <summary>Initializes a new instance of the <see cref="SealingCheckpointStore"/> class.</summary>
    /// <param name="inner">The store the sealed rows go to and the rows to open come from.</param>
    /// <param name="ring">The runner's keys.</param>
    /// <param name="anchors">The tenant anchor store (ADR 0065 decision 6); with one, every environment on the ring is anchored.</param>
    public SealingCheckpointStore(IWorkflowCheckpointStore inner, RunnerKeyRing ring, ITenantAnchorStore? anchors = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(ring);
        this.inner = inner;
        this.ring = ring;
        this.anchoring = anchors is null ? null : new CheckpointAnchoring(anchors);
    }

    /// <summary>Gets whether an environment is anchored here: on the ring, with a tenant anchor store to write.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> when loads and saves for the environment go through the anchor.</returns>
    public bool IsAnchored(string environment) => this.anchoring is not null && this.ring.TryGet(environment, out _);

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
        return this.anchoring is null
            ? this.inner.SaveAsync(address, sealedRow, index, expected, cancellationToken)
            : this.SaveAnchoredAsync(address, sealedRow, index, expected, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? loaded = await this.inner.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (this.IsAnchored(address.Environment))
        {
            return await this.LoadAnchoredAsync(address, loaded, cancellationToken).ConfigureAwait(false);
        }

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

        if (CheckpointRow.Parse(checkpoint.Row.Span).Algorithm == CheckpointAlgorithm.SealedGenesis)
        {
            // A sealed start's genesis row (decision 9). A runner that holds the environment opens it; one that does
            // not gets the envelope, as any keyless reader does.
            return this.ring.TryGet(address.Environment, out RunnerEnvironmentKeys sealKeys)
                ? new WorkflowCheckpoint(OpenSealedGenesis(checkpoint, address, sealKeys), checkpoint.Etag)
                : checkpoint;
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

    // The digest the anchor commits to: over the submitted bytes of the row as the store holds it, never the
    // control-plane region, which is joined at read time and is not the runner's to commit to (decision 6).
    // A sealed start's genesis row is digested over its own pinned layout (decision 6): the sealed inputs and the
    // initiator's signature, which is the only tenant-side authenticator that exists at sequence 0.
    private static AnchorDigest DigestOf(ReadOnlyMemory<byte> row)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row.Span);
        return layout.Algorithm == CheckpointAlgorithm.SealedGenesis
            ? CheckpointDigest.ForGenesis(row.Span[layout.Payload], row.Span[layout.Mac])
            : CheckpointDigest.ForSubmitted(row.Span[..layout.SubmittedLength]);
    }

    // Opens a sealed start's genesis row (decision 9) into the clear row the run resumes from: the envelope as the
    // control plane wrote it, and the inputs the initiator sealed. Every refusal carries the row, so the runner can
    // record the refusal on the run itself.
    private static byte[] OpenSealedGenesis(in WorkflowCheckpoint checkpoint, in WorkflowRunAddress address, in RunnerEnvironmentKeys keys)
    {
        ReadOnlyMemory<byte> row = checkpoint.Row;
        CheckpointRowLayout layout = CheckpointRow.Parse(row.Span);
        if (!keys.OpensSealedStarts || keys.SealPrivateKey is not { } sealKey || keys.InitiatorKeys is not { } initiators)
        {
            throw ThrowHelper.GetSealedStartException(address, SealedStartRefusal.NoSealKey, row, checkpoint.Etag);
        }

        if (!string.Equals(keys.KeyId, CheckpointIntegrity.KeyIdOf(row.Span), StringComparison.Ordinal))
        {
            throw ThrowHelper.GetSealedStartException(address, SealedStartRefusal.UnknownGeneration, row, checkpoint.Etag);
        }

        // The binding is re-derived from what the runner knows on its own account: the address it claimed, and the
        // workflow the envelope names, which the control plane authored and the initiator sealed for. A row moved to
        // another run, environment, workflow or generation therefore does not open, whatever it says about itself.
        if (!WorkflowCheckpointSerializer.TryProject(row, out CheckpointProjection projection)
            || projection.Sequence != 0
            || projection.Epoch is not null
            || !string.Equals(projection.Environment, address.Environment, StringComparison.Ordinal)
            || !WorkflowVersionId.TryParse(projection.Index.WorkflowId, out string baseWorkflowId, out int versionNumber))
        {
            throw ThrowHelper.GetSealedStartException(address, SealedStartRefusal.Unopenable, row, checkpoint.Etag);
        }

        ReadOnlySpan<byte> enc = row.Span[layout.Salt];
        ReadOnlySpan<byte> ciphertext = row.Span[layout.Payload];
        ReadOnlySpan<byte> signature = row.Span[layout.Mac];
        byte[] binding = new byte[SealedStartSignature.BindingLength(address.Environment, baseWorkflowId, keys.KeyId, address.RunId.Value)];
        SealedStartSignature.WriteBinding(address.Environment, baseWorkflowId, versionNumber, keys.KeyId, address.RunId.Value, binding);
        bool signed = false;
        foreach (byte[] initiator in initiators)
        {
            signed |= SealedStartSignature.Verify(initiator, binding, enc, ciphertext, signature);
        }

        if (!signed)
        {
            throw ThrowHelper.GetSealedStartException(address, SealedStartRefusal.UnpinnedInitiator, row, checkpoint.Etag);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(ciphertext.Length - InputSeal.TagLength);
        Span<byte> inputs = rented.AsSpan(0, ciphertext.Length - InputSeal.TagLength);
        try
        {
            InputSeal.Open(sealKey, enc, SealedStartSignature.SealInfo, binding, ciphertext, inputs);
            using ParsedJsonDocument<JsonElement> parsed = ParsedJsonDocument<JsonElement>.Parse(inputs.ToArray());
            return CheckpointRow.WriteClear(row.Span[layout.RunnerRegion], WorkflowCheckpointSerializer.SerializeStartPayload(parsed.RootElement), row.Span[layout.ControlPlaneRegion]);
        }
        catch (Exception ex) when (ex is CryptographicException or Corvus.Text.Json.JsonException or System.Text.Json.JsonException or FormatException)
        {
            throw ThrowHelper.GetSealedStartException(address, SealedStartRefusal.Unopenable, row, checkpoint.Etag);
        }
        finally
        {
            inputs.Clear();
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // An anchored save: the mark is staged with the tenant before the row is dispatched, the dispatch is
    // acknowledged to the anchor, and the run's gate is held throughout so one save is in flight per run. A dispatch
    // that fails leaves the mark staged: a 409 is not an abandonment, and the next open resolves it.
    private async ValueTask<WorkflowEtag> SaveAnchoredAsync(WorkflowRunAddress address, byte[] sealedRow, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        CheckpointAnchoring.RunSlot staged = await this.anchoring!.StageAsync(address, sealedRow, cancellationToken).ConfigureAwait(false);
        try
        {
            WorkflowEtag etag = await this.inner.SaveAsync(address, sealedRow, index, expected, cancellationToken).ConfigureAwait(false);
            await this.anchoring.AcknowledgeAsync(staged, address, cancellationToken).ConfigureAwait(false);
            return etag;
        }
        finally
        {
            this.anchoring.Release(staged, address);
        }
    }

    // An anchored load: the row is classified for the decision table (absent, unreadable, the genesis row, or a
    // readable row at its MAC-verified sequence and incarnation), the table is evaluated and applied, and only then
    // is the payload opened and the row handed on.
    private async ValueTask<WorkflowCheckpoint?> LoadAnchoredAsync(WorkflowRunAddress address, WorkflowCheckpoint? loaded, CancellationToken cancellationToken)
    {
        if (loaded is not { } checkpoint)
        {
            await this.anchoring!.OpenAsync(address, AnchorStoreRow.Absent, cancellationToken).ConfigureAwait(false);
            return null;
        }

        this.ring.TryGet(address.Environment, out RunnerEnvironmentKeys keys);
        string? keyId = CheckpointIntegrity.KeyIdOf(checkpoint.Row.Span);
        if (keyId is null)
        {
            // A clear row. The one an anchored environment reads is the genesis row (decisions 4 and 6): written by
            // the control plane before any runner has claimed, holding no key and no lease, at sequence 0 by
            // definition. Any other clear row was written without the key, and is unreadable to the table.
            bool genesis = WorkflowCheckpointSerializer.TryProject(checkpoint.Row, out CheckpointProjection projection)
                && projection.Epoch is null
                && projection.Sequence == 0;
            await this.anchoring!.OpenAsync(address, genesis ? AnchorStoreRow.Genesis(DigestOf(checkpoint.Row), 0) : AnchorStoreRow.Unreadable, cancellationToken).ConfigureAwait(false);
            return checkpoint;
        }

        if (CheckpointRow.Parse(checkpoint.Row.Span).Algorithm == CheckpointAlgorithm.SealedGenesis)
        {
            // A sealed start's genesis row (decision 9): the origin the anchor commits to, digested over its own
            // layout, and opened only once the table has admitted the claim.
            await this.anchoring!.OpenAsync(address, AnchorStoreRow.Genesis(DigestOf(checkpoint.Row), 0), cancellationToken).ConfigureAwait(false);
            return new WorkflowCheckpoint(OpenSealedGenesis(checkpoint, address, keys), checkpoint.Etag);
        }

        if (!string.Equals(keys.KeyId, keyId, StringComparison.Ordinal)
            || !CheckpointIntegrity.Verify(checkpoint.Row.Span, keys.EnvelopeMac)
            || !WorkflowCheckpointSerializer.TryProject(checkpoint.Row, out CheckpointProjection verified))
        {
            // Table row 5: a row whose MAC does not verify has no trustworthy sequence, and is a hard fault the
            // anchor records rather than a bare integrity fault.
            await this.anchoring!.OpenAsync(address, AnchorStoreRow.Unreadable, cancellationToken).ConfigureAwait(false);
            throw ThrowHelper.GetCheckpointIntegrityException(address);
        }

        bool clearPayload = CheckpointRow.Parse(checkpoint.Row.Span).Algorithm == CheckpointAlgorithm.Clear;
        if (clearPayload && keys.Sealed)
        {
            throw ThrowHelper.GetCheckpointCleartextRefusedException(address);
        }

        // The sequence and incarnation come from the MAC-verified region, never from anything the server projected.
        await this.anchoring!.OpenAsync(address, AnchorStoreRow.At((ulong)verified.Sequence, DigestOf(checkpoint.Row), verified.Incarnation ?? 0), cancellationToken).ConfigureAwait(false);
        return clearPayload ? checkpoint : new WorkflowCheckpoint(Open(checkpoint.Row, address, keys), checkpoint.Etag);
    }
}