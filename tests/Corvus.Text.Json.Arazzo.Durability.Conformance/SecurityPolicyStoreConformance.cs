// <copyright file="SecurityPolicyStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="ISecurityPolicyStore"/> must satisfy: rule + binding CRUD, optimistic
/// concurrency via etag, and the snapshot/generation token a resolver caches against. A backend's test project
/// derives a concrete <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>; the in-memory
/// store is the reference implementation and runs the same suite.
/// </summary>
public abstract class SecurityPolicyStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Materializes a grant's rule names for assertions (the grant exposes them as a JSON array, not a list).</summary>
    /// <param name="grant">The verb grant.</param>
    /// <returns>The rule names in order.</returns>
    private static List<string> RuleNames(VerbGrant grant)
    {
        var names = new List<string>();
        if (grant.RuleNames.IsNotUndefined())
        {
            foreach (Corvus.Text.Json.Arazzo.Durability.JsonString name in grant.RuleNames.EnumerateArray())
            {
                names.Add((string)name);
            }
        }

        return names;
    }

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ISecurityPolicyStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task A_rule_round_trips_through_add_get_and_list()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        using (ParsedJsonDocument<SecurityRuleDocument> added = await AddRuleDraftAsync(store, "tenant-scoped", "tenant == $claim.tenant", "Tenant isolation.", "alice"))
        {
            added.RootElement.NameValue.ShouldBe("tenant-scoped");
            added.RootElement.ExpressionValue.ShouldBe("tenant == $claim.tenant");
            added.RootElement.DescriptionOrNull.ShouldBe("Tenant isolation.");
            added.RootElement.CreatedByValue.ShouldBe("alice");
            added.RootElement.EtagValue.IsNone.ShouldBeFalse();
        }

        using (ParsedJsonDocument<SecurityRuleDocument>? fetched = await store.GetRuleAsync("tenant-scoped", default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.ExpressionValue.ShouldBe("tenant == $claim.tenant");
        }

        using (PooledDocumentList<SecurityRuleDocument> list = await store.ListRulesAsync(default))
        {
            list.Select(r => r.NameValue).ShouldBe(["tenant-scoped"]);
        }

        (await store.GetRuleAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Adding_a_duplicate_rule_name_fails()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        using (await AddRuleDraftAsync(store, "r", "tenant", null, "alice"))
        {
        }

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await AddRuleDraftAsync(store, "r", "team", null, "bob"));
    }

    [TestMethod]
    public async Task Updating_a_rule_bumps_the_etag_and_records_the_actor()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<SecurityRuleDocument> added = await AddRuleDraftAsync(store, "r", "tenant", null, "alice"))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (ParsedJsonDocument<SecurityRuleDocument>? updated = await UpdateRuleDraftAsync(store, "r", "team", "now team", addedEtag, "bob"))
        {
            updated.ShouldNotBeNull();
            updated!.RootElement.ExpressionValue.ShouldBe("team");
            updated.RootElement.DescriptionOrNull.ShouldBe("now team");
            updated.RootElement.UpdatedByOrNull.ShouldBe("bob");
            (updated.RootElement.EtagValue == addedEtag).ShouldBeFalse();
        }

        (await UpdateRuleDraftAsync(store, "missing", "x", null, WorkflowEtag.None, "bob")).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_rule_update_or_delete_conflicts()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<SecurityRuleDocument> added = await AddRuleDraftAsync(store, "r", "tenant", null, "alice"))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (await UpdateRuleDraftAsync(store, "r", "team", null, addedEtag, "bob"))
        {
            // etag now advanced
        }

        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await UpdateRuleDraftAsync(store, "r", "x", null, addedEtag, "carol"));
        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await store.DeleteRuleAsync("r", addedEtag, default));

        // WorkflowEtag.None overwrites/deletes unconditionally.
        (await store.DeleteRuleAsync("r", WorkflowEtag.None, default)).ShouldBeTrue();
        (await store.DeleteRuleAsync("r", WorkflowEtag.None, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_binding_round_trips_and_lists_in_order()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        string aId;
        using (ParsedJsonDocument<SecurityBindingDocument> a = await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "tenant-admin", VerbGrant.Rules("tenant-scoped"), VerbGrant.Rules("tenant-scoped"), VerbGrant.None, Order: 10),
            "alice",
            default))
        {
            aId = a.RootElement.IdValue;
        }

        string bId;
        using (ParsedJsonDocument<SecurityBindingDocument> b = await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "operator", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full, Order: 5),
            "alice",
            default))
        {
            bId = b.RootElement.IdValue;
        }

        aId.ShouldNotBe(bId);
        using (ParsedJsonDocument<SecurityBindingDocument>? fetched = await store.GetBindingAsync(aId, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.ClaimValueOrNull.ShouldBe("tenant-admin");
            RuleNames(fetched.RootElement.Read).ShouldBe(["tenant-scoped"]);
            RuleNames(fetched.RootElement.Write).ShouldBe(["tenant-scoped"]);
            fetched.RootElement.Purge.IsEmptyValue.ShouldBeTrue();
        }

        // Ordered by Order ascending: operator (5) before tenant-admin (10).
        using (PooledDocumentList<SecurityBindingDocument> list = await store.ListBindingsAsync(default))
        {
            list.Select(x => x.IdValue).ShouldBe([bId, aId]);
        }

        using (ParsedJsonDocument<SecurityBindingDocument>? operatorBinding = await store.GetBindingAsync(bId, default))
        {
            operatorBinding!.RootElement.Read.IsUnrestrictedValue.ShouldBeTrue();
        }
    }

    [TestMethod]
    public async Task A_binding_updates_and_deletes_under_optimistic_concurrency()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        string addedId;
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<SecurityBindingDocument> added = await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "viewer", VerbGrant.Rules("r1"), VerbGrant.None, VerbGrant.None),
            "alice",
            default))
        {
            addedId = added.RootElement.IdValue;
            addedEtag = added.RootElement.EtagValue;
        }

        WorkflowEtag updatedEtag;
        using (ParsedJsonDocument<SecurityBindingDocument>? updated = await store.UpdateBindingAsync(
            addedId,
            new SecurityBindingDefinition("role", "viewer", VerbGrant.Rules("r1", "r2"), VerbGrant.None, VerbGrant.None, Description: "two rules"),
            addedEtag,
            "bob",
            default))
        {
            updated.ShouldNotBeNull();
            RuleNames(updated!.RootElement.Read).ShouldBe(["r1", "r2"]);
            updatedEtag = updated.RootElement.EtagValue;
        }

        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await store.UpdateBindingAsync(addedId, new SecurityBindingDefinition("role", "viewer", VerbGrant.None, VerbGrant.None, VerbGrant.None), addedEtag, "carol", default));

        (await store.DeleteBindingAsync(addedId, updatedEtag, default)).ShouldBeTrue();
        (await store.GetBindingAsync(addedId, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_snapshot_generation_advances_on_every_mutation()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        long g0;
        using (SecurityPolicySnapshot s0 = await store.LoadSnapshotAsync(default))
        {
            g0 = s0.Generation;
        }

        using (await AddRuleDraftAsync(store, "r", "tenant", null, "alice"))
        {
        }

        long g1;
        using (SecurityPolicySnapshot s1 = await store.LoadSnapshotAsync(default))
        {
            g1 = s1.Generation;
        }

        (g1 > g0).ShouldBeTrue();

        using (SecurityPolicySnapshot snapshot = await store.LoadSnapshotAsync(default))
        {
            snapshot.Generation.ShouldBe(g1); // a read does not advance the generation
            snapshot.Rules.Select(r => r.NameValue).ShouldBe(["r"]);
        }

        using (await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "alice", default))
        {
        }

        using (SecurityPolicySnapshot s2 = await store.LoadSnapshotAsync(default))
        {
            (s2.Generation > g1).ShouldBeTrue();
        }
    }

    private async ValueTask<ISecurityPolicyStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        ISecurityPolicyStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }

    // Builds the draft rule (a pooled, disposable document), adds it, and disposes the draft once the store has read it.
    private static async Task<ParsedJsonDocument<SecurityRuleDocument>> AddRuleDraftAsync(ISecurityPolicyStore store, string name, string expression, string? description, string actor)
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft(expression, description);
        return await store.AddRuleAsync(name, draft.RootElement, actor, default);
    }

    private static async Task<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleDraftAsync(ISecurityPolicyStore store, string name, string expression, string? description, WorkflowEtag expectedEtag, string actor)
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft(expression, description);
        return await store.UpdateRuleAsync(name, draft.RootElement, expectedEtag, actor, default);
    }
}
