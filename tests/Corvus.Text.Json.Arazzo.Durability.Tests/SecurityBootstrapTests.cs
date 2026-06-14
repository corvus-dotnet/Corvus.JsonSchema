// <copyright file="SecurityBootstrapTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Coverage of <see cref="SecurityBootstrap"/> seeding (§14.2): the ready-to-use bootstrap rules are seeded idempotently and compile.</summary>
[TestClass]
public sealed class SecurityBootstrapTests
{
    [TestMethod]
    public async Task Seeding_adds_the_bootstrap_rules_once_and_they_compile()
    {
        var store = new InMemorySecurityPolicyStore();

        IReadOnlyList<string> firstSeed = await SecurityBootstrap.SeedAsync(store);
        firstSeed.OrderBy(x => x, StringComparer.Ordinal).ShouldBe(["abac-superset", "intersection", "tenant-scoped"]);

        IReadOnlyList<SecurityRuleDocument> rules = await store.ListRulesAsync(default);
        rules.Count.ShouldBe(3);
        foreach (SecurityRuleDocument rule in rules)
        {
            Should.NotThrow(() => SecurityRule.Compile(rule.ExpressionValue)); // every seeded rule is valid grammar
        }

        // Idempotent: a second seed adds nothing.
        (await SecurityBootstrap.SeedAsync(store)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Seeding_preserves_a_user_edit_to_a_bootstrap_rule()
    {
        var store = new InMemorySecurityPolicyStore();
        await SecurityBootstrap.SeedAsync(store);

        SecurityRuleDocument tenant = (await store.GetRuleAsync(SecurityBootstrap.TenantScopedRuleName, default))!.Value;
        await store.UpdateRuleAsync(SecurityBootstrap.TenantScopedRuleName, new SecurityRuleDefinition("sys:tenant == $claim.tenant"), tenant.EtagValue, "alice", default);

        // Re-seeding must not clobber the edit.
        (await SecurityBootstrap.SeedAsync(store)).ShouldBeEmpty();
        (await store.GetRuleAsync(SecurityBootstrap.TenantScopedRuleName, default))!.Value.ExpressionValue.ShouldBe("sys:tenant == $claim.tenant");
    }
}
