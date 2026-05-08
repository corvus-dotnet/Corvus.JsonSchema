// <copyright file="PatchBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

[TestClass]
public class PatchBuilderTests
{
    [TestMethod]
    public void BuildAndApplyAddOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Add("/baz"u8, "qux"u8);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
        Assert.AreEqual("qux", root.GetProperty("baz").GetString());
        Assert.AreEqual("bar", root.GetProperty("foo").GetString());
    }

    [TestMethod]
    public void BuildAndApplyRemoveOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": "qux"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Remove("/baz"u8);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
        Assert.IsFalse(root.TryGetProperty("baz"u8, out _));
        Assert.AreEqual("bar", root.GetProperty("foo").GetString());
    }

    [TestMethod]
    public void BuildAndApplyReplaceOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Replace("/foo"u8, 42);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
        Assert.AreEqual(42, root.GetProperty("foo").GetInt32());
    }

    [TestMethod]
    public void BuildAndApplyMoveOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": {"bar": "baz"}, "qux": {"corge": "grault"}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Move("/foo/bar"u8, "/qux/thud"u8);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
        Assert.AreEqual("baz", root.GetProperty("qux").GetProperty("thud").GetString());
        Assert.IsFalse(root.GetProperty("foo").TryGetProperty("bar"u8, out _));
    }

    [TestMethod]
    public void BuildAndApplyCopyOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Copy("/foo"u8, "/baz"u8);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
        Assert.AreEqual("bar", root.GetProperty("foo").GetString());
        Assert.AreEqual("bar", root.GetProperty("baz").GetString());
    }

    [TestMethod]
    public void BuildAndApplyTestOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Test("/foo"u8, "bar"u8);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
    }

    [TestMethod]
    public void BuildAndApplyTestOperationFailsOnMismatch()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Test("/foo"u8, "baz"u8);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsFalse(root.TryApplyPatch(patch));
    }

    [TestMethod]
    public void BuildAndApplyMultipleOperations()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": "qux"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patchBuilder = root.BeginPatch(workspace)
            .Remove("/baz"u8)
            .Add("/hello"u8, "world"u8)
            .Replace("/foo"u8, 42);

        JsonPatchDocument patch = patchBuilder.GetPatchAndDispose();
        Assert.IsTrue(root.TryApplyPatch(patch));
        Assert.IsFalse(root.TryGetProperty("baz"u8, out _));
        Assert.AreEqual("world", root.GetProperty("hello").GetString());
        Assert.AreEqual(42, root.GetProperty("foo").GetInt32());
    }
}

[TestClass]
public class IndividualOperationTests
{
    [TestMethod]
    public void TryAddToObject()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = "qux";

        Assert.IsTrue(root.TryAdd("/baz"u8, in value));
        Assert.AreEqual("qux", root.GetProperty("baz").GetString());
    }

    [TestMethod]
    public void TryAddToArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 4;

        Assert.IsTrue(root.TryAdd("/-"u8, in value));
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(4, root[3].GetInt32());
    }

    [TestMethod]
    public void TryRemoveProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": "qux"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.IsTrue(root.TryRemove("/baz"u8));
        Assert.IsFalse(root.TryGetProperty("baz"u8, out _));
    }

    [TestMethod]
    public void TryRemoveArrayElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.IsTrue(root.TryRemove("/1"u8));
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(3, root[1].GetInt32());
    }

    [TestMethod]
    public void TryReplaceProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.IsTrue(root.TryReplace("/foo"u8, in value));
        Assert.AreEqual(42, root.GetProperty("foo").GetInt32());
    }

    [TestMethod]
    public void TryReplaceNonExistentFails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.IsFalse(root.TryReplace("/baz"u8, in value));
    }

    [TestMethod]
    public void TryMoveProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": {}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.IsTrue(root.TryMove("/foo"u8, "/baz/qux"u8));
        Assert.IsFalse(root.TryGetProperty("foo"u8, out _));
        Assert.AreEqual("bar", root.GetProperty("baz").GetProperty("qux").GetString());
    }

    [TestMethod]
    public void TryCopyProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.IsTrue(root.TryCopy("/foo"u8, "/baz"u8));
        Assert.AreEqual("bar", root.GetProperty("foo").GetString());
        Assert.AreEqual("bar", root.GetProperty("baz").GetString());
    }

    [TestMethod]
    public void TryTestMatching()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("\"bar\"");

        Assert.IsTrue(root.TryTest("/foo"u8, in expected));
    }

    [TestMethod]
    public void TryTestNonMatching()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("\"baz\"");

        Assert.IsFalse(root.TryTest("/foo"u8, in expected));
    }

    [TestMethod]
    public void TryRemoveRootFails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.IsFalse(root.TryRemove(""u8));
    }

    [TestMethod]
    public void TryAddReplaceRoot()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.IsTrue(root.TryAdd(""u8, in value));
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
        Assert.AreEqual(42, root.GetInt32());
    }
}
