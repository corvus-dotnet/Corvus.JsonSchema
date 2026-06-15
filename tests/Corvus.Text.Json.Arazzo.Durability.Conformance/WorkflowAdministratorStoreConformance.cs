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

        using (ParsedJsonDocument<WorkflowAdministrators> put = await store.PutAsync("nightly-reconcile", [acme], WorkflowEtag.None, "alice", default))
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
        await store.PutAsync("nightly-reconcile", [Administrator("acme")], WorkflowEtag.None, "alice", default);

        // A second None-etag put (the caller believing no record exists) must be rejected — the record now exists.
        await Should.ThrowAsync<WorkflowAdministrationConflictException>(
            async () => await store.PutAsync("nightly-reconcile", [Administrator("globex")], WorkflowEtag.None, "mallory", default));
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
        using (ParsedJsonDocument<WorkflowAdministrators> created = await store.PutAsync("nightly-reconcile", [acme], WorkflowEtag.None, "alice", default))
        {
            firstEtag = created.RootElement.EtagValue;
            created.RootElement.LastUpdatedByOrNull.ShouldBeNull();
        }

        using ParsedJsonDocument<WorkflowAdministrators> replaced = await store.PutAsync("nightly-reconcile", [acme, globex], firstEtag, "bob", default);

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
        await store.PutAsync("nightly-reconcile", [Administrator("acme")], WorkflowEtag.None, "alice", default);

        await Should.ThrowAsync<WorkflowAdministrationConflictException>(
            async () => await store.PutAsync("nightly-reconcile", [Administrator("globex")], new WorkflowEtag("stale"), "bob", default));
    }

    [TestMethod]
    public async Task Replacing_a_missing_record_with_a_real_etag_conflicts()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();

        // No record exists, yet the caller presents an etag (it believed one did) — a conflict, not a silent create.
        await Should.ThrowAsync<WorkflowAdministrationConflictException>(
            async () => await store.PutAsync("nightly-reconcile", [Administrator("acme")], new WorkflowEtag("ghost"), "alice", default));
    }

    [TestMethod]
    public async Task Multiple_administrator_identities_round_trip_with_set_membership()
    {
        IWorkflowAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = AdministratorWith(("sys:tenant", "acme"), ("team", "payments"));
        SecurityTagSet globex = Administrator("globex");

        await store.PutAsync("nightly-reconcile", [acme, globex], WorkflowEtag.None, "alice", default);

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
        await store.PutAsync("nightly-reconcile", [Administrator("acme")], WorkflowEtag.None, "alice", default);
        await store.PutAsync("daily-export", [Administrator("globex")], WorkflowEtag.None, "bob", default);

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

    private static SecurityTagSet Administrator(string tenant) => SecurityTagSet.FromTags([new SecurityTag("sys:tenant", tenant)]);

    private static SecurityTagSet AdministratorWith(params (string Key, string Value)[] tags)
        => SecurityTagSet.FromTags([.. tags.Select(t => new SecurityTag(t.Key, t.Value))]);

    private async ValueTask<IWorkflowAdministratorStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IWorkflowAdministratorStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}