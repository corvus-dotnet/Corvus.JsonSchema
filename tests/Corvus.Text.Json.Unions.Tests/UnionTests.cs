// <copyright file="UnionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Unions.Tests;

/// <summary>
/// A generated <c>oneOf</c>/<c>anyOf</c> type is a C# union: <c>switch</c> and <c>is</c> patterns over its branch
/// types work with the C# 15 compiler, on every target framework.
/// </summary>
[TestClass]
public class UnionTests
{
    private static string Describe(in Shape shape) => shape switch
    {
        Shape.Circle c => $"circle {(double)c.Radius}",
        Shape.Square s => $"square {(double)s.Side}",
        JsonString label => $"label {(string)label}",
        Shape.ShapeArray shapes => $"shapes x{shapes.GetArrayLength()}",
        null => "no shape",
    };

    [TestMethod]
    [DataRow("""{"kind":"circle","radius":2.5}""", "circle 2.5")]
    [DataRow("""{"kind":"square","side":4}""", "square 4")]
    [DataRow("\"hello\"", "label hello")]
    [DataRow("""[{"kind":"circle","radius":1},"x"]""", "shapes x2")]
    [DataRow("""{"kind":"triangle"}""", "no shape")]
    [DataRow("42", "no shape")]
    public void Switch_dispatches_on_the_branch_that_matches(string json, string expected)
    {
        using var doc = ParsedJsonDocument<Shape>.Parse(json);
        Assert.AreEqual(expected, Describe(doc.RootElement));
    }

    [TestMethod]
    public void Is_pattern_tests_a_branch_and_binds_its_type()
    {
        using var doc = ParsedJsonDocument<Shape>.Parse("""{"kind":"circle","radius":2.5}""");
        Shape shape = doc.RootElement;

        Assert.IsTrue(shape is Shape.Circle);
        Assert.IsFalse(shape is Shape.Square);

        if (shape is Shape.Circle circle)
        {
            Assert.AreEqual(2.5, (double)circle.Radius);
        }
        else
        {
            Assert.Fail("expected a circle");
        }
    }

    [TestMethod]
    public void A_branch_value_converts_to_the_union()
    {
        using var doc = ParsedJsonDocument<Shape.Circle>.Parse("""{"kind":"circle","radius":1}""");
        Shape shape = doc.RootElement;   // union conversion through IUnionMembers.Create

        Assert.AreEqual("circle 1", Describe(shape));
    }

    [TestMethod]
    public void The_provider_reports_whether_any_branch_matches()
    {
        using var matching = ParsedJsonDocument<Shape>.Parse("\"label\"");
        using var notMatching = ParsedJsonDocument<Shape>.Parse("42");

        Shape.IUnionMembers valid = matching.RootElement;
        Shape.IUnionMembers invalid = notMatching.RootElement;

        Assert.IsTrue(valid.HasValue);
        Assert.IsInstanceOfType<JsonString>(valid.Value);
        Assert.IsFalse(invalid.HasValue);
        Assert.IsNull(invalid.Value);
    }

    [TestMethod]
    public void Union_and_Match_agree_on_every_branch()
    {
        foreach (string json in new[] { """{"kind":"circle","radius":2.5}""", """{"kind":"square","side":4}""", "\"hello\"", """[]""" })
        {
            using var doc = ParsedJsonDocument<Shape>.Parse(json);
            Shape shape = doc.RootElement;

            string viaMatch = shape.Match(
                static (in Shape.Circle c) => "circle",
                static (in Shape.Square s) => "square",
                static (in JsonString l) => "label",
                static (in Shape.ShapeArray a) => "shapes",
                static (in Shape _) => "none");

            string viaSwitch = shape switch
            {
                Shape.Circle => "circle",
                Shape.Square => "square",
                JsonString => "label",
                Shape.ShapeArray => "shapes",
                null => "none",
            };

            Assert.AreEqual(viaMatch, viaSwitch, json);
        }
    }

    [TestMethod]
    public void AnyOf_picks_the_first_branch_in_schema_order_when_branches_overlap()
    {
        // Both object branches accept an object with a name and an id; the first in schema order wins, as with Match.
        using var doc = ParsedJsonDocument<Loose>.Parse("""{"name":"n","id":1}""");
        Loose loose = doc.RootElement;

        string viaSwitch = loose switch
        {
            Loose.RequiredName => "name",
            Loose.RequiredId => "id",
            JsonBoolean => "bool",
            null => "none",
        };

        Assert.AreEqual("name", viaSwitch);
        Assert.IsTrue(loose is Loose.RequiredId, "a pattern for the other matching branch is still true");
    }

    [TestMethod]
    public void A_composition_with_an_unconstrained_branch_is_not_a_union()
    {
        Assert.IsFalse(typeof(NotAUnion).GetNestedType("IUnionMembers") is not null);
        Assert.IsFalse(Attribute.IsDefined(typeof(NotAUnion), typeof(System.Runtime.CompilerServices.UnionAttribute)));
        Assert.IsTrue(Attribute.IsDefined(typeof(Shape), typeof(System.Runtime.CompilerServices.UnionAttribute)));
    }
}
