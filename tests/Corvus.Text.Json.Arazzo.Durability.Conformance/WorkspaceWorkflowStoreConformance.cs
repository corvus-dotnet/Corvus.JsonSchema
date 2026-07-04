// <copyright file="WorkspaceWorkflowStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IWorkspaceWorkflowStore"/> must satisfy (workflow-designer design §4.1):
/// working-copy CRUD keyed by the server-minted <c>id</c>, optimistic concurrency via etag (a stale save conflicts
/// rather than clobbering a collaborator), deterministic keyset list ordering by id, immutable id/provenance/tags/audit
/// on save carried bytes-to-bytes, the Arazzo document round-tripped verbatim and replaced-or-carried-forward on save,
/// and reach-scoped non-disclosing reads. A backend's test project derives a concrete
/// <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>; the in-memory store is the reference
/// implementation and runs the same suite.
/// </summary>
public abstract class WorkspaceWorkflowStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IWorkspaceWorkflowStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task A_working_copy_round_trips_through_add_get_and_list()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        string id;
        using (ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("retry tuning", DocUtf8("v1"), default, "nightly-reconcile", 4, default))
        using (ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(draft.RootElement, "alice", default))
        {
            id = added.RootElement.IdValue;
            id.ShouldNotBeNullOrEmpty();
            ((string)added.RootElement.Name).ShouldBe("retry tuning");
            ((string)added.RootElement.BaseWorkflowId).ShouldBe("nightly-reconcile");
            ((int)added.RootElement.BasedOnVersion).ShouldBe(4);
            ((string)added.RootElement.CreatedBy).ShouldBe("alice");
            DocJson(added.RootElement).ShouldContain("v1");
            added.RootElement.EtagValue.IsNone.ShouldBeFalse();
        }

        using (ParsedJsonDocument<WorkspaceWorkflow>? fetched = await store.GetAsync(id, AccessContext.System, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.IdValue.ShouldBe(id);
            DocJson(fetched.RootElement).ShouldContain("v1"); // the full document comes back on a single read
        }

        using (WorkspaceWorkflowPage page = await store.ListAsync(AccessContext.System, 1000, default, default))
        {
            page.WorkingCopies.Select(w => w.IdValue).ShouldBe([id]);
        }

        (await store.GetAsync("no-such-id", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Every_add_mints_a_distinct_id()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        string first = await this.SeedAsync(store, "one");
        string second = await this.SeedAsync(store, "two");
        first.ShouldNotBe(second);

        using WorkspaceWorkflowPage page = await store.ListAsync(AccessContext.System, 1000, default, default);
        page.WorkingCopies.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task Saving_bumps_the_etag_carries_identity_and_provenance_and_records_the_actor()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        string id;
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<WorkspaceWorkflow> seed = WorkspaceWorkflow.Draft("first cut", DocUtf8("v1"), default, "nightly-reconcile", 4, default))
        using (ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            id = added.RootElement.IdValue;
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("second cut", DocUtf8("v2"), default, null, null, default))
        using (ParsedJsonDocument<WorkspaceWorkflow>? saved = await store.UpdateAsync(id, draft.RootElement, addedEtag, "bob", AccessContext.System, default))
        {
            saved.ShouldNotBeNull();
            saved!.RootElement.IdValue.ShouldBe(id);                                    // immutable identity carried forward
            ((string)saved.RootElement.BaseWorkflowId).ShouldBe("nightly-reconcile");   // provenance carried forward
            ((int)saved.RootElement.BasedOnVersion).ShouldBe(4);
            ((string)saved.RootElement.CreatedBy).ShouldBe("alice");                    // created-* audit carried forward
            ((string)saved.RootElement.Name).ShouldBe("second cut");                    // mutable content updated
            DocJson(saved.RootElement).ShouldContain("v2");
            ((string)saved.RootElement.LastUpdatedBy).ShouldBe("bob");
            saved.RootElement.LastUpdatedAt.IsNotUndefined().ShouldBeTrue();
            (saved.RootElement.EtagValue == addedEtag).ShouldBeFalse();
        }

        using ParsedJsonDocument<WorkspaceWorkflow> missingDraft = WorkspaceWorkflow.Draft("x", DocUtf8("v1"), default, null, null, default);
        (await store.UpdateAsync("no-such-id", missingDraft.RootElement, WorkflowEtag.None, "bob", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_document_is_carried_forward_when_a_save_omits_it_and_replaced_when_it_supplies_one()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        string id;
        WorkflowEtag etag;
        using (ParsedJsonDocument<WorkspaceWorkflow> seed = WorkspaceWorkflow.Draft("wc", DocUtf8("v1"), default, null, null, default))
        using (ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            id = added.RootElement.IdValue;
            etag = added.RootElement.EtagValue;
        }

        // A save that omits the document keeps the stored one (a rename-only edit).
        using (ParsedJsonDocument<WorkspaceWorkflow> renameDraft = WorkspaceWorkflow.Draft("renamed", ReadOnlyMemory<byte>.Empty, default, null, null, default))
        using (ParsedJsonDocument<WorkspaceWorkflow>? renamed = await store.UpdateAsync(id, renameDraft.RootElement, etag, "bob", AccessContext.System, default))
        {
            renamed.ShouldNotBeNull();
            ((string)renamed!.RootElement.Name).ShouldBe("renamed");
            DocJson(renamed.RootElement).ShouldContain("v1"); // carried forward unchanged
            etag = renamed.RootElement.EtagValue;
        }

        // A save that supplies a document replaces it.
        using (ParsedJsonDocument<WorkspaceWorkflow> saveDraft = WorkspaceWorkflow.Draft("renamed", DocUtf8("v2"), default, null, null, default))
        using (ParsedJsonDocument<WorkspaceWorkflow>? saved = await store.UpdateAsync(id, saveDraft.RootElement, etag, "carol", AccessContext.System, default))
        {
            saved.ShouldNotBeNull();
            DocJson(saved!.RootElement).ShouldContain("v2");
            DocJson(saved.RootElement).ShouldNotContain("v1");
        }
    }

    [TestMethod]
    public async Task A_stale_etag_on_save_or_delete_conflicts()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        string id;
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<WorkspaceWorkflow> seed = WorkspaceWorkflow.Draft("wc", DocUtf8("v1"), default, null, null, default))
        using (ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(seed.RootElement, "alice", default))
        {
            id = added.RootElement.IdValue;
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("wc", DocUtf8("v2"), default, null, null, default))
        using (await store.UpdateAsync(id, draft.RootElement, addedEtag, "bob", AccessContext.System, default))
        {
            // etag now advanced — bob's save landed first
        }

        using ParsedJsonDocument<WorkspaceWorkflow> stale = WorkspaceWorkflow.Draft("wc", DocUtf8("v3"), default, null, null, default);
        await Should.ThrowAsync<WorkspaceWorkflowConflictException>(async () =>
            await store.UpdateAsync(id, stale.RootElement, addedEtag, "carol", AccessContext.System, default));
        await Should.ThrowAsync<WorkspaceWorkflowConflictException>(async () =>
            await store.DeleteAsync(id, addedEtag, AccessContext.System, default));

        // WorkflowEtag.None deletes unconditionally.
        (await store.DeleteAsync(id, WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();
        (await store.DeleteAsync(id, WorkflowEtag.None, AccessContext.System, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Listing_keyset_pages_in_id_order_without_gaps_or_duplicates()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        var ids = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            ids.Add(await this.SeedAsync(store, $"wc {i}"));
        }

        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using WorkspaceWorkflowPage page = await store.ListAsync(AccessContext.System, 3, tokenDoc?.RootElement ?? default, default);
            page.WorkingCopies.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (WorkspaceWorkflow w in page.WorkingCopies)
            {
                seen.Add(w.IdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 8 items, 3 per page → 3 pages; no duplicates or gaps; contractual id order.
        pages.ShouldBe(3);
        seen.ShouldBe(ids.OrderBy(i => i, StringComparer.Ordinal).ToArray());

        // A malformed token is rejected (rather than silently restarting).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<JsonString> badToken = AsPageToken("this~is~not~a~token"u8);
            using WorkspaceWorkflowPage bad = await store.ListAsync(AccessContext.System, 3, badToken.RootElement, default);
        });
    }

    [TestMethod]
    public async Task Security_tags_round_trip_and_are_immutable_on_save()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();
        string id;
        WorkflowEtag etag;
        using (ParsedJsonDocument<WorkspaceWorkflow> seed = WorkspaceWorkflow.Draft("wc", DocUtf8("v1"), default, null, null, Tenant("acme")))
        using (ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(seed.RootElement, "system", default))
        {
            id = added.RootElement.IdValue;
            added.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
            etag = added.RootElement.EtagValue;
        }

        // A save NEVER re-tags — the reach scope is immutable and carried forward bytes-to-bytes, even when the draft
        // smuggles a different tag set.
        using (ParsedJsonDocument<WorkspaceWorkflow> smuggled = WorkspaceWorkflow.Draft("wc", DocUtf8("v2"), default, null, null, Tenant("globex")))
        using (ParsedJsonDocument<WorkspaceWorkflow>? saved = await store.UpdateAsync(id, smuggled.RootElement, etag, "bob", AccessContext.System, default))
        {
            saved.ShouldNotBeNull();
            saved!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
        }
    }

    [TestMethod]
    public async Task Reads_and_writes_are_reach_filtered_and_non_disclosing()
    {
        IWorkspaceWorkflowStore store = await this.NewStoreAsync();

        string acmeId = await this.SeedAsync(store, "acme wc", "system", Tenant("acme"));
        string globexId = await this.SeedAsync(store, "globex wc", "system", Tenant("globex"));

        AccessContext acme = Scope("acme");

        // acme sees its own working copy; globex's is invisible (non-disclosing).
        (await store.GetAsync(acmeId, acme, default)).ShouldNotBeNull().Dispose();
        (await store.GetAsync(globexId, acme, default)).ShouldBeNull();

        using (WorkspaceWorkflowPage page = await store.ListAsync(acme, 1000, default, default))
        {
            page.WorkingCopies.Select(w => w.IdValue).ShouldBe([acmeId]);
        }

        // acme cannot save or delete globex's working copy — reported as absent, not forbidden.
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("hijack", DocUtf8("v2"), default, null, null, default);
        (await store.UpdateAsync(globexId, draft.RootElement, WorkflowEtag.None, "mallory", acme, default)).ShouldBeNull();
        (await store.DeleteAsync(globexId, WorkflowEtag.None, acme, default)).ShouldBeFalse();
        (await store.GetAsync(globexId, Scope("globex"), default)).ShouldNotBeNull().Dispose();
    }

    // A minimal, marker-bearing Arazzo document for round-trip / replacement assertions.
    private static ReadOnlyMemory<byte> DocUtf8(string marker)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","x-marker":"{{marker}}"}""");

    // Serializes a working copy's Arazzo document back to a JSON string (the raw stored bytes) for content assertions.
    private static string DocJson(WorkspaceWorkflow workingCopy)
        => Encoding.UTF8.GetString(JsonMarshal.GetRawUtf8Value(workingCopy.Document).Memory.Span);

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

    // Seeds a working copy, returning its minted id (the pooled document is disposed here).
    private async Task<string> SeedAsync(IWorkspaceWorkflowStore store, string name, string actor = "alice", SecurityTagSet tags = default)
    {
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft(name, DocUtf8(name), default, null, null, tags);
        using ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(draft.RootElement, actor, default);
        return added.RootElement.IdValue;
    }

    private async ValueTask<IWorkspaceWorkflowStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IWorkspaceWorkflowStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}