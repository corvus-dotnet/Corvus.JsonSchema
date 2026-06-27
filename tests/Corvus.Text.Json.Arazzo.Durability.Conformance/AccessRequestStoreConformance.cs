// <copyright file="AccessRequestStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IAccessRequestStore"/> must satisfy (design §16.5): create/get/list with
/// filtering and oldest-first ordering, the terminal decision transition recording the grant and expiry, and
/// optimistic concurrency via etag (so two administrators cannot double-decide). A backend's test project derives a
/// concrete <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>; the in-memory store is
/// the reference implementation and runs the same suite.
/// </summary>
public abstract class AccessRequestStoreConformance
{
    private static readonly DateTimeOffset GrantExpiry = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IAccessRequestStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task A_request_round_trips_through_create_get_and_list()
    {
        IAccessRequestStore store = await this.NewStoreAsync();
        string id;
        using (ParsedJsonDocument<AccessRequest> created = await CreateRequestAsync(store, AccessRequest.Draft("nightly-reconcile", ["runs:write", "runs:read"], "sub", "alice", requesterLabel: "Alice", reason: "on-call", requestedDurationSeconds: 3600),
            "alice",
            default))
        {
            id = created.RootElement.IdValue;
            created.RootElement.BaseWorkflowIdValue.ShouldBe("nightly-reconcile");
            created.RootElement.RequestedScopesArray().ShouldBe(["runs:write", "runs:read"]);
            created.RootElement.SubjectClaimTypeValue.ShouldBe("sub");
            created.RootElement.SubjectClaimValueValue.ShouldBe("alice");
            created.RootElement.RequesterLabelOrNull.ShouldBe("Alice");
            created.RootElement.ReasonOrNull.ShouldBe("on-call");
            created.RootElement.RequestedDurationSecondsOrNull.ShouldBe(3600);
            created.RootElement.StatusValue.ShouldBe("Pending");
            created.RootElement.CreatedByValue.ShouldBe("alice");
            created.RootElement.EtagValue.IsNone.ShouldBeFalse();
            created.RootElement.DecidedByOrNull.ShouldBeNull();
            created.RootElement.GrantedUntilValue.ShouldBeNull();
        }

        using (ParsedJsonDocument<AccessRequest>? fetched = await store.GetAsync(id, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.SubjectClaimValueValue.ShouldBe("alice");
        }

        using (PooledDocumentList<AccessRequest> list = await store.ListAsync(default, default))
        {
            list.Select(r => r.IdValue).ShouldBe([id]);
        }

        (await store.GetAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Listing_filters_by_status_workflow_and_subject()
    {
        IAccessRequestStore store = await this.NewStoreAsync();
        string aliceReconcile = await this.CreateAsync(store, "nightly-reconcile", "alice");
        string bobReconcile = await this.CreateAsync(store, "nightly-reconcile", "bob");
        string aliceOnboard = await this.CreateAsync(store, "onboard-customer", "alice");

        // By workflow (baseWorkflowId carried as its request JSON value, reified at the store's own leaf).
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> bw = AsJsonString("nightly-reconcile"))
        {
            (await this.IdsAsync(store, new AccessRequestQuery(BaseWorkflowId: bw.RootElement))).ShouldBe([aliceReconcile, bobReconcile], ignoreOrder: true);
        }

        // By subject (alice's requests across workflows).
        (await this.IdsAsync(store, new AccessRequestQuery(SubjectClaimType: "sub", SubjectClaimValue: "alice"))).ShouldBe([aliceReconcile, aliceOnboard], ignoreOrder: true);

        // By workflow AND subject.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> bw = AsJsonString("nightly-reconcile"))
        {
            (await this.IdsAsync(store, new AccessRequestQuery(BaseWorkflowId: bw.RootElement, SubjectClaimType: "sub", SubjectClaimValue: "bob"))).ShouldBe([bobReconcile]);
        }

        // By status: all are Pending until decided; deny one and filter.
        await store.DecideAsync(bobReconcile, new AccessRequestDecision(AccessRequestStatus.Denied), WorkflowEtag.None, "admin", default);
        (await this.IdsAsync(store, new AccessRequestQuery(Status: AccessRequestStatus.Pending))).ShouldBe([aliceReconcile, aliceOnboard], ignoreOrder: true);
        (await this.IdsAsync(store, new AccessRequestQuery(Status: AccessRequestStatus.Denied))).ShouldBe([bobReconcile]);
    }

    [TestMethod]
    public async Task Requests_list_oldest_first()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IAccessRequestStore store = await this.NewStoreAsync(clock);

        string first = await this.CreateAsync(store, "w", "alice");
        clock.Advance(TimeSpan.FromSeconds(1));
        string second = await this.CreateAsync(store, "w", "bob");
        clock.Advance(TimeSpan.FromSeconds(1));
        string third = await this.CreateAsync(store, "w", "carol");

        (await this.IdsAsync(store, default)).ShouldBe([first, second, third]);
    }

    [TestMethod]
    public async Task Approving_records_the_decision_grant_and_expiry()
    {
        IAccessRequestStore store = await this.NewStoreAsync();
        string id = await this.CreateAsync(store, "nightly-reconcile", "alice");

        using ParsedJsonDocument<AccessRequest>? decided = await store.DecideAsync(
            id,
            new AccessRequestDecision(AccessRequestStatus.Approved, DecisionReason: "looks good", GrantedBindingId: "bnd-42", GrantedUntil: GrantExpiry),
            WorkflowEtag.None,
            "boss",
            default);

        decided.ShouldNotBeNull();
        decided!.RootElement.StatusValue.ShouldBe("Approved");
        decided.RootElement.DecidedByOrNull.ShouldBe("boss");
        decided.RootElement.DecisionReasonOrNull.ShouldBe("looks good");
        decided.RootElement.GrantedBindingIdOrNull.ShouldBe("bnd-42");
        decided.RootElement.GrantedUntilValue.ShouldBe(GrantExpiry);
        decided.RootElement.DecidedAtValue.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Deciding_a_missing_request_returns_null()
    {
        IAccessRequestStore store = await this.NewStoreAsync();
        (await store.DecideAsync("missing", new AccessRequestDecision(AccessRequestStatus.Denied), WorkflowEtag.None, "admin", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_decide_conflicts_so_two_admins_cannot_double_decide()
    {
        IAccessRequestStore store = await this.NewStoreAsync();
        WorkflowEtag created;
        string id;
        using (ParsedJsonDocument<AccessRequest> request = await CreateRequestAsync(store, AccessRequest.Draft("w", ["runs:write"], "sub", "alice"),
            "alice",
            default))
        {
            id = request.RootElement.IdValue;
            created = request.RootElement.EtagValue;
        }

        // The first administrator approves (etag advances).
        using (await store.DecideAsync(id, new AccessRequestDecision(AccessRequestStatus.Approved, GrantedBindingId: "bnd-1"), created, "first", default))
        {
        }

        // A second administrator racing on the original etag conflicts.
        await Should.ThrowAsync<AccessRequestConflictException>(async () =>
            await store.DecideAsync(id, new AccessRequestDecision(AccessRequestStatus.Denied), created, "second", default));

        // WorkflowEtag.None applies unconditionally (administrator override).
        using ParsedJsonDocument<AccessRequest>? overridden = await store.DecideAsync(id, new AccessRequestDecision(AccessRequestStatus.Withdrawn), WorkflowEtag.None, "ops", default);
        overridden!.RootElement.StatusValue.ShouldBe("Withdrawn");
    }

    [TestMethod]
    public async Task Listing_keyset_pages_oldest_first_without_gaps_or_duplicates()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IAccessRequestStore store = await this.NewStoreAsync(clock);

        // Distinct createdAt per request (the clock steps), so (createdAt, id) order is creation order.
        var expected = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            expected.Add(await this.CreateAsync(store, "w", $"user-{i}"));
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Walk every page via the continuation token with a small limit; collect the ids in page order. The token is
        // round-tripped through the JsonString seam exactly as the HTTP layer does.
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using AccessRequestPage page = await store.ListAsync(default, 3, tokenDoc?.RootElement ?? default, default);
            page.Requests.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (AccessRequest request in page.Requests)
            {
                seen.Add(request.IdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 8 items, 3 per page → 3 pages; oldest-first, no duplicates or gaps across boundaries.
        pages.ShouldBe(3);
        seen.ShouldBe(expected);

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> badToken = AsJsonString("this~is~not~a~token"u8);
            using AccessRequestPage bad = await store.ListAsync(default, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Listing_filters_by_the_administered_workflow_set_for_the_approver_inbox()
    {
        IAccessRequestStore store = await this.NewStoreAsync();
        string aliceReconcile = await this.CreateAsync(store, "nightly-reconcile", "alice");
        string bobReconcile = await this.CreateAsync(store, "nightly-reconcile", "bob");
        string aliceOnboard = await this.CreateAsync(store, "onboard-customer", "alice");
        string carolExport = await this.CreateAsync(store, "daily-export", "carol");

        // The approver inbox: every request targeting a workflow in the administered set (reconcile + export), across all
        // subjects — onboard-customer is outside the set and never surfaced.
        (await this.IdsAsync(store, new AccessRequestQuery(AdministeredBaseWorkflowIds: ["nightly-reconcile", "daily-export"])))
            .ShouldBe([aliceReconcile, bobReconcile, carolExport], ignoreOrder: true);

        // Combined with a status filter (the inbox's optional status filter).
        await store.DecideAsync(bobReconcile, new AccessRequestDecision(AccessRequestStatus.Denied), WorkflowEtag.None, "admin", default);
        (await this.IdsAsync(store, new AccessRequestQuery(Status: AccessRequestStatus.Pending, AdministeredBaseWorkflowIds: ["nightly-reconcile", "daily-export"])))
            .ShouldBe([aliceReconcile, carolExport], ignoreOrder: true);

        // A single-workflow administered set is exactly that workflow's queue.
        (await this.IdsAsync(store, new AccessRequestQuery(AdministeredBaseWorkflowIds: ["onboard-customer"]))).ShouldBe([aliceOnboard]);
    }

    [TestMethod]
    public async Task The_approver_inbox_keyset_pages_oldest_first_across_the_administered_set()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IAccessRequestStore store = await this.NewStoreAsync(clock);

        // Interleave requests across two administered workflows and one the caller does NOT administer; the inbox must
        // page the administered two oldest-first and never surface the third.
        string[] workflows = ["alpha", "beta", "gamma"]; // gamma is not administered
        var expected = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            string workflow = workflows[i % 3];
            string id = await this.CreateAsync(store, workflow, $"user-{i}");
            if (workflow != "gamma")
            {
                expected.Add(id);
            }

            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var query = new AccessRequestQuery(AdministeredBaseWorkflowIds: ["alpha", "beta"]);
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using AccessRequestPage page = await store.ListAsync(query, 2, tokenDoc?.RootElement ?? default, default);
            page.Requests.Count.ShouldBeLessThanOrEqualTo(2);
            foreach (AccessRequest request in page.Requests)
            {
                seen.Add(request.IdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 6 administered requests, 2 per page → 3 pages; oldest-first, no duplicates, gamma excluded.
        pages.ShouldBe(3);
        seen.ShouldBe(expected);
    }

    // Wraps a value as the JSON string a request carries it as — the conformance carries a filter value as the request
    // JSON the store reifies at its own leaf, and round-trips a page token (the store's emitted bytes) the same way.
    private static ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> AsJsonString(ReadOnlySpan<byte> valueUtf8)
    {
        byte[] quoted = new byte[valueUtf8.Length + 2];
        quoted[0] = (byte)'"';
        valueUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>.Parse(quoted);
    }

    private static ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> AsJsonString(string value)
        => AsJsonString(System.Text.Encoding.UTF8.GetBytes(value));

    private async ValueTask<IAccessRequestStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IAccessRequestStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }

    private async ValueTask<string> CreateAsync(IAccessRequestStore store, string baseWorkflowId, string subject)
    {
        using ParsedJsonDocument<AccessRequest> created = await CreateRequestAsync(store, AccessRequest.Draft(baseWorkflowId, ["runs:write"], "sub", subject),
            subject,
            default);
        return created.RootElement.IdValue;
    }

    // Creates the (pooled, disposable) draft request, disposing the draft once the store has read it; the created
    // document is returned for the caller to assert on and dispose.
    private static async Task<ParsedJsonDocument<AccessRequest>> CreateRequestAsync(IAccessRequestStore store, ParsedJsonDocument<AccessRequest> draft, string actor, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await store.CreateAsync(draft.RootElement, actor, cancellationToken);
        }
    }

    private async ValueTask<List<string>> IdsAsync(IAccessRequestStore store, AccessRequestQuery query)
    {
        using PooledDocumentList<AccessRequest> list = await store.ListAsync(query, default);
        return list.Select(r => r.IdValue).ToList();
    }

    // A controllable clock so the oldest-first ordering test is deterministic even where the id tiebreak is a GUID.
    private sealed class StepClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}