// <copyright file="GeneratedTypePatchTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Patch;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// A JSON Patch applies to the mutable element of a generated type
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/943).
/// </summary>
[TestClass]
public class GeneratedTypePatchTests
{
    private const string Json = """{"address":{"street":"1 High Street","city":"Bath"},"notes":"n"}""";

    [TestMethod]
    public void TryApplyPatch_AppliesThePatchToTheElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<NestedObject>.Parse(Json);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = source.RootElement.CreateBuilder(workspace);
        using var patch = ParsedJsonDocument<JsonPatchDocument>.Parse("""
            [
              { "op": "replace", "path": "/address/city", "value": "Bristol" },
              { "op": "remove", "path": "/notes" }
            ]
            """);

        NestedObject.Mutable root = builder.RootElement;
        Assert.IsTrue(root.TryApplyPatch(patch.RootElement));

        Assert.AreEqual("Bristol", (string)root.Address.City);
        Assert.IsTrue(root.Notes.IsUndefined());
        Assert.AreEqual("""{"address":{"street":"1 High Street","city":"Bristol"}}""", builder.RootElement.ToString());
    }

    [TestMethod]
    public void TryApplyPatch_AtTheRoot_GivesTheNewValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<NestedObject>.Parse(Json);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = source.RootElement.CreateBuilder(workspace);
        using var patch = ParsedJsonDocument<JsonPatchDocument>.Parse("""[{ "op": "replace", "path": "", "value": {"address":{"street":"2 Low Road"}} }]""");

        NestedObject.Mutable root = builder.RootElement;
        Assert.IsTrue(root.TryApplyPatch(patch.RootElement));

        Assert.AreEqual("2 Low Road", (string)root.Address.Street);
    }

    [TestMethod]
    public void TryApplyPatch_ThatCannotApply_ReturnsFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<NestedObject>.Parse(Json);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = source.RootElement.CreateBuilder(workspace);
        using var patch = ParsedJsonDocument<JsonPatchDocument>.Parse("""[{ "op": "test", "path": "/notes", "value": "other" }]""");

        NestedObject.Mutable root = builder.RootElement;
        Assert.IsFalse(root.TryApplyPatch(patch.RootElement));
    }

    [TestMethod]
    public void TryValidateAndApplyPatch_RefusesADocumentThatIsNotAPatch()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<NestedObject>.Parse(Json);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = source.RootElement.CreateBuilder(workspace);
        using var notAPatch = ParsedJsonDocument<JsonPatchDocument>.Parse("""[{ "op": "frobnicate", "path": "/notes" }]""");
        using var patch = ParsedJsonDocument<JsonPatchDocument>.Parse("""[{ "op": "add", "path": "/notes", "value": "added" }]""");

        NestedObject.Mutable root = builder.RootElement;
        Assert.IsFalse(root.TryValidateAndApplyPatch(notAPatch.RootElement));
        Assert.AreEqual("n", (string)root.Notes);

        Assert.IsTrue(root.TryValidateAndApplyPatch(patch.RootElement));
        Assert.AreEqual("added", (string)root.Notes);
    }

    [TestMethod]
    public void AMergePatchThatReplacesTheRoot_AppliesToAGeneratedTypesBuilder()
    {
        // The merge patch takes JsonElement.Mutable; replacing the root of a generated type's builder cast the
        // builder to the wrong type.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<NestedObject>.Parse(Json);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = source.RootElement.CreateBuilder(workspace);
        using var patch = ParsedJsonDocument<JsonElement>.Parse("""[1, 2]""");

        JsonElement.Mutable root = JsonElement.Mutable.From(builder.RootElement);
        JsonMergePatchExtensions.ApplyMergePatch(ref root, patch.RootElement);

        Assert.AreEqual("[1, 2]", builder.RootElement.ToString());
    }
}