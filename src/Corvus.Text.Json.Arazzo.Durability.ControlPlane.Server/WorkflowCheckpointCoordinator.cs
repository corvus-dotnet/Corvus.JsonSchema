// <copyright file="WorkflowCheckpointCoordinator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The runner-side terminus of the serverless checkpoint surface (ADR 0055): it turns a baked function's opaque,
/// fire-and-forget checkpoint writes into durable saves against the real state store, under the lease the dispatcher
/// already holds. A function binds no store SDK and holds no store credentials — it loads and saves a run's checkpoint
/// over HTTP, and this coordinator terminates those calls into <see cref="IWorkflowCheckpointStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The store is a single overwritten slot per run guarded by an etag, and the function's saves are fire-and-forget, so
/// they race and can arrive out of order. Two invariants keep the one slot only ever moving forward:
/// </para>
/// <list type="number">
/// <item><description>
/// A per-run monotonic write-sequence: a save whose sequence is not greater than the last applied is dropped as a
/// benign no-op (a superseded or duplicate arrival). The terminal checkpoint always carries the highest sequence, so
/// it is never dropped here.
/// </description></item>
/// <item><description>
/// Per-run serialization plus etag threading: each run's saves run one at a time behind a gate, threading the store's
/// returned etag into the next save, so the coordinator's own writes never conflict. The lease the dispatcher holds
/// makes the coordinator the sole writer, so a conflict signals a lost or stolen lease and is surfaced (never
/// silently overwritten) — the run stays claimable for idempotent re-invocation.
/// </description></item>
/// </list>
/// <para>
/// The per-run state is in-memory and reconstructed from the store on demand, so it is bounded by an idle sweep rather
/// than kept for a run's whole life. It is not evicted when a run reaches a terminal status: a late interim save could
/// still arrive after the terminal one under the fire-and-forget race, and the retained sequence is what drops it.
/// </para>
/// </remarks>
public sealed class WorkflowCheckpointCoordinator
{
    private static readonly TimeSpan SlotIdleTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly IWorkflowCheckpointStore store;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, RunSlot> slots = new(StringComparer.Ordinal);
    private long lastSweepTimestamp;

    /// <summary>Initializes a new instance of the <see cref="WorkflowCheckpointCoordinator"/> class.</summary>
    /// <param name="store">The real state store the runner terminates checkpoints into.</param>
    /// <param name="timeProvider">The time source for the idle sweep; defaults to <see cref="TimeProvider.System"/>.</param>
    public WorkflowCheckpointCoordinator(IWorkflowCheckpointStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.lastSweepTimestamp = this.timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Loads a run's checkpoint for a function to resume from, and aligns the coordinator's per-run state to the store
    /// so subsequent saves thread forward from it.
    /// </summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The checkpoint bytes, its etag, and the last applied write-sequence; or <see langword="null"/> if the run has no checkpoint.</returns>
    public async ValueTask<CheckpointLoad?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            // No checkpoint yet: the function reads this as a run with no persisted state. Do not create a slot — the
            // first save creates one, seeded from write-sequence zero.
            return null;
        }

        RunSlot slot = this.GetSlot(id.Value);
        long appliedSequence;
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The store's etag is the authority; align the slot to it so a warm advance's next save threads forward
            // from the state the function just loaded. The applied sequence carries across advances on this runner so a
            // reused function instance keeps stamping monotonically.
            slot.Etag = checkpoint.Value.Etag;
            slot.Seeded = true;
            appliedSequence = slot.LastAppliedSequence;
        }
        finally
        {
            slot.Gate.Release();
        }

        return new CheckpointLoad(checkpoint.Value.Utf8, checkpoint.Value.Etag, appliedSequence);
    }

    /// <summary>
    /// Terminates one fire-and-forget checkpoint save into the store, applying the monotonic write-sequence and etag
    /// invariants. The <paramref name="index"/> is projected by the caller from <paramref name="checkpointUtf8"/>, so a
    /// malformed body is rejected before it reaches the coordinator.
    /// </summary>
    /// <param name="id">The run id.</param>
    /// <param name="checkpointUtf8">The checkpoint bytes to persist verbatim.</param>
    /// <param name="index">The index projected from the same bytes.</param>
    /// <param name="sequence">The save's monotonic per-run write-sequence.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome — applied, superseded (a benign stale/out-of-order drop), or a conflict.</returns>
    public async ValueTask<CheckpointSaveOutcome> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, WorkflowRunIndexEntry index, long sequence, CancellationToken cancellationToken)
    {
        RunSlot slot = this.GetSlot(id.Value);
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (sequence <= slot.LastAppliedSequence)
            {
                // A stale or out-of-order fire-and-forget arrival: the single stored slot already holds this sequence or
                // a newer one, so applying older bytes would regress it. Drop it — a lost interim checkpoint is a safe
                // idempotent replay at worst, and the terminal always carries the highest sequence so it is never
                // dropped here.
                return CheckpointSaveOutcome.Superseded;
            }

            if (!slot.Seeded)
            {
                // No load preceded this save (a fresh run with no persisted checkpoint, or a slot swept between load and
                // save). Seed the etag from the store so the write is conditioned correctly: None creates a new entry, a
                // real etag overwrites the existing one.
                WorkflowCheckpoint? existing = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
                slot.Etag = existing?.Etag ?? WorkflowEtag.None;
                slot.Seeded = true;
            }

            try
            {
                slot.Etag = await this.store.SaveAsync(id, checkpointUtf8, index, slot.Etag, cancellationToken).ConfigureAwait(false);
                slot.LastAppliedSequence = sequence;
                return CheckpointSaveOutcome.Applied;
            }
            catch (WorkflowConflictException)
            {
                // The sole-writer invariant is broken (a lost or stolen lease, or a peer advancing the run): do not
                // advance the slot, and surface the conflict so the run's outcome is not reported on this write — it
                // stays claimable for idempotent re-invocation.
                return CheckpointSaveOutcome.Conflict;
            }
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    private RunSlot GetSlot(string runId)
    {
        this.MaybeSweep();
        RunSlot slot = this.slots.GetOrAdd(runId, static _ => new RunSlot());

        // Touch before the caller operates so an in-use slot always reads as fresh to the sweep.
        slot.TouchedTimestamp = this.timeProvider.GetTimestamp();
        return slot;
    }

    private void MaybeSweep()
    {
        long now = this.timeProvider.GetTimestamp();
        long last = Interlocked.Read(ref this.lastSweepTimestamp);
        if (this.timeProvider.GetElapsedTime(last, now) < SweepInterval)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref this.lastSweepTimestamp, now, last) != last)
        {
            // Another thread just claimed this sweep.
            return;
        }

        foreach (KeyValuePair<string, RunSlot> entry in this.slots)
        {
            RunSlot slot = entry.Value;
            if (this.timeProvider.GetElapsedTime(slot.TouchedTimestamp, now) < SlotIdleTtl)
            {
                continue;
            }

            // Remove only a slot whose gate is free (no save in flight) and that is still idle under the gate. A slot
            // touched within the TTL is skipped above, so a removed slot has no live user; its state is reconstructed
            // from the store on the next load or save.
            if (slot.Gate.Wait(0))
            {
                try
                {
                    if (this.timeProvider.GetElapsedTime(slot.TouchedTimestamp, now) >= SlotIdleTtl)
                    {
                        this.slots.TryRemove(entry);
                    }
                }
                finally
                {
                    slot.Gate.Release();
                }
            }
        }
    }

    private sealed class RunSlot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public long LastAppliedSequence { get; set; }

        public WorkflowEtag Etag { get; set; }

        public bool Seeded { get; set; }

        public long TouchedTimestamp { get; set; }
    }
}

/// <summary>The result of a <see cref="WorkflowCheckpointCoordinator.LoadAsync"/>: the checkpoint and the state a function needs to continue the monotonic write-sequence.</summary>
/// <param name="Checkpoint">The stored checkpoint bytes.</param>
/// <param name="Etag">The checkpoint's etag (advisory to the function; the coordinator threads it).</param>
/// <param name="LastAppliedSequence">The highest write-sequence the coordinator has applied for this run, so the function continues past it.</param>
public readonly record struct CheckpointLoad(ReadOnlyMemory<byte> Checkpoint, WorkflowEtag Etag, long LastAppliedSequence);

/// <summary>The outcome of terminating a checkpoint save.</summary>
public enum CheckpointSaveOutcome
{
    /// <summary>The checkpoint was written to the store and is now the run's durable state.</summary>
    Applied,

    /// <summary>The save was stale or out of order (its sequence was already met or exceeded) and was dropped as a benign no-op.</summary>
    Superseded,

    /// <summary>The store rejected the write on an etag conflict, signalling a broken sole-writer invariant; the run stays claimable.</summary>
    Conflict,
}