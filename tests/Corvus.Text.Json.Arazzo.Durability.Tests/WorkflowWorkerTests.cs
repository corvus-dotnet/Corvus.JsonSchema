// <copyright file="WorkflowWorkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the <see cref="WorkflowWorker"/> guards that are not exercised by the happy-path resume
/// end-to-end tests: it requires a wait index, skips a run another worker leases, and skips a run that is no
/// longer suspended by the time the lease is taken (a stale index entry).
/// </summary>
[TestClass]
public sealed class WorkflowWorkerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_store_without_a_wait_index_cannot_drive_a_worker()
    {
        Should.Throw<ArgumentException>(() => new WorkflowWorker(new CoreOnlyStore(), "me"));
    }

    [TestMethod]
    public async Task Worker_skips_a_run_another_owner_holds()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        await SuspendTimer(store, time, "held", TimeSpan.FromMinutes(1));

        // Another worker holds the lease; the timer is due.
        await store.AcquireLeaseAsync("held", "other-worker", TimeSpan.FromMinutes(5), default);
        time.Advance(TimeSpan.FromMinutes(2));

        var worker = new WorkflowWorker(store, "me", time);
        bool resumed = false;
        int count = await worker.ResumeDueTimersAsync(
            (run, ct) => { resumed = true; return ValueTask.FromResult(WorkflowRunResultKind.Completed); },
            default);

        count.ShouldBe(0);
        resumed.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Worker_skips_a_run_that_is_no_longer_suspended()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);

        // A stale index: it reports the run as suspended-and-due, but the checkpoint says completed (the run
        // finished between the index query and the lease).
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        byte[] checkpoint = WorkflowCheckpointSerializer.Serialize(
            "stale",
            "wf",
            WorkflowRunStatus.Completed,
            cursor: 0,
            Start,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default);
        var index = new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Suspended, Start, Start, DueAt: Start);
        await store.SaveAsync("stale", checkpoint, index, WorkflowEtag.None, default);
        time.Advance(TimeSpan.FromMinutes(1));

        var worker = new WorkflowWorker(store, "me", time);
        bool resumed = false;
        int count = await worker.ResumeDueTimersAsync(
            (run, ct) => { resumed = true; return ValueTask.FromResult(WorkflowRunResultKind.Completed); },
            default);

        count.ShouldBe(0);
        resumed.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Worker_skips_a_run_the_store_can_no_longer_load()
    {
        // The index reports a due run, but the checkpoint is gone (deleted between query and lease) — the
        // worker loads null and skips it.
        var store = new PhantomWaitIndexStore();
        var worker = new WorkflowWorker(store, "me");

        bool resumed = false;
        int count = await worker.ResumeDueTimersAsync(
            (run, ct) => { resumed = true; return ValueTask.FromResult(WorkflowRunResultKind.Completed); },
            default);

        count.ShouldBe(0);
        resumed.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Worker_honours_an_explicit_lease_ttl_and_resumes()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        await SuspendTimer(store, time, "x", TimeSpan.FromMinutes(1));
        time.Advance(TimeSpan.FromMinutes(2));

        var worker = new WorkflowWorker(store, "me", time, TimeSpan.FromSeconds(30));
        int count = await worker.ResumeDueTimersAsync(
            (run, ct) => ValueTask.FromResult(WorkflowRunResultKind.Completed),
            default);

        count.ShouldBe(1);
    }

    private static async ValueTask SuspendTimer(InMemoryWorkflowStateStore store, TimeProvider time, string id, TimeSpan delay)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time);
        await run.SuspendForTimerAsync(1, delay, default);
    }

    // A store that implements only the universal core (no IWorkflowWaitIndex), to prove the worker rejects it.
    private sealed class CoreOnlyStore : IWorkflowStateStore
    {
        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => ValueTask.FromResult(WorkflowEtag.None);

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowCheckpoint?>(null);

        public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowLease?>(null);

        public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken) => default;

        public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken) => default;
    }

    // A store whose index reports a due run the store can never load (a delete-after-query race), to exercise
    // the worker's load-returned-null guard.
    private sealed class PhantomWaitIndexStore : IWorkflowStateStore, IWorkflowWaitIndex
    {
        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => ValueTask.FromResult(WorkflowEtag.None);

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowCheckpoint?>(null);

        public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
            => ValueTask.FromResult<WorkflowLease?>(new WorkflowLease(id, owner, "token", Start + ttl));

        public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken) => default;

        public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken) => default;

#pragma warning disable CS1998 // async iterator with no await is the simplest shape for a fixed sequence
        public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new WorkflowRunId("phantom");
        }

        public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield break;
        }
#pragma warning restore CS1998

        public ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
            => ValueTask.FromResult(WorkflowRunPage.Create([]));
    }
}