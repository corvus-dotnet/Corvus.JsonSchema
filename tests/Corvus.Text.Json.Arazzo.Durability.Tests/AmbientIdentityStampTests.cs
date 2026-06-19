// <copyright file="AmbientIdentityStampTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Covers the ambient-dimension primitives (design §16.5.5): <see cref="AmbientDimensionSet"/> carrying its tags in both
/// the managed-string and bytes-to-bytes forms, and <see cref="AmbientIdentityStamp"/> generalising
/// <c>DirectoryIssuer.Stamp</c> to strip-and-restamp the governed dimensions mapper-immutably and fail-closed.
/// </summary>
[TestClass]
public sealed class AmbientIdentityStampTests
{
    [TestMethod]
    public void The_empty_set_holds_no_tags()
    {
        AmbientDimensionSet.Empty.IsEmpty.ShouldBeTrue();
        AmbientDimensionSet.Empty.Tags.ShouldBeEmpty();
        AmbientDimensionSet.Create([]).ShouldBeSameAs(AmbientDimensionSet.Empty);
    }

    [TestMethod]
    public void A_created_set_round_trips_its_tags()
    {
        AmbientDimensionSet set = AmbientDimensionSet.Create([new SecurityTag("sys:tenant", "acme")]);
        set.IsEmpty.ShouldBeFalse();
        set.Tags.ShouldBe([new SecurityTag("sys:tenant", "acme")]);
    }

    [TestMethod]
    public void WriteTo_appends_the_dimensions_bytes_to_bytes()
    {
        AmbientDimensionSet set = AmbientDimensionSet.Create([new SecurityTag("sys:tenant", "acme"), new SecurityTag("sys:region", "eu")]);

        // The span path: write the ambient dimensions straight into a pooled identity buffer (no per-tag string).
        SecurityTagSet built = SecurityTagSet.Build(in set, static (ref IdentityBuilder builder, in AmbientDimensionSet s) => s.WriteTo(ref builder));

        built.ToList().ShouldBe([new SecurityTag("sys:tenant", "acme"), new SecurityTag("sys:region", "eu")], ignoreOrder: true);
    }

    [TestMethod]
    public void Apply_without_a_provider_returns_the_input_unchanged()
    {
        IReadOnlyList<SecurityTag> tags = [new SecurityTag("sys:sub", "alice")];
        AmbientIdentityStamp.Apply(null, tags).ShouldBeSameAs(tags);

        SecurityTagSet set = SecurityTagSet.FromTags(tags);
        AmbientIdentityStamp.Apply(null, set).ShouldBe(set);
    }

    [TestMethod]
    public void Apply_appends_the_resolved_ambient_dimension()
    {
        var provider = new FakeAmbient(AmbientDimensionSet.Create([new SecurityTag("sys:tenant", "acme")]), "sys:tenant");

        IReadOnlyList<SecurityTag> stamped = AmbientIdentityStamp.Apply(provider, [new SecurityTag("sys:sub", "alice")]);
        stamped.ShouldBe([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "acme")], ignoreOrder: true);
    }

    [TestMethod]
    public void Apply_is_mapper_immutable_a_smuggled_governed_tag_is_replaced()
    {
        // A mapper that tries to forge sys:tenant=evil; the provider governs sys:tenant and resolves acme → the forged
        // value is stripped and the authoritative value stamped.
        var provider = new FakeAmbient(AmbientDimensionSet.Create([new SecurityTag("sys:tenant", "acme")]), "sys:tenant");

        IReadOnlyList<SecurityTag> stamped = AmbientIdentityStamp.Apply(provider, [new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "evil")]);
        stamped.ShouldBe([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "acme")], ignoreOrder: true);
    }

    [TestMethod]
    public void Apply_fails_closed_an_unresolved_governed_tag_is_stripped_not_honoured()
    {
        // The provider governs sys:tenant but this context resolves none (Empty) — a smuggled sys:tenant is still
        // stripped, so the identity carries no tenant (inert / no cross-tenant access), never the forged value.
        var provider = new FakeAmbient(AmbientDimensionSet.Empty, "sys:tenant");

        IReadOnlyList<SecurityTag> stamped = AmbientIdentityStamp.Apply(provider, [new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "evil")]);
        stamped.ShouldBe([new SecurityTag("sys:sub", "alice")]);
    }

    [TestMethod]
    public void Apply_on_a_set_strips_and_restamps()
    {
        var provider = new FakeAmbient(AmbientDimensionSet.Create([new SecurityTag("sys:tenant", "acme")]), "sys:tenant");

        SecurityTagSet stamped = AmbientIdentityStamp.Apply(provider, SecurityTagSet.FromTags([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "evil")]));
        stamped.ToList().ShouldBe([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:tenant", "acme")], ignoreOrder: true);
    }

    private sealed class FakeAmbient(AmbientDimensionSet set, params string[] governed) : IAmbientIdentityDimensions
    {
        public IReadOnlyCollection<string> GovernedKeys { get; } = governed;

        public AmbientDimensionSet Resolve() => set;
    }
}