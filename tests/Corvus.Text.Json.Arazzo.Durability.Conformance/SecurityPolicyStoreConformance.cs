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
        SecurityRuleDocument added = await store.AddRuleAsync("tenant-scoped", new SecurityRuleDefinition("tenant == $claim.tenant", "Tenant isolation."), "alice", default);

        added.NameValue.ShouldBe("tenant-scoped");
        added.ExpressionValue.ShouldBe("tenant == $claim.tenant");
        added.DescriptionOrNull.ShouldBe("Tenant isolation.");
        added.CreatedByValue.ShouldBe("alice");
        added.EtagValue.IsNone.ShouldBeFalse();

        SecurityRuleDocument? fetched = await store.GetRuleAsync("tenant-scoped", default);
        fetched.ShouldNotBeNull();
        fetched.Value.ExpressionValue.ShouldBe("tenant == $claim.tenant");

        (await store.ListRulesAsync(default)).Select(r => r.NameValue).ShouldBe(["tenant-scoped"]);
        (await store.GetRuleAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Adding_a_duplicate_rule_name_fails()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        await store.AddRuleAsync("r", new SecurityRuleDefinition("tenant"), "alice", default);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.AddRuleAsync("r", new SecurityRuleDefinition("team"), "bob", default));
    }

    [TestMethod]
    public async Task Updating_a_rule_bumps_the_etag_and_records_the_actor()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        SecurityRuleDocument added = await store.AddRuleAsync("r", new SecurityRuleDefinition("tenant"), "alice", default);

        SecurityRuleDocument? updated = await store.UpdateRuleAsync("r", new SecurityRuleDefinition("team", "now team"), added.EtagValue, "bob", default);
        updated.ShouldNotBeNull();
        updated.Value.ExpressionValue.ShouldBe("team");
        updated.Value.DescriptionOrNull.ShouldBe("now team");
        updated.Value.UpdatedByOrNull.ShouldBe("bob");
        (updated.Value.EtagValue == added.EtagValue).ShouldBeFalse();

        (await store.UpdateRuleAsync("missing", new SecurityRuleDefinition("x"), WorkflowEtag.None, "bob", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_rule_update_or_delete_conflicts()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        SecurityRuleDocument added = await store.AddRuleAsync("r", new SecurityRuleDefinition("tenant"), "alice", default);
        await store.UpdateRuleAsync("r", new SecurityRuleDefinition("team"), added.EtagValue, "bob", default); // etag now advanced

        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await store.UpdateRuleAsync("r", new SecurityRuleDefinition("x"), added.EtagValue, "carol", default));
        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await store.DeleteRuleAsync("r", added.EtagValue, default));

        // WorkflowEtag.None overwrites/deletes unconditionally.
        (await store.DeleteRuleAsync("r", WorkflowEtag.None, default)).ShouldBeTrue();
        (await store.DeleteRuleAsync("r", WorkflowEtag.None, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_binding_round_trips_and_lists_in_order()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        SecurityBindingDocument a = await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "tenant-admin", VerbGrant.Rules("tenant-scoped"), VerbGrant.Rules("tenant-scoped"), VerbGrant.None, Order: 10),
            "alice",
            default);
        SecurityBindingDocument b = await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "operator", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full, Order: 5),
            "alice",
            default);

        a.IdValue.ShouldNotBe(b.IdValue);
        SecurityBindingDocument? fetched = await store.GetBindingAsync(a.IdValue, default);
        fetched.ShouldNotBeNull();
        fetched.Value.ClaimValueOrNull.ShouldBe("tenant-admin");
        fetched.Value.Read.RuleNameList.ShouldBe(["tenant-scoped"]);
        fetched.Value.Write.RuleNameList.ShouldBe(["tenant-scoped"]);
        fetched.Value.Purge.IsEmptyValue.ShouldBeTrue();

        // Ordered by Order ascending: operator (5) before tenant-admin (10).
        (await store.ListBindingsAsync(default)).Select(x => x.IdValue).ShouldBe([b.IdValue, a.IdValue]);

        SecurityBindingDocument? operatorBinding = await store.GetBindingAsync(b.IdValue, default);
        operatorBinding!.Value.Read.IsUnrestrictedValue.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_binding_updates_and_deletes_under_optimistic_concurrency()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        SecurityBindingDocument added = await store.AddBindingAsync(
            new SecurityBindingDefinition("role", "viewer", VerbGrant.Rules("r1"), VerbGrant.None, VerbGrant.None),
            "alice",
            default);

        SecurityBindingDocument? updated = await store.UpdateBindingAsync(
            added.IdValue,
            new SecurityBindingDefinition("role", "viewer", VerbGrant.Rules("r1", "r2"), VerbGrant.None, VerbGrant.None, Description: "two rules"),
            added.EtagValue,
            "bob",
            default);
        updated.ShouldNotBeNull();
        updated.Value.Read.RuleNameList.ShouldBe(["r1", "r2"]);

        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await store.UpdateBindingAsync(added.IdValue, new SecurityBindingDefinition("role", "viewer", VerbGrant.None, VerbGrant.None, VerbGrant.None), added.EtagValue, "carol", default));

        (await store.DeleteBindingAsync(added.IdValue, updated.Value.EtagValue, default)).ShouldBeTrue();
        (await store.GetBindingAsync(added.IdValue, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_snapshot_generation_advances_on_every_mutation()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        long g0 = (await store.LoadSnapshotAsync(default)).Generation;

        await store.AddRuleAsync("r", new SecurityRuleDefinition("tenant"), "alice", default);
        long g1 = (await store.LoadSnapshotAsync(default)).Generation;
        (g1 > g0).ShouldBeTrue();

        SecurityPolicySnapshot snapshot = await store.LoadSnapshotAsync(default);
        snapshot.Generation.ShouldBe(g1); // a read does not advance the generation
        snapshot.Rules.Select(r => r.NameValue).ShouldBe(["r"]);

        await store.AddBindingAsync(new SecurityBindingDefinition("*", null, VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "alice", default);
        ((await store.LoadSnapshotAsync(default)).Generation > g1).ShouldBeTrue();
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
}
