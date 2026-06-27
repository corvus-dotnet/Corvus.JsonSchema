// <copyright file="WorkflowAdministratorStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IWorkflowAdministratorStore"/> must satisfy (design §13/§14.2): lazy
/// materialization (a workflow with no record reads back <see langword="null"/>), a create-or-replace
/// <see cref="IWorkflowAdministratorStore.PutAsync"/> under optimistic concurrency, administrator-set round-trip with
/// order-independent membership, immutable creation audit carried across updates, and per-base-id isolation. A
/// backend's test project derives a concrete <see cref="TestClassAttribute"/> and implements
/// <see cref="CreateStoreAsync"/>; the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class WorkflowAdministratorStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    // Backs the durable AdministratorIdentity values passed to PutAsync; held for the whole test (across awaits) and
    // disposed in CleanupAsync, which may run on a different thread — so it must be the unrented, thread-affinity-free form.
    private readonly JsonWorkspace workspace = JsonWorkspace.CreateUnrented();

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IWorkflowAdministratorStore> CreateStoreAsync(TimeProvider timeProvider);

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
        this.workspace.Dispose();
    }

    [TestMethod]
    public async Task A_workflow_with_no_record_reads_back_null()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        using ParsedJsonDocument<WorkflowAdministrators>? record = await store.GetAsync("nightly-reconcile", default);
        record.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_record_is_materialized_against_the_none_etag_and_round_trips()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");

        using (ParsedJsonDocument<WorkflowAdministrators> put = await store.PutAsync("nightly-reconcile", [this.Identity(acme)], WorkflowEtag.None, "alice", default))
        {
            put.RootElement.BaseWorkflowIdValue.ShouldBe("nightly-reconcile");
            put.RootElement.CreatedByValue.ShouldBe("alice");
            put.RootElement.EtagValue.IsNone.ShouldBeFalse();
            put.RootElement.AdministratorCount.ShouldBe(1);
            put.RootElement.IsAdministeredBy(acme).ShouldBeTrue();
        }

        using ParsedJsonDocument<WorkflowAdministrators>? fetched = await store.GetAsync("nightly-reconcile", default);
        fetched.ShouldNotBeNull();
        fetched!.RootElement.IsAdministeredBy(acme).ShouldBeTrue();
        fetched.RootElement.IsAdministeredBy(Administrator("globex")).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Materializing_over_an_existing_record_conflicts()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);

        // A second None-etag put (the caller believing no record exists) must be rejected — the record now exists.
        await Should.ThrowAsync<WorkflowAdministrationConflictException>(
            async () => await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("globex"))], WorkflowEtag.None, "mallory", default));
    }

    [TestMethod]
    public async Task A_replace_under_the_current_etag_updates_administrators_and_preserves_creation_audit()
    {
        // Clock-free: the conformance suite runs against every backend over TimeProvider.System and so does not assert
        // exact instants — the audit-timestamp progression is covered by an InMemory unit test using TestTimeProvider.
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        SecurityTagSet globex = Administrator("globex");

        WorkflowEtag firstEtag;
        using (ParsedJsonDocument<WorkflowAdministrators> created = await store.PutAsync("nightly-reconcile", [this.Identity(acme)], WorkflowEtag.None, "alice", default))
        {
            firstEtag = created.RootElement.EtagValue;
            created.RootElement.LastUpdatedByOrNull.ShouldBeNull();
        }

        using ParsedJsonDocument<WorkflowAdministrators> replaced = await store.PutAsync("nightly-reconcile", [this.Identity(acme), this.Identity(globex)], firstEtag, "bob", default);

        replaced.RootElement.AdministratorCount.ShouldBe(2);
        replaced.RootElement.IsAdministeredBy(acme).ShouldBeTrue();
        replaced.RootElement.IsAdministeredBy(globex).ShouldBeTrue();
        replaced.RootElement.EtagValue.ShouldNotBe(firstEtag);
        replaced.RootElement.CreatedByValue.ShouldBe("alice"); // creation audit immutable
        replaced.RootElement.LastUpdatedByOrNull.ShouldBe("bob");
    }

    [TestMethod]
    public async Task A_replace_under_a_stale_etag_conflicts()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);

        await Should.ThrowAsync<WorkflowAdministrationConflictException>(
            async () => await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("globex"))], new WorkflowEtag("stale"), "bob", default));
    }

    [TestMethod]
    public async Task Replacing_a_missing_record_with_a_real_etag_conflicts()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();

        // No record exists, yet the caller presents an etag (it believed one did) — a conflict, not a silent create.
        await Should.ThrowAsync<WorkflowAdministrationConflictException>(
            async () => await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("acme"))], new WorkflowEtag("ghost"), "alice", default));
    }

    [TestMethod]
    public async Task Multiple_administrator_identities_round_trip_with_set_membership()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = AdministratorWith(("sys:tenant", "acme"), ("team", "payments"));
        SecurityTagSet globex = Administrator("globex");

        await store.PutAsync("nightly-reconcile", [this.Identity(acme), this.Identity(globex)], WorkflowEtag.None, "alice", default);

        using ParsedJsonDocument<WorkflowAdministrators>? fetched = await store.GetAsync("nightly-reconcile", default);
        fetched.ShouldNotBeNull();

        // Membership is order-independent set equality — a reordered identity still matches, a subset does not.
        fetched!.RootElement.IsAdministeredBy(AdministratorWith(("team", "payments"), ("sys:tenant", "acme"))).ShouldBeTrue();
        fetched.RootElement.IsAdministeredBy(globex).ShouldBeTrue();
        fetched.RootElement.IsAdministeredBy(Administrator("acme")).ShouldBeFalse(); // subset of acme's identity
    }

    [TestMethod]
    public async Task Records_for_distinct_base_ids_are_isolated()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);
        await store.PutAsync("daily-export", [this.Identity(Administrator("globex"))], WorkflowEtag.None, "bob", default);

        using ParsedJsonDocument<WorkflowAdministrators>? a = await store.GetAsync("nightly-reconcile", default);
        using ParsedJsonDocument<WorkflowAdministrators>? b = await store.GetAsync("daily-export", default);
        a!.RootElement.IsAdministeredBy(Administrator("acme")).ShouldBeTrue();
        a.RootElement.IsAdministeredBy(Administrator("globex")).ShouldBeFalse();
        b!.RootElement.IsAdministeredBy(Administrator("globex")).ShouldBeTrue();
        b.RootElement.IsAdministeredBy(Administrator("acme")).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_empty_administrator_set_is_rejected()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        await Should.ThrowAsync<ArgumentException>(
            async () => await store.PutAsync("nightly-reconcile", [], WorkflowEtag.None, "alice", default));
    }

    [TestMethod]
    public async Task The_reverse_index_lists_the_base_ids_an_identity_administers()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        SecurityTagSet globex = Administrator("globex");

        // acme administers two workflows (one shared with globex); globex administers one.
        await store.PutAsync("nightly-reconcile", [this.Identity(acme)], WorkflowEtag.None, "alice", default);
        await store.PutAsync("daily-export", [this.Identity(acme), this.Identity(globex)], WorkflowEtag.None, "alice", default);

        (await this.AdministeredByAsync(store, acme)).ShouldBe(["daily-export", "nightly-reconcile"]);
        (await this.AdministeredByAsync(store, globex)).ShouldBe(["daily-export"]);
    }

    [TestMethod]
    public async Task The_reverse_index_is_refreshed_when_the_administrator_set_changes()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        SecurityTagSet globex = Administrator("globex");

        WorkflowEtag etag;
        using (ParsedJsonDocument<WorkflowAdministrators> created = await store.PutAsync("nightly-reconcile", [this.Identity(acme)], WorkflowEtag.None, "alice", default))
        {
            etag = created.RootElement.EtagValue;
        }

        (await this.AdministeredByAsync(store, acme)).ShouldBe(["nightly-reconcile"]);

        // Transfer administration from acme to globex: acme drops out of the reverse index, globex appears.
        await store.PutAsync("nightly-reconcile", [this.Identity(globex)], etag, "bob", default);
        (await this.AdministeredByAsync(store, acme)).ShouldBeEmpty();
        (await this.AdministeredByAsync(store, globex)).ShouldBe(["nightly-reconcile"]);
    }

    [TestMethod]
    public async Task An_identity_administering_nothing_returns_an_empty_page()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("nightly-reconcile", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);

        (await this.AdministeredByAsync(store, Administrator("nobody"))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task The_reverse_index_keyset_pages_by_base_id_without_gaps_or_duplicates()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        string[] ids = ["flow-3", "flow-1", "flow-5", "flow-2", "flow-0", "flow-4"];
        foreach (string id in ids)
        {
            await store.PutAsync(id, [this.Identity(acme)], WorkflowEtag.None, "alice", default);
        }

        // Walk every page via the continuation token with a small limit, round-tripped through the JsonString seam
        // exactly as the HTTP layer does; collect base ids in page order.
        string digest = SecurityIdentityDigest.Compute(acme)!;
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using WorkflowAdministeredPage page = await store.ListAdministeredAsync(digest, 2, tokenDoc?.RootElement ?? default, default);
            page.BaseWorkflowIds.Count.ShouldBeLessThanOrEqualTo(2);
            seen.AddRange(page.BaseWorkflowIds);
            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 6 base ids, 2 per page → 3 pages; no duplicates or gaps; contractual baseWorkflowId (ordinal) order.
        pages.ShouldBe(3);
        seen.ShouldBe(ids.OrderBy(x => x, StringComparer.Ordinal).ToArray());

        // A malformed token is rejected rather than silently restarting from the first page.
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> badToken = AsJsonString("this~is~not~a~token"u8);
            using WorkflowAdministeredPage bad = await store.ListAdministeredAsync(digest, 2, badToken.RootElement, default);
        });
    }

    private static SecurityTagSet Administrator(string tenant) => SecurityTagSet.FromTags([new SecurityTag("sys:tenant", tenant)]);

    private static SecurityTagSet AdministratorWith(params (string Key, string Value)[] tags)
        => SecurityTagSet.FromTags([.. tags.Select(t => new SecurityTag(t.Key, t.Value))]);

    // Materializes a resolved identity (tags only — the persisted administrator form) for PutAsync; backed by the
    // test-lived workspace so it survives the store's synchronous serialization.
    private WorkflowAdministrators.AdministratorIdentity Identity(SecurityTagSet tags)
        => WorkflowAdministrators.BuildIdentity(this.workspace, tags, default, hasKind: false, default, hasLabel: false);

    private async ValueTask<IWorkflowAdministratorStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IWorkflowAdministratorStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }

    // Pages the reverse administration index fully for an identity, returning the administered base ids in keyset order —
    // the token round-tripped through the JsonString seam exactly as the HTTP layer does.
    private async ValueTask<List<string>> AdministeredByAsync(IWorkflowAdministratorStore store, SecurityTagSet identity)
    {
        string digest = SecurityIdentityDigest.Compute(identity)!;
        var result = new List<string>();
        byte[]? token = null;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using WorkflowAdministeredPage page = await store.ListAdministeredAsync(digest, 0, tokenDoc?.RootElement ?? default, default);
            result.AddRange(page.BaseWorkflowIds);
            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
        }
        while (token is not null);

        return result;
    }

    // Wraps a value's UTF-8 as the JSON string a request carries it as — the conformance feeds a previous page's
    // NextPageToken (the store's emitted bytes) back through the JsonString pageToken seam, mirroring HTTP.
    private static ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> AsJsonString(ReadOnlySpan<byte> valueUtf8)
    {
        byte[] quoted = new byte[valueUtf8.Length + 2];
        quoted[0] = (byte)'"';
        valueUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>.Parse(quoted);
    }
}
