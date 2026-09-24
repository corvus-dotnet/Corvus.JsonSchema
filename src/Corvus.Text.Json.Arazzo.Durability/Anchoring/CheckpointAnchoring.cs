// <copyright file="CheckpointAnchoring.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The anchor protocol as the lease holder runs it (ADR 0065 decision 6 and the normative tenant-anchor
/// specification): at open, the decision table over the row the store holds and the record the tenant holds, with
/// the promote or discard it decides; before every dispatch, the staged mark (<c>Prepare</c>, or the fused
/// <c>PromoteAndPrepare</c> once the previous save was acknowledged); after a terminal save's acknowledgement, the
/// promote and the finalize. Every hard fault, refused claim and rejected write is a <see cref="CheckpointAnchorException"/>.
/// </summary>
/// <remarks>
/// <para>
/// One gate per run makes the single-flight rule an enforced one: a second save for a run waits for the first's
/// acknowledgement rather than racing it, so two well-behaved components in one process cannot manufacture the
/// divergence the table's row 10 exists to catch. The gate is per run and per process; the lease is what makes
/// the process the run's sole writer.
/// </para>
/// <para>
/// Promote is fused in the steady state: a save made mid-advance (the row says the run is still running) stays
/// staged once acknowledged, and the next save promotes it in the same write that stages its own mark. A save
/// that ends the advance (the run suspends, faults, or finishes) is promoted as soon as it is acknowledged, in a
/// write of its own, so the tenant's committed mark is the last checkpoint before the run rests, and a control
/// plane that rolls that one back meets table row 13 rather than the lost-acknowledgement row 11. A finishing
/// save is promoted and then finalized. A save whose dispatch failed, superseded or otherwise, stays staged: a
/// <c>409</c> is not an abandonment, and the next open resolves it by row 9 or row 11. A save staged over an
/// unacknowledged one is refused, since re-preparing over an outstanding save replaces the record of what is in
/// flight.
/// </para>
/// </remarks>
internal sealed class CheckpointAnchoring
{
    // Slots for runs this process has opened or saved. Pruned of idle ones once the table grows past this, so a
    // long-lived runner does not hold a slot for every run it ever touched; a slot is small, and a pruned one is
    // simply re-read from the tenant store at the run's next open.
    private const int PruneThreshold = 1024;
    private static readonly TimeSpan IdleFor = TimeSpan.FromMinutes(10);

    private readonly ITenantAnchorStore anchors;
    private readonly ConcurrentDictionary<WorkflowRunAddress, RunSlot> runs = new();

    /// <summary>Initializes a new instance of the <see cref="CheckpointAnchoring"/> class.</summary>
    /// <param name="anchors">The tenant anchor store.</param>
    public CheckpointAnchoring(ITenantAnchorStore anchors)
    {
        this.anchors = anchors;
    }

    /// <summary>Gets the tenant anchor store.</summary>
    public ITenantAnchorStore Store => this.anchors;

    /// <summary>Whether a run status closes the anchor record: the run finished, or was cancelled. A faulted run
    /// stays live, because a retry at its cursor is still an advance of the same run.</summary>
    /// <param name="status">The status the row carries.</param>
    /// <returns>Whether the record is finalized after the save is acknowledged.</returns>
    public static bool ClosesRecord(WorkflowRunStatus status)
        => status is WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled;

    /// <summary>Whether a run status ends the advance without closing the record: the run rests, and its last
    /// checkpoint is promoted at once rather than left for a next save that is not coming.</summary>
    /// <param name="status">The status the row carries.</param>
    /// <returns>Whether the staged mark is promoted when the save is acknowledged.</returns>
    public static bool RestsRun(WorkflowRunStatus status)
        => status is not WorkflowRunStatus.Running;

    /// <summary>
    /// Evaluates the decision table for a run's stored row and applies what it decides: nothing, a promote, a
    /// discard, or a fault. The first claim of a fresh run is remembered, and its <c>Create</c> is written before
    /// the first save.
    /// </summary>
    /// <param name="address">The run.</param>
    /// <param name="row">The store row, parsed and verified (or absent, or flagged unreadable).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The row the table matched.</returns>
    /// <exception cref="CheckpointAnchorException">The table refused the claim or hard-faulted, the environment has no attested incarnation, or the store rejected the promote or discard.</exception>
    public async ValueTask<AnchorOpenDecision> OpenAsync(WorkflowRunAddress address, AnchorStoreRow row, CancellationToken cancellationToken)
    {
        RunSlot slot = this.Slot(address);
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ulong attested = await this.anchors.ReadAttestedIncarnationAsync(address.Environment, cancellationToken).ConfigureAwait(false)
                ?? throw ThrowHelper.GetCheckpointAnchorUnattestedException(address);
            AnchorRecord? record = await this.anchors.ReadAsync(address.Environment, address.RunId.Value, cancellationToken).ConfigureAwait(false);
            AnchorOpenDecision decision = AnchorOpen.Evaluate(record, row, attested);
            slot.Reset(record, attested);

            switch (decision.Outcome)
            {
                case AnchorOpenOutcome.NotFound:
                case AnchorOpenOutcome.Proceed:
                    break;

                case AnchorOpenOutcome.Create:
                    // The runner creates the entry at first claim (decision 6). The genesis digest is what the record
                    // commits to; the write itself waits for the first save, which carries the ordering key.
                    slot.CreatePending = true;
                    slot.GenesisDigest = row.Digest;
                    break;

                case AnchorOpenOutcome.PromoteThenProceed:
                {
                    AnchorRecord r = record!.Value;
                    await this.WriteAsync(slot, address, r with { Committed = r.Pending!.Value, Pending = null }, AnchorWriteKind.Promote, cancellationToken).ConfigureAwait(false);
                    break;
                }

                case AnchorOpenOutcome.DiscardThenProceed:
                {
                    AnchorRecord r = record!.Value;
                    await this.WriteAsync(slot, address, r with { Pending = null }, AnchorWriteKind.Discard, cancellationToken).ConfigureAwait(false);
                    break;
                }

                default:
                    this.runs.TryRemove(address, out _);
                    throw ThrowHelper.GetCheckpointAnchorRefusedException(address, decision);
            }

            return decision;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary>
    /// Stages a save: takes the run's gate, writes the run's <c>Create</c> if its first claim is outstanding, then
    /// stages the mark for <paramref name="sealedRow"/> under the ordering key its region carries. The gate is held
    /// until <see cref="Release"/>; the caller dispatches the row in between and calls <see cref="AcknowledgeAsync"/>
    /// when the store acknowledges it.
    /// </summary>
    /// <param name="address">The run.</param>
    /// <param name="sealedRow">The row about to be dispatched, exactly as it will be submitted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The staged save.</returns>
    /// <exception cref="CheckpointAnchorException">The row carries no grant, the environment has no attestation, a save is already outstanding, or the store rejected the write.</exception>
    public async ValueTask<RunSlot> StageAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> sealedRow, CancellationToken cancellationToken)
    {
        RunSlot slot = this.Slot(address);
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // An anchored save is staged under the writer's ordering key, so the row has to carry a grant's epoch and
            // the incarnation the writer read from the tenant's own attestation (invariant A5, the writer's obligation).
            if (!WorkflowCheckpointSerializer.TryProject(sealedRow, out CheckpointProjection projection)
                || projection.Epoch is not { } epoch
                || projection.Incarnation is not { } incarnation)
            {
                throw ThrowHelper.GetCheckpointAnchorNeedsGrantException(address);
            }

            if (slot.Attested is null)
            {
                // A save with no open in this process: the record is whatever the tenant holds.
                slot.Reset(
                    await this.anchors.ReadAsync(address.Environment, address.RunId.Value, cancellationToken).ConfigureAwait(false),
                    await this.anchors.ReadAttestedIncarnationAsync(address.Environment, cancellationToken).ConfigureAwait(false)
                        ?? throw ThrowHelper.GetCheckpointAnchorUnattestedException(address));
            }

            if (incarnation != slot.Attested)
            {
                throw ThrowHelper.GetCheckpointAnchorNeedsGrantException(address);
            }

            var key = new AnchorOrderingKey(incarnation, (ulong)epoch);
            if (slot.CreatePending)
            {
                var created = new AnchorRecord(address.RunId.Value, address.Environment, AnchorState.Live, key, new AnchorMark(key, 0, slot.GenesisDigest), null, 0);
                await this.WriteAsync(slot, address, created, AnchorWriteKind.Create, cancellationToken).ConfigureAwait(false);
                slot.CreatePending = false;
            }

            if (slot.Record is not { } record)
            {
                // No record and no first claim: this process never opened the run through the table.
                throw ThrowHelper.GetCheckpointAnchorWriteRejectedException(address, AnchorWriteKind.Prepare);
            }

            var staged = new AnchorMark(key, (ulong)projection.Sequence, CheckpointDigest.ForSubmitted(CheckpointRow.SubmittedBytes(sealedRow).Span));
            AnchorWriteKind intended;
            AnchorRecord proposed;
            if (record.Pending is { } outstanding)
            {
                if (!slot.Acknowledged)
                {
                    // The previous dispatch's fate is unknown. Re-preparing over it would replace the record of what
                    // is in flight and drive the next open to row 10; the run re-opens instead.
                    throw ThrowHelper.GetCheckpointAnchorWriteRejectedException(address, AnchorWriteKind.Prepare);
                }

                intended = AnchorWriteKind.PromoteAndPrepare;
                proposed = record with { Committed = outstanding, Pending = staged, EpochHighWater = key };
            }
            else
            {
                intended = AnchorWriteKind.Prepare;
                proposed = record with { Pending = staged, EpochHighWater = key };
            }

            await this.WriteAsync(slot, address, proposed, intended, cancellationToken).ConfigureAwait(false);
            slot.Acknowledged = false;
            slot.StagedCloses = ClosesRecord(projection.Index.Status);
            slot.StagedRests = RestsRun(projection.Index.Status);
            return slot;
        }
        catch
        {
            slot.Gate.Release();
            throw;
        }
    }

    /// <summary>
    /// Records that the store acknowledged the staged save. A save that closes the run is promoted and the record
    /// finalized at once, in two writes; one that rests the run is promoted in one; a mid-advance save stays staged
    /// for the next save to fuse.
    /// </summary>
    /// <param name="slot">The staged save.</param>
    /// <param name="address">The run.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the record reflects the acknowledgement.</returns>
    public async ValueTask AcknowledgeAsync(RunSlot slot, WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        slot.Acknowledged = true;
        if (!slot.StagedRests)
        {
            return;
        }

        AnchorRecord r = slot.Record!.Value;
        await this.WriteAsync(slot, address, r with { Committed = r.Pending!.Value, Pending = null }, AnchorWriteKind.Promote, cancellationToken).ConfigureAwait(false);
        slot.Acknowledged = false;
        if (!slot.StagedCloses)
        {
            return;
        }

        r = slot.Record!.Value;
        await this.WriteAsync(slot, address, r with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed }, AnchorWriteKind.Finalize, cancellationToken).ConfigureAwait(false);
        slot.Closed = true;
    }

    /// <summary>Gives the run's gate back after a dispatch, acknowledged or not, and forgets a closed run.</summary>
    /// <param name="slot">The staged save.</param>
    /// <param name="address">The run.</param>
    public void Release(RunSlot slot, WorkflowRunAddress address)
    {
        if (slot.Closed)
        {
            this.runs.TryRemove(address, out _);
        }

        slot.Gate.Release();
    }

    private async ValueTask WriteAsync(RunSlot slot, WorkflowRunAddress address, AnchorRecord proposed, AnchorWriteKind intended, CancellationToken cancellationToken)
    {
        AnchorWriteKind admitted = await this.anchors.WriteAsync(address.Environment, slot.Record, proposed, cancellationToken).ConfigureAwait(false);
        if (admitted == AnchorWriteKind.Rejected)
        {
            this.runs.TryRemove(address, out _);
            throw ThrowHelper.GetCheckpointAnchorWriteRejectedException(address, intended);
        }

        slot.Record = proposed;
    }

    private RunSlot Slot(WorkflowRunAddress address)
    {
        if (this.runs.Count > PruneThreshold)
        {
            this.Prune();
        }

        RunSlot slot = this.runs.GetOrAdd(address, static _ => new RunSlot());
        slot.Touched = Environment.TickCount64;
        return slot;
    }

    private void Prune()
    {
        long idleBefore = Environment.TickCount64 - (long)IdleFor.TotalMilliseconds;
        foreach (KeyValuePair<WorkflowRunAddress, RunSlot> entry in this.runs)
        {
            // Only a slot nobody holds and nobody has touched for a while. A slot removed here is re-read from the
            // tenant store at the run's next open, so nothing is lost by dropping it.
            if (entry.Value.Touched < idleBefore && entry.Value.Gate.CurrentCount == 1)
            {
                this.runs.TryRemove(entry.Key, out _);
            }
        }
    }

    /// <summary>What this process holds for one run between anchor operations.</summary>
    internal sealed class RunSlot
    {
        /// <summary>Gets the per-run gate: one open or save at a time.</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>Gets or sets the record as last read or written, which is the compare half of the next write.</summary>
        public AnchorRecord? Record { get; set; }

        /// <summary>Gets or sets the attested incarnation read at open, or <see langword="null"/> before any open.</summary>
        public ulong? Attested { get; set; }

        /// <summary>Gets or sets a value indicating whether the open decided the first claim, whose <c>Create</c> the first save writes.</summary>
        public bool CreatePending { get; set; }

        /// <summary>Gets or sets the genesis digest the <c>Create</c> commits to.</summary>
        public AnchorDigest GenesisDigest { get; set; }

        /// <summary>Gets or sets a value indicating whether the staged save was acknowledged by the store.</summary>
        public bool Acknowledged { get; set; }

        /// <summary>Gets or sets a value indicating whether the staged save closes the record.</summary>
        public bool StagedCloses { get; set; }

        /// <summary>Gets or sets a value indicating whether the staged save rests the run, so it is promoted on acknowledgement.</summary>
        public bool StagedRests { get; set; }

        /// <summary>Gets or sets a value indicating whether the record was finalized.</summary>
        public bool Closed { get; set; }

        /// <summary>Gets or sets when the slot was last used.</summary>
        public long Touched { get; set; }

        /// <summary>Starts the slot over from what an open read.</summary>
        /// <param name="record">The record the tenant holds.</param>
        /// <param name="attested">The attested incarnation.</param>
        public void Reset(AnchorRecord? record, ulong attested)
        {
            this.Record = record;
            this.Attested = attested;
            this.CreatePending = false;
            this.Acknowledged = false;
            this.StagedCloses = false;
            this.StagedRests = false;
            this.Closed = false;
        }
    }
}