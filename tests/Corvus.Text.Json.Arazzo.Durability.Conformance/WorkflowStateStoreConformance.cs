// <copyright file="WorkflowStateStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IWorkflowStateStore"/> (and its <see cref="IWorkflowWaitIndex"/>)
/// must satisfy, regardless of backend: optimistic-concurrency save/load, the advisory single-owner lease,
/// delete, and the due/awaiting/visibility queries. A backend's test project derives a concrete
/// <see cref="TestClassAttribute"/> from this and implements <see cref="CreateStoreAsync"/>; the in-memory
/// store is the reference implementation and runs the same suite.
/// </summary>
public abstract class WorkflowStateStoreConformance
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for lease expiry.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider);

    /// <summary>Disposes any stores created during the test.</summary>
    /// <returns>A task that completes when cleanup is done.</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (IAsyncDisposable disposable in this.disposables)
        {
            await disposable.DisposeAsync();
        }

        this.disposables.Clear();
    }

    [TestMethod]
    public async Task Save_creates_then_load_returns_bytes_and_etag()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        WorkflowEtag etag = await store.SaveAsync("run-1", Bytes("""{"v":1}"""), Index(), WorkflowEtag.None, default);

        etag.IsNone.ShouldBeFalse();
        WorkflowCheckpoint? loaded = await store.LoadAsync("run-1", default);
        loaded.ShouldNotBeNull();
        loaded.Value.Etag.ShouldBe(etag);
        Encoding.UTF8.GetString(loaded.Value.Utf8.Span).ShouldBe("""{"v":1}""");
    }

    [TestMethod]
    public async Task Load_of_unknown_run_returns_null()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        (await store.LoadAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Save_with_None_when_run_exists_conflicts()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("run-1", Bytes("a"), Index(), WorkflowEtag.None, default);

        WorkflowConflictException ex = await Should.ThrowAsync<WorkflowConflictException>(
            async () => await store.SaveAsync("run-1", Bytes("b"), Index(), WorkflowEtag.None, default));
        ex.RunId.ShouldBe(new WorkflowRunId("run-1"));
    }

    [TestMethod]
    public async Task Save_with_current_etag_advances_the_etag()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        WorkflowEtag first = await store.SaveAsync("run-1", Bytes("a"), Index(), WorkflowEtag.None, default);

        WorkflowEtag second = await store.SaveAsync("run-1", Bytes("b"), Index(), first, default);

        second.ShouldNotBe(first);
        WorkflowCheckpoint? loaded = await store.LoadAsync("run-1", default);
        Encoding.UTF8.GetString(loaded!.Value.Utf8.Span).ShouldBe("b");
        loaded.Value.Etag.ShouldBe(second);
    }

    [TestMethod]
    public async Task Save_with_stale_etag_conflicts()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        WorkflowEtag first = await store.SaveAsync("run-1", Bytes("a"), Index(), WorkflowEtag.None, default);
        await store.SaveAsync("run-1", Bytes("b"), Index(), first, default);

        await Should.ThrowAsync<WorkflowConflictException>(
            async () => await store.SaveAsync("run-1", Bytes("c"), Index(), first, default));
    }

    [TestMethod]
    public async Task Save_with_expected_etag_when_run_absent_conflicts()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await Should.ThrowAsync<WorkflowConflictException>(
            async () => await store.SaveAsync("ghost", Bytes("a"), Index(), new WorkflowEtag("7"), default));
    }

    [TestMethod]
    public async Task Delete_removes_the_run()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("run-1", Bytes("a"), Index(), WorkflowEtag.None, default);

        await store.DeleteAsync("run-1", default);

        (await store.LoadAsync("run-1", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Delete_of_unknown_run_is_a_no_op()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await Should.NotThrowAsync(async () => await store.DeleteAsync("missing", default));
    }

    [TestMethod]
    public async Task Lease_is_held_against_a_second_owner_until_released()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();

        WorkflowLease? a = await store.AcquireLeaseAsync("run-1", "worker-a", TimeSpan.FromMinutes(1), default);
        WorkflowLease? b = await store.AcquireLeaseAsync("run-1", "worker-b", TimeSpan.FromMinutes(1), default);

        a.ShouldNotBeNull();
        b.ShouldBeNull();

        await store.ReleaseLeaseAsync(a.Value, default);
        (await store.AcquireLeaseAsync("run-1", "worker-b", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Lease_is_reacquirable_after_it_expires()
    {
        var clock = new TestClock(T0);
        IWorkflowStateStore store = await this.NewStoreAsync(clock);

        await store.AcquireLeaseAsync("run-1", "worker-a", TimeSpan.FromSeconds(30), default);
        clock.Advance(TimeSpan.FromSeconds(31));

        WorkflowLease? b = await store.AcquireLeaseAsync("run-1", "worker-b", TimeSpan.FromSeconds(30), default);
        b.ShouldNotBeNull();
        b.Value.Owner.ShouldBe("worker-b");
    }

    [TestMethod]
    public async Task The_same_owner_can_renew_its_lease()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();

        await store.AcquireLeaseAsync("run-1", "worker-a", TimeSpan.FromMinutes(1), default);
        (await store.AcquireLeaseAsync("run-1", "worker-a", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Releasing_a_superseded_lease_does_not_free_the_current_holder()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        WorkflowLease? stale = await store.AcquireLeaseAsync("run-1", "worker-a", TimeSpan.FromMinutes(1), default);
        await store.ReleaseLeaseAsync(stale!.Value, default);
        WorkflowLease? current = await store.AcquireLeaseAsync("run-1", "worker-b", TimeSpan.FromMinutes(1), default);

        await store.ReleaseLeaseAsync(stale.Value, default);

        current.ShouldNotBeNull();
        (await store.AcquireLeaseAsync("run-1", "worker-c", TimeSpan.FromMinutes(1), default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Releasing_a_lease_for_an_unleased_run_is_a_no_op()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await Should.NotThrowAsync(async () =>
            await store.ReleaseLeaseAsync(new WorkflowLease("never", "owner", "token", default), default));
    }

    [TestMethod]
    public async Task QueryDue_returns_only_suspended_runs_whose_timer_is_due()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("soon", Bytes("a"), Suspended(dueAt: T0 + TimeSpan.FromMinutes(1)), WorkflowEtag.None, default);
        await store.SaveAsync("later", Bytes("a"), Suspended(dueAt: T0 + TimeSpan.FromHours(1)), WorkflowEtag.None, default);
        await store.SaveAsync("running", Bytes("a"), Index(), WorkflowEtag.None, default);

        List<WorkflowRunId> due = await Collect(((IWorkflowWaitIndex)store).QueryDueAsync(T0 + TimeSpan.FromMinutes(5), default));

        due.ShouldHaveSingleItem();
        due[0].ShouldBe(new WorkflowRunId("soon"));
    }

    [TestMethod]
    public async Task QueryAwaiting_matches_channel_and_correlation()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("orderA", Bytes("a"), Suspended(channel: "responses", correlationId: "order-A"), WorkflowEtag.None, default);
        await store.SaveAsync("orderB", Bytes("a"), Suspended(channel: "responses", correlationId: "order-B"), WorkflowEtag.None, default);
        await store.SaveAsync("other", Bytes("a"), Suspended(channel: "notifications", correlationId: "order-A"), WorkflowEtag.None, default);

        var index = (IWorkflowWaitIndex)store;
        (await Collect(index.QueryAwaitingAsync("responses", "order-A", default))).ShouldHaveSingleItem().ShouldBe(new WorkflowRunId("orderA"));
        (await Collect(index.QueryAwaitingAsync("responses", null, default))).Count.ShouldBe(2);
        (await Collect(index.QueryAwaitingAsync("responses", "order-Z", default))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task QueryAwaiting_matches_a_run_awaiting_a_channel_with_no_correlation()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("any", Bytes("a"), Suspended(channel: "events", correlationId: null), WorkflowEtag.None, default);

        List<WorkflowRunId> matched = await Collect(((IWorkflowWaitIndex)store).QueryAwaitingAsync("events", "incoming-99", default));
        matched.ShouldHaveSingleItem().ShouldBe(new WorkflowRunId("any"));
    }

    [TestMethod]
    public async Task Query_filters_by_status_and_workflow()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("s1", Bytes("a"), Suspended(dueAt: T0 + TimeSpan.FromMinutes(1)), WorkflowEtag.None, default);
        await store.SaveAsync("r1", Bytes("a"), Index(), WorkflowEtag.None, default);

        var index = (IWorkflowWaitIndex)store;
        WorkflowRunPage suspended = await index.QueryAsync(new WorkflowQuery(Status: WorkflowRunStatus.Suspended), default);
        suspended.Runs.ShouldHaveSingleItem();
        suspended.Runs[0].Id.ShouldBe(new WorkflowRunId("s1"));
        suspended.Runs[0].Index.Status.ShouldBe(WorkflowRunStatus.Suspended);

        WorkflowRunPage byWorkflow = await index.QueryAsync(new WorkflowQuery(WorkflowId: "wf"), default);
        byWorkflow.Runs.Count.ShouldBe(2);

        WorkflowRunPage byMissingWorkflow = await index.QueryAsync(new WorkflowQuery(WorkflowId: "nope"), default);
        byMissingWorkflow.Runs.ShouldBeEmpty();
    }

    private static WorkflowRunIndexEntry Index(WorkflowRunStatus status = WorkflowRunStatus.Running)
        => new("wf", status, T0, T0);

    private static WorkflowRunIndexEntry Suspended(DateTimeOffset? dueAt = null, string? channel = null, string? correlationId = null)
        => new("wf", WorkflowRunStatus.Suspended, T0, T0, dueAt, channel, correlationId);

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static async ValueTask<List<WorkflowRunId>> Collect(IAsyncEnumerable<WorkflowRunId> source)
    {
        var list = new List<WorkflowRunId>();
        await foreach (WorkflowRunId id in source)
        {
            list.Add(id);
        }

        return list;
    }

    private async ValueTask<IWorkflowStateStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IWorkflowStateStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}