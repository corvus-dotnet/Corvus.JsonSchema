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

    // The standard ascending classification ordering used by the ordered-comparison tests.
    private static SecurityLabelOrderings Classification()
        => new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["classification"] = ["public", "internal", "confidential", "restricted"],
        });

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

    [TestMethod]
    public void Claims_superset_admits_a_row_only_when_every_label_is_covered()
    {
        // ABAC clearance: the principal must hold every label the row carries.
        SecurityRule rule = SecurityRule.Compile("$claims.superset");
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims = Claims(("tenant", "acme"), ("team", "payments"));

        rule.IsSatisfiedBy([new("tenant", "acme"), new("team", "payments")], claims).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme")], claims).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme"), new("team", "hr")], claims).ShouldBeFalse(); // team=hr uncovered
        rule.IsSatisfiedBy([new("region", "eu")], claims).ShouldBeFalse(); // no region claim
        rule.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeFalse(); // empty claims cover nothing
    }

    [TestMethod]
    public void Claims_intersects_admits_a_row_when_any_label_is_covered()
    {
        SecurityRule rule = SecurityRule.Compile("$claims.intersects");
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims = Claims(("tenant", "acme"), ("team", "payments"));

        rule.IsSatisfiedBy([new("tenant", "acme"), new("team", "hr")], claims).ShouldBeTrue(); // tenant covered
        rule.IsSatisfiedBy([new("region", "eu")], claims).ShouldBeFalse(); // nothing covered
        rule.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeFalse(); // empty claims
    }

    [TestMethod]
    public void Claims_predicates_compose_and_negate()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims = Claims(("tenant", "acme"), ("team", "payments"));

        SecurityRule shareAndTenant = SecurityRule.Compile("$claims.intersects && tenant == $claim.tenant");
        shareAndTenant.IsSatisfiedBy([new("tenant", "acme"), new("team", "hr")], claims).ShouldBeTrue();
        shareAndTenant.IsSatisfiedBy([new("tenant", "globex"), new("team", "payments")], claims).ShouldBeFalse();

        SecurityRule notSuperset = SecurityRule.Compile("!$claims.superset");
        notSuperset.IsSatisfiedBy([new("tenant", "acme"), new("team", "hr")], claims).ShouldBeTrue(); // not fully covered
        notSuperset.IsSatisfiedBy([new("tenant", "acme")], claims).ShouldBeFalse(); // fully covered
    }

    [TestMethod]
    public void An_unknown_claims_predicate_is_rejected_at_compile()
    {
        Should.Throw<FormatException>(() => SecurityRule.Compile("$claims.bogus"));
        Should.Throw<FormatException>(() => SecurityRule.Compile("$claims"));
    }

    [TestMethod]
    public void Set_membership_admits_a_row_whose_label_is_one_of_the_listed_literals()
    {
        SecurityRule rule = SecurityRule.Compile("tenant in ('acme', 'globex')");

        rule.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "globex")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "initech")], Claims()).ShouldBeFalse();
        rule.IsSatisfiedBy([new("team", "payments")], Claims()).ShouldBeFalse(); // dimension absent
    }

    [TestMethod]
    public void Set_membership_is_set_intersection_over_a_multi_valued_label()
    {
        SecurityRule rule = SecurityRule.Compile("team in ('billing')");

        rule.IsSatisfiedBy([new("team", "payments"), new("team", "billing")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("team", "payments"), new("team", "hr")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void Set_membership_negates_with_the_not_operator()
    {
        SecurityRule rule = SecurityRule.Compile("!(tenant in ('acme'))");

        rule.IsSatisfiedBy([new("tenant", "globex")], Claims()).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void Set_membership_over_a_claim_tests_the_principal_values()
    {
        SecurityRule rule = SecurityRule.Compile("$claim.role in ('admin', 'superuser')");

        rule.IsSatisfiedBy([new("tenant", "acme")], Claims(("role", "admin"))).ShouldBeTrue();
        rule.IsSatisfiedBy([new("tenant", "acme")], Claims(("role", "viewer"))).ShouldBeFalse();
        rule.IsSatisfiedBy([new("tenant", "acme")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void A_malformed_in_list_is_rejected_at_compile()
    {
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant in ()"));        // empty list
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant in ('a'"));      // unterminated
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant in ('a' 'b')")); // missing comma
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant in (acme)"));    // unquoted value
        Should.Throw<FormatException>(() => SecurityRule.Compile("tenant in ($claim.x)")); // claim not allowed
    }

    [TestMethod]
    public void Ordered_comparison_ranks_a_classification_against_a_literal_bound()
    {
        SecurityRule atOrBelow = SecurityRule.Compile("classification <= 'confidential'", Classification());
        atOrBelow.IsSatisfiedBy([new("classification", "public")], Claims()).ShouldBeTrue();
        atOrBelow.IsSatisfiedBy([new("classification", "confidential")], Claims()).ShouldBeTrue();
        atOrBelow.IsSatisfiedBy([new("classification", "restricted")], Claims()).ShouldBeFalse();
        atOrBelow.IsSatisfiedBy([], Claims()).ShouldBeFalse(); // dimension absent → deny

        SecurityRule below = SecurityRule.Compile("classification < 'internal'", Classification());
        below.IsSatisfiedBy([new("classification", "public")], Claims()).ShouldBeTrue();
        below.IsSatisfiedBy([new("classification", "internal")], Claims()).ShouldBeFalse();

        SecurityRule atOrAbove = SecurityRule.Compile("classification >= 'confidential'", Classification());
        atOrAbove.IsSatisfiedBy([new("classification", "restricted")], Claims()).ShouldBeTrue();
        atOrAbove.IsSatisfiedBy([new("classification", "confidential")], Claims()).ShouldBeTrue();
        atOrAbove.IsSatisfiedBy([new("classification", "internal")], Claims()).ShouldBeFalse();

        SecurityRule above = SecurityRule.Compile("classification > 'internal'", Classification());
        above.IsSatisfiedBy([new("classification", "confidential")], Claims()).ShouldBeTrue();
        above.IsSatisfiedBy([new("classification", "internal")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void Ordered_comparison_is_conservative_over_a_multi_valued_classification()
    {
        SecurityRule atOrBelow = SecurityRule.Compile("classification <= 'confidential'", Classification());

        // Every row value must satisfy the bound: max rank wins for an upper bound.
        atOrBelow.IsSatisfiedBy([new("classification", "public"), new("classification", "internal")], Claims()).ShouldBeTrue();
        atOrBelow.IsSatisfiedBy([new("classification", "public"), new("classification", "restricted")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void Ordered_comparison_resolves_a_claim_bound_to_the_most_permissive_rank()
    {
        SecurityRule atOrBelow = SecurityRule.Compile("classification <= $claim.clearance", Classification());

        // A single clearance.
        atOrBelow.IsSatisfiedBy([new("classification", "confidential")], Claims(("clearance", "confidential"))).ShouldBeTrue();
        atOrBelow.IsSatisfiedBy([new("classification", "restricted")], Claims(("clearance", "confidential"))).ShouldBeFalse();

        // Multiple clearances: an upper bound takes the highest the principal holds.
        atOrBelow.IsSatisfiedBy([new("classification", "restricted")], Claims(("clearance", "public"), ("clearance", "restricted"))).ShouldBeTrue();

        // A lower bound takes the lowest the principal holds.
        SecurityRule atOrAbove = SecurityRule.Compile("classification >= $claim.clearance", Classification());
        atOrAbove.IsSatisfiedBy([new("classification", "internal")], Claims(("clearance", "public"), ("clearance", "confidential"))).ShouldBeTrue();
    }

    [TestMethod]
    public void Ordered_comparison_fails_closed_on_unranked_values_and_unordered_dimensions()
    {
        SecurityLabelOrderings orderings = Classification();

        // An unranked row value denies even when another value would satisfy.
        SecurityRule atOrBelow = SecurityRule.Compile("classification <= 'confidential'", orderings);
        atOrBelow.IsSatisfiedBy([new("classification", "weird")], Claims()).ShouldBeFalse();
        atOrBelow.IsSatisfiedBy([new("classification", "public"), new("classification", "weird")], Claims()).ShouldBeFalse();

        // A bound with no ranked value denies.
        atOrBelow = SecurityRule.Compile("classification <= 'bogus'", orderings);
        atOrBelow.IsSatisfiedBy([new("classification", "public")], Claims()).ShouldBeFalse();

        // An unordered dimension denies (no ordering configured for 'region').
        SecurityRule region = SecurityRule.Compile("region <= 'eu'", orderings);
        region.IsSatisfiedBy([new("region", "eu")], Claims()).ShouldBeFalse();

        // No orderings at all (the parameterless Compile) → every ordered comparison denies.
        SecurityRule noOrderings = SecurityRule.Compile("classification <= 'confidential'");
        noOrderings.IsSatisfiedBy([new("classification", "public")], Claims()).ShouldBeFalse();
    }

    [TestMethod]
    public void A_malformed_ordered_comparison_is_rejected_at_compile()
    {
        Should.Throw<FormatException>(() => SecurityRule.Compile("'x' <= 'y'", Classification()));            // LHS must be a dimension
        Should.Throw<FormatException>(() => SecurityRule.Compile("classification <= other", Classification())); // RHS must be literal/claim
        Should.Throw<FormatException>(() => SecurityRule.Compile("classification <=", Classification()));       // missing RHS
    }
}