// <copyright file="WorkflowCheckpointCoordinatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of the <see cref="WorkflowCheckpointCoordinator"/>: the monotonic write-sequence that keeps the single
/// overwritten store slot moving forward under out-of-order fire-and-forget saves, the etag threading that makes the
/// coordinator's own writes conflict-free while surfacing a broken sole-writer invariant, and the idle sweep that
/// bounds the per-run state.
/// </summary>
[TestClass]
public sealed class WorkflowCheckpointCoordinatorTests
{
    private static readonly WorkflowRunId Run = new("run-1");
    private static readonly WorkflowRunAddress Address = new("development", Run);

    [TestMethod]
    public async Task Load_of_an_unknown_run_is_null()
    {
        var coordinator = new WorkflowCheckpointCoordinator(new InMemoryWorkflowStateStore());

        (await coordinator.LoadAsync(Address, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Load_returns_the_checkpoint_and_seeds_the_sequence_from_the_stored_row()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveAsync(Address, Bytes(1), Index(WorkflowRunStatus.Running), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store);

        CheckpointLoad? loaded = await coordinator.LoadAsync(Address, default);

        loaded.ShouldNotBeNull();
        loaded!.Value.Checkpoint.ToArray().ShouldBe(Bytes(1));
        loaded.Value.LastAppliedSequence.ShouldBe(1, "the sequence the stored row carries, so the next save proposes 2");
        loaded.Value.Etag.IsNone.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Save_applies_the_checkpoint_to_the_store()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        CheckpointSaveResult outcome = await coordinator.SaveAsync(Address, Bytes(7), WorkflowCheckpointSerializer.Project(Bytes(7)), 1, default);

        outcome.Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(7));
    }

    [TestMethod]
    public async Task Successive_saves_advance_the_sequence_and_thread_the_etag()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        (await coordinator.SaveAsync(Address, Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await coordinator.SaveAsync(Address, Bytes(2), WorkflowCheckpointSerializer.Project(Bytes(2)), 2, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await coordinator.SaveAsync(Address, Bytes(3), WorkflowCheckpointSerializer.Project(Bytes(3)), 3, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);

        // No conflict despite the store's strict etag concurrency, because the coordinator threads the returned etag.
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(3));
    }

    [TestMethod]
    public async Task A_stale_sequence_is_superseded_and_does_not_regress_the_slot()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        await coordinator.SaveAsync(Address, Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default);

        // A resend of a sequence already persisted. It is refused, and told the sequence that would be accepted, so a
        // caller can tell its own duplicate from a genuine divergence.
        CheckpointSaveResult outcome = await coordinator.SaveAsync(Address, Bytes(9), WorkflowCheckpointSerializer.Project(Bytes(9)), 1, default);

        outcome.Outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        outcome.AcceptedSequence.ShouldBe(2);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(1));
    }

    [TestMethod]
    public async Task An_equal_sequence_is_superseded()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        await coordinator.SaveAsync(Address, Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default);

        (await coordinator.SaveAsync(Address, Bytes(9), WorkflowCheckpointSerializer.Project(Bytes(9)), 1, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(1));
    }

    [TestMethod]
    public async Task An_out_of_order_arrival_is_refused_and_lands_once_its_turn_comes()
    {
        // The server validates rather than assigns (ADR 0065 decision 6), so a gap is refused exactly as a stale
        // arrival is: accepting sequence 3 over a persisted 1 would leave the run's history with a hole that neither
        // side could later tell from a lost write.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        (await coordinator.SaveAsync(Address, Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);

        CheckpointSaveResult ahead = await coordinator.SaveAsync(Address, Bytes(3), WorkflowCheckpointSerializer.Project(Bytes(3)), 3, default);
        ahead.Outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        ahead.AcceptedSequence.ShouldBe(2);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(1));

        // The delayed sequence 2 is what the store was waiting for, so it lands.
        (await coordinator.SaveAsync(Address, Bytes(2), WorkflowCheckpointSerializer.Project(Bytes(2)), 2, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(2));
    }

    [TestMethod]
    public async Task A_conflicting_write_is_surfaced_and_the_slot_is_not_advanced()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveAsync(Address, Bytes(1), Index(WorkflowRunStatus.Running), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store);

        // The coordinator seeds the run's etag from the load.
        CheckpointLoad seeded = (await coordinator.LoadAsync(Address, default))!.Value;

        // A peer writes the run out of band (a lost/stolen lease), advancing the store's etag past the seeded one.
        await store.SaveAsync(Address, Bytes(2), Index(WorkflowRunStatus.Running), seeded.Etag, default);

        // The coordinator's next save carries the now-stale seeded etag, so the store rejects it.
        (await coordinator.SaveAsync(Address, Bytes(3), WorkflowCheckpointSerializer.Project(Bytes(3)), 2, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Conflict);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(2));
    }

    [TestMethod]
    public async Task A_terminal_status_does_not_evict_the_slot_so_a_late_interim_is_still_dropped()
    {
        // The terminal checkpoint arrives (seq 2, Completed). A delayed interim (seq 1) arrives after it; because the
        // slot is retained past the terminal status, the retained sequence drops it rather than regressing the run.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        await coordinator.SaveAsync(Address, Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default);
        await coordinator.SaveAsync(Address, Bytes(2), WorkflowCheckpointSerializer.Project(Bytes(2)), sequence: 2, default);

        (await coordinator.SaveAsync(Address, Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(2));
    }

    [TestMethod]
    public async Task An_idle_slot_is_swept_so_the_sequence_registry_does_not_grow_unbounded()
    {
        // With the slot retained the low sequence would be dropped; once the idle sweep evicts it the sequence registry
        // resets, so the same low sequence applies — observably proving the eviction happened.
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        await coordinator.SaveAsync(Address, Bytes(5), WorkflowCheckpointSerializer.Project(Bytes(5)), 5, default);

        // Idle past the slot TTL, then touch a different run to trigger the opportunistic sweep of run-1's stale slot.
        time.Advance(TimeSpan.FromMinutes(20));
        await coordinator.SaveAsync(new WorkflowRunAddress("development", new WorkflowRunId("run-2")), Bytes(1), WorkflowCheckpointSerializer.Project(Bytes(1)), 1, default);

        CheckpointSaveResult afterSweep = await coordinator.SaveAsync(Address, Bytes(8), WorkflowCheckpointSerializer.Project(Bytes(8)), 1, default);

        afterSweep.Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(Bytes(8));
    }

    [TestMethod]
    public async Task A_save_past_the_fuel_is_refused_and_the_run_is_recorded_as_faulted_on_fuel()
    {
        // ADR 0068, the authoritative half: the runner's save is not applied, and the last durable row (not the
        // runner's over-budget body) gains the fault in its control-plane region (ADR 0065 decision 7). The runner's
        // own regions and sequence are untouched.
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 2, sequence: 1, TwoSteps, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        byte[] overBudget = Checkpoint(journalEntries: 3, sequence: 2, TwoSteps, T0);
        CheckpointSaveResult result = await Save(coordinator, Address, overBudget, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        result.FaultError.ShouldBe(ExecutionBudgetFault.Fuel);
        result.AcceptedSequence.ShouldBe(2);

        WorkflowCheckpoint row = (await store.LoadAsync(Address, default))!.Value;
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(row.Row);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(ExecutionBudgetFault.Fuel);
        WorkflowCheckpointSerializer.TryReadSequence(row.Row, out long sequence).ShouldBeTrue();
        sequence.ShouldBe(1, "the control plane's decision consumes no runner sequence");
        WorkflowCheckpointSerializer.TryReadBudgetFacts(row.Row, out CheckpointBudgetFacts facts).ShouldBeTrue();
        facts.JournalCount.ShouldBe(2);
        facts.BudgetFaulted.ShouldBeTrue();

        // The runner's submitted bytes are exactly the stored row's: only the control-plane region was written.
        CheckpointRowLayout before = CheckpointRow.Parse(stored);
        row.Row.Span[..before.SubmittedLength].SequenceEqual(stored.AsSpan(0, before.SubmittedLength)).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_save_from_a_run_older_than_its_wall_clock_is_refused_and_the_run_is_recorded_as_faulted_on_deadline()
    {
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 0, sequence: 1, OneHour, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        time.UtcNow = T0 + TimeSpan.FromHours(2);
        byte[] late = Checkpoint(journalEntries: 1, sequence: 2, OneHour, T0);
        CheckpointSaveResult result = await Save(coordinator, Address, late, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        result.FaultError.ShouldBe(ExecutionBudgetFault.Deadline);
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Address, default))!.Value.Row);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(ExecutionBudgetFault.Deadline);
        index.UpdatedAt.ShouldBe(time.UtcNow);
    }

    [TestMethod]
    public async Task A_runner_authored_deadline_fault_arriving_past_the_wall_clock_is_applied_as_the_terminal_record()
    {
        // A runner that honours its budget faults the run itself, and that save necessarily arrives after the deadline
        // it reports. It is the record the control plane would otherwise write, so it is applied, not refused.
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 0, sequence: 1, OneHour, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        time.UtcNow = T0 + TimeSpan.FromHours(2);
        var fault = new WorkflowFault("s1", 1, ExecutionBudgetFault.Deadline, time.UtcNow);
        byte[] selfFaulted = Checkpoint(journalEntries: 1, sequence: 2, OneHour, T0, WorkflowRunStatus.Faulted, fault: fault);
        CheckpointSaveResult result = await Save(coordinator, Address, selfFaulted, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(selfFaulted);
    }

    [TestMethod]
    public async Task A_budget_fault_record_does_not_excuse_a_save_that_is_not_terminal_or_is_over_fuel()
    {
        // The waiver is for the terminal record alone. A body that carries a budget fault record but leaves the run
        // Running is a runner keeping itself alive past its deadline, and a journal past the fuel is never the save of
        // a runner that honoured the budget: both are refused and the control plane authors the fault.
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 0, sequence: 1, TwoSteps, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        var fuelFault = new WorkflowFault("s3", 1, ExecutionBudgetFault.Fuel, T0);
        byte[] overFuel = Checkpoint(journalEntries: 3, sequence: 2, TwoSteps, T0, WorkflowRunStatus.Faulted, fault: fuelFault);
        CheckpointSaveResult refusedOnFuel = await Save(coordinator, Address, overFuel, 2);
        refusedOnFuel.Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        refusedOnFuel.FaultError.ShouldBe(ExecutionBudgetFault.Fuel);

        var lateStore = new InMemoryWorkflowStateStore();
        await lateStore.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var lateCoordinator = new WorkflowCheckpointCoordinator(lateStore, time);
        time.UtcNow = T0 + TimeSpan.FromHours(2);
        var deadlineFault = new WorkflowFault("s1", 1, ExecutionBudgetFault.Deadline, time.UtcNow);
        byte[] stillRunning = Checkpoint(journalEntries: 1, sequence: 2, TwoSteps, T0, WorkflowRunStatus.Running, fault: deadlineFault);
        CheckpointSaveResult refusedOnDeadline = await Save(lateCoordinator, Address, stillRunning, 2);
        refusedOnDeadline.Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        refusedOnDeadline.FaultError.ShouldBe(ExecutionBudgetFault.Deadline);
    }

    [TestMethod]
    public async Task A_save_within_budget_is_applied_and_a_run_without_a_budget_is_unbounded()
    {
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        byte[] first = Checkpoint(journalEntries: 1, sequence: 1, TwoSteps, T0);
        (await Save(coordinator, Address, first, 1)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        time.UtcNow = T0 + TimeSpan.FromMinutes(59);
        byte[] atTheLimit = Checkpoint(journalEntries: 2, sequence: 2, TwoSteps, T0, WorkflowRunStatus.Completed);
        (await Save(coordinator, Address, atTheLimit, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);

        // A checkpoint from before budgets existed carries none, and nothing bounds it here.
        var legacy = new WorkflowRunAddress("development", new WorkflowRunId("run-legacy"));
        byte[] unbounded = Checkpoint(journalEntries: 40, sequence: 1, null, T0 - TimeSpan.FromDays(30));
        (await Save(coordinator, legacy, unbounded, 1)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
    }

    [TestMethod]
    public async Task A_save_that_widens_drops_or_moves_the_budget_or_the_creation_time_is_rejected()
    {
        // The budget and the creation time are frozen with the identity: a runner cannot extend its own bound.
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 0, sequence: 1, TwoSteps, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, new ControlledTimeProvider());

        byte[] widened = Checkpoint(journalEntries: 1, sequence: 2, new ExecutionBudget(5, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes), T0);
        (await Save(coordinator, Address, widened, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);

        byte[] dropped = Checkpoint(journalEntries: 1, sequence: 2, null, T0);
        (await Save(coordinator, Address, dropped, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);

        byte[] younger = Checkpoint(journalEntries: 1, sequence: 2, TwoSteps, T0 + TimeSpan.FromHours(3));
        (await Save(coordinator, Address, younger, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);

        // Nothing was written, and the honest save still lands.
        byte[] honest = Checkpoint(journalEntries: 1, sequence: 2, TwoSteps, T0);
        (await Save(coordinator, Address, honest, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
    }

    [TestMethod]
    public async Task The_first_accepted_save_freezes_the_budget()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store, new ControlledTimeProvider());

        byte[] first = Checkpoint(journalEntries: 0, sequence: 1, TwoSteps, T0);
        (await Save(coordinator, Address, first, 1)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);

        byte[] widened = Checkpoint(journalEntries: 1, sequence: 2, ExecutionBudget.Default, T0);
        (await Save(coordinator, Address, widened, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
    }

    [TestMethod]
    public async Task A_stale_over_budget_arrival_is_superseded_rather_than_acted_on()
    {
        // The budget is judged only on the save that would otherwise be applied; a race stays reported as a race.
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 1, sequence: 1, TwoSteps, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, new ControlledTimeProvider());

        byte[] stale = Checkpoint(journalEntries: 3, sequence: 1, TwoSteps, T0);
        CheckpointSaveResult result = await Save(coordinator, Address, stale, 1);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(stored);
    }

    [TestMethod]
    public async Task After_exhaustion_a_resend_is_superseded_and_a_further_over_budget_save_churns_nothing()
    {
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 2, sequence: 1, TwoSteps, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, new ControlledTimeProvider());

        byte[] overBudget = Checkpoint(journalEntries: 3, sequence: 2, TwoSteps, T0);
        (await Save(coordinator, Address, overBudget, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        WorkflowCheckpoint faulted = (await store.LoadAsync(Address, default))!.Value;

        // The resend of the refused sequence carries the region the runner loaded, which the fault has since
        // replaced; a runner that missed the refusal is told the run is over, and nothing is rewritten.
        CheckpointSaveResult resend = await Save(coordinator, Address, overBudget, 2);
        resend.Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        resend.AcceptedSequence.ShouldBe(2);

        // A runner that presses on past the sequence the store is at is superseded like any gap, and the faulted
        // row is not rewritten either way.
        byte[] next = Checkpoint(journalEntries: 4, sequence: 3, TwoSteps, T0);
        CheckpointSaveResult again = await Save(coordinator, Address, next, 3);
        again.Outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        again.AcceptedSequence.ShouldBe(2);
        WorkflowCheckpoint after = (await store.LoadAsync(Address, default))!.Value;
        after.Etag.ShouldBe(faulted.Etag);
        after.Row.ToArray().ShouldBe(faulted.Row.ToArray());
    }

    [TestMethod]
    public async Task A_truncated_journal_is_over_any_budget()
    {
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = Checkpoint(journalEntries: 0, sequence: 1, ExecutionBudget.Default, T0);
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, new ControlledTimeProvider());

        byte[] truncated = Checkpoint(journalEntries: 1, sequence: 2, ExecutionBudget.Default, T0, truncated: true);
        CheckpointSaveResult result = await Save(coordinator, Address, truncated, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.BudgetExceeded);
        result.FaultError.ShouldBe(ExecutionBudgetFault.Fuel);
    }

    [TestMethod]
    public async Task A_save_that_carries_a_control_plane_region_other_than_the_stored_one_is_rejected()
    {
        // ADR 0065 decision 7: the control-plane region is the server's. A runner that rewrites it, here to cancel its
        // own cancellation, is refused, and a runner that carries it back unchanged is applied.
        var store = new InMemoryWorkflowStateStore();
        byte[] stored = CheckpointRow.WithControlPlaneRegion(
            Checkpoint(journalEntries: 0, sequence: 1, TwoSteps, T0),
            new ControlPlaneRecord(Budget: TwoSteps, Cancellation: new ControlPlaneCancellation(T0.AddMinutes(1))).ToUtf8());
        await store.SaveAsync(Address, stored, WorkflowCheckpointSerializer.ProjectIndex(stored), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store, new ControlledTimeProvider());

        byte[] uncancelled = Checkpoint(journalEntries: 1, sequence: 2, TwoSteps, T0);
        (await Save(coordinator, Address, uncancelled, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
        (await store.LoadAsync(Address, default))!.Value.Row.ToArray().ShouldBe(stored);

        CheckpointRowLayout layout = CheckpointRow.Parse(stored);
        byte[] carried = CheckpointRow.WithControlPlaneRegion(uncancelled, stored[layout.ControlPlaneRegion]);
        (await Save(coordinator, Address, carried, 2)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Address, default))!.Value.Row).Status.ShouldBe(WorkflowRunStatus.Cancelled);
    }

    [TestMethod]
    public void Rejects_a_null_store()
    {
        Should.Throw<ArgumentNullException>(() => new WorkflowCheckpointCoordinator(null!));
    }

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ExecutionBudget TwoSteps = new(2, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
    private static readonly ExecutionBudget OneHour = new(ExecutionBudget.MaxStepsCeiling, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);

    // A distinct, well-formed row per marker: the marker is the runner sequence the row carries, and the bytes differ
    // by it alone, so a stored row identifies which save landed.
    private static byte[] Bytes(byte marker) => Checkpoint(journalEntries: 0, sequence: marker, null, T0);

    private static WorkflowRunIndexEntry Index(WorkflowRunStatus status) => new("wf", status, default, default);

    // Saves a real checkpoint the way the surfaces do: one projection of the bytes, its parts handed to the coordinator.
    private static ValueTask<CheckpointSaveResult> Save(WorkflowCheckpointCoordinator coordinator, WorkflowRunAddress address, byte[] checkpoint, long sequence)
    {
        CheckpointProjection projection = WorkflowCheckpointSerializer.Project(checkpoint);
        return coordinator.SaveAsync(address, checkpoint, projection, sequence, default);
    }

    // A real checkpoint document of run-1 in the development environment, with the given journal length and budget.
    private static byte[] Checkpoint(int journalEntries, long sequence, ExecutionBudget? budget, DateTimeOffset createdAt, WorkflowRunStatus status = WorkflowRunStatus.Running, bool truncated = false, WorkflowFault? fault = null)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        var journal = new List<WorkflowStepJournalEntry>(journalEntries);
        for (int i = 1; i <= journalEntries; i++)
        {
            journal.Add(new WorkflowStepJournalEntry($"s{i}", WorkflowStepStatus.Succeeded, 1, createdAt.AddSeconds(i), createdAt.AddSeconds(i + 1)));
        }

        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                Run,
                "development",
                "wf",
                status,
                journalEntries,
                sequence,
                Epoch: null,
                createdAt,
                createdAt,
                null,
                null,
                default,
                default,
                journal,
                truncated,
                null,
                fault),
            retryCounters,
            new Dictionary<string, byte[]>(),
            default,
            stepOutputs,
            default,
            new ControlPlaneRecord(Budget: budget).ToUtf8());
    }

    // A TimeProvider whose timestamp only advances when the test tells it to, so the idle sweep is deterministic. It
    // keeps the base TimestampFrequency, so GetElapsedTime converts the advanced ticks back to the intended interval.
    // Its wall clock is set by the test too, so the budget's deadline is judged against a known age.
    private sealed class ControlledTimeProvider : TimeProvider
    {
        private long timestamp;

        public DateTimeOffset UtcNow { get; set; } = T0;

        public override long GetTimestamp() => this.timestamp;

        public override DateTimeOffset GetUtcNow() => this.UtcNow;

        public void Advance(TimeSpan by)
        {
            this.timestamp += (long)(by.TotalSeconds * this.TimestampFrequency);
            this.UtcNow += by;
        }
    }
}
