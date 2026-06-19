// <copyright file="SecurityIdentityDigestTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Locks the contract of <see cref="SecurityIdentityDigest"/> (the collision-probe index key): the digest is
/// order-independent, content-determined, escaping-stable, and a fixed 64-char lower-case hex string — the properties the
/// allocation-optimised parse/sort/hash implementation must preserve.
/// </summary>
[TestClass]
public sealed class SecurityIdentityDigestTests
{
    [TestMethod]
    public void The_empty_identity_has_no_digest()
        => SecurityIdentityDigest.Compute(SecurityTagSet.Empty).ShouldBeNull();

    [TestMethod]
    public void A_digest_is_order_independent()
    {
        SecurityTagSet forward = Tags(("sys:tenant", "acme"), ("sys:sub", "alice"), ("sys:iss", "ldap"));
        SecurityTagSet reversed = Tags(("sys:iss", "ldap"), ("sys:sub", "alice"), ("sys:tenant", "acme"));
        SecurityIdentityDigest.Compute(forward).ShouldBe(SecurityIdentityDigest.Compute(reversed));
    }

    [TestMethod]
    public void Distinct_identities_have_distinct_digests()
        => SecurityIdentityDigest.Compute(Tags(("sys:iss", "ldap"), ("sys:sub", "alice")))
            .ShouldNotBe(SecurityIdentityDigest.Compute(Tags(("sys:iss", "ldap"), ("sys:sub", "bob"))));

    [TestMethod]
    public void A_subset_and_a_superset_differ()
        => SecurityIdentityDigest.Compute(Tags(("sys:sub", "alice")))
            .ShouldNotBe(SecurityIdentityDigest.Compute(Tags(("sys:sub", "alice"), ("sys:tenant", "acme"))));

    [TestMethod]
    public void Tags_that_could_be_confused_without_delimiting_stay_distinct()
        // {key="ab", value="c"} vs {key="a", value="bc"} must not collide — the length-prefixed canonical form prevents it.
        => SecurityIdentityDigest.Compute(Tags(("ab", "c")))
            .ShouldNotBe(SecurityIdentityDigest.Compute(Tags(("a", "bc"))));

    [TestMethod]
    public void A_digest_is_a_64_character_lowercase_hex_string()
    {
        string digest = SecurityIdentityDigest.Compute(Tags(("sys:sub", "alice")))!;
        digest.Length.ShouldBe(64);
        digest.ShouldAllBe(c => char.IsAsciiHexDigitLower(c));
    }

    [TestMethod]
    public void A_value_needing_json_escaping_digests_stably()
    {
        // Values whose characters the serializer escapes (quote, backslash, control, non-ASCII) must still produce a
        // stable, content-determined digest: equal to itself, distinct from a different value.
        const string Awkward = "a\"b\\cé";
        SecurityIdentityDigest.Compute(Tags(("sys:sub", Awkward))).ShouldBe(SecurityIdentityDigest.Compute(Tags(("sys:sub", Awkward))));
        SecurityIdentityDigest.Compute(Tags(("sys:sub", Awkward))).ShouldNotBe(SecurityIdentityDigest.Compute(Tags(("sys:sub", "abc"))));
    }

    [TestMethod]
    public void Many_tags_beyond_the_stack_table_digest_consistently()
    {
        // Exceed StackTagCapacity (32) so the slice table spills to the pool; the same set in a different order must
        // still digest identically.
        var forward = new List<(string Key, string Value)>();
        for (int i = 0; i < 40; i++)
        {
            forward.Add(($"sys:k{i:D2}", $"v{i:D2}"));
        }

        var reversed = new List<(string Key, string Value)>(forward);
        reversed.Reverse();
        SecurityIdentityDigest.Compute(Tags([.. forward])).ShouldBe(SecurityIdentityDigest.Compute(Tags([.. reversed])));
    }

    [TestMethod]
    public void Two_principals_differing_only_by_the_ambient_tenant_do_not_collide()
        // §16.5.5 collision-probe lock: the ambient dimension (sys:tenant) is part of the whole-set digest, so the same
        // person resolved in two tenants does not collide in FindIdentityConflictAsync (a future digest change that
        // dropped it would fail here).
        => SecurityIdentityDigest.Compute(Tags(("sys:iss", "kc"), ("sys:sub", "alice"), ("sys:tenant", "acme")))
            .ShouldNotBe(SecurityIdentityDigest.Compute(Tags(("sys:iss", "kc"), ("sys:sub", "alice"), ("sys:tenant", "globex"))));

    private static SecurityTagSet Tags(params (string Key, string Value)[] tags)
        => SecurityTagSet.FromTags([.. tags.Select(t => new SecurityTag(t.Key, t.Value))]);
}