// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the discriminated-union conversions (issues #812 / #806): a composed branch's
/// mutable view converts directly to the composing union type in a single implicit hop.
/// </summary>
[TestClass]
public class DiscriminatedUnionConversionTests
{
    [TestMethod]
    public void BranchMutable_ConvertsToUnion_InOneHop()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        using JsonDocumentBuilder<Shape.Circle.Mutable> builder =
            JsonDocumentBuilder<Shape.Circle.Mutable>.Parse(ws, """{"kind":"Circle","radius":2.5}""");

        Shape.Circle.Mutable circle = builder.RootElement;

        // Before #812/#806 this required two hops (Circle.Mutable -> Circle -> Shape), which C#
        // will not chain. The direct Circle.Mutable -> Shape conversion makes this a single hop.
        Shape shape = circle;

        Assert.AreEqual(JsonValueKind.Object, shape.ValueKind);
        Assert.IsTrue(shape.TryGetAsCircle(out Shape.Circle asCircle));
        Assert.AreEqual(2.5, (double)asCircle.Radius);
    }

    [TestMethod]
    public void BranchMutable_PassedWhereUnionExpected()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        using JsonDocumentBuilder<Shape.Rectangle.Mutable> builder =
            JsonDocumentBuilder<Shape.Rectangle.Mutable>.Parse(ws, """{"kind":"Rectangle","width":3,"height":4}""");

        // The mutable branch flows straight into a method expecting the union, no intermediate.
        string kind = DescribeShape(builder.RootElement);

        Assert.AreEqual("Rectangle", kind);

        static string DescribeShape(Shape shape) => (string)shape.Kind;
    }

    [TestMethod]
    public void Constituent_FlowsIntoContainingTypeBuild()
    {
        // Issue #812: a discriminated-union branch can be passed directly where the union's Source
        // is expected (here a containing type's CreateBuilder), so a containing structure can be
        // built from a branch without first materialising the union. This exercises the
        // constituent-builder Source wiring that is enabled for discriminated unions.
        using JsonWorkspace ws = JsonWorkspace.Create();
        using ParsedJsonDocument<ShapeHolder.Circle> circleDoc =
            ParsedJsonDocument<ShapeHolder.Circle>.Parse("""{"kind":"Circle","radius":2.5}""");
        ShapeHolder.Circle circle = circleDoc.RootElement;

        // 'circle' converts implicitly to ShapeHolder.Shape.Source for the required 'shape' property.
        using JsonDocumentBuilder<ShapeHolder.Mutable> holderBuilder = ShapeHolder.CreateBuilder(ws, circle);
        ShapeHolder.Mutable holder = holderBuilder.RootElement;

        ShapeHolder.Shape shape = holder.ShapeValue;
        Assert.AreEqual("Circle", (string)shape.Kind);
        Assert.IsTrue(shape.TryGetAsCircle(out ShapeHolder.Circle built));
        Assert.AreEqual(2.5, (double)built.Radius);
    }

    [TestMethod]
    public void Constituent_BuiltInline_FlowsIntoContainingTypeBuild()
    {
        // Issue #812: build a branch with Circle.Build(...) and pass the result straight into a
        // containing type's CreateBuilder for the union property, with no intermediate document.
        using JsonWorkspace ws = JsonWorkspace.Create();

        using JsonDocumentBuilder<ShapeHolder.Mutable> holderBuilder = ShapeHolder.CreateBuilder(
            ws,
            ShapeHolder.Circle.Build(static (ref ShapeHolder.Circle.Builder b) =>
            {
                b.AddProperty("kind"u8, "Circle"u8);
                b.AddProperty("radius"u8, 2.5);
            }));
        ShapeHolder.Mutable holder = holderBuilder.RootElement;

        ShapeHolder.Shape shape = holder.ShapeValue;
        Assert.AreEqual("Circle", (string)shape.Kind);
        Assert.IsTrue(shape.TryGetAsCircle(out ShapeHolder.Circle built));
        Assert.AreEqual(2.5, (double)built.Radius);
    }
}
