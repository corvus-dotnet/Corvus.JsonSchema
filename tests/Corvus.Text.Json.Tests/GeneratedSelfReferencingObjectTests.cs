// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for self-referencing object types (where a property references the containing type).
/// Verifies that codegen correctly suppresses the convenience CreateBuilder overload
/// when it would collide with the existing CreateBuilder(workspace, Source, int) overload.
/// </summary>
[TestClass]
public class GeneratedSelfReferencingObjectTests
{
    [TestMethod]
    public void SelfReferencingObject_CanBuildWithBuilderDelegate()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<SelfReferencingObject.Mutable> builder =
            SelfReferencingObject.CreateBuilder(
                workspace,
                static (ref objectBuilder) =>
                {
                    objectBuilder.Create(
                        parent: SelfReferencingObject.Build(
                            static (ref innerBuilder) =>
                            {
                                innerBuilder.Create();
                            }));
                });

        Assert.AreEqual("""{"parent":{}}""", builder.RootElement.ToString());
    }

    [TestMethod]
    public void SelfReferencingObject_CanBuildNestedStructure()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<SelfReferencingObject.Mutable> builder =
            SelfReferencingObject.CreateBuilder(
                workspace,
                static (ref objectBuilder) =>
                {
                    objectBuilder.Create(
                        parent: SelfReferencingObject.Build(
                            static (ref innerBuilder) =>
                            {
                                innerBuilder.Create(
                                    parent: SelfReferencingObject.Build(
                                        static (ref leaf) =>
                                        {
                                            leaf.Create();
                                        }));
                            }));
                });

        Assert.AreEqual("""{"parent":{"parent":{}}}""", builder.RootElement.ToString());
    }

    [TestMethod]
    public void SelfReferencingObject_CanBuildEmpty()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<SelfReferencingObject.Mutable> builder =
            SelfReferencingObject.CreateBuilder(workspace);

        Assert.AreEqual("{}", builder.RootElement.ToString());
    }

    [TestMethod]
    public void SelfReferencingObject_CanMutateParent()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<SelfReferencingObject.Mutable> builder =
            SelfReferencingObject.CreateBuilder(workspace);

        SelfReferencingObject.Mutable root = builder.RootElement;
        Assert.IsTrue(root.Parent.IsUndefined());

        root.SetParent(
            SelfReferencingObject.Build(
                static (ref innerBuilder) =>
                {
                    innerBuilder.Create();
                }));

        Assert.IsFalse(root.Parent.IsUndefined());
        Assert.AreEqual("""{"parent":{}}""", root.ToString());
    }

    [TestMethod]
    public void SelfReferencingObject_RoundTripsNestedStructure()
    {
        string json = """{"parent":{"parent":{}}}""";

        using var parsed =
            ParsedJsonDocument<SelfReferencingObject>.Parse(json);

        Assert.IsFalse(parsed.RootElement.Parent.IsUndefined());
        Assert.IsFalse(parsed.RootElement.Parent.Parent.IsUndefined());
        Assert.IsTrue(parsed.RootElement.Parent.Parent.Parent.IsUndefined());

        Assert.AreEqual(json, parsed.RootElement.ToString());
    }
}
