// <copyright file="SecurityRuleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of <see cref="SecurityRule"/> (§14.2 slice 2): the tag-rule grammar and set-intersection
/// evaluation over a row's security tags and a principal's claims.
/// </summary>
[TestClass]
public sealed class SecurityRuleTests
{
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Claims(params (string Name, string Value)[] claims)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach ((string name, string value) in claims)
        {
            map[name] = map.TryGetValue(name, out IReadOnlyList<string>? existing) ? [.. existing, value] : [value];
        }

        return map;
    }

    [TestMethod]
    public void Tenant_scoped_rule_matches_the_row_tenant_against_the_principal_claim()
    {
        SecurityRule rule = SecurityRule.Compile("tenant == $claim.tenant");
        SecurityTag[] row = [new("tenant", "acme")];

        rule.IsSatisfiedBy(row, Claims(("tenant", "acme"))).ShouldBeTrue();
        rule.IsSatisfiedBy(row, Claims(("tenant", "globex"))).ShouldBeFalse();
        rule.IsSatisfiedBy(row, Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void A_compound_rule_combines_literals_with_and_or_and_grouping()
    {
        SecurityRule rule = SecurityRule.Compile("tenant == 'acme' && (team == 'payments' || team == 'billing')");

        rule.IsSatisfiedBy([new("tenant", "acme"), new("team", "payments")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme"), new("team", "billing")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme"), new("team", "hr")], Claims()).ShouldBeFalse();
        rule.IsSatisfiedBy([new("tenant", "globex"), new("team", "payments")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void A_claim_only_rule_ignores_the_row_tags()
    {
        SecurityRule rule = SecurityRule.Compile("$claim.role == 'admin'");

        rule.IsSatisfiedBy([new("tenant", "anything")], Claims(("role", "admin"))).ShouldBeTrue();
        rule.IsSatisfiedBy([], Claims(("role", "admin"))).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme")], Claims(("role", "viewer"))).ShouldBeFalse();
    }

    [TestMethod]
    public void Equality_is_set_intersection_over_multi_valued_labels_and_claims()
    {
        // The row carries two teams; the principal is granted two teams; they overlap on 'billing'.
        SecurityRule rule = SecurityRule.Compile("team == $claim.team");
        SecurityTag[] row = [new("team", "payments"), new("team", "billing")];

        rule.IsSatisfiedBy(row, Claims(("team", "hr"), ("team", "billing"))).ShouldBeTrue();
        rule.IsSatisfiedBy(row, Claims(("team", "hr"))).ShouldBeFalse();
    }

    [TestMethod]
    public void Not_equal_and_negation_exclude_matches()
    {
        SecurityRule notRestricted = SecurityRule.Compile("classification != 'restricted'");
        notRestricted.IsSatisfiedBy([new("classification", "public")], Claims()).ShouldBeTrue();
        notRestricted.IsSatisfiedBy([new("classification", "restricted")], Claims()).ShouldBeFalse();

        // != against an absent label: the sets cannot intersect, so it holds (no restricted label present).
        notRestricted.IsSatisfiedBy([], Claims()).ShouldBeTrue();

        SecurityRule notAcme = SecurityRule.Compile("!(tenant == 'acme')");
        notAcme.IsSatisfiedBy([new("tenant", "globex")], Claims()).ShouldBeTrue();
        notAcme.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void A_bare_operand_is_truthy_when_the_label_is_present()
    {
        SecurityRule rule = SecurityRule.Compile("tenant");

        rule.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("team", "payments")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void A_malformed_rule_is_rejected_at_compile()
    {
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant == "));
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant == 'acme' &&"));
        Should.Throw<FormatException>(() => SecurityRule.Compile("(tenant == 'acme'"));
    }
}