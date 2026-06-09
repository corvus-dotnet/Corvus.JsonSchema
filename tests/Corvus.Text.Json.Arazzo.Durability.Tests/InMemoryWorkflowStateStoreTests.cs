// <copyright file="InMemoryWorkflowStateStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the in-memory reference store: optimistic-concurrency save/load, the advisory single-owner
/// lease (contention, release, TTL expiry), and delete.
/// </summary>
[TestClass]
public sealed class InMemoryWorkflowStateStoreTests
{
    private static readonly WorkflowRunIndexEntry Index = new(
        "wf",
        WorkflowRunStatus.Running,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task Save_creates_then_load_returns_bytes_and_etag()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";
        byte[] payload = Encoding.UTF8.GetBytes("""{"v":1}""");

        WorkflowEtag etag = await store.SaveAsync(id, payload, Index, WorkflowEtag.None, default);

        etag.IsNone.ShouldBeFalse();
        WorkflowCheckpoint? loaded = await store.LoadAsync(id, default);
        loaded.ShouldNotBeNull();
        loaded.Value.Etag.ShouldBe(etag);
        Encoding.UTF8.GetString(loaded.Value.Utf8.Span).ShouldBe("""{"v":1}""");
    }

    [TestMethod]
    public async Task Load_of_unknown_run_returns_null()
    {
        var store = new InMemoryWorkflowStateStore();

        (await store.LoadAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Save_with_None_when_run_exists_conflicts()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";
        await store.SaveAsync(id, "a"u8.ToArray(), Index, WorkflowEtag.None, default);

        WorkflowConflictException ex = await Should.ThrowAsync<WorkflowConflictException>(
            async () => await store.SaveAsync(id, "b"u8.ToArray(), Index, WorkflowEtag.None, default));

        ex.RunId.ShouldBe(id);
    }

    [TestMethod]
    public async Task Save_with_current_etag_advances_the_etag()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";
        WorkflowEtag first = await store.SaveAsync(id, "a"u8.ToArray(), Index, WorkflowEtag.None, default);

        WorkflowEtag second = await store.SaveAsync(id, "b"u8.ToArray(), Index, first, default);

        second.ShouldNotBe(first);
        WorkflowCheckpoint? loaded = await store.LoadAsync(id, default);
        Encoding.UTF8.GetString(loaded!.Value.Utf8.Span).ShouldBe("b");
    }

    [TestMethod]
    public async Task Save_with_stale_etag_conflicts()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";
        WorkflowEtag first = await store.SaveAsync(id, "a"u8.ToArray(), Index, WorkflowEtag.None, default);
        await store.SaveAsync(id, "b"u8.ToArray(), Index, first, default);

        await Should.ThrowAsync<WorkflowConflictException>(
            async () => await store.SaveAsync(id, "c"u8.ToArray(), Index, first, default));
    }

    [TestMethod]
    public async Task Save_with_expected_etag_when_run_absent_conflicts()
    {
        var store = new InMemoryWorkflowStateStore();

        await Should.ThrowAsync<WorkflowConflictException>(
            async () => await store.SaveAsync("ghost", "a"u8.ToArray(), Index, new WorkflowEtag("7"), default));
    }

    [TestMethod]
    public async Task Delete_removes_the_run()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";
        await store.SaveAsync(id, "a"u8.ToArray(), Index, WorkflowEtag.None, default);

        await store.DeleteAsync(id, default);

        (await store.LoadAsync(id, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Delete_of_unknown_run_is_a_no_op()
    {
        var store = new InMemoryWorkflowStateStore();

        await Should.NotThrowAsync(async () => await store.DeleteAsync("missing", default));
    }

    [TestMethod]
    public async Task Lease_is_held_against_a_second_owner_until_released()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";

        WorkflowLease? a = await store.AcquireLeaseAsync(id, "worker-a", TimeSpan.FromMinutes(1), default);
        WorkflowLease? b = await store.AcquireLeaseAsync(id, "worker-b", TimeSpan.FromMinutes(1), default);

        a.ShouldNotBeNull();
        b.ShouldBeNull();

        await store.ReleaseLeaseAsync(a.Value, default);
        WorkflowLease? bAfterRelease = await store.AcquireLeaseAsync(id, "worker-b", TimeSpan.FromMinutes(1), default);
        bAfterRelease.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Lease_is_reacquirable_after_it_expires()
    {
        var time = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowStateStore(time);
        WorkflowRunId id = "run-1";

        await store.AcquireLeaseAsync(id, "worker-a", TimeSpan.FromSeconds(30), default);
        time.Advance(TimeSpan.FromSeconds(31));

        WorkflowLease? b = await store.AcquireLeaseAsync(id, "worker-b", TimeSpan.FromSeconds(30), default);
        b.ShouldNotBeNull();
        b.Value.Owner.ShouldBe("worker-b");
    }

    [TestMethod]
    public async Task The_same_owner_can_renew_its_lease()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";

        await store.AcquireLeaseAsync(id, "worker-a", TimeSpan.FromMinutes(1), default);
        WorkflowLease? renewed = await store.AcquireLeaseAsync(id, "worker-a", TimeSpan.FromMinutes(1), default);

        renewed.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Releasing_a_superseded_lease_is_a_no_op()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "run-1";
        WorkflowLease? stale = await store.AcquireLeaseAsync(id, "worker-a", TimeSpan.FromMinutes(1), default);
        await store.ReleaseLeaseAsync(stale!.Value, default);
        WorkflowLease? current = await store.AcquireLeaseAsync(id, "worker-b", TimeSpan.FromMinutes(1), default);

        // Releasing the stale (already-released, now worker-b's) lease must not free worker-b's hold.
        await store.ReleaseLeaseAsync(stale.Value, default);

        WorkflowLease? contended = await store.AcquireLeaseAsync(id, "worker-c", TimeSpan.FromMinutes(1), default);
        contended.ShouldBeNull();
        current.ShouldNotBeNull();
    }
}