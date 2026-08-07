// <copyright file="WorkflowCheckpointCoordinator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The server-side terminus of every remote checkpoint surface: it turns opaque, fire-and-forget checkpoint writes into
/// durable saves against the real state store, under the lease the writer already holds. Both the serverless checkpoint
/// surface (ADR 0055) and the runner API (ADR 0065) sit on it, so a baked function and a runner alike bind no store SDK
/// and hold no store credentials — they load and save a run's checkpoint over HTTP, and this coordinator terminates
/// those calls into <see cref="IWorkflowCheckpointStore"/>.
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
            slot.LastAppliedSequence = WorkflowCheckpointSerializer.TryReadSequence(checkpoint.Value.Utf8, out long persisted)
                ? persisted
                : 0;
            CaptureIdentity(slot, checkpoint.Value.Utf8);
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
    /// <returns>The outcome, and the sequence the store will accept next.</returns>
    public async ValueTask<CheckpointSaveResult> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, WorkflowRunIndexEntry index, long sequence, CancellationToken cancellationToken)
    {
        RunSlot slot = this.GetSlot(id.Value);
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!slot.Seeded)
            {
                // No load preceded this save (a fresh run with no persisted checkpoint, or a slot swept between load and
                // save). Seed the etag AND the persisted sequence from the store, so the write is conditioned correctly
                // and the acceptance rule is evaluated against what the row actually holds rather than against a
                // process-local counter that a restart reset to zero.
                WorkflowCheckpoint? existing = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
                slot.Etag = existing?.Etag ?? WorkflowEtag.None;
                slot.LastAppliedSequence = existing is { } row && WorkflowCheckpointSerializer.TryReadSequence(row.Utf8, out long persisted)
                    ? persisted
                    : 0;
                if (existing is { } identityRow)
                {
                    CaptureIdentity(slot, identityRow.Utf8);
                }

                slot.Seeded = true;
            }

            // ADR 0065's mutual distrust, the control plane's half: the runner owns the run's working state and not the
            // run's identity. The index arrives projected from the runner's own bytes and no store backend compares it
            // to anything, so this is where a save that re-points a run at another environment, another workflow, or
            // another owner group is refused. Checked before the sequence rule, so such a save is reported as what it
            // is rather than as a race the caller should retry.
            if (slot.IdentityEstablished && !slot.Identity.Matches(index))
            {
                return new CheckpointSaveResult(CheckpointSaveOutcome.Rejected, slot.LastAppliedSequence + 1);
            }

            // ADR 0065 decision 6: the server validates rather than assigns, accepting only the persisted sequence plus
            // one. Both a stale arrival and a gap are refused, and the caller is told which sequence is accepted.
            long accepted = slot.LastAppliedSequence + 1;
            if (sequence != accepted)
            {
                return new CheckpointSaveResult(CheckpointSaveOutcome.Superseded, accepted);
            }

            try
            {
                slot.Etag = await this.store.SaveAsync(id, checkpointUtf8, index, slot.Etag, cancellationToken).ConfigureAwait(false);
                slot.LastAppliedSequence = sequence;

                // A run with no stored row has no identity to preserve, so its first accepted save is what sets one.
                if (!slot.IdentityEstablished)
                {
                    slot.Identity = RunIdentity.From(index);
                    slot.IdentityEstablished = true;
                }

                return new CheckpointSaveResult(CheckpointSaveOutcome.Applied, sequence + 1);
            }
            catch (WorkflowConflictException)
            {
                // The sole-writer invariant is broken (a lost or stolen lease, or a peer advancing the run): do not
                // advance the slot, and surface the conflict so the run's outcome is not reported on this write — it
                // stays claimable for idempotent re-invocation. The slot is now untrustworthy, so drop its seeding and
                // let the next save re-read the row rather than deciding against a stale sequence.
                slot.Seeded = false;
                return new CheckpointSaveResult(CheckpointSaveOutcome.Conflict, accepted);
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

    // Reads the run's identity out of a stored checkpoint. A row that does not project is left alone rather than
    // treated as identity-less: the runner API refuses a malformed body before it is ever stored, so an unprojectable
    // row is a different problem, and inventing an empty identity for it would turn that problem into a free rewrite.
    private static void CaptureIdentity(RunSlot slot, ReadOnlyMemory<byte> checkpointUtf8)
    {
        if (WorkflowCheckpointSerializer.TryProjectIndex(checkpointUtf8, out WorkflowRunIndexEntry stored))
        {
            slot.Identity = RunIdentity.From(stored);
            slot.IdentityEstablished = true;
        }
    }

    /// <summary>
    /// The part of a run's index the writer does not own: which environment it belongs to, which workflow it is of, and
    /// the tags that decide who can see and claim it.
    /// </summary>
    private readonly record struct RunIdentity(string? Environment, string WorkflowId, SecurityTagSet SecurityTags)
    {
        public static RunIdentity From(in WorkflowRunIndexEntry index)
            => new(index.Environment, index.WorkflowId, index.SecurityTags);

        public bool Matches(in WorkflowRunIndexEntry index)
            => string.Equals(this.Environment, index.Environment, StringComparison.Ordinal)
            && string.Equals(this.WorkflowId, index.WorkflowId, StringComparison.Ordinal)
            && this.SecurityTags.SetEquals(index.SecurityTags);
    }

    private sealed class RunSlot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public long LastAppliedSequence { get; set; }

        public WorkflowEtag Etag { get; set; }

        public bool Seeded { get; set; }

        public RunIdentity Identity { get; set; }

        public bool IdentityEstablished { get; set; }

        public long TouchedTimestamp { get; set; }
    }
}

/// <summary>The result of a <see cref="WorkflowCheckpointCoordinator.LoadAsync"/>: the checkpoint and the state a function needs to continue the monotonic write-sequence.</summary>
/// <param name="Checkpoint">The stored checkpoint bytes.</param>
/// <param name="Etag">The checkpoint's etag (advisory to the function; the coordinator threads it).</param>
/// <param name="LastAppliedSequence">The highest write-sequence the coordinator has applied for this run, so the function continues past it.</param>
public readonly record struct CheckpointLoad(ReadOnlyMemory<byte> Checkpoint, WorkflowEtag Etag, long LastAppliedSequence);

/// <summary>The outcome of one checkpoint save, and the sequence the store will accept next.</summary>
/// <param name="Outcome">What happened to the save.</param>
/// <param name="AcceptedSequence">The sequence the store will accept next, which is its persisted sequence plus one.
/// Carried on every outcome so a refused caller can tell a duplicate resend from a genuine divergence without a second
/// round trip.</param>
public readonly record struct CheckpointSaveResult(CheckpointSaveOutcome Outcome, long AcceptedSequence);

/// <summary>The outcome of terminating a checkpoint save.</summary>
public enum CheckpointSaveOutcome
{
    /// <summary>The checkpoint was written to the store and is now the run's durable state.</summary>
    Applied,

    /// <summary>The proposed sequence was not the persisted sequence plus one, so nothing was written. The caller is
    /// told, and told which sequence would be accepted: a superseded save reported as success is indistinguishable
    /// from a durable write, which is what would let a runner's anchor commit to a checkpoint the store never took.</summary>
    Superseded,

    /// <summary>The store rejected the write on an etag conflict, signalling a broken sole-writer invariant; the run stays claimable.</summary>
    Conflict,

    /// <summary>
    /// The save carried an index that changed something the writer does not own — the run's environment, its workflow
    /// id, or its security tags. Nothing was written. Distinct from <see cref="Superseded"/> and <see cref="Conflict"/>,
    /// which are both ordinary races a healthy writer retries: this one is a write no honest writer produces, so a
    /// caller that sees it has a defect or an attack rather than a lost lease.
    /// </summary>
    Rejected,
}