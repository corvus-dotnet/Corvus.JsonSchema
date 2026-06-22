// <copyright file="WorkflowStateStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
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
    public async Task QueryClaimable_returns_pending_and_lease_expired_running_for_hosted_workflows()
    {
        var clock = new TestClock(T0);
        IWorkflowStateStore store = await this.NewStoreAsync(clock);
        var index = (IWorkflowDispatchIndex)store;

        await store.SaveAsync("pending", Bytes("a"), Index(WorkflowRunStatus.Pending), WorkflowEtag.None, default);
        await store.SaveAsync("orphan", Bytes("a"), Index(WorkflowRunStatus.Running), WorkflowEtag.None, default);
        await store.SaveAsync("held", Bytes("a"), Index(WorkflowRunStatus.Running), WorkflowEtag.None, default);
        await store.AcquireLeaseAsync("held", "other-runner", TimeSpan.FromMinutes(5), default);
        await store.SaveAsync("done", Bytes("a"), Index(WorkflowRunStatus.Completed), WorkflowEtag.None, default);
        await store.SaveAsync("unhosted", Bytes("a"), new WorkflowRunIndexEntry("other-wf", WorkflowRunStatus.Pending, T0, T0), WorkflowEtag.None, default);

        // At T0 the held lease (expires at T0+5m) is still live, so that run is not claimable; the fresh
        // Pending run and the unleased Running orphan are. The completed and unhosted runs never are.
        List<string> claimable = (await Collect(index.QueryClaimableAsync(["wf"], T0, default))).Select(r => r.Value).ToList();
        claimable.ShouldContain("pending");
        claimable.ShouldContain("orphan");
        claimable.ShouldNotContain("held");
        claimable.ShouldNotContain("done");
        claimable.ShouldNotContain("unhosted");

        // Once the lease has expired, the previously-held Running run becomes a claimable orphan.
        List<string> afterExpiry = (await Collect(index.QueryClaimableAsync(["wf"], T0 + TimeSpan.FromMinutes(6), default))).Select(r => r.Value).ToList();
        afterExpiry.ShouldContain("held");
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

    [TestMethod]
    public async Task Query_filters_by_a_created_and_updated_time_window()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();

        // Three runs created an hour apart; each updated one minute after it was created.
        await store.SaveAsync("c0", Bytes("x"), At(T0, T0 + TimeSpan.FromMinutes(1)), WorkflowEtag.None, default);
        await store.SaveAsync("c1", Bytes("x"), At(T0 + TimeSpan.FromHours(1), T0 + TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1)), WorkflowEtag.None, default);
        await store.SaveAsync("c2", Bytes("x"), At(T0 + TimeSpan.FromHours(2), T0 + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1)), WorkflowEtag.None, default);

        var index = (IWorkflowWaitIndex)store;

        // CreatedAfter is inclusive: T0+1h keeps c1 and c2.
        WorkflowRunPage createdAfter = await index.QueryAsync(new WorkflowQuery(CreatedAfter: T0 + TimeSpan.FromHours(1)), default);
        createdAfter.Runs.Select(r => r.Id.Value).ShouldBe(["c1", "c2"]);

        // CreatedBefore is exclusive: T0+1h keeps only c0.
        WorkflowRunPage createdBefore = await index.QueryAsync(new WorkflowQuery(CreatedBefore: T0 + TimeSpan.FromHours(1)), default);
        createdBefore.Runs.ShouldHaveSingleItem().Id.Value.ShouldBe("c0");

        // A half-open window [T0+1h, T0+2h) keeps only c1.
        WorkflowRunPage window = await index.QueryAsync(
            new WorkflowQuery(CreatedAfter: T0 + TimeSpan.FromHours(1), CreatedBefore: T0 + TimeSpan.FromHours(2)),
            default);
        window.Runs.ShouldHaveSingleItem().Id.Value.ShouldBe("c1");

        // The updated-window filters read the updated timestamp (c2 was updated last).
        WorkflowRunPage updatedAfter = await index.QueryAsync(new WorkflowQuery(UpdatedAfter: T0 + TimeSpan.FromHours(2)), default);
        updatedAfter.Runs.ShouldHaveSingleItem().Id.Value.ShouldBe("c2");
    }

    [TestMethod]
    public async Task Query_filters_by_tags_and_correlation_id()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        await store.SaveAsync("r-a", Bytes("x"), Tagged("trace-1", "tenant-42", "priority"), WorkflowEtag.None, default);
        await store.SaveAsync("r-b", Bytes("x"), Tagged("trace-2", "tenant-42"), WorkflowEtag.None, default);
        await store.SaveAsync("r-c", Bytes("x"), Tagged(null), WorkflowEtag.None, default);

        // A LIKE-metacharacter tag must match literally, not as a wildcard (guards the SQL stores' escaping).
        await store.SaveAsync("r-d", Bytes("x"), Tagged(null, "a_b"), WorkflowEtag.None, default);
        await store.SaveAsync("r-e", Bytes("x"), Tagged(null, "axb"), WorkflowEtag.None, default);

        var index = (IWorkflowWaitIndex)store;

        // A single tag matches every run carrying it (ascending id order).
        WorkflowRunPage byTag = await index.QueryAsync(new WorkflowQuery(Tags: TagSet.FromTags(["tenant-42"])), default);
        byTag.Runs.Select(r => r.Id.Value).ShouldBe(["r-a", "r-b"]);

        // Multiple tags are AND-matched (contains all).
        WorkflowRunPage byBoth = await index.QueryAsync(new WorkflowQuery(Tags: TagSet.FromTags(["tenant-42", "priority"])), default);
        byBoth.Runs.ShouldHaveSingleItem().Id.Value.ShouldBe("r-a");

        // The underscore is a literal, not a single-char wildcard: "a_b" must not match "axb".
        WorkflowRunPage literalUnderscore = await index.QueryAsync(new WorkflowQuery(Tags: TagSet.FromTags(["a_b"])), default);
        literalUnderscore.Runs.ShouldHaveSingleItem().Id.Value.ShouldBe("r-d");

        // Correlation id is an exact match.
        WorkflowRunPage byCorrelation = await index.QueryAsync(new WorkflowQuery(CorrelationId: "trace-2"), default);
        byCorrelation.Runs.ShouldHaveSingleItem().Id.Value.ShouldBe("r-b");

        // The projected entry round-trips both fields back out of the store. (A positional ctor arg is used
        // so the Limit default applies — `new WorkflowQuery()` is the struct's parameterless ctor, Limit 0.)
        WorkflowRunPage all = await index.QueryAsync(new WorkflowQuery(Limit: 100), default);
        WorkflowRunListing a = all.Runs.Single(r => r.Id.Value == "r-a");
        a.Index.CorrelationId.ShouldBe("trace-1");
        a.Index.Tags.IsEmpty.ShouldBeFalse();
        a.Index.Tags.ToList().ShouldBe(["tenant-42", "priority"], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Query_pages_through_results_with_a_continuation_token()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        string[] ids = ["run-01", "run-02", "run-03", "run-04", "run-05"];
        foreach (string id in ids)
        {
            await store.SaveAsync(id, Bytes("x"), Index(), WorkflowEtag.None, default);
        }

        var index = (IWorkflowWaitIndex)store;
        var collected = new List<string>();

        // Round-trip the token through the JsonString seam exactly as the HTTP layer does: the store emits it as UTF-8,
        // the next request carries it as a JSON string. The page owns the token buffer (freed on dispose), so copy it
        // out per page.
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using WorkflowRunPage page = await index.QueryAsync(new WorkflowQuery(Limit: 2, ContinuationToken: tokenDoc?.RootElement ?? default), default);
            page.Runs.Count.ShouldBeLessThanOrEqualTo(2);
            foreach (WorkflowRunListing listing in page.Runs)
            {
                collected.Add(listing.Id.Value);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            (++pages).ShouldBeLessThanOrEqualTo(10); // guard against a non-terminating cursor
        }
        while (token is not null);

        // Every run returned exactly once, in ascending id order, and the last page cleared the token.
        collected.ShouldBe(ids);
    }

    [TestMethod]
    public async Task Query_applies_a_row_security_reach_filter_matching_the_evaluator()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        if (store is not ISupportsRowSecurityFilter)
        {
            Assert.Inconclusive("This store does not yet push the row-security reach filter down (§14.4).");
            return;
        }

        // Runs spanning single-value, multi-value and no-tag shapes.
        (string Id, SecurityTag[] Tags)[] rows =
        [
            ("run-a", [new("tenant", "acme"), new("team", "payments")]),
            ("run-b", [new("tenant", "acme"), new("team", "hr")]),
            ("run-c", [new("tenant", "globex"), new("team", "payments")]),
            ("run-d", []),
            ("run-e", [new("tenant", "acme"), new("tenant", "beta")]),
        ];
        foreach ((string id, SecurityTag[] tags) in rows)
        {
            await store.SaveAsync(id, Bytes("x"), Secured(tags), WorkflowEtag.None, default);
        }

        var index = (IWorkflowWaitIndex)store;
        var claims = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["tenant"] = ["acme"],
            ["both"] = ["acme", "globex"],
            ["team"] = ["payments", "hr"],
        };

        // Every operator/operand shape the translator handles; each cross-checked against the in-memory evaluator.
        string[] ruleShapes =
        [
            "tenant == $claim.tenant",
            "tenant == 'acme'",
            "tenant != $claim.tenant",
            "tenant",
            "tenant && team == 'payments'",
            "tenant == 'acme' || team == 'hr'",
            "!(tenant == 'globex')",
            "tenant == $claim.missing",
            "'a' == 'a'",
            "team == team",
            "tenant == $claim.both",
            "$claims.intersects",
            "$claims.superset",
            "!$claims.superset",
            "$claims.intersects && tenant == $claim.tenant",
            "$claims.superset || team == 'payments'",
            "!($claims.intersects)",
        ];

        foreach (string ruleText in ruleShapes)
        {
            var filter = new SecurityFilter([SecurityRule.Compile(ruleText)], claims);
            WorkflowRunPage page = await index.QueryAsync(new WorkflowQuery(Limit: 1000, Security: filter), default);

            List<string> actual = page.Runs.Select(r => r.Id.Value).OrderBy(x => x, StringComparer.Ordinal).ToList();
            List<string> expected = rows.Where(r => filter.IsSatisfiedBy(r.Tags)).Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            actual.ShouldBe(expected, $"rule: {ruleText}");
        }
    }

    [TestMethod]
    public async Task Deleting_a_run_removes_its_security_tags()
    {
        IWorkflowStateStore store = await this.NewStoreAsync();
        if (store is not ISupportsRowSecurityFilter)
        {
            Assert.Inconclusive("This store does not yet push the row-security reach filter down (§14.4).");
            return;
        }

        var index = (IWorkflowWaitIndex)store;
        var filter = new SecurityFilter([SecurityRule.Compile("tenant == 'acme'")], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

        await store.SaveAsync("run-x", Bytes("x"), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: filter), default)).Runs.Count.ShouldBe(1);

        await store.DeleteAsync("run-x", default);

        // The tags are gone too: a run later re-created with the same id must not inherit the deleted run's tags.
        await store.SaveAsync("run-x", Bytes("x"), Index(), WorkflowEtag.None, default);
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: filter), default)).Runs.ShouldBeEmpty();
    }

    private static WorkflowRunIndexEntry Secured(SecurityTag[] securityTags)
        => new("wf", WorkflowRunStatus.Running, T0, T0, SecurityTags: SecurityTagSet.FromTags(securityTags));

    private static WorkflowRunIndexEntry Index(WorkflowRunStatus status = WorkflowRunStatus.Running)
        => new("wf", status, T0, T0);

    private static WorkflowRunIndexEntry At(DateTimeOffset createdAt, DateTimeOffset updatedAt)
        => new("wf", WorkflowRunStatus.Running, createdAt, updatedAt);

    private static WorkflowRunIndexEntry Tagged(string? correlationId, params string[] tags)
        => new("wf", WorkflowRunStatus.Running, T0, T0, CorrelationId: correlationId, Tags: TagSet.FromTags(tags));

    private static WorkflowRunIndexEntry Suspended(DateTimeOffset? dueAt = null, string? channel = null, string? correlationId = null)
        => new("wf", WorkflowRunStatus.Suspended, T0, T0, dueAt, channel, correlationId);

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    // Wraps an opaque page token's UTF-8 as the JSON string value a request carries it as — the conformance feeds a
    // previous page's NextPageToken (the store's emitted bytes) back through the JsonString seam, mirroring HTTP.
    private static ParsedJsonDocument<JsonString> AsPageToken(ReadOnlySpan<byte> tokenUtf8)
    {
        byte[] quoted = new byte[tokenUtf8.Length + 2];
        quoted[0] = (byte)'"';
        tokenUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(quoted);
    }

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