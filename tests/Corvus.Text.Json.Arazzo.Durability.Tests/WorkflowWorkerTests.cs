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

    [TestMethod]
    public async Task Worker_scoped_to_an_environment_skips_a_due_run_pinned_to_another_environment()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        await SuspendTimer(store, time, "dev-timer", TimeSpan.FromMinutes(1), environment: "development");
        time.Advance(TimeSpan.FromMinutes(2)); // the timer is now due

        var worker = new WorkflowWorker(store, "runner", time);

        // A runner serving "system" must NOT resume a run pinned to "development": the timer-resume path is
        // environment-scoped exactly as dispatch is (§5.5), so a due run never crosses the credential boundary. This is
        // the defect behind the demo's system-runner faulting on the development-pinned $schedule run every minute.
        int crossEnv = await worker.ResumeDueTimersAsync(
            (run, ct) => ValueTask.FromResult(WorkflowRunResultKind.Completed),
            "system",
            default);
        crossEnv.ShouldBe(0);

        // Its own environment resumes it (and the unscoped overload still resumes every due run).
        int sameEnv = await worker.ResumeDueTimersAsync(
            (run, ct) => ValueTask.FromResult(WorkflowRunResultKind.Completed),
            "development",
            default);
        sameEnv.ShouldBe(1);
    }

    [TestMethod]
    public async Task Two_runners_on_a_shared_store_each_resume_only_their_own_environments_due_run()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);

        // A shared store with two due suspended runs, mirroring the control-plane demo: one pinned to "development" (the
        // app runner's environment, the seeded $schedule) and one to "system" (the system runner's environment). This is
        // the exact two-runner topology where the timer-resume isolation defect fired every minute.
        await SuspendTimer(store, time, "dev-schedule", TimeSpan.FromMinutes(1), environment: "development");
        await SuspendTimer(store, time, "system-run", TimeSpan.FromMinutes(1), environment: "system");
        time.Advance(TimeSpan.FromMinutes(2)); // both timers are now due

        var resumedByDev = new List<string>();
        var resumedBySystem = new List<string>();
        WorkflowResumer devResumer = async (run, ct) => { resumedByDev.Add(run.Id.Value); await run.CompleteAsync(default, ct); return WorkflowRunResultKind.Completed; };
        WorkflowResumer systemResumer = async (run, ct) => { resumedBySystem.Add(run.Id.Value); await run.CompleteAsync(default, ct); return WorkflowRunResultKind.Completed; };

        var appRunner = new WorkflowWorker(store, "app-runner", time);
        var systemRunner = new WorkflowWorker(store, "system-runner", time);

        // Both runners poll the same shared store, but each resumes ONLY its own environment's due run and never sees the
        // other's, so the system runner never faults on the development-pinned $schedule run (the original symptom).
        int devCount = await appRunner.ResumeDueTimersAsync(devResumer, "development", default);
        int systemCount = await systemRunner.ResumeDueTimersAsync(systemResumer, "system", default);

        devCount.ShouldBe(1);
        systemCount.ShouldBe(1);
        resumedByDev.ShouldBe(["dev-schedule"]);
        resumedBySystem.ShouldBe(["system-run"]);
    }

    private static async ValueTask SuspendTimer(InMemoryWorkflowStateStore store, TimeProvider time, string id, TimeSpan delay, string environment = "development")
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, environment, time);
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