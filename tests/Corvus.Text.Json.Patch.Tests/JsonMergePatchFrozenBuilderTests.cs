// <copyright file="JsonMergePatchFrozenBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Regression tests for issue #820: applying an RFC 7396 merge patch where the merge recurses
/// into a nested object left the parent element version-stale, so the next sibling property threw
/// (<see cref="InvalidOperationException"/> / <see cref="ObjectDisposedException"/>) during apply,
/// and a frozen patch document was reported as unreadable afterwards.
/// </summary>
[TestClass]
public class JsonMergePatchFrozenBuilderTests
{
    /// <summary>
    /// The exact shape from issue #820: parse the patch into a mutable builder, freeze it, apply
    /// it, then read the frozen patch back. The frozen patch must remain fully readable and
    /// unchanged.
    /// </summary>
    [TestMethod]
    public void FrozenMutablePatch_RemainsReadableAfterApply()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":{"b":"c"}}""");
        JsonElement.Mutable target = targetBuilder.RootElement;

        using JsonDocumentBuilder<JsonElement.Mutable> patchBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":{"b":"d","e":"f"}}""");
        JsonElement frozenPatch = patchBuilder.RootElement.Freeze();

        string before = frozenPatch.ToString();

        JsonMergePatchExtensions.ApplyMergePatch(ref target, in frozenPatch);

        // Previously threw ObjectDisposedException: 'JsonDocument'.
        Assert.AreEqual(before, frozenPatch.ToString(), "the frozen patch must be unchanged and readable");
        Assert.AreEqual("""{"a":{"b":"d","e":"f"}}""", target.ToString());
    }

    /// <summary>
    /// The core defect: a merge that recurses into a nested object and then processes a further
    /// sibling property of the same (non-root) parent. Before the fix the recursion left the parent
    /// element version-stale and the sibling threw during apply.
    /// </summary>
    [TestMethod]
    [DataRow("{}", """{"a":{"b":{"x":1},"c":2}}""", """{"a":{"b":{"x":1},"c":2}}""", DisplayName = "object-then-sibling into empty target")]
    [DataRow("""{"a":{}}""", """{"a":{"b":{"x":1},"c":2}}""", """{"a":{"b":{"x":1},"c":2}}""", DisplayName = "object-then-sibling into empty nested object")]
    [DataRow("""{"a":{"b":{"old":0},"c":0}}""", """{"a":{"b":{"x":1},"c":2}}""", """{"a":{"b":{"old":0,"x":1},"c":2}}""", DisplayName = "object-then-sibling merging existing")]
    [DataRow("""{"a":{"b":1}}""", """{"a":{"c":{"d":1},"e":2}}""", """{"a":{"b":1,"c":{"d":1},"e":2}}""", DisplayName = "nested new object then sibling")]
    [DataRow("{}", """{"a":{"b":{"c":{"d":1},"e":2},"f":3}}""", """{"a":{"b":{"c":{"d":1},"e":2},"f":3}}""", DisplayName = "object-then-sibling at two depths")]
    public void NestedMerge_ObjectThenSibling(string targetJson, string patchJson, string expected)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, targetJson);
        JsonElement.Mutable target = targetBuilder.RootElement;

        using JsonDocumentBuilder<JsonElement.Mutable> patchBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, patchJson);
        JsonElement frozenPatch = patchBuilder.RootElement.Freeze();

        string patchBefore = frozenPatch.ToString();

        JsonMergePatchExtensions.ApplyMergePatch(ref target, in frozenPatch);

        using ParsedJsonDocument<JsonElement> expectedDoc = ParsedJsonDocument<JsonElement>.Parse(expected);
        Assert.IsTrue(
            target.Equals(expectedDoc.RootElement),
            $"Expected: {expected}\nActual: {target}");
        Assert.AreEqual(patchBefore, frozenPatch.ToString(), "the frozen patch must be unchanged and readable");
    }

    /// <summary>
    /// The merge target may itself be a non-root element of a larger document. After the recursion
    /// the non-root target must be refreshed so its subsequent siblings (and reads) succeed.
    /// </summary>
    [TestMethod]
    public void MergeIntoNonRootTarget()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"outer":{"a":{"b":1}}}""");
        JsonElement.Mutable root = targetBuilder.RootElement;
        Assert.IsTrue(root.TryGetProperty("outer"u8, out JsonElement.Mutable inner));

        using JsonDocumentBuilder<JsonElement.Mutable> patchBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":{"c":{"d":1},"e":2}}""");
        JsonElement frozenPatch = patchBuilder.RootElement.Freeze();

        JsonMergePatchExtensions.ApplyMergePatch(ref inner, in frozenPatch);

        using ParsedJsonDocument<JsonElement> expectedDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"outer":{"a":{"b":1,"c":{"d":1},"e":2}}}""");
        Assert.IsTrue(
            root.Equals(expectedDoc.RootElement),
            $"Actual: {root}");
    }

    /// <summary>
    /// A frozen patch built by mutation (its values are dynamic-value blobs rather than raw JSON)
    /// must also remain readable after the merge copies values out of it.
    /// </summary>
    [TestMethod]
    public void MutatedThenFrozenPatch_RemainsReadable()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":{"b":"c"}}""");
        JsonElement.Mutable target = targetBuilder.RootElement;

        using JsonDocumentBuilder<JsonElement.Mutable> patchBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":{}}""");
        JsonElement.Mutable patchRoot = patchBuilder.RootElement;
        Assert.IsTrue(patchRoot.TryGetProperty("a"u8, out JsonElement.Mutable patchA));
        patchA.SetProperty("b"u8, "d"u8);
        patchA.SetProperty("e"u8, "f"u8);

        JsonElement frozenPatch = patchRoot.Freeze();
        string before = frozenPatch.ToString();

        JsonMergePatchExtensions.ApplyMergePatch(ref target, in frozenPatch);

        Assert.AreEqual(before, frozenPatch.ToString());
        Assert.AreEqual("""{"a":{"b":"d","e":"f"}}""", target.ToString());
    }

    /// <summary>
    /// A realistic merge (nested object merge, null removal, wholesale array replacement and a new
    /// scalar) from a frozen mutable patch — the frozen patch stays readable afterwards.
    /// </summary>
    [TestMethod]
    public void RealisticMerge_FrozenMutablePatch_RemainsReadable()
    {
        const string targetJson =
            """{"title":"Goodbye!","author":{"givenName":"John","familyName":"Doe"},"tags":["example","sample"],"content":"unchanged"}""";
        const string patchJson =
            """{"title":"Hello!","author":{"familyName":null},"tags":["example"],"phoneNumber":"+01-123-456-7890"}""";

        using JsonWorkspace workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> targetBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, targetJson);
        JsonElement.Mutable target = targetBuilder.RootElement;

        using JsonDocumentBuilder<JsonElement.Mutable> patchBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, patchJson);
        JsonElement frozenPatch = patchBuilder.RootElement.Freeze();

        string before = frozenPatch.ToString();

        JsonMergePatchExtensions.ApplyMergePatch(ref target, in frozenPatch);

        Assert.AreEqual(before, frozenPatch.ToString());

        using ParsedJsonDocument<JsonElement> expectedDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"title":"Hello!","author":{"givenName":"John"},"tags":["example"],"content":"unchanged","phoneNumber":"+01-123-456-7890"}""");
        Assert.IsTrue(target.Equals(expectedDoc.RootElement), $"Actual: {target}");
    }
}
