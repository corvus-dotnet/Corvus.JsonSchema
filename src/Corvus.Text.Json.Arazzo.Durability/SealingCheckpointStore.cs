// <copyright file="SealingCheckpointStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The runner's side of checkpoint integrity (ADR 0065 decisions 4 and 10): a decorator over the runner's checkpoint
/// store that seals every row it saves for an environment its <see cref="RunnerKeyRing"/> holds, and verifies every
/// row it loads. A load whose MAC does not verify, or that names a generation the ring does not hold, faults before
/// any of its envelope is trusted; a clear row for an environment the runner serves sealed is refused the same way.
/// The run itself stays crypto-free: it sees rows go out and come back exactly as before.
/// </summary>
public sealed class SealingCheckpointStore : IWorkflowCheckpointStore, IWorkflowCheckpointFlush
{
    private readonly IWorkflowCheckpointStore inner;
    private readonly RunnerKeyRing ring;

    /// <summary>Initializes a new instance of the <see cref="SealingCheckpointStore"/> class.</summary>
    /// <param name="inner">The store the sealed rows go to and the rows to verify come from.</param>
    /// <param name="ring">The runner's keys.</param>
    public SealingCheckpointStore(IWorkflowCheckpointStore inner, RunnerKeyRing ring)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(ring);
        this.inner = inner;
        this.ring = ring;
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointRow, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        if (!this.ring.TryGet(address.Environment, out RunnerEnvironmentKeys keys))
        {
            return this.inner.SaveAsync(address, checkpointRow, index, expected, cancellationToken);
        }

        byte[] sealedRow = CheckpointIntegrity.Seal(checkpointRow.Span, keys.KeyId, keys.EnvelopeMac);
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

        return checkpoint;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
        => this.inner is IWorkflowCheckpointFlush flush ? flush.FlushAsync(cancellationToken) : default;
}