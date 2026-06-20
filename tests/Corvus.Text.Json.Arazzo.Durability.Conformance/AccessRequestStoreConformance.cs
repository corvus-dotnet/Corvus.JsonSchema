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

        // By workflow.
        (await this.IdsAsync(store, new AccessRequestQuery(BaseWorkflowId: "nightly-reconcile"))).ShouldBe([aliceReconcile, bobReconcile], ignoreOrder: true);

        // By subject (alice's requests across workflows).
        (await this.IdsAsync(store, new AccessRequestQuery(SubjectClaimType: "sub", SubjectClaimValue: "alice"))).ShouldBe([aliceReconcile, aliceOnboard], ignoreOrder: true);

        // By workflow AND subject.
        (await this.IdsAsync(store, new AccessRequestQuery(BaseWorkflowId: "nightly-reconcile", SubjectClaimType: "sub", SubjectClaimValue: "bob"))).ShouldBe([bobReconcile]);

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