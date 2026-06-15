// <copyright file="SecurityFilterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of <see cref="SecurityFilter"/> applied as a row-authorization filter on run queries (§14.2 slice
/// 3): a list query restricts results to runs whose security tags satisfy the principal's rule(s) — evaluated
/// in the store, not post-filtered by the caller.
/// </summary>
[TestClass]
public sealed class SecurityFilterTests
{
    private static readonly TestTimeProvider Time = new(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero));

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Claims(params (string Name, string Value)[] claims)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach ((string name, string value) in claims)
        {
            map[name] = map.TryGetValue(name, out IReadOnlyList<string>? existing) ? [.. existing, value] : [value];
        }

        return map;
    }

    private static async Task SeedAsync(InMemoryWorkflowStateStore store, string id, params SecurityTag[] security)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "x": 1 }"""u8.ToArray());
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, Time, securityTags: SecurityTagSet.FromTags(security));
        await run.EnqueueAsync(default);
    }

    [TestMethod]
    public async Task A_tenant_scoped_filter_returns_only_the_principals_tenant_runs()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        await SeedAsync(store, "run-acme", new SecurityTag("tenant", "acme"));
        await SeedAsync(store, "run-globex", new SecurityTag("tenant", "globex"));
        await SeedAsync(store, "run-untagged");

        var filter = new SecurityFilter([SecurityRule.Compile("tenant == $claim.tenant")], Claims(("tenant", "acme")));
        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Status: null), AccessContext.Uniform(filter), default);

        page.Runs.Select(r => r.Id.Value).ShouldBe(["run-acme"]);
    }

    [TestMethod]
    public void A_store_that_does_not_push_the_reach_filter_down_fails_loud()
    {
        var filter = new SecurityFilter([SecurityRule.Compile("tenant == 'acme'")], Claims());

        // A store without ISupportsRowSecurityFilter must refuse a filtered query rather than silently ignore it.
        Should.Throw<NotSupportedException>(() => RowSecurityPushdown.EnsureSupported(filter, new object()));

        // …but a null filter is always fine, and a store that implements the marker is accepted.
        Should.NotThrow(() => RowSecurityPushdown.EnsureSupported(null, new object()));
        Should.NotThrow(() => RowSecurityPushdown.EnsureSupported(filter, new InMemoryWorkflowStateStore()));
    }

    [TestMethod]
    public async Task A_null_filter_is_unrestricted()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        await SeedAsync(store, "run-acme", new SecurityTag("tenant", "acme"));
        await SeedAsync(store, "run-globex", new SecurityTag("tenant", "globex"));

        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Status: null), AccessContext.System, default);

        page.Runs.Count.ShouldBe(2);
    }

    [TestMethod]
    public void An_empty_rule_set_denies_by_default()
    {
        // "No restriction" is a null filter (AccessContext.System), never an empty rule set; an under-specified
        // filter must deny rather than admit everything (fail-closed).
        var filter = new SecurityFilter([], Claims(("tenant", "acme")));
        filter.IsSatisfiedBy([new SecurityTag("tenant", "acme")]).ShouldBeFalse();
        filter.IsSatisfiedBy([]).ShouldBeFalse();
    }

    [TestMethod]
    public void An_untagged_row_is_denied_even_by_a_rule_that_would_otherwise_admit_it()
    {
        // A negation rule is satisfied by the absence of the value — but an unclassified (untagged) row must not
        // be visible to a scoped principal regardless. Only a null (full-reach) filter sees untagged rows.
        var filter = new SecurityFilter([SecurityRule.Compile("tenant != 'globex'")], Claims());
        filter.IsSatisfiedBy([new SecurityTag("tenant", "acme")]).ShouldBeTrue();
        filter.IsSatisfiedBy([]).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_untagged_run_is_invisible_to_a_scoped_principal()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        await SeedAsync(store, "run-acme", new SecurityTag("tenant", "acme"));
        await SeedAsync(store, "run-untagged");

        // A permissive-looking rule still must not surface the unclassified run.
        var filter = new SecurityFilter([SecurityRule.Compile("tenant != 'globex'")], Claims());
        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Status: null), AccessContext.Uniform(filter), default);

        page.Runs.Select(r => r.Id.Value).ShouldBe(["run-acme"]);
    }

    [TestMethod]
    public async Task A_conjunction_of_rules_models_the_deployment_wrapper_and_the_user_rule()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        await SeedAsync(store, "run-acme-payments", new SecurityTag("tenant", "acme"), new SecurityTag("team", "payments"));
        await SeedAsync(store, "run-acme-hr", new SecurityTag("tenant", "acme"), new SecurityTag("team", "hr"));
        await SeedAsync(store, "run-globex-payments", new SecurityTag("tenant", "globex"), new SecurityTag("team", "payments"));

        // Wrapper mandates the tenant; the user rule narrows to a team. Both must hold.
        var filter = new SecurityFilter(
            [SecurityRule.Compile("tenant == $claim.tenant"), SecurityRule.Compile("team == 'payments'")],
            Claims(("tenant", "acme")));
        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Status: null), AccessContext.Uniform(filter), default);

        page.Runs.Select(r => r.Id.Value).ShouldBe(["run-acme-payments"]);
    }
}