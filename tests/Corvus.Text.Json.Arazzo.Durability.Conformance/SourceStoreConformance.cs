// <copyright file="SourceStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="ISourceStore"/> must satisfy (design §7.6): source CRUD keyed by <c>name</c>,
/// optimistic concurrency via etag, deterministic keyset list ordering by name, immutable identity/type/audit on update
/// carried bytes-to-bytes, the registered document round-tripped verbatim and rotated-or-carried-forward on update, and
/// reach-scoped non-disclosing reads. A backend's test project derives a concrete <see cref="TestClassAttribute"/> and
/// implements <see cref="CreateStoreAsync"/>; the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class SourceStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ISourceStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task A_source_round_trips_through_add_get_and_list()
    {
        ISourceStore store = await this.NewStoreAsync();
        using (ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft("petstore", "openapi", DocUtf8("v1"), "Pet Store", "The pet store API.", default))
        using (ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(draft.RootElement, "alice", default))
        {
            ((string)added.RootElement.Name).ShouldBe("petstore");
            ((string)added.RootElement.Type).ShouldBe("openapi");
            ((string)added.RootElement.DisplayName).ShouldBe("Pet Store");
            ((string)added.RootElement.Description).ShouldBe("The pet store API.");
            ((string)added.RootElement.CreatedBy).ShouldBe("alice");
            DocJson(added.RootElement).ShouldContain("v1");
            added.RootElement.EtagValue.IsNone.ShouldBeFalse();
        }

        using (ParsedJsonDocument<RegisteredSource>? fetched = await store.GetAsync("petstore", AccessContext.System, default))
        {
            fetched.ShouldNotBeNull();
            ((string)fetched!.RootElement.Name).ShouldBe("petstore");
            DocJson(fetched.RootElement).ShouldContain("v1"); // the full document comes back on a single read
        }

        using (SourcePage page = await store.ListAsync(AccessContext.System, 1000, default, default))
        {
            page.Sources.Select(s => s.NameValue).ShouldBe(["petstore"]);
        }

        (await store.GetAsync("ledger", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Adding_a_duplicate_name_fails()
    {
        ISourceStore store = await this.NewStoreAsync();
        await this.SeedAsync(store, "petstore");

        await Should.ThrowAsync<InvalidOperationException>(async () => await this.SeedAsync(store, "petstore", "bob"));

        // A different name is a distinct source.
        await this.SeedAsync(store, "ledger");
        using SourcePage page = await store.ListAsync(AccessContext.System, 1000, default, default);
        page.Sources.Select(s => s.NameValue).ShouldBe(["ledger", "petstore"]);
    }

    [TestMethod]
    public async Task Updating_a_source_bumps_the_etag_carries_identity_and_records_the_actor()
    {
        ISourceStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<RegisteredSource> seed = RegisteredSource.Draft("petstore", "openapi", DocUtf8("v1"), null, null, default))
        using (ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft("petstore", null, default, "Pet Store (renamed)", "Now with a description.", default))
        using (ParsedJsonDocument<RegisteredSource>? updated = await store.UpdateAsync("petstore", draft.RootElement, addedEtag, "bob", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
            ((string)updated!.RootElement.Name).ShouldBe("petstore");                   // immutable identity carried forward
            ((string)updated.RootElement.Type).ShouldBe("openapi");                     // immutable type carried forward
            ((string)updated.RootElement.CreatedBy).ShouldBe("alice");                  // created-* audit carried forward
            ((string)updated.RootElement.DisplayName).ShouldBe("Pet Store (renamed)");  // mutable content updated
            ((string)updated.RootElement.LastUpdatedBy).ShouldBe("bob");
            updated.RootElement.LastUpdatedAt.IsNotUndefined().ShouldBeTrue();
            (updated.RootElement.EtagValue == addedEtag).ShouldBeFalse();
        }

        using ParsedJsonDocument<RegisteredSource> missingDraft = RegisteredSource.Draft("missing", null, default, null, null, default);
        (await store.UpdateAsync("missing", missingDraft.RootElement, WorkflowEtag.None, "bob", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_document_is_carried_forward_when_an_update_omits_it_and_rotated_when_it_supplies_one()
    {
        ISourceStore store = await this.NewStoreAsync();
        WorkflowEtag etag;
        using (ParsedJsonDocument<RegisteredSource> seed = RegisteredSource.Draft("petstore", "openapi", DocUtf8("v1"), null, null, default))
        using (ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            etag = added.RootElement.EtagValue;
        }

        // An update that omits the document keeps the stored one (a metadata-only edit / rename).
        using (ParsedJsonDocument<RegisteredSource> renameDraft = RegisteredSource.Draft("petstore", null, default, "Pet Store", null, default))
        using (ParsedJsonDocument<RegisteredSource>? renamed = await store.UpdateAsync("petstore", renameDraft.RootElement, etag, "bob", AccessContext.System, default))
        {
            renamed.ShouldNotBeNull();
            ((string)renamed!.RootElement.DisplayName).ShouldBe("Pet Store");
            DocJson(renamed.RootElement).ShouldContain("v1"); // carried forward unchanged
            etag = renamed.RootElement.EtagValue;
        }

        // An update that supplies a document rotates it.
        using (ParsedJsonDocument<RegisteredSource> rotateDraft = RegisteredSource.Draft("petstore", null, DocUtf8("v2"), null, null, default))
        using (ParsedJsonDocument<RegisteredSource>? rotated = await store.UpdateAsync("petstore", rotateDraft.RootElement, etag, "carol", AccessContext.System, default))
        {
            rotated.ShouldNotBeNull();
            DocJson(rotated!.RootElement).ShouldContain("v2");
            DocJson(rotated.RootElement).ShouldNotContain("v1");
        }
    }

    [TestMethod]
    public async Task A_stale_etag_on_update_or_delete_conflicts()
    {
        ISourceStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<RegisteredSource> seed = RegisteredSource.Draft("petstore", "openapi", DocUtf8("v1"), null, null, default))
        using (ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft("petstore", null, DocUtf8("v2"), null, null, default))
        using (await store.UpdateAsync("petstore", draft.RootElement, addedEtag, "bob", AccessContext.System, default))
        {
            // etag now advanced
        }

        using ParsedJsonDocument<RegisteredSource> stale = RegisteredSource.Draft("petstore", null, DocUtf8("v3"), null, null, default);
        await Should.ThrowAsync<SourceConflictException>(async () =>
            await store.UpdateAsync("petstore", stale.RootElement, addedEtag, "carol", AccessContext.System, default));
        await Should.ThrowAsync<SourceConflictException>(async () =>
            await store.DeleteAsync("petstore", addedEtag, AccessContext.System, default));

        // WorkflowEtag.None deletes unconditionally.
        (await store.DeleteAsync("petstore", WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();
        (await store.DeleteAsync("petstore", WorkflowEtag.None, AccessContext.System, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task The_listing_is_ordered_by_name()
    {
        ISourceStore store = await this.NewStoreAsync();
        await this.SeedAsync(store, "zeta");
        await this.SeedAsync(store, "alpha");
        await this.SeedAsync(store, "mid");

        using SourcePage page = await store.ListAsync(AccessContext.System, 1000, default, default);
        page.Sources.Select(s => s.NameValue).ShouldBe(["alpha", "mid", "zeta"]);
    }

    [TestMethod]
    public async Task Listing_keyset_pages_in_name_order_without_gaps_or_duplicates()
    {
        ISourceStore store = await this.NewStoreAsync();
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
            using SourcePage page = await store.ListAsync(AccessContext.System, 3, tokenDoc?.RootElement ?? default, default);
            page.Sources.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (RegisteredSource s in page.Sources)
            {
                seen.Add(s.NameValue);
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
            using SourcePage bad = await store.ListAsync(AccessContext.System, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Security_tags_round_trip_and_are_immutable_on_update()
    {
        ISourceStore store = await this.NewStoreAsync();
        WorkflowEtag etag;
        using (ParsedJsonDocument<RegisteredSource> seed = RegisteredSource.Draft("petstore", "openapi", DocUtf8("v1"), null, null, Tenant("acme")))
        using (ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(seed.RootElement, "system", default))
        {
            added.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
            etag = added.RootElement.EtagValue;
        }

        // An update may not change the security tags (the immutable row-authorization scope) — they are carried forward.
        using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft("petstore", null, default, "renamed", null, Tenant("globex"));
        using ParsedJsonDocument<RegisteredSource>? updated = await store.UpdateAsync("petstore", draft.RootElement, etag, "bob", AccessContext.System, default);
        updated.ShouldNotBeNull();
        updated!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
    }

    [TestMethod]
    public async Task Management_reads_are_reach_filtered_and_non_disclosing()
    {
        ISourceStore store = await this.NewStoreAsync();

        // Two tenants each register a source named "petstore" — distinguished by their security tags.
        await this.SeedAsync(store, "petstore", "system", Tenant("acme"));
        await this.SeedAsync(store, "petstore", "system", Tenant("globex"));

        AccessContext acme = Scope("acme");

        // A get under acme's reach returns acme's source; globex's is invisible (non-disclosing).
        using (ParsedJsonDocument<RegisteredSource>? fetched = await store.GetAsync("petstore", acme, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
        }

        using (SourcePage page = await store.ListAsync(acme, 1000, default, default))
        {
            page.Sources.Select(s => s.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        // acme deletes only its own; globex's survived (out of acme's write reach).
        (await store.DeleteAsync("petstore", WorkflowEtag.None, acme, default)).ShouldBeTrue();
        (await store.GetAsync("petstore", Scope("globex"), default)).ShouldNotBeNull();
    }

    // A minimal, marker-bearing source document for round-trip / rotation assertions.
    private static ReadOnlyMemory<byte> DocUtf8(string marker)
        => Encoding.UTF8.GetBytes($$"""{"openapi":"3.1.0","x-marker":"{{marker}}"}""");

    // Serializes a source's registered document back to a JSON string (the raw stored bytes) for content assertions.
    private static string DocJson(RegisteredSource source)
        => Encoding.UTF8.GetString(JsonMarshal.GetRawUtf8Value(source.Document).Memory.Span);

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

    // Seeds a source and disposes the returned pooled document (the tests that need the document add inline).
    private async Task SeedAsync(ISourceStore store, string name, string actor = "alice", SecurityTagSet tags = default)
    {
        using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(name, "openapi", DocUtf8(name), null, null, tags);
        using ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(draft.RootElement, actor, default);
    }

    private async ValueTask<ISourceStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        ISourceStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}