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

    [TestMethod]
    public async Task Load_of_an_unknown_run_is_null()
    {
        var coordinator = new WorkflowCheckpointCoordinator(new InMemoryWorkflowStateStore());

        (await coordinator.LoadAsync(Run, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Load_returns_the_checkpoint_and_a_zero_sequence_before_any_save()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store);

        CheckpointLoad? loaded = await coordinator.LoadAsync(Run, default);

        loaded.ShouldNotBeNull();
        loaded!.Value.Checkpoint.ToArray().ShouldBe(Bytes(1));
        loaded.Value.LastAppliedSequence.ShouldBe(0);
        loaded.Value.Etag.IsNone.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Save_applies_the_checkpoint_to_the_store()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        CheckpointSaveOutcome outcome = await coordinator.SaveAsync(Run, Bytes(7), Index(WorkflowRunStatus.Running), sequence: 1, default);

        outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(7));
    }

    [TestMethod]
    public async Task Successive_saves_advance_the_sequence_and_thread_the_etag()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        (await coordinator.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), 1, default)).ShouldBe(CheckpointSaveOutcome.Applied);
        (await coordinator.SaveAsync(Run, Bytes(2), Index(WorkflowRunStatus.Running), 2, default)).ShouldBe(CheckpointSaveOutcome.Applied);
        (await coordinator.SaveAsync(Run, Bytes(3), Index(WorkflowRunStatus.Completed), 3, default)).ShouldBe(CheckpointSaveOutcome.Applied);

        // No conflict despite the store's strict etag concurrency, because the coordinator threads the returned etag.
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(3));
    }

    [TestMethod]
    public async Task A_stale_sequence_is_superseded_and_does_not_regress_the_slot()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        await coordinator.SaveAsync(Run, Bytes(2), Index(WorkflowRunStatus.Running), sequence: 2, default);

        CheckpointSaveOutcome outcome = await coordinator.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), sequence: 1, default);

        outcome.ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(2));
    }

    [TestMethod]
    public async Task An_equal_sequence_is_superseded()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        await coordinator.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), sequence: 1, default);

        (await coordinator.SaveAsync(Run, Bytes(9), Index(WorkflowRunStatus.Running), sequence: 1, default)).ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(1));
    }

    [TestMethod]
    public async Task Out_of_order_arrivals_keep_the_highest_sequence()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        (await coordinator.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), 1, default)).ShouldBe(CheckpointSaveOutcome.Applied);
        (await coordinator.SaveAsync(Run, Bytes(3), Index(WorkflowRunStatus.Running), 3, default)).ShouldBe(CheckpointSaveOutcome.Applied);

        // Sequence 2 arrives last (a delayed fire-and-forget); it is older than the applied 3, so it is dropped.
        (await coordinator.SaveAsync(Run, Bytes(2), Index(WorkflowRunStatus.Running), 2, default)).ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(3));
    }

    [TestMethod]
    public async Task A_conflicting_write_is_surfaced_and_the_slot_is_not_advanced()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), WorkflowEtag.None, default);
        var coordinator = new WorkflowCheckpointCoordinator(store);

        // The coordinator seeds the run's etag from the load.
        CheckpointLoad seeded = (await coordinator.LoadAsync(Run, default))!.Value;

        // A peer writes the run out of band (a lost/stolen lease), advancing the store's etag past the seeded one.
        await store.SaveAsync(Run, Bytes(2), Index(WorkflowRunStatus.Running), seeded.Etag, default);

        // The coordinator's next save carries the now-stale seeded etag, so the store rejects it.
        (await coordinator.SaveAsync(Run, Bytes(3), Index(WorkflowRunStatus.Running), sequence: 1, default)).ShouldBe(CheckpointSaveOutcome.Conflict);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(2));
    }

    [TestMethod]
    public async Task A_terminal_status_does_not_evict_the_slot_so_a_late_interim_is_still_dropped()
    {
        // The terminal checkpoint arrives (seq 2, Completed). A delayed interim (seq 1) arrives after it; because the
        // slot is retained past the terminal status, the retained sequence drops it rather than regressing the run.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        await coordinator.SaveAsync(Run, Bytes(2), Index(WorkflowRunStatus.Completed), sequence: 2, default);

        (await coordinator.SaveAsync(Run, Bytes(1), Index(WorkflowRunStatus.Running), sequence: 1, default)).ShouldBe(CheckpointSaveOutcome.Superseded);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(2));
    }

    [TestMethod]
    public async Task An_idle_slot_is_swept_so_the_sequence_registry_does_not_grow_unbounded()
    {
        // With the slot retained the low sequence would be dropped; once the idle sweep evicts it the sequence registry
        // resets, so the same low sequence applies — observably proving the eviction happened.
        var time = new ControlledTimeProvider();
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store, time);

        await coordinator.SaveAsync(Run, Bytes(5), Index(WorkflowRunStatus.Running), sequence: 5, default);

        // Idle past the slot TTL, then touch a different run to trigger the opportunistic sweep of run-1's stale slot.
        time.Advance(TimeSpan.FromMinutes(20));
        await coordinator.SaveAsync(new WorkflowRunId("run-2"), Bytes(1), Index(WorkflowRunStatus.Running), sequence: 1, default);

        CheckpointSaveOutcome afterSweep = await coordinator.SaveAsync(Run, Bytes(8), Index(WorkflowRunStatus.Running), sequence: 1, default);

        afterSweep.ShouldBe(CheckpointSaveOutcome.Applied);
        (await store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(Bytes(8));
    }

    [TestMethod]
    public void Rejects_a_null_store()
    {
        Should.Throw<ArgumentNullException>(() => new WorkflowCheckpointCoordinator(null!));
    }

    private static byte[] Bytes(byte marker) => [marker, marker, marker];

    private static WorkflowRunIndexEntry Index(WorkflowRunStatus status) => new("wf", status, default, default);

    // A TimeProvider whose timestamp only advances when the test tells it to, so the idle sweep is deterministic. It
    // keeps the base TimestampFrequency, so GetElapsedTime converts the advanced ticks back to the intended interval.
    private sealed class ControlledTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long GetTimestamp() => this.timestamp;

        public void Advance(TimeSpan by) => this.timestamp += (long)(by.TotalSeconds * this.TimestampFrequency);
    }
}
