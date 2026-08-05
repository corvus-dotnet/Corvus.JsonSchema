// <copyright file="OwnerGroupTagTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The one derivation and the one reader of the owner-group tag (ADR 0065). What is asserted here is agreement: the key
/// a deployment stamps under and the key a reader looks for are the same value, and a reader looking for the wrong one
/// answers "nobody owns this" rather than failing.
/// </summary>
[TestClass]
public sealed class OwnerGroupTagTests
{
    [TestMethod]
    public void The_default_key_is_the_default_prefix_joined_to_the_dimension()
    {
        // Both are declared separately — one as a literal for the no-configuration case, one built from a prefix — so
        // the property that matters is that they agree. A drift between them would go unseen: a reader using the
        // literal against rows stamped by the builder simply finds no owner group anywhere.
        byte[] built = OwnerGroupTag.KeyFor(SecurityShell.DefaultInternalPrefix);

        built.AsSpan().SequenceEqual(OwnerGroupTag.DefaultKeyUtf8).ShouldBeTrue();
        Encoding.UTF8.GetString(OwnerGroupTag.DefaultKeyUtf8).ShouldBe(SecurityShell.DefaultInternalPrefix + OwnerGroupTag.Dimension);
    }

    [TestMethod]
    public void A_configured_prefix_moves_the_key_with_it()
    {
        Encoding.UTF8.GetString(OwnerGroupTag.KeyFor("corp:")).ShouldBe("corp:tenant");
        Encoding.UTF8.GetString(OwnerGroupTag.KeyFor(string.Empty)).ShouldBe("tenant");
    }

    [TestMethod]
    public void An_owner_group_is_read_back_under_the_key_it_was_stamped_with()
    {
        using ParsedJsonDocument<Environment> environment = Stamped("sys:tenant", "acme");

        OwnerGroupTag.Read(environment.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBe("acme");
        OwnerGroupTag.IsTenantOwned(environment.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBeTrue();
    }

    [TestMethod]
    public void A_reader_looking_under_a_different_key_sees_no_owner_at_all()
    {
        // The silent failure this type exists to prevent, asserted rather than described: the row is owned, and a
        // reader with the wrong prefix reports it as owned by nobody. Nothing throws, so only agreement protects it.
        using ParsedJsonDocument<Environment> environment = Stamped("sys:tenant", "acme");

        OwnerGroupTag.Read(environment.RootElement, OwnerGroupTag.KeyFor("corp:")).ShouldBeNull();
        OwnerGroupTag.IsTenantOwned(environment.RootElement, OwnerGroupTag.KeyFor("corp:")).ShouldBeFalse();
    }

    [TestMethod]
    public void An_environment_carrying_no_owner_group_belongs_to_nobody()
    {
        // A legitimate state rather than an error: an environment nobody claims is not counted by the tenancy gate and
        // is not charged to a tenant's quota.
        using ParsedJsonDocument<Environment> untagged = Environment.Draft("production", null, null, SecurityTagSet.Empty);

        OwnerGroupTag.Read(untagged.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBeNull();
        OwnerGroupTag.IsTenantOwned(untagged.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBeFalse();
    }

    [TestMethod]
    public void An_empty_owner_group_is_the_same_as_none()
    {
        // An empty value names no tenant, so treating it as one would put every environment stamped with an empty
        // string onto a single shared counter.
        using ParsedJsonDocument<Environment> environment = Stamped("sys:tenant", string.Empty);

        OwnerGroupTag.Read(environment.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBeNull();
        OwnerGroupTag.IsTenantOwned(environment.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBeFalse();
    }

    [TestMethod]
    public void The_owner_group_is_found_among_other_tags()
    {
        // The tag set is a walk, so a row carrying several internal tags must still resolve, whatever the order.
        using ParsedJsonDocument<Environment> environment = Environment.Draft(
            "production",
            null,
            null,
            SecurityTagSet.FromTags(
            [
                new SecurityTag("sys:workflow", "nightly-reconcile"),
                new SecurityTag("sys:tenant", "acme"),
                new SecurityTag("sys:team", "platform"),
            ]));

        OwnerGroupTag.Read(environment.RootElement, OwnerGroupTag.DefaultKeyUtf8).ShouldBe("acme");
    }

    private static ParsedJsonDocument<Environment> Stamped(string key, string value)
        => Environment.Draft("production", null, null, SecurityTagSet.FromTags([new SecurityTag(key, value)]));
}