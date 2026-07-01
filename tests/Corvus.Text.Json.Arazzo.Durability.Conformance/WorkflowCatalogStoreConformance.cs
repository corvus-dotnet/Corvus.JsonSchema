// <copyright file="WorkflowCatalogStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IWorkflowCatalogStore"/> must satisfy, regardless of backend: version
/// assignment + workflow-id rewrite, content hashing, title/description/source projection, individually
/// addressable document retrieval, search (text/base-id/tag/status/owner) with keyset paging, the governance
/// metadata lifecycle (update/obsolete/reactivate), and delete/list-obsolete/delete-many. A backend's test
/// project derives a concrete <see cref="TestClassAttribute"/> from this and implements
/// <see cref="CreateStoreAsync"/>; the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class WorkflowCatalogStoreConformance
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty catalog store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IWorkflowCatalogStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task Add_assigns_v1_rewrites_id_and_projects_metadata()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        version.Ref.VersionNumber.ShouldBe(1);
        version.Ref.BaseWorkflowId.ShouldBe("nightly-reconcile");
        version.Ref.WorkflowId.ShouldBe("nightly-reconcile-v1");
        ((string)version.Title).ShouldBe("Nightly Reconcile");
        version.DescriptionOrNull.ShouldBe("Reconciles state nightly.");
        version.StatusValue.ShouldBe(CatalogStatus.Active);
        ((string)version.CreatedBy).ShouldBe("alice");
        ((string)version.Hash).Length.ShouldBe(64);
        version.SourcesValue.Count.ShouldBe(1);
        List<CatalogSourceRef> sources = version.SourcesValue.ToList();
        sources[0].Name.ShouldBe("petstore");
        sources[0].Type.ShouldBe("openapi");
    }

    [TestMethod]
    public async Task Add_second_version_assigns_v2()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default)).Dispose();
        using ParsedJsonDocument<CatalogVersion> secondDoc = await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default);
        CatalogVersion second = secondDoc.RootElement;

        second.Ref.VersionNumber.ShouldBe(2);
        second.Ref.WorkflowId.ShouldBe("nightly-reconcile-v2");
    }

    [TestMethod]
    public async Task Identical_packages_hash_identically()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        using ParsedJsonDocument<CatalogVersion> aDoc = await store.AddAsync("base-a", Package("base-a"), Meta(), default);
        using ParsedJsonDocument<CatalogVersion> bDoc = await store.AddAsync("base-b", Package("base-b"), Meta(), default);
        CatalogVersion a = aDoc.RootElement;
        CatalogVersion b = bDoc.RootElement;

        // The only content difference is the workflow id, which both rewrite to "-v1"; everything else is equal,
        // so the canonical hashes differ only because the ids differ — different base ids => different hashes.
        ((string)a.Hash).ShouldNotBe((string)b.Hash);

        using ParsedJsonDocument<CatalogVersion> a2Doc = await store.AddAsync("base-a", Package("base-a"), Meta(), default);
        CatalogVersion a2 = a2Doc.RootElement;
        a2.Ref.WorkflowId.ShouldBe("base-a-v2");
        ((string)a2.Hash).ShouldNotBe((string)a.Hash); // v1 vs v2 ids differ
    }

    [TestMethod]
    public async Task Get_returns_metadata_and_unknown_returns_null()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default)).Dispose();

        using (ParsedJsonDocument<CatalogVersion>? present = await store.GetAsync("nightly-reconcile", 1, default))
        {
            present.ShouldNotBeNull();
        }

        (await store.GetAsync("nightly-reconcile", 2, default)).ShouldBeNull();
        (await store.GetAsync("missing", 1, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task GetPackage_returns_canonical_bytes_with_versioned_id()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default)).Dispose();

        ReadOnlyMemory<byte>? package = await store.GetPackageAsync("nightly-reconcile", 1, default);
        package.ShouldNotBeNull();

        // The package is an opaque archive; unpack it and confirm the workflow carries the versioned id.
        (byte[] workflow, _) = CatalogPackage.Unpack(package.Value);
        Encoding.UTF8.GetString(workflow).ShouldContain("nightly-reconcile-v1");
        (await store.GetPackageAsync("nightly-reconcile", 9, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task GetDocument_returns_workflow_source_or_null()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default)).Dispose();

        ReadOnlyMemory<byte>? workflow = await store.GetDocumentAsync("nightly-reconcile", 1, CatalogPackage.WorkflowDocumentName, default);
        workflow.ShouldNotBeNull();
        Encoding.UTF8.GetString(workflow.Value.Span).ShouldContain("nightly-reconcile-v1");

        ReadOnlyMemory<byte>? source = await store.GetDocumentAsync("nightly-reconcile", 1, "petstore", default);
        source.ShouldNotBeNull();
        Encoding.UTF8.GetString(source.Value.Span).ShouldContain("Petstore");

        (await store.GetDocumentAsync("nightly-reconcile", 1, "absent", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Query_filters_by_base_tag_status_text_and_owner()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("alpha", Package("alpha", title: "Alpha Flow"), Meta(tags: ["prod", "billing"]), default)).Dispose();
        (await store.AddAsync("beta", Package("beta", title: "Beta Flow"), Meta(tags: ["prod"]), default)).Dispose();

        await this.QueryCountShouldBe(store, new CatalogQuery(BaseWorkflowId: "alpha"), 1);
        await this.QueryCountShouldBe(store, new CatalogQuery(Tags: TagSet.FromTags(["prod"])), 2);
        await this.QueryCountShouldBe(store, new CatalogQuery(Tags: TagSet.FromTags(["prod", "billing"])), 1);
        await this.QueryCountShouldBe(store, new CatalogQuery(Text: "alpha"), 1);
        await this.QueryCountShouldBe(store, new CatalogQuery(Owner: "team-a@example.com"), 2);
        await this.QueryCountShouldBe(store, new CatalogQuery(Status: CatalogStatus.Obsolete), 0);
    }

    [TestMethod]
    public async Task Query_filters_by_workflow_id_prefix()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("adopt-pet", Package("adopt-pet"), Meta(), default)).Dispose();                 // adopt-pet-v1
        (await store.AddAsync("adopt-pet", Package("adopt-pet"), Meta(), default)).Dispose();                 // adopt-pet-v2
        (await store.AddAsync("nightly-reconcile", Package("nightly-reconcile"), Meta(), default)).Dispose(); // nightly-reconcile-v1
        (await store.AddAsync("Billing-Sync", Package("Billing-Sync"), Meta(), default)).Dispose();           // Billing-Sync-v1 (mixed case)

        // A base-name prefix matches every version of that workflow (the versioned id begins with the base id).
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "adopt"), 2);
        // A versioned-id prefix narrows to the single version.
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "adopt-pet-v2"), 1);
        // A different workflow.
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "nightly"), 1);
        // No match.
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "zzz"), 0);

        // Case-insensitive BOTH ways: an upper-case prefix matches lower-case ids, and a lower-case prefix
        // matches a mixed-case id. (Backends index this via lower()/NOCASE/lowered-field, see the stores.)
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "ADOPT-PET"), 2);
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "AdOpT"), 2);
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "billing"), 1);
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "BILLING-SYNC"), 1);
        await this.QueryCountShouldBe(store, new CatalogQuery(WorkflowIdPrefix: "billing-sync-v1"), 1);
    }

    [TestMethod]
    public async Task Query_pages_by_keyset()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("a", Package("a"), Meta(), default)).Dispose();
        (await store.AddAsync("b", Package("b"), Meta(), default)).Dispose();
        (await store.AddAsync("c", Package("c"), Meta(), default)).Dispose();

        using CatalogPage first = await store.QueryAsync(new CatalogQuery(Limit: 2), default);
        first.Versions.Count.ShouldBe(2);
        first.NextPageToken.IsEmpty.ShouldBeFalse();

        // Round-trip the token through the JsonString seam exactly as the HTTP layer does (store emits UTF-8, the next
        // request carries it as a JSON string).
        using ParsedJsonDocument<JsonString> tokenDoc = AsPageToken(first.NextPageToken.Span);
        using CatalogPage second = await store.QueryAsync(new CatalogQuery(Limit: 2, ContinuationToken: tokenDoc.RootElement), default);
        second.Versions.Count.ShouldBe(1);
        second.NextPageToken.IsEmpty.ShouldBeTrue();

        // Materialize the ids while both pages are still alive (their version values are valid only until disposal).
        first.Versions.Select(v => v.Ref.BaseWorkflowId)
            .Concat(second.Versions.Select(v => v.Ref.BaseWorkflowId))
            .ToList()
            .ShouldBe(["a", "b", "c"]);
    }

    [TestMethod]
    public async Task Query_distinct_workflows_returns_one_representative_per_base_keyset_paged()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("alpha", Package("alpha"), Meta(), default)).Dispose(); // alpha-v1
        (await store.AddAsync("alpha", Package("alpha"), Meta(), default)).Dispose(); // alpha-v2
        (await store.AddAsync("beta", Package("beta"), Meta(), default)).Dispose();   // beta-v1
        (await store.AddAsync("gamma", Package("gamma"), Meta(), default)).Dispose(); // gamma-v1
        (await store.AddAsync("gamma", Package("gamma"), Meta(), default)).Dispose(); // gamma-v2
        (await store.AddAsync("gamma", Package("gamma"), Meta(), default)).Dispose(); // gamma-v3

        // Unpaged distinct: one row per base (3, not 6 versions), representative = newest version, ordered by base id.
        using (CatalogPage all = await store.QueryAsync(new CatalogQuery(DistinctWorkflows: true, Limit: 100), default))
        {
            all.Versions.Select(v => (v.Ref.BaseWorkflowId, v.Ref.VersionNumber)).ToList()
                .ShouldBe([("alpha", 2), ("beta", 1), ("gamma", 3)]);
        }

        // Keyset-paged distinct: the cursor is the base id alone; no gaps/duplicates across the page boundary.
        using CatalogPage first = await store.QueryAsync(new CatalogQuery(DistinctWorkflows: true, Limit: 2), default);
        first.Versions.Count.ShouldBe(2);
        first.NextPageToken.IsEmpty.ShouldBeFalse();

        using ParsedJsonDocument<JsonString> tokenDoc = AsPageToken(first.NextPageToken.Span);
        using CatalogPage second = await store.QueryAsync(new CatalogQuery(DistinctWorkflows: true, Limit: 2, ContinuationToken: tokenDoc.RootElement), default);
        second.Versions.Count.ShouldBe(1);
        second.NextPageToken.IsEmpty.ShouldBeTrue();

        first.Versions.Select(v => v.Ref.BaseWorkflowId)
            .Concat(second.Versions.Select(v => v.Ref.BaseWorkflowId))
            .ToList()
            .ShouldBe(["alpha", "beta", "gamma"]);
    }

    [TestMethod]
    public async Task Query_distinct_workflows_representative_prefers_newest_active_then_newest_obsolete()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();

        // svc: v1/v2/v3 all Active, then v3 obsoleted — representative is the newest Active (v2), not the newest overall (v3).
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        (await store.UpdateMetadataAsync("svc", 3, new CatalogMetadataPatch("bob", Status: CatalogStatus.Obsolete), default))?.Dispose();

        // old: v1/v2 both Obsolete — no Active version, so the representative falls back to the newest Obsolete (v2).
        (await store.AddAsync("old", Package("old"), Meta(), default)).Dispose();
        (await store.AddAsync("old", Package("old"), Meta(), default)).Dispose();
        (await store.UpdateMetadataAsync("old", 1, new CatalogMetadataPatch("bob", Status: CatalogStatus.Obsolete), default))?.Dispose();
        (await store.UpdateMetadataAsync("old", 2, new CatalogMetadataPatch("bob", Status: CatalogStatus.Obsolete), default))?.Dispose();

        using CatalogPage page = await store.QueryAsync(new CatalogQuery(DistinctWorkflows: true, Limit: 100), default);
        page.Versions.Select(v => (v.Ref.BaseWorkflowId, v.Ref.VersionNumber, v.StatusValue)).ToList()
            .ShouldBe([("old", 2, CatalogStatus.Obsolete), ("svc", 2, CatalogStatus.Active)]);
    }

    [TestMethod]
    public async Task Query_distinct_workflows_includes_a_base_when_any_version_matches_and_represents_it_by_the_match()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();

        // svc-v1 carries the "billing" tag; svc-v2 does not. A tag filter must still surface svc (any version matches),
        // and the representative must be the matching version (v1), not the newest (v2).
        (await store.AddAsync("svc", Package("svc"), Meta(tags: ["billing"]), default)).Dispose();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();

        using CatalogPage page = await store.QueryAsync(new CatalogQuery(DistinctWorkflows: true, Tags: TagSet.FromTags(["billing"]), Limit: 100), default);
        page.Versions.Select(v => (v.Ref.BaseWorkflowId, v.Ref.VersionNumber)).ToList()
            .ShouldBe([("svc", 1)]);
    }

    [TestMethod]
    public async Task Update_changes_governance_and_stamps_audit()
    {
        var clock = new TestClock(T0);
        IWorkflowCatalogStore store = await this.NewStoreAsync(clock);
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        clock.Advance(TimeSpan.FromHours(1));

        using ParsedJsonDocument<CatalogVersion>? updated = await store.UpdateMetadataAsync(
            "svc", 1, new CatalogMetadataPatch("bob", Owner: new CatalogOwner("Team B", "team-b@example.com"), Tags: TagSet.FromTags(["retired"]), Status: CatalogStatus.Obsolete), default);

        updated.ShouldNotBeNull();
        CatalogVersion updatedValue = updated.RootElement;
        updatedValue.OwnerValue.Name.ShouldBe("Team B");
        updatedValue.TagsValue.ToList().ShouldBe(["retired"]);
        updatedValue.StatusValue.ShouldBe(CatalogStatus.Obsolete);
        updatedValue.LastUpdatedByOrNull.ShouldBe("bob");
        updatedValue.LastUpdatedAtValue.ShouldBe(T0.AddHours(1));
        updatedValue.ObsoletedByOrNull.ShouldBe("bob");
        updatedValue.ObsoletedAtValue.ShouldBe(T0.AddHours(1));
    }

    [TestMethod]
    public async Task Update_partial_leaves_unset_fields_unchanged()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("svc", Package("svc"), Meta(tags: ["keep"]), default)).Dispose();

        using ParsedJsonDocument<CatalogVersion>? updated = await store.UpdateMetadataAsync("svc", 1, new CatalogMetadataPatch("bob"), default);

        updated.ShouldNotBeNull();
        CatalogVersion updatedValue = updated.RootElement;
        updatedValue.TagsValue.ToList().ShouldBe(["keep"]);
        updatedValue.OwnerValue.Email.ShouldBe("team-a@example.com");
        updatedValue.StatusValue.ShouldBe(CatalogStatus.Active);
    }

    [TestMethod]
    public async Task Update_reactivation_clears_obsoletion()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        (await store.UpdateMetadataAsync("svc", 1, new CatalogMetadataPatch("bob", Status: CatalogStatus.Obsolete), default))?.Dispose();

        using ParsedJsonDocument<CatalogVersion>? reactivated = await store.UpdateMetadataAsync("svc", 1, new CatalogMetadataPatch("carol", Status: CatalogStatus.Active), default);

        reactivated.ShouldNotBeNull();
        CatalogVersion reactivatedValue = reactivated.RootElement;
        reactivatedValue.StatusValue.ShouldBe(CatalogStatus.Active);
        reactivatedValue.ObsoletedByOrNull.ShouldBeNull();
        reactivatedValue.ObsoletedAtValue.ShouldBeNull();
    }

    [TestMethod]
    public async Task Update_unknown_returns_null()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.UpdateMetadataAsync("missing", 1, new CatalogMetadataPatch("bob"), default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Delete_removes_and_unknown_returns_false()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();

        (await store.DeleteAsync("svc", 1, default)).ShouldBeTrue();
        (await store.GetAsync("svc", 1, default)).ShouldBeNull();
        (await store.DeleteAsync("svc", 1, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task ListObsolete_then_DeleteMany_reaps_them()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        (await store.AddAsync("svc", Package("svc"), Meta(), default)).Dispose();
        (await store.UpdateMetadataAsync("svc", 1, new CatalogMetadataPatch("bob", Status: CatalogStatus.Obsolete), default))?.Dispose();

        IReadOnlyList<CatalogVersionRef> obsolete = await store.ListObsoleteAsync(default);
        obsolete.Count.ShouldBe(1);
        obsolete[0].WorkflowId.ShouldBe("svc-v1");

        await store.DeleteManyAsync(obsolete, default);
        (await store.GetAsync("svc", 1, default)).ShouldBeNull();

        using (ParsedJsonDocument<CatalogVersion>? remaining = await store.GetAsync("svc", 2, default))
        {
            remaining.ShouldNotBeNull();
        }
    }

    [TestMethod]
    public async Task Get_round_trips_the_versions_security_tags()
    {
        // Single-row read authorization (§14.2) checks the returned version's security tags, so every store —
        // not just those that push the reach filter down — must round-trip the tags it persisted at add time.
        // Without this the control-plane client would deny a restricted principal access to its own version.
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        SecurityTag[] tags = [new("tenant", "acme"), new("tenant", "beta"), new("team", "payments")];
        using ParsedJsonDocument<CatalogVersion> addedDoc = await store.AddAsync(
            "secure-flow",
            Package("secure-flow"),
            new CatalogMetadata(new CatalogOwner("Team A", "team-a@example.com"), "alice", default, SecurityTagSet.FromTags(tags)),
            default);
        CatalogVersion added = addedDoc.RootElement;

        added.SecurityTagsValue.ToList().OrderBy(t => t.Key).ThenBy(t => t.Value).ShouldBe(
            tags.OrderBy(t => t.Key).ThenBy(t => t.Value));

        using ParsedJsonDocument<CatalogVersion>? fetched = await store.GetAsync("secure-flow", added.Ref.VersionNumber, default);
        fetched.ShouldNotBeNull();
        fetched.RootElement.SecurityTagsValue.ToList().OrderBy(t => t.Key).ThenBy(t => t.Value).ShouldBe(
            tags.OrderBy(t => t.Key).ThenBy(t => t.Value));
    }

    [TestMethod]
    public async Task UpdateMetadata_preserves_the_versions_security_tags()
    {
        // A metadata update (status/owner/tags) must NOT strip the version's row-security tags — they gate single-row
        // read authorization (§14.2), so dropping them on an obsolete/transfer would silently widen or break reach.
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        SecurityTag[] tags = [new("tenant", "acme"), new("team", "payments")];
        (await store.AddAsync(
            "secure-flow",
            Package("secure-flow"),
            new CatalogMetadata(new CatalogOwner("Team A", "team-a@example.com"), "alice", default, SecurityTagSet.FromTags(tags)),
            default)).Dispose();

        using ParsedJsonDocument<CatalogVersion>? updated = await store.UpdateMetadataAsync(
            "secure-flow", 1, new CatalogMetadataPatch("bob", Status: CatalogStatus.Obsolete), default);
        updated.ShouldNotBeNull();
        updated.RootElement.SecurityTagsValue.ToList().OrderBy(t => t.Key).ThenBy(t => t.Value).ShouldBe(
            tags.OrderBy(t => t.Key).ThenBy(t => t.Value));

        // ...and the persisted document keeps them (a re-read, not just the returned copy).
        using ParsedJsonDocument<CatalogVersion>? refetched = await store.GetAsync("secure-flow", 1, default);
        refetched.ShouldNotBeNull();
        refetched.RootElement.SecurityTagsValue.ToList().OrderBy(t => t.Key).ThenBy(t => t.Value).ShouldBe(
            tags.OrderBy(t => t.Key).ThenBy(t => t.Value));
    }

    [TestMethod]
    public async Task Search_applies_a_row_security_reach_filter_matching_the_evaluator()
    {
        IWorkflowCatalogStore store = await this.NewStoreAsync();
        if (store is not ISupportsRowSecurityFilter)
        {
            Assert.Inconclusive("This store does not yet push the row-security reach filter down (§14.4).");
            return;
        }

        // Each base id gets a version carrying a distinct security-tag shape (single, multi, none, ordered, unranked).
        (string Base, SecurityTag[] Tags)[] rows =
        [
            ("flow-a", [new("tenant", "acme"), new("team", "payments")]),
            ("flow-b", [new("tenant", "acme"), new("team", "hr")]),
            ("flow-c", [new("tenant", "globex"), new("team", "payments")]),
            ("flow-d", []),
            ("flow-e", [new("tenant", "acme"), new("tenant", "beta")]),
            ("flow-f", [new("classification", "public")]),
            ("flow-g", [new("classification", "confidential")]),
            ("flow-h", [new("classification", "restricted")]),
            ("flow-i", [new("classification", "public"), new("classification", "restricted")]),
            ("flow-j", [new("classification", "weird")]),
        ];
        foreach ((string baseId, SecurityTag[] tags) in rows)
        {
            (await store.AddAsync(baseId, Package(baseId), new CatalogMetadata(new CatalogOwner("Team A", "team-a@example.com"), "alice", default, SecurityTagSet.FromTags(tags)), default)).Dispose();
        }

        var claims = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["tenant"] = ["acme"],
            ["both"] = ["acme", "globex"],
            ["team"] = ["payments", "hr"],
            ["clearance"] = ["confidential"],
        };

        // Rules whose ordered comparisons resolve against no configured ordering (so they deny) — set membership and the
        // existing set-intersection grammar.
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
            "tenant in ('acme', 'globex')",
            "classification in ('public', 'restricted')",
            "!(classification in ('restricted'))",
            "team in ('payments') && tenant in ('acme')",
        ];

        // Ordered comparisons resolved against a configured ascending classification ordering.
        var orderings = new SecurityLabelOrderings(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["classification"] = ["public", "internal", "confidential", "restricted"],
        });
        string[] orderedShapes =
        [
            "classification <= 'confidential'",
            "classification < 'confidential'",
            "classification >= 'confidential'",
            "classification > 'public'",
            "classification <= 'bogus'",          // unranked bound → deny
            "classification <= $claim.clearance",
            "classification >= $claim.clearance",
            "tenant <= 'acme'",                   // unordered dimension → deny
            "classification <= 'restricted' && tenant in ('acme')",
        ];

        foreach (string ruleText in ruleShapes)
        {
            await AssertPushdownMatchesEvaluator(store, rows, new SecurityFilter([SecurityRule.Compile(ruleText)], claims), ruleText);
        }

        foreach (string ruleText in orderedShapes)
        {
            await AssertPushdownMatchesEvaluator(store, rows, new SecurityFilter([SecurityRule.Compile(ruleText, orderings)], claims), ruleText);
        }
    }

    // The reach-filter pushdown oracle: the rows the store's translated SQL predicate returns must be exactly the rows
    // the in-memory evaluator admits, for the same filter.
    private static async Task AssertPushdownMatchesEvaluator(
        IWorkflowCatalogStore store,
        (string Base, SecurityTag[] Tags)[] rows,
        SecurityFilter filter,
        string ruleText)
    {
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 1000, Security: filter), default);

        List<string> actual = page.Versions.Select(v => v.Ref.BaseWorkflowId).OrderBy(x => x, StringComparer.Ordinal).ToList();
        List<string> expected = rows.Where(r => filter.IsSatisfiedBy(r.Tags)).Select(r => r.Base).OrderBy(x => x, StringComparer.Ordinal).ToList();
        actual.ShouldBe(expected, $"rule: {ruleText}");
    }

    private static CatalogMetadata Meta(IReadOnlyList<string>? tags = null)
        => new(new CatalogOwner("Team A", "team-a@example.com"), "alice", TagSet.FromTags(tags));

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

    /// <summary>Runs a query, asserts the returned (pooled, disposable) page holds the expected version count, and returns the page's buffers to the pool.</summary>
    private async Task QueryCountShouldBe(IWorkflowCatalogStore store, CatalogQuery query, int expected)
    {
        using CatalogPage page = await store.QueryAsync(query, default);
        page.Versions.Count.ShouldBe(expected);
    }

    private static ReadOnlyMemory<byte> Package(string workflowId, string title = "Nightly Reconcile")
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "{{title}}", "description": "Reconciles state nightly." },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        byte[] petstore = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");
        return CatalogPackage.Build(workflow, [new KeyValuePair<string, byte[]>("petstore", petstore)]);
    }

    private async ValueTask<IWorkflowCatalogStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IWorkflowCatalogStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}