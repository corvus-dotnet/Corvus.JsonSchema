// <copyright file="EnvironmentStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IEnvironmentStore"/> must satisfy (design §7.7): environment CRUD keyed by
/// <c>name</c>, optimistic concurrency via etag, deterministic keyset list ordering by name, immutable identity/audit on
/// update carried bytes-to-bytes, and reach-scoped non-disclosing reads. A backend's test project derives a concrete
/// <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>; the in-memory store is the reference
/// implementation and runs the same suite.
/// </summary>
public abstract class EnvironmentStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IEnvironmentStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task An_environment_round_trips_through_add_get_and_list()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        using (ParsedJsonDocument<Environment> draft = Environment.Draft("production", "Production", "The live environment.", default))
        using (ParsedJsonDocument<Environment> added = await store.AddAsync(draft.RootElement, "alice", default))
        {
            ((string)added.RootElement.Name).ShouldBe("production");
            ((string)added.RootElement.DisplayName).ShouldBe("Production");
            ((string)added.RootElement.Description).ShouldBe("The live environment.");
            ((string)added.RootElement.CreatedBy).ShouldBe("alice");
            added.RootElement.EtagValue.IsNone.ShouldBeFalse();
        }

        using (ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", AccessContext.System, default))
        {
            fetched.ShouldNotBeNull();
            ((string)fetched!.RootElement.Name).ShouldBe("production");
        }

        using (EnvironmentPage page = await store.ListAsync(AccessContext.System, 1000, default, default))
        {
            page.Environments.Select(e => e.NameValue).ShouldBe(["production"]);
        }

        (await store.GetAsync("staging", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Adding_a_duplicate_name_fails()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        await this.SeedAsync(store, "production");

        await Should.ThrowAsync<InvalidOperationException>(async () => await this.SeedAsync(store, "production", "bob"));

        // A different name is a distinct environment.
        await this.SeedAsync(store, "staging");
        using EnvironmentPage page = await store.ListAsync(AccessContext.System, 1000, default, default);
        page.Environments.Select(e => e.NameValue).ShouldBe(["production", "staging"]);
    }

    [TestMethod]
    public async Task Updating_an_environment_bumps_the_etag_carries_identity_and_records_the_actor()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<Environment> seed = Environment.Draft("production", null, null, default))
        using (ParsedJsonDocument<Environment> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("production", "Prod (renamed)", "Now with a description.", default))
        using (ParsedJsonDocument<Environment>? updated = await store.UpdateAsync("production", draft.RootElement, addedEtag, "bob", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
            ((string)updated!.RootElement.Name).ShouldBe("production");           // immutable identity carried forward
            ((string)updated.RootElement.CreatedBy).ShouldBe("alice");            // created-* audit carried forward
            ((string)updated.RootElement.DisplayName).ShouldBe("Prod (renamed)"); // mutable content updated
            ((string)updated.RootElement.LastUpdatedBy).ShouldBe("bob");
            updated.RootElement.LastUpdatedAt.IsNotUndefined().ShouldBeTrue();
            (updated.RootElement.EtagValue == addedEtag).ShouldBeFalse();
        }

        using ParsedJsonDocument<Environment> missingDraft = Environment.Draft("missing", null, null, default);
        (await store.UpdateAsync("missing", missingDraft.RootElement, WorkflowEtag.None, "bob", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_update_or_delete_conflicts()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<Environment> seed = Environment.Draft("production", null, null, default))
        using (ParsedJsonDocument<Environment> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("production", "v2", null, default))
        using (await store.UpdateAsync("production", draft.RootElement, addedEtag, "bob", AccessContext.System, default))
        {
            // etag now advanced
        }

        using ParsedJsonDocument<Environment> stale = Environment.Draft("production", "v3", null, default);
        await Should.ThrowAsync<EnvironmentConflictException>(async () =>
            await store.UpdateAsync("production", stale.RootElement, addedEtag, "carol", AccessContext.System, default));
        await Should.ThrowAsync<EnvironmentConflictException>(async () =>
            await store.DeleteAsync("production", addedEtag, AccessContext.System, default));

        // WorkflowEtag.None deletes unconditionally.
        (await store.DeleteAsync("production", WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();
        (await store.DeleteAsync("production", WorkflowEtag.None, AccessContext.System, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task The_listing_is_ordered_by_name()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        await this.SeedAsync(store, "zeta");
        await this.SeedAsync(store, "alpha");
        await this.SeedAsync(store, "mid");

        using EnvironmentPage page = await store.ListAsync(AccessContext.System, 1000, default, default);
        page.Environments.Select(e => e.NameValue).ShouldBe(["alpha", "mid", "zeta"]);
    }

    [TestMethod]
    public async Task Listing_keyset_pages_in_name_order_without_gaps_or_duplicates()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        string[] names = ["petstore", "ledger", "alpha", "zeta", "mid", "qa", "staging", "prod"];

        // Add out of order, to prove the store (not insertion order) establishes the page order.
        foreach (string name in names.OrderDescending(StringComparer.Ordinal))
        {
            await this.SeedAsync(store, name, "system");
        }

        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using EnvironmentPage page = await store.ListAsync(AccessContext.System, 3, tokenDoc?.RootElement ?? default, default);
            page.Environments.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (Environment e in page.Environments)
            {
                seen.Add(e.NameValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 8 items, 3 per page → 3 pages; no duplicates or gaps; contractual name order.
        pages.ShouldBe(3);
        seen.ShouldBe(names.OrderBy(n => n, StringComparer.Ordinal).ToArray());

        // A malformed token is rejected (rather than silently restarting).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<JsonString> badToken = AsPageToken("this~is~not~a~token"u8);
            using EnvironmentPage bad = await store.ListAsync(AccessContext.System, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Security_tags_round_trip_carry_forward_when_omitted_and_are_replaced_on_re_tag()
    {
        IEnvironmentStore store = await this.NewStoreAsync();
        WorkflowEtag etag;
        using (ParsedJsonDocument<Environment> seed = Environment.Draft("production", null, null, Tenant("acme")))
        using (ParsedJsonDocument<Environment> added = await store.AddAsync(seed.RootElement, "system", default))
        {
            added.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
            etag = added.RootElement.EtagValue;
        }

        // An update that OMITS management tags (empty draft set) leaves them unchanged — carried forward bytes-to-bytes.
        using (ParsedJsonDocument<Environment> renameOnly = Environment.Draft("production", "renamed", null, SecurityTagSet.Empty))
        using (ParsedJsonDocument<Environment>? unchanged = await store.UpdateAsync("production", renameOnly.RootElement, etag, "bob", AccessContext.System, default))
        {
            unchanged.ShouldNotBeNull();
            unchanged!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
            etag = unchanged.RootElement.EtagValue;
        }

        // An update that SUPPLIES management tags re-tags the reach scope: the store replaces them with the given set
        // (the handler is responsible for merging in the preserved internal tags, so at the store level it is a replace).
        using (ParsedJsonDocument<Environment> reTag = Environment.Draft("production", null, null, Tenant("globex")))
        using (ParsedJsonDocument<Environment>? updated = await store.UpdateAsync("production", reTag.RootElement, etag, "carol", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
            updated!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "globex"));
        }

        // The re-tag is durable and the row is still found by name (the create-time key is frozen; lookup is by name+reach).
        using ParsedJsonDocument<Environment>? refetched = await store.GetAsync("production", AccessContext.System, default);
        refetched.ShouldNotBeNull();
        refetched!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "globex"));
    }

    [TestMethod]
    public async Task Management_reads_are_reach_filtered_and_non_disclosing()
    {
        IEnvironmentStore store = await this.NewStoreAsync();

        // Two tenants each create an environment named "production" — distinguished by their security tags.
        await this.SeedAsync(store, "production", "system", Tenant("acme"));
        await this.SeedAsync(store, "production", "system", Tenant("globex"));

        AccessContext acme = Scope("acme");

        // A get under acme's reach returns acme's environment; globex's is invisible (non-disclosing).
        using (ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", acme, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
        }

        using (EnvironmentPage page = await store.ListAsync(acme, 1000, default, default))
        {
            page.Environments.Select(e => e.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        // acme deletes only its own; globex's survived (out of acme's write reach).
        (await store.DeleteAsync("production", WorkflowEtag.None, acme, default)).ShouldBeTrue();
        (await store.GetAsync("production", Scope("globex"), default)).ShouldNotBeNull();
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

    private static SecurityTagSet Tenant(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    // A read/write/purge reach that admits exactly the rows tagged tenant=<tenant> (tenant == $claim.tenant resolved
    // against a single-tenant claim).
    private static AccessContext Scope(string tenant) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile("tenant == $claim.tenant")], new Dictionary<string, IReadOnlyList<string>> { ["tenant"] = [tenant] }));

    // Seeds an environment and disposes the returned pooled document (the tests that need the document add inline).
    private async Task SeedAsync(IEnvironmentStore store, string name, string actor = "alice", SecurityTagSet tags = default)
    {
        using ParsedJsonDocument<Environment> draft = Environment.Draft(name, null, null, tags);
        using ParsedJsonDocument<Environment> added = await store.AddAsync(draft.RootElement, actor, default);
    }

    private async ValueTask<IEnvironmentStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IEnvironmentStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}