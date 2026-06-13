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
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, Time, securityTags: security);
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
        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Security: filter), default);

        page.Runs.Select(r => r.Id.Value).ShouldBe(["run-acme"]);
    }

    [TestMethod]
    public async Task A_null_filter_is_unrestricted()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        await SeedAsync(store, "run-acme", new SecurityTag("tenant", "acme"));
        await SeedAsync(store, "run-globex", new SecurityTag("tenant", "globex"));

        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Security: null), default);

        page.Runs.Count.ShouldBe(2);
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
        WorkflowRunPage page = await management.ListAsync(new WorkflowQuery(Security: filter), default);

        page.Runs.Select(r => r.Id.Value).ShouldBe(["run-acme-payments"]);
    }
}