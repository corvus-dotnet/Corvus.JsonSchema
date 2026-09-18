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
/// A per-run monotonic write-sequence: the slot accepts exactly the next sequence (last applied + 1). Any other
/// proposal is refused as Superseded and told the sequence that would be accepted — both a stale or duplicate
/// arrival below the next, and a gap above it (accepting a gap would leave a hole neither side could later tell
/// from a lost write). A delayed arrival lands once its turn comes. A terminal checkpoint is no exception: it is
/// accepted only when its own sequence is the next one, and a gap before it is refused like any other.
/// </description></item>
/// <item><description>
/// Per-run serialization plus etag threading: each run's saves run one at a time behind a gate, threading the store's
/// returned etag into the next save, so the coordinator's own writes never conflict. The lease the dispatcher holds
/// makes the coordinator the sole writer, so a conflict signals a lost or stolen lease and is surfaced (never
/// silently overwritten) — the run stays claimable for idempotent re-invocation.
/// </description></item>
/// </list>
/// <para>
/// The coordinator is also where the execution budget is verified (ADR 0068). The budget and the creation time are
/// part of the run's frozen identity, and on every in-turn save the journal length and the run's age, read from the
/// body the caller already projected, are compared against them; a save past either limit is not applied. Instead
/// the coordinator itself rewrites the last durable checkpoint as terminally faulted on the limit that was hit and
/// reports <see cref="CheckpointSaveOutcome.BudgetExceeded"/>, so a runner that does not honour the budget cannot
/// persist the run past it.
/// </para>
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
    private readonly ConcurrentDictionary<WorkflowRunAddress, RunSlot> slots = new();
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
    /// <param name="address">The run's <c>(environment, runId)</c> address.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The checkpoint bytes, its etag, and the last applied write-sequence; or <see langword="null"/> if the run has no checkpoint.</returns>
    public async ValueTask<CheckpointLoad?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            // No checkpoint yet: the function reads this as a run with no persisted state. Do not create a slot — the
            // first save creates one, seeded from write-sequence zero.
            return null;
        }

        RunSlot slot = this.GetSlot(address);
        long appliedSequence;
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The store's etag is the authority; align the slot to it so a warm advance's next save threads forward
            // from the state the function just loaded. The applied sequence carries across advances on this runner so a
            // reused function instance keeps stamping monotonically.
            slot.Etag = checkpoint.Value.Etag;
            SeedFromStored(slot, checkpoint.Value.Utf8);
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
    /// <param name="address">The run's <c>(environment, runId)</c> address.</param>
    /// <param name="checkpointUtf8">The checkpoint bytes to persist verbatim.</param>
    /// <param name="index">The index projected from the same bytes.</param>
    /// <param name="claimedEnvironment">The environment the checkpoint BODY claims, taken from the caller's one
    /// projection of the same bytes (<see cref="WorkflowCheckpointSerializer.ProjectIndex(ReadOnlyMemory{byte}, out string?)"/>).
    /// A body claiming an environment other than <paramref name="address"/>'s — or claiming none — is refused on
    /// EVERY save (ADR 0065 decision 9): the environment is the run's address, and no save may re-home it.</param>
    /// <param name="facts">The execution-budget facts the caller's one projection of the same bytes reports
    /// (<see cref="CheckpointProjection.Facts"/>): the budget the body carries, which joins the frozen identity, and
    /// the journal length and truncation the budget is verified against (ADR 0068).</param>
    /// <param name="sequence">The save's monotonic per-run write-sequence.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome, and the sequence the store will accept next.</returns>
    public async ValueTask<CheckpointSaveResult> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointUtf8, WorkflowRunIndexEntry index, string? claimedEnvironment, CheckpointBudgetFacts facts, long sequence, CancellationToken cancellationToken)
    {
        RunSlot slot = this.GetSlot(address);
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!slot.Seeded)
            {
                // No load preceded this save (a fresh run with no persisted checkpoint, or a slot swept between load and
                // save). Seed the etag AND the persisted sequence from the store, so the write is conditioned correctly
                // and the acceptance rule is evaluated against what the row actually holds rather than against a
                // process-local counter that a restart reset to zero.
                WorkflowCheckpoint? existing = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
                slot.Etag = existing?.Etag ?? WorkflowEtag.None;
                if (existing is { } row)
                {
                    SeedFromStored(slot, row.Utf8);
                }
                else
                {
                    slot.LastAppliedSequence = 0;
                }

                slot.Seeded = true;
            }

            // ADR 0065's mutual distrust, the control plane's half: the runner owns the run's working state and not
            // the run's identity. The environment IS the run's address (decision 9), so the body's claim is checked
            // against the address structurally on EVERY save — first save included — replacing the old first-write
            // identity pin for the environment: a first save could otherwise establish whatever environment it
            // claimed. Workflow id and security tags stay first-write-pinned below (a fresh run legitimately states
            // them once). Checked before the sequence rule, so such a save is reported as what it is rather than as
            // a race the caller should retry.
            if (!string.Equals(claimedEnvironment, address.Environment, StringComparison.Ordinal))
            {
                return new CheckpointSaveResult(CheckpointSaveOutcome.Rejected, slot.LastAppliedSequence + 1);
            }

            // The budget and the creation time join the first-write-pinned identity (ADR 0068): a later save that widens
            // the budget, drops it, or moves the run's creation forward is a save no well-behaved writer produces, and
            // either would let the runner extend its own bound. The facts come from the caller's one projection of the
            // same bytes, so the coordinator reads the body no further.
            if (slot.IdentityEstablished && !slot.Identity.Matches(index, facts.Budget))
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

            // ADR 0068: the authoritative half of the budget. Decided after the sequence rule so only the save that
            // would otherwise be applied is judged (a stale or out-of-order arrival is refused as the race it is, not
            // acted on), and against the identity's frozen budget and creation time, never the body's claim of them.
            if (slot.IdentityEstablished
                && slot.Identity.Budget is { } budget
                && ExecutionBudgetFault.Find(budget, facts, slot.Identity.CreatedAt, this.timeProvider.GetUtcNow()) is { } exceeded)
            {
                return await this.FaultExhaustedAsync(address, slot, accepted, exceeded, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                slot.Etag = await this.store.SaveAsync(address, checkpointUtf8, index, slot.Etag, cancellationToken).ConfigureAwait(false);
                slot.LastAppliedSequence = sequence;

                // A run with no stored row has no identity to preserve, so its first accepted save is what sets one.
                if (!slot.IdentityEstablished)
                {
                    slot.Identity = RunIdentity.From(index, facts.Budget);
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

    // Records the budget fault the control plane decided: the last durable checkpoint is rewritten as terminally
    // faulted on the limit that was hit, under the slot's etag (the coordinator is the sole writer), consuming the
    // sequence the refused save proposed so the runner's resend of it is superseded rather than re-judged. A row that
    // already carries a budget fault is left as it is, so a runner that keeps resending after the refusal churns
    // nothing. Called under the slot's gate.
    private async ValueTask<CheckpointSaveResult> FaultExhaustedAsync(WorkflowRunAddress address, RunSlot slot, long accepted, string error, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? stored = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (stored is not { } row)
        {
            // The row went away under an established identity: the sole-writer invariant is broken, and the slot
            // can no longer be trusted.
            slot.Seeded = false;
            return new CheckpointSaveResult(CheckpointSaveOutcome.Conflict, accepted);
        }

        if (WorkflowCheckpointSerializer.TryReadBudgetFacts(row.Utf8, out CheckpointBudgetFacts storedFacts) && storedFacts.BudgetFaulted)
        {
            return new CheckpointSaveResult(CheckpointSaveOutcome.BudgetExceeded, accepted, error);
        }

        byte[] faulted = WorkflowCheckpointSerializer.RewriteFaulted(row.Utf8.Span, accepted, error, this.timeProvider.GetUtcNow());
        try
        {
            slot.Etag = await this.store.SaveAsync(address, faulted, WorkflowCheckpointSerializer.ProjectIndex(faulted), slot.Etag, cancellationToken).ConfigureAwait(false);
            slot.LastAppliedSequence = accepted;
            return new CheckpointSaveResult(CheckpointSaveOutcome.BudgetExceeded, accepted + 1, error);
        }
        catch (WorkflowConflictException)
        {
            slot.Seeded = false;
            return new CheckpointSaveResult(CheckpointSaveOutcome.Conflict, accepted);
        }
    }

    private RunSlot GetSlot(in WorkflowRunAddress address)
    {
        this.MaybeSweep();
        RunSlot slot = this.slots.GetOrAdd(address, static _ => new RunSlot());

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

        foreach (KeyValuePair<WorkflowRunAddress, RunSlot> entry in this.slots)
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

    // Seeds the slot's persisted sequence and the run's identity from a stored checkpoint, in one parse. A row that
    // does not project is left identity-less rather than given an empty identity: the runner API refuses a malformed
    // body before it is ever stored, so an unprojectable row is a different problem, and inventing an identity for it
    // would turn that problem into a free rewrite. Its sequence reads as zero, as a row carrying none does.
    private static void SeedFromStored(RunSlot slot, ReadOnlyMemory<byte> checkpointUtf8)
    {
        if (WorkflowCheckpointSerializer.TryProject(checkpointUtf8, out CheckpointProjection stored))
        {
            slot.LastAppliedSequence = stored.Sequence ?? 0;
            slot.Identity = RunIdentity.From(stored.Index, stored.Facts.Budget);
            slot.IdentityEstablished = true;
        }
        else
        {
            slot.LastAppliedSequence = 0;
        }
    }

    /// <summary>
    /// The part of a run's index the writer does not own and that a first save legitimately states once: which
    /// workflow it is of, the tags that decide who can see and claim it, and (ADR 0068) the execution budget the
    /// control plane resolved at start together with the creation time the budget's wall clock runs from. The run's
    /// environment is NOT here — it is the address itself, checked structurally against the route on every save
    /// (ADR 0065 decision 9).
    /// </summary>
    private readonly record struct RunIdentity(string WorkflowId, SecurityTagSet SecurityTags, ExecutionBudget? Budget, DateTimeOffset CreatedAt)
    {
        public static RunIdentity From(in WorkflowRunIndexEntry index, ExecutionBudget? budget)
            => new(index.WorkflowId, index.SecurityTags, budget, index.CreatedAt);

        public bool Matches(in WorkflowRunIndexEntry index, ExecutionBudget? budget)
            => string.Equals(this.WorkflowId, index.WorkflowId, StringComparison.Ordinal)
            && this.SecurityTags.SetEquals(index.SecurityTags)
            && Nullable.Equals(this.Budget, budget)
            && this.CreatedAt == index.CreatedAt;
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
/// <param name="FaultError">On <see cref="CheckpointSaveOutcome.BudgetExceeded"/>, the budget fault the run was
/// recorded with (one of <see cref="ExecutionBudgetFault"/>); otherwise <see langword="null"/>.</param>
public readonly record struct CheckpointSaveResult(CheckpointSaveOutcome Outcome, long AcceptedSequence, string? FaultError = null);

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
    /// The save changed something the writer does not own — the body claimed an environment other than the
    /// addressed one (checked on every save: the environment is the run's address, ADR 0065 decision 9), or the
    /// index re-pointed the run's workflow id or security tags. Nothing was written. Distinct from
    /// <see cref="Superseded"/> and <see cref="Conflict"/>, which are both ordinary races a healthy writer retries:
    /// this one is a write no well-behaved writer produces, so a caller that sees it has a defect or an attack
    /// rather than a lost lease.
    /// </summary>
    Rejected,

    /// <summary>
    /// The save was past the run's execution budget (ADR 0068): its journal is longer than the fuel or truncated at
    /// the cap, or the run is older than its wall clock. The proposed checkpoint was not written. The control plane
    /// instead recorded the run as terminally faulted on the limit it hit (<see cref="CheckpointSaveResult.FaultError"/>),
    /// consuming the proposed sequence, so nothing resumes or reclaims it. The caller should stop advancing the run.
    /// </summary>
    BudgetExceeded,
}