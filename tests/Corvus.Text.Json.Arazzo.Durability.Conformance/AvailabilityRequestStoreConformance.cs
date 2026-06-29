// <copyright file="AvailabilityRequestStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IAvailabilityRequestStore"/> must satisfy (design §7.8): create/get/list with
/// filtering (status / environment / requester) and oldest-first ordering, the terminal decision transition, optimistic
/// concurrency via etag (so two administrators cannot double-decide), keyset paging, and the approver inbox filtered by the
/// administered-environment set. A backend's test project derives a concrete <see cref="TestClassAttribute"/> and implements
/// <see cref="CreateStoreAsync"/>; the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class AvailabilityRequestStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider);

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
        IAvailabilityRequestStore store = await this.NewStoreAsync();
        string id;
        using (ParsedJsonDocument<AvailabilityRequest> created = await CreateRequestAsync(store, AvailabilityRequest.Draft("checkout", 3, "production", "please promote"), "alice", default))
        {
            id = created.RootElement.IdValue;
            created.RootElement.BaseWorkflowIdValue.ShouldBe("checkout");
            created.RootElement.VersionNumberValue.ShouldBe(3);
            created.RootElement.EnvironmentValue.ShouldBe("production");
            created.RootElement.ReasonOrNull.ShouldBe("please promote");
            created.RootElement.StatusValue.ShouldBe("Pending");
            created.RootElement.CreatedByValue.ShouldBe("alice");
            created.RootElement.EtagValue.IsNone.ShouldBeFalse();
            created.RootElement.DecidedByOrNull.ShouldBeNull();
        }

        using (ParsedJsonDocument<AvailabilityRequest>? fetched = await store.GetAsync(id, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.EnvironmentValue.ShouldBe("production");
        }

        using (PooledDocumentList<AvailabilityRequest> list = await store.ListAsync(default, default))
        {
            list.Select(r => r.IdValue).ShouldBe([id]);
        }

        (await store.GetAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Listing_filters_by_status_environment_and_requester()
    {
        IAvailabilityRequestStore store = await this.NewStoreAsync();
        string aliceProd = await this.CreateAsync(store, "checkout", 1, "production", "alice");
        string bobProd = await this.CreateAsync(store, "checkout", 2, "production", "bob");
        string aliceStaging = await this.CreateAsync(store, "checkout", 1, "staging", "alice");

        // By environment (the environment queue).
        (await this.IdsAsync(store, new AvailabilityRequestQuery(Environment: "production"))).ShouldBe([aliceProd, bobProd], ignoreOrder: true);

        // By requester (the "mine" view — alice's requests across environments).
        (await this.IdsAsync(store, new AvailabilityRequestQuery(CreatedBy: "alice"))).ShouldBe([aliceProd, aliceStaging], ignoreOrder: true);

        // By environment AND requester.
        (await this.IdsAsync(store, new AvailabilityRequestQuery(Environment: "production", CreatedBy: "bob"))).ShouldBe([bobProd]);

        // By status: all are Pending until decided; deny one and filter.
        await store.DecideAsync(bobProd, new AvailabilityRequestDecision(AvailabilityRequestStatus.Denied), WorkflowEtag.None, "admin", default);
        (await this.IdsAsync(store, new AvailabilityRequestQuery(Status: AvailabilityRequestStatus.Pending))).ShouldBe([aliceProd, aliceStaging], ignoreOrder: true);
        (await this.IdsAsync(store, new AvailabilityRequestQuery(Status: AvailabilityRequestStatus.Denied))).ShouldBe([bobProd]);
    }

    [TestMethod]
    public async Task Requests_list_oldest_first()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IAvailabilityRequestStore store = await this.NewStoreAsync(clock);

        string first = await this.CreateAsync(store, "w", 1, "production", "alice");
        clock.Advance(TimeSpan.FromSeconds(1));
        string second = await this.CreateAsync(store, "w", 2, "production", "bob");
        clock.Advance(TimeSpan.FromSeconds(1));
        string third = await this.CreateAsync(store, "w", 3, "production", "carol");

        (await this.IdsAsync(store, default)).ShouldBe([first, second, third]);
    }

    [TestMethod]
    public async Task Approving_records_the_decision()
    {
        IAvailabilityRequestStore store = await this.NewStoreAsync();
        string id = await this.CreateAsync(store, "checkout", 3, "production", "alice");

        using ParsedJsonDocument<AvailabilityRequest>? decided = await store.DecideAsync(
            id,
            new AvailabilityRequestDecision(AvailabilityRequestStatus.Approved, DecisionReason: "looks good"),
            WorkflowEtag.None,
            "boss",
            default);

        decided.ShouldNotBeNull();
        decided!.RootElement.StatusValue.ShouldBe("Approved");
        decided.RootElement.DecidedByOrNull.ShouldBe("boss");
        decided.RootElement.DecisionReasonOrNull.ShouldBe("looks good");
        decided.RootElement.DecidedAtValue.ShouldNotBeNull();

        // The content fields carry through the decision unchanged.
        decided.RootElement.BaseWorkflowIdValue.ShouldBe("checkout");
        decided.RootElement.VersionNumberValue.ShouldBe(3);
        decided.RootElement.EnvironmentValue.ShouldBe("production");
    }

    [TestMethod]
    public async Task Deciding_a_missing_request_returns_null()
    {
        IAvailabilityRequestStore store = await this.NewStoreAsync();
        (await store.DecideAsync("missing", new AvailabilityRequestDecision(AvailabilityRequestStatus.Denied), WorkflowEtag.None, "admin", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_decide_conflicts_so_two_admins_cannot_double_decide()
    {
        IAvailabilityRequestStore store = await this.NewStoreAsync();
        WorkflowEtag created;
        string id;
        using (ParsedJsonDocument<AvailabilityRequest> request = await CreateRequestAsync(store, AvailabilityRequest.Draft("w", 1, "production"), "alice", default))
        {
            id = request.RootElement.IdValue;
            created = request.RootElement.EtagValue;
        }

        // The first administrator approves (etag advances).
        using (await store.DecideAsync(id, new AvailabilityRequestDecision(AvailabilityRequestStatus.Approved), created, "first", default))
        {
        }

        // A second administrator racing on the original etag conflicts.
        await Should.ThrowAsync<AvailabilityRequestConflictException>(async () =>
            await store.DecideAsync(id, new AvailabilityRequestDecision(AvailabilityRequestStatus.Denied), created, "second", default));

        // WorkflowEtag.None applies unconditionally (administrator override).
        using ParsedJsonDocument<AvailabilityRequest>? overridden = await store.DecideAsync(id, new AvailabilityRequestDecision(AvailabilityRequestStatus.Withdrawn), WorkflowEtag.None, "ops", default);
        overridden!.RootElement.StatusValue.ShouldBe("Withdrawn");
    }

    [TestMethod]
    public async Task Listing_keyset_pages_oldest_first_without_gaps_or_duplicates()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IAvailabilityRequestStore store = await this.NewStoreAsync(clock);

        var expected = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            expected.Add(await this.CreateAsync(store, "w", i + 1, "production", $"user-{i}"));
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using AvailabilityRequestPage page = await store.ListAsync(default, 3, tokenDoc?.RootElement ?? default, default);
            page.Requests.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (AvailabilityRequest request in page.Requests)
            {
                seen.Add(request.IdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 8 items, 3 per page
        seen.ShouldBe(expected);

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<JsonString> badToken = AsPageToken("this~is~not~a~token"u8);
            using AvailabilityRequestPage bad = await store.ListAsync(default, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Listing_filters_by_the_administered_environment_set_for_the_approver_inbox()
    {
        IAvailabilityRequestStore store = await this.NewStoreAsync();
        string aliceProd = await this.CreateAsync(store, "checkout", 1, "production", "alice");
        string bobProd = await this.CreateAsync(store, "billing", 2, "production", "bob");
        string aliceStaging = await this.CreateAsync(store, "checkout", 1, "staging", "alice");
        string carolQa = await this.CreateAsync(store, "audit", 1, "qa", "carol");

        // The approver inbox: every request targeting an environment in the administered set (production + qa), across all
        // requesters — staging is outside the set and never surfaced.
        (await this.IdsAsync(store, new AvailabilityRequestQuery(AdministeredEnvironments: ["production", "qa"])))
            .ShouldBe([aliceProd, bobProd, carolQa], ignoreOrder: true);

        // Combined with a status filter (the inbox's optional status filter).
        await store.DecideAsync(bobProd, new AvailabilityRequestDecision(AvailabilityRequestStatus.Denied), WorkflowEtag.None, "admin", default);
        (await this.IdsAsync(store, new AvailabilityRequestQuery(Status: AvailabilityRequestStatus.Pending, AdministeredEnvironments: ["production", "qa"])))
            .ShouldBe([aliceProd, carolQa], ignoreOrder: true);

        // A single-environment administered set is exactly that environment's queue.
        (await this.IdsAsync(store, new AvailabilityRequestQuery(AdministeredEnvironments: ["staging"]))).ShouldBe([aliceStaging]);
    }

    [TestMethod]
    public async Task The_approver_inbox_keyset_pages_oldest_first_across_the_administered_set()
    {
        var clock = new StepClock(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        IAvailabilityRequestStore store = await this.NewStoreAsync(clock);

        // Interleave requests across two administered environments and one the caller does NOT administer; the inbox must
        // page the administered two oldest-first and never surface the third.
        string[] environments = ["alpha", "beta", "gamma"]; // gamma is not administered
        var expected = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            string environment = environments[i % 3];
            string id = await this.CreateAsync(store, "w", i + 1, environment, $"user-{i}");
            if (environment != "gamma")
            {
                expected.Add(id);
            }

            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var query = new AvailabilityRequestQuery(AdministeredEnvironments: ["alpha", "beta"]);
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using AvailabilityRequestPage page = await store.ListAsync(query, 2, tokenDoc?.RootElement ?? default, default);
            page.Requests.Count.ShouldBeLessThanOrEqualTo(2);
            foreach (AvailabilityRequest request in page.Requests)
            {
                seen.Add(request.IdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 6 administered requests, 2 per page
        seen.ShouldBe(expected);
    }

    // Wraps an opaque page token's UTF-8 as the JSON string value a request carries it as (mirroring HTTP).
    private static ParsedJsonDocument<JsonString> AsPageToken(ReadOnlySpan<byte> tokenUtf8)
    {
        byte[] quoted = new byte[tokenUtf8.Length + 2];
        quoted[0] = (byte)'"';
        tokenUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(quoted);
    }

    private async ValueTask<IAvailabilityRequestStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IAvailabilityRequestStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }

    private async ValueTask<string> CreateAsync(IAvailabilityRequestStore store, string baseWorkflowId, int versionNumber, string environment, string actor)
    {
        using ParsedJsonDocument<AvailabilityRequest> created = await CreateRequestAsync(store, AvailabilityRequest.Draft(baseWorkflowId, versionNumber, environment), actor, default);
        return created.RootElement.IdValue;
    }

    // Creates the (pooled, disposable) draft request, disposing the draft once the store has read it; the created
    // document is returned for the caller to assert on and dispose.
    private static async Task<ParsedJsonDocument<AvailabilityRequest>> CreateRequestAsync(IAvailabilityRequestStore store, ParsedJsonDocument<AvailabilityRequest> draft, string actor, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await store.CreateAsync(draft.RootElement, actor, cancellationToken);
        }
    }

    private async ValueTask<List<string>> IdsAsync(IAvailabilityRequestStore store, AvailabilityRequestQuery query)
    {
        using PooledDocumentList<AvailabilityRequest> list = await store.ListAsync(query, default);
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