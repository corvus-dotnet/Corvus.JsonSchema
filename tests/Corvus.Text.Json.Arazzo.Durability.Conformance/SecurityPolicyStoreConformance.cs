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
    public async Task Listing_rules_keyset_pages_in_name_order_without_gaps_or_duplicates()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        string[] names =
        [
            "tenant-scoped", "abac-superset", "shares-a-label", "team-scoped",
            "viewer", "admin", "level-secret", "ordered-clearance",
        ];

        // Add out of order, to prove the store (not insertion order) establishes the page order.
        foreach (string name in names.OrderByDescending(n => n, StringComparer.Ordinal))
        {
            using (await AddRuleDraftAsync(store, name, $"tenant == {name}", null, "system"))
            {
            }
        }

        // Walk every page via the continuation token with a small limit; collect the names in page order. The token is
        // round-tripped through the JsonString seam exactly as the HTTP layer does: the store emits it as UTF-8, which the
        // next request carries as a JSON string. The page owns the token buffer (freed on dispose), so copy it out per page.
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using SecurityRulePage page = await store.ListRulesAsync(3, tokenDoc?.RootElement ?? default, default, default);
            page.Rules.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (SecurityRuleDocument r in page.Rules)
            {
                seen.Add(r.NameValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 8 rules, 3 per page → 3 pages; no duplicates or gaps across boundaries; contractual name (ordinal) order.
        pages.ShouldBe(3);
        seen.ShouldBe(names.OrderBy(n => n, StringComparer.Ordinal).ToArray());

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> badToken = AsJsonString("this~is~not~a~token"u8);
            using SecurityRulePage bad = await store.ListRulesAsync(3, badToken.RootElement, default, default);
        });
    }

    [TestMethod]
    public async Task Listing_rules_filters_by_a_case_insensitive_substring_of_name_or_expression()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        using (await AddRuleDraftAsync(store, "tenant-scoped", "tenant == $claim.tenant", null, "system"))
        {
        }

        using (await AddRuleDraftAsync(store, "team-scoped", "team == $claim.team", null, "system"))
        {
        }

        using (await AddRuleDraftAsync(store, "public", "true", "Everyone may read.", "system"))
        {
        }

        // Matches the name, case-insensitively.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> q = AsJsonString("TENANT"u8))
        using (SecurityRulePage page = await store.ListRulesAsync(50, default, q.RootElement, default))
        {
            page.Rules.Select(r => r.NameValue).ShouldBe(["tenant-scoped"]);
        }

        // Matches the expression, not just the name.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> q = AsJsonString("team =="u8))
        using (SecurityRulePage page = await store.ListRulesAsync(50, default, q.RootElement, default))
        {
            page.Rules.Select(r => r.NameValue).ShouldBe(["team-scoped"]);
        }

        // No filter returns every rule in name order.
        using (SecurityRulePage page = await store.ListRulesAsync(50, default, default, default))
        {
            page.Rules.Select(r => r.NameValue).ShouldBe(["public", "team-scoped", "tenant-scoped"]);
        }
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
        using (ParsedJsonDocument<SecurityBindingDocument> a = await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("role", "tenant-admin", VerbGrant.Rules("tenant-scoped"), VerbGrant.Rules("tenant-scoped"), VerbGrant.None, order: 10),
            "alice",
            default))
        {
            aId = a.RootElement.IdValue;
        }

        string bId;
        using (ParsedJsonDocument<SecurityBindingDocument> b = await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("role", "operator", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full, order: 5),
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
    public async Task Listing_bindings_keyset_pages_in_order_id_without_gaps_or_duplicates()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();

        // Distinct orders plus a couple sharing an order, to exercise the id tie-breaker. Added out of order.
        int[] orders = [30, 10, 20, 10, 40, 0, 20, 50];
        var added = new List<(int Order, string Id)>();
        foreach (int order in orders)
        {
            using ParsedJsonDocument<SecurityBindingDocument> doc = await AddBindingDraftAsync(
                store,
                SecurityBindingDocument.Draft("role", $"role-{added.Count}", VerbGrant.Rules("r"), VerbGrant.None, VerbGrant.None, order: order),
                "alice",
                default);
            added.Add((order, doc.RootElement.IdValue));
        }

        // Walk every page via the continuation token with a small limit; collect the (order, id) in page order.
        var seen = new List<(int Order, string Id)>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using SecurityBindingPage page = await store.ListBindingsAsync(3, tokenDoc?.RootElement ?? default, default, default);
            page.Bindings.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (SecurityBindingDocument b in page.Bindings)
            {
                seen.Add((b.OrderValue, b.IdValue));
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 8 items, 3 per page → 3 pages; the page sequence is exactly the (order asc, id ordinal asc) total order.
        pages.ShouldBe(3);
        seen.ShouldBe(added.OrderBy(a => a.Order).ThenBy(a => a.Id, StringComparer.Ordinal).ToList());

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> badToken = AsJsonString("this~is~not~a~token"u8);
            using SecurityBindingPage bad = await store.ListBindingsAsync(3, badToken.RootElement, default, default);
        });
    }

    [TestMethod]
    public async Task Listing_bindings_filters_by_a_case_insensitive_substring_of_claim_or_description()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        using (await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("role", "tenant-admin", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: 1, description: "Tenant admins."), "alice", default))
        {
        }

        using (await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("team", "payments", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: 2), "alice", default))
        {
        }

        using (await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("group", "ops", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: 3, description: "Operators."), "alice", default))
        {
        }

        // Matches the claim value, case-insensitively.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> q = AsJsonString("PAYMENTS"u8))
        using (SecurityBindingPage page = await store.ListBindingsAsync(50, default, q.RootElement, default))
        {
            page.Bindings.Select(b => b.ClaimValueOrNull).ShouldBe(["payments"]);
        }

        // Matches the claim type.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> q = AsJsonString("group"u8))
        using (SecurityBindingPage page = await store.ListBindingsAsync(50, default, q.RootElement, default))
        {
            page.Bindings.Select(b => b.ClaimTypeValue).ShouldBe(["group"]);
        }

        // Matches the description, not just the claim.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> q = AsJsonString("operators"u8))
        using (SecurityBindingPage page = await store.ListBindingsAsync(50, default, q.RootElement, default))
        {
            page.Bindings.Select(b => b.ClaimValueOrNull).ShouldBe(["ops"]);
        }
    }

    [TestMethod]
    public async Task A_binding_updates_and_deletes_under_optimistic_concurrency()
    {
        ISecurityPolicyStore store = await this.NewStoreAsync();
        string addedId;
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<SecurityBindingDocument> added = await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("role", "viewer", VerbGrant.Rules("r1"), VerbGrant.None, VerbGrant.None),
            "alice",
            default))
        {
            addedId = added.RootElement.IdValue;
            addedEtag = added.RootElement.EtagValue;
        }

        WorkflowEtag updatedEtag;
        using (ParsedJsonDocument<SecurityBindingDocument>? updated = await UpdateBindingDraftAsync(
            store,
            addedId,
            SecurityBindingDocument.Draft("role", "viewer", VerbGrant.Rules("r1", "r2"), VerbGrant.None, VerbGrant.None, description: "two rules"),
            addedEtag,
            "bob",
            default))
        {
            updated.ShouldNotBeNull();
            RuleNames(updated!.RootElement.Read).ShouldBe(["r1", "r2"]);
            updatedEtag = updated.RootElement.EtagValue;
        }

        await Should.ThrowAsync<SecurityPolicyConflictException>(async () =>
            await UpdateBindingDraftAsync(store, addedId, SecurityBindingDocument.Draft("role", "viewer", VerbGrant.None, VerbGrant.None, VerbGrant.None), addedEtag, "carol", default));

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

        using (await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("*", null, VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "alice", default))
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

    // Wraps UTF-8 as the JSON string value a request carries it as — used both to feed a previous page's NextPageToken
    // (the store's emitted bytes) back through the pageToken seam and to pass the q filter, mirroring HTTP.
    private static ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> AsJsonString(ReadOnlySpan<byte> valueUtf8)
    {
        byte[] quoted = new byte[valueUtf8.Length + 2];
        quoted[0] = (byte)'"';
        valueUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>.Parse(quoted);
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

    // Adds the (pooled, disposable) draft binding, disposing the draft once the store has read it; the created document
    // is returned for the caller to assert on and dispose.
    private static async Task<ParsedJsonDocument<SecurityBindingDocument>> AddBindingDraftAsync(ISecurityPolicyStore store, ParsedJsonDocument<SecurityBindingDocument> draft, string actor, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await store.AddBindingAsync(draft.RootElement, actor, cancellationToken);
        }
    }

    private static async Task<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingDraftAsync(ISecurityPolicyStore store, string id, ParsedJsonDocument<SecurityBindingDocument> draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await store.UpdateBindingAsync(id, draft.RootElement, expectedEtag, actor, cancellationToken);
        }
    }
}
