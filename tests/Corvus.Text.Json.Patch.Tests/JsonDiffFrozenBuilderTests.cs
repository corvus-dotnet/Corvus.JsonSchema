// <copyright file="JsonDiffFrozenBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Regression tests for issue #809: creating a patch from <em>frozen</em> elements that
/// originate from a <see cref="JsonDocumentBuilder{T}"/> threw
/// "You cannot modify an immutable document". Copying an element out of an immutable
/// source document is a read-only operation and must be allowed.
/// </summary>
[TestClass]
public class JsonDiffFrozenBuilderTests
{
    [TestMethod]
    public void CreatePatchFromFrozenBuilderTargets()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> sourceBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(ws, """{"a":1}""");
        JsonElement source = sourceBuilder.RootElement.Freeze();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(ws, """{"a":1,"b":2}""");
        JsonElement target = targetBuilder.RootElement.Freeze();

        // Before the fix this threw InvalidOperationException: "You cannot modify an immutable document."
        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(source, target, ws);

        int count = 0;
        foreach (JsonPatchDocument.PatchOperation op in patch.EnumerateArray())
        {
            _ = op;
            count++;
        }

        Assert.AreEqual(1, count, "Adding property 'b' should produce a single 'add' operation.");
    }

    [TestMethod]
    public void CreatePatchFromFrozenBuilderRoundTrips()
    {
        const string sourceJson = """{"name":"Alice","scores":[1,2,3],"nested":{"x":1}}""";
        const string targetJson = """{"name":"Bob","scores":[1,2,3,4],"nested":{"x":2},"added":true}""";

        using JsonWorkspace ws = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> sourceBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(ws, sourceJson);
        JsonElement source = sourceBuilder.RootElement.Freeze();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(ws, targetJson);
        JsonElement target = targetBuilder.RootElement.Freeze();

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(source, target, ws);

        // Apply the patch to a mutable copy of the source and confirm we reach the target.
        using JsonDocumentBuilder<JsonElement.Mutable> applyBuilder = source.CreateBuilder(ws);
        JsonElement.Mutable mutable = applyBuilder.RootElement;
        Assert.IsTrue(mutable.TryApplyPatch(patch), "Patch application should succeed.");

        using ParsedJsonDocument<JsonElement> expected = ParsedJsonDocument<JsonElement>.Parse(targetJson);
        Assert.AreEqual(expected.RootElement, mutable.Clone());
    }
}
