// <copyright file="EnvironmentAdministratorStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IEnvironmentAdministratorStore"/> must satisfy (design §7.7): a record reads back
/// <see langword="null"/> until materialized, a create-or-replace <see cref="IEnvironmentAdministratorStore.PutAsync"/>
/// under optimistic concurrency, administrator-set round-trip with order-independent membership, immutable creation audit
/// carried across updates, per-environment isolation, and removal via <see cref="IEnvironmentAdministratorStore.DeleteAsync"/>.
/// A backend's test project derives a concrete <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>;
/// the in-memory store is the reference implementation and runs the same suite. It also covers the reverse administration
/// index (<see cref="IEnvironmentAdministratorStore.ListAdministeredAsync"/>, design §7.8) that powers the promotion inbox:
/// the environments an administrator administers, keyset-paged, retracted when the set changes or the record is deleted.
/// </summary>
public abstract class EnvironmentAdministratorStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    // Backs the durable AdministratorIdentity values passed to PutAsync; held for the whole test (across awaits) and
    // disposed in CleanupAsync, which may run on a different thread — so it must be the unrented, thread-affinity-free form.
    private readonly JsonWorkspace workspace = JsonWorkspace.CreateUnrented();

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IEnvironmentAdministratorStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task An_environment_with_no_record_reads_back_null()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        using ParsedJsonDocument<EnvironmentAdministrators>? record = await store.GetAsync("production", default);
        record.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_record_is_materialized_against_the_none_etag_and_round_trips()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");

        using (ParsedJsonDocument<EnvironmentAdministrators> put = await store.PutAsync("production", [this.Identity(acme)], WorkflowEtag.None, "alice", default))
        {
            put.RootElement.EnvironmentNameValue.ShouldBe("production");
            put.RootElement.CreatedByValue.ShouldBe("alice");
            put.RootElement.EtagValue.IsNone.ShouldBeFalse();
            put.RootElement.AdministratorCount.ShouldBe(1);
            put.RootElement.IsAdministeredBy(acme).ShouldBeTrue();
        }

        using ParsedJsonDocument<EnvironmentAdministrators>? fetched = await store.GetAsync("production", default);
        fetched.ShouldNotBeNull();
        fetched!.RootElement.IsAdministeredBy(acme).ShouldBeTrue();
        fetched.RootElement.IsAdministeredBy(Administrator("globex")).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Materializing_over_an_existing_record_conflicts()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("production", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);

        await Should.ThrowAsync<EnvironmentAdministrationConflictException>(
            async () => await store.PutAsync("production", [this.Identity(Administrator("globex"))], WorkflowEtag.None, "mallory", default));
    }

    [TestMethod]
    public async Task A_replace_under_the_current_etag_updates_administrators_and_preserves_creation_audit()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        SecurityTagSet globex = Administrator("globex");

        WorkflowEtag firstEtag;
        using (ParsedJsonDocument<EnvironmentAdministrators> created = await store.PutAsync("production", [this.Identity(acme)], WorkflowEtag.None, "alice", default))
        {
            firstEtag = created.RootElement.EtagValue;
            created.RootElement.LastUpdatedByOrNull.ShouldBeNull();
        }

        using ParsedJsonDocument<EnvironmentAdministrators> replaced = await store.PutAsync("production", [this.Identity(acme), this.Identity(globex)], firstEtag, "bob", default);

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
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("production", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);

        await Should.ThrowAsync<EnvironmentAdministrationConflictException>(
            async () => await store.PutAsync("production", [this.Identity(Administrator("globex"))], new WorkflowEtag("stale"), "bob", default));
    }

    [TestMethod]
    public async Task Replacing_a_missing_record_with_a_real_etag_conflicts()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();

        await Should.ThrowAsync<EnvironmentAdministrationConflictException>(
            async () => await store.PutAsync("production", [this.Identity(Administrator("acme"))], new WorkflowEtag("ghost"), "alice", default));
    }

    [TestMethod]
    public async Task Multiple_administrator_identities_round_trip_with_set_membership()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = AdministratorWith(("sys:tenant", "acme"), ("team", "payments"));
        SecurityTagSet globex = Administrator("globex");

        await store.PutAsync("production", [this.Identity(acme), this.Identity(globex)], WorkflowEtag.None, "alice", default);

        using ParsedJsonDocument<EnvironmentAdministrators>? fetched = await store.GetAsync("production", default);
        fetched.ShouldNotBeNull();

        // Membership is order-independent set equality — a reordered identity still matches, a subset does not.
        fetched!.RootElement.IsAdministeredBy(AdministratorWith(("team", "payments"), ("sys:tenant", "acme"))).ShouldBeTrue();
        fetched.RootElement.IsAdministeredBy(globex).ShouldBeTrue();
        fetched.RootElement.IsAdministeredBy(Administrator("acme")).ShouldBeFalse(); // subset of acme's identity
    }

    [TestMethod]
    public async Task Records_for_distinct_environments_are_isolated()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("production", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);
        await store.PutAsync("staging", [this.Identity(Administrator("globex"))], WorkflowEtag.None, "bob", default);

        using ParsedJsonDocument<EnvironmentAdministrators>? a = await store.GetAsync("production", default);
        using ParsedJsonDocument<EnvironmentAdministrators>? b = await store.GetAsync("staging", default);
        a!.RootElement.IsAdministeredBy(Administrator("acme")).ShouldBeTrue();
        a.RootElement.IsAdministeredBy(Administrator("globex")).ShouldBeFalse();
        b!.RootElement.IsAdministeredBy(Administrator("globex")).ShouldBeTrue();
        b.RootElement.IsAdministeredBy(Administrator("acme")).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_empty_administrator_set_is_rejected()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        await Should.ThrowAsync<ArgumentException>(
            async () => await store.PutAsync("production", [], WorkflowEtag.None, "alice", default));
    }

    [TestMethod]
    public async Task Deleting_a_record_removes_it()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        await store.PutAsync("production", [this.Identity(Administrator("acme"))], WorkflowEtag.None, "alice", default);

        await store.DeleteAsync("production", default);

        using ParsedJsonDocument<EnvironmentAdministrators>? gone = await store.GetAsync("production", default);
        gone.ShouldBeNull();
    }

    [TestMethod]
    public async Task Deleting_a_missing_record_is_a_no_op()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        await store.DeleteAsync("production", default); // does not throw
    }

    [TestMethod]
    public async Task The_reverse_index_lists_the_environments_an_administrator_administers()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        SecurityTagSet globex = Administrator("globex");
        await store.PutAsync("production", [this.Identity(acme)], WorkflowEtag.None, "alice", default);
        await store.PutAsync("staging", [this.Identity(acme), this.Identity(globex)], WorkflowEtag.None, "alice", default);
        await store.PutAsync("qa", [this.Identity(globex)], WorkflowEtag.None, "bob", default);

        // Each administrator sees exactly the environments their identity administers, ordered by environment name.
        (await ListAdministeredAsync(store, Digest(acme))).ShouldBe(["production", "staging"]);
        (await ListAdministeredAsync(store, Digest(globex))).ShouldBe(["qa", "staging"]);
        (await ListAdministeredAsync(store, Digest(Administrator("nobody")))).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Changing_the_administrator_set_retracts_stale_reverse_index_entries()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        SecurityTagSet globex = Administrator("globex");

        WorkflowEtag etag;
        using (ParsedJsonDocument<EnvironmentAdministrators> put = await store.PutAsync("production", [this.Identity(acme)], WorkflowEtag.None, "alice", default))
        {
            etag = put.RootElement.EtagValue;
        }

        // Transfer administration from acme to globex: acme must no longer appear, globex now does.
        await store.PutAsync("production", [this.Identity(globex)], etag, "alice", default);

        (await ListAdministeredAsync(store, Digest(acme))).ShouldBeEmpty();
        (await ListAdministeredAsync(store, Digest(globex))).ShouldBe(["production"]);
    }

    [TestMethod]
    public async Task Deleting_a_record_retracts_it_from_the_reverse_index()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        await store.PutAsync("production", [this.Identity(acme)], WorkflowEtag.None, "alice", default);
        await store.PutAsync("staging", [this.Identity(acme)], WorkflowEtag.None, "alice", default);

        await store.DeleteAsync("production", default);

        (await ListAdministeredAsync(store, Digest(acme))).ShouldBe(["staging"]);
    }

    [TestMethod]
    public async Task The_reverse_index_keyset_pages_without_gaps_or_duplicates()
    {
        IEnvironmentAdministratorStore store = await this.NewStoreAsync();
        SecurityTagSet acme = Administrator("acme");
        string[] environments = ["prod", "staging", "qa", "dev", "alpha", "zeta", "mid"];
        foreach (string environment in environments)
        {
            await store.PutAsync(environment, [this.Identity(acme)], WorkflowEtag.None, "alice", default);
        }

        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using EnvironmentAdministeredPage page = await store.ListAdministeredAsync(Digest(acme), 3, tokenDoc?.RootElement ?? default, default);
            page.EnvironmentNames.Count.ShouldBeLessThanOrEqualTo(3);
            seen.AddRange(page.EnvironmentNames);
            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 7 entries, 3 per page
        seen.ShouldBe(environments.OrderBy(e => e, StringComparer.Ordinal).ToArray());
    }

    // The collision-probe digest of an identity (the reverse-index key); the identities used here always have a digest.
    private static string Digest(SecurityTagSet tags)
        => SecurityIdentityDigest.Compute(tags) ?? throw new InvalidOperationException("The identity has no digest.");

    // Drains the reverse index for a digest across all keyset pages into one ordered list.
    private static async Task<List<string>> ListAdministeredAsync(IEnvironmentAdministratorStore store, string digest)
    {
        var all = new List<string>();
        byte[]? token = null;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using EnvironmentAdministeredPage page = await store.ListAdministeredAsync(digest, 1000, tokenDoc?.RootElement ?? default, default);
            all.AddRange(page.EnvironmentNames);
            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
        }
        while (token is not null);

        return all;
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

    private static SecurityTagSet Administrator(string tenant) => SecurityTagSet.FromTags([new SecurityTag("sys:tenant", tenant)]);

    private static SecurityTagSet AdministratorWith(params (string Key, string Value)[] tags)
        => SecurityTagSet.FromTags([.. tags.Select(t => new SecurityTag(t.Key, t.Value))]);

    // Materializes a resolved identity (tags only — the persisted administrator form) for PutAsync; backed by the
    // test-lived workspace so it survives the store's synchronous serialization.
    private EnvironmentAdministrators.AdministratorIdentity Identity(SecurityTagSet tags)
        => EnvironmentAdministrators.BuildIdentity(this.workspace, tags, default, hasKind: false, default, hasLabel: false);

    private async ValueTask<IEnvironmentAdministratorStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IEnvironmentAdministratorStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}