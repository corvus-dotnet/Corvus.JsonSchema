// <copyright file="SecurityShellTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of <see cref="SecurityShell"/> (§14.3): the deployment shell reserves the internal-tag keyspace,
/// strips internal tags from client views, and composes its mandated wrapper rule with the user's rule.
/// </summary>
[TestClass]
public sealed class SecurityShellTests
{
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Claims(string name, string value)
        => new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { [name] = [value] };

    [TestMethod]
    public void User_tags_using_the_reserved_prefix_are_rejected()
    {
        var shell = new SecurityShell([]);

        Should.Throw<ArgumentException>(() => shell.ValidateUserTags([new SecurityTag("sys:tenant", "acme")]))
            .Message.ShouldContain("sys:");

        // Unprefixed user tags are fine.
        Should.NotThrow(() => shell.ValidateUserTags([new SecurityTag("team", "payments")]));
    }

    [TestMethod]
    public void User_tags_using_the_reserved_prefix_are_rejected_via_the_span_overload()
    {
        // The credential-write path validates a non-owning SecurityTagSet view (string-free); the reserved-prefix
        // rejection must hold through that span path too, with the offending key surfaced in the message.
        var shell = new SecurityShell([]);

        Should.Throw<ArgumentException>(() => shell.ValidateUserTags(SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")])))
            .Message.ShouldContain("sys:tenant");

        // Unprefixed user tags are fine (including one with an escaped value, exercising the span enumerator's decode).
        Should.NotThrow(() => shell.ValidateUserTags(SecurityTagSet.FromTags([new SecurityTag("team", "payments \"north\"")])));

        // A custom prefix is honoured on the span path too.
        var custom = new SecurityShell([], internalPrefix: "_internal.");
        Should.Throw<ArgumentException>(() => custom.ValidateUserTags(SecurityTagSet.FromTags([new SecurityTag("_internal.region", "eu")])));
        Should.NotThrow(() => custom.ValidateUserTags(SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")])));
    }

    [TestMethod]
    public void Internal_tags_are_stripped_from_client_views()
    {
        var shell = new SecurityShell([]);
        SecurityTag[] all = [new("sys:tenant", "acme"), new("team", "payments"), new("sys:region", "eu")];

        shell.StripInternal(all).ShouldBe([new SecurityTag("team", "payments")]);
    }

    [TestMethod]
    public void The_mandated_wrapper_is_anded_with_the_user_rule_and_cannot_be_widened()
    {
        // The deployment mandates tenant isolation against an internal tag; the user rule narrows by team.
        var shell = new SecurityShell([SecurityRule.Compile("sys:tenant == $claim.tenant")]);
        SecurityFilter filter = shell.BuildFilter([SecurityRule.Compile("team == 'payments'")], Claims("tenant", "acme"));

        // Same tenant + matching team → visible.
        filter.IsSatisfiedBy([new("sys:tenant", "acme"), new("team", "payments")]).ShouldBeTrue();

        // A user rule cannot reach another tenant: the wrapper still fails even though the user rule matches.
        filter.IsSatisfiedBy([new("sys:tenant", "globex"), new("team", "payments")]).ShouldBeFalse();

        // Right tenant but wrong team → the user rule fails.
        filter.IsSatisfiedBy([new("sys:tenant", "acme"), new("team", "hr")]).ShouldBeFalse();
    }

    [TestMethod]
    public void A_custom_prefix_is_honoured()
    {
        var shell = new SecurityShell([], internalPrefix: "_internal.");

        shell.IsInternal(new SecurityTag("_internal.tenant", "acme")).ShouldBeTrue();
        shell.IsInternal(new SecurityTag("sys:tenant", "acme")).ShouldBeFalse();
    }
}